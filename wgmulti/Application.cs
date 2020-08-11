using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace wgmulti
{
  public class Application
  {
    public static Config masterConfig;
    public static int grabbingRoundNumber = 0;
    public static Xmltv epg = new Xmltv();

    /// <summary>
    /// Start the main application
    /// </summary>
    /// <param name="configDir">Overwrite the configuration directory. (when testing)</param>
    public static void Run(String configDir = null)
    {
      if (configDir != null)
        Arguments.configDir = configDir;

      Report.executionStartTime = DateTime.Now;

      try
      {
        var configFilePath = Arguments.useJsonConfig ? Arguments.jsonConfigFileName : Config.configFileName;
        configFilePath = Path.Combine(Arguments.configDir, configFilePath);
        masterConfig = Config.DeserializeFromFile(configFilePath);
        Report.ActiveConfig = masterConfig;

        // Export json config if we are using xml config
        if (Arguments.exportJsonConfig)
          masterConfig.Save(
            Path.Combine(Arguments.configDir, "exported_wgmulti.config.json"), true);

        if (!masterConfig.postProcess.grab)
        {
          Log.Info("Grabbing disabled. Enable by setting the postprocess 'grab' value to 'on'");
          return;
        }

        //masterConfig.InitSiteinis();

        // Grab channel programs
        DoGrabbing();

        // Save separate EPG files for each channel
        if (Arguments.saveStandaloneGuides)
          masterConfig.SaveStandaloneGuides();

        // Create the combined xmltv EPG
        epg = masterConfig.GetChannelsGuides();

        // Save post process EPG file
        if (masterConfig.postProcess.run)
          epg.Save(masterConfig.postProcess.fileName, true);

        // Save main EPG file
        epg.Save(masterConfig.outputFilePath);

        // Run Postprocess script
        if (Arguments.runPostprocessScript && Arguments.postprocessScript != "")
          RunPostProcessScript();

      }
      catch (FileNotFoundException fnfe)
      {
        Log.Error(fnfe.ToString());
        return;
      }
      catch (Exception ex)
      {
        if (ex.ToString().Contains("annot find the")) //Could come from Linux based OS
          Log.Error("WebGrab+Plus.exe not found or not executable!");
        else
          Log.Error(ex.Message);
        return;
      }

      Report.executionEndTime = DateTime.Now;

      if (Arguments.generateReport)
        Report.Save();
    }

    static void DoGrabbing()
    {
      // Try grabbing all empty channels using the next available siteini 
      // until there are no more site inis or all channels have programmes
      Log.Line();
      var doGrabbing = true;
      var maxGrabbingRounds = 999;

      while (doGrabbing)
      {
        if (grabbingRoundNumber == maxGrabbingRounds)
        {
          Log.Info($"Max grabbing rounds limit reached. Stopping.");
          break;
        }

        doGrabbing = false;

        // Get only the currently active channels every time
        var activeChannels = masterConfig.GetChannels(onlyActive: true);
        foreach (var channel in activeChannels)
        {
          // TODO rewrite channel activiation deactivation to handle it here not in the SetActiveSiteIni funciton. 
          // Also, SetActiveSiteIni automatically as per the grabbing round
          // Set next siteini as active, or deactivate channel if there are no more siteinis
          if (!channel.HasPrograms)
            channel.SetActiveSiteIni();

          // Even if we have a single active channel, we will proceed with grabbing
          if (channel.active)
            doGrabbing = true;
        }

        if (!doGrabbing)
          break;

        // Each grabber contains one or more channels grouped by Siteini
        var i = 1;
        List<Grabber> grabberGroups = (
          from channel in masterConfig.GetChannels(onlyActive: true).Where(channel => !channel.HasPrograms)
          group channel by channel.GetActiveSiteIni().name into grouped
          select new Grabber(grouped.Key, grouped.ToList(), i++)
          ).Where(grabber => grabber.enabled).ToList();

        if (grabberGroups.Count == 0)
        {
          Log.Info("No active grabbers created. Exiting!");
          break;
        }

        if (Arguments.randomStartOrder && grabberGroups.Count() > 1)
          grabberGroups = grabberGroups.OrderBy(item => new Random().Next()).ToList();

        var grd = new GrabbingRoundStats(grabbingRoundNumber);
        Report.grabbingRoundsData.Add(grd);

        Log.Line();
        Log.Info(String.Format("----------------- Grabbing Round {0} ------------------", grabbingRoundNumber + 1));
        Log.Line();
        Log.Info(String.Format("Round {0} Starting {1} grabbers asynchronously, {2} at a time",
          grabbingRoundNumber + 1, grabberGroups.Count(), Arguments.maxAsyncProcesses));

        var options = new ParallelOptions { MaxDegreeOfParallelism = Arguments.maxAsyncProcesses };

        Parallel.ForEach(grabberGroups, options, grabber => { grabber.Grab(); });

        Log.Line();
        
        var channels = masterConfig.GetChannels(includeOffset: true, onlyActive: true);
        var channelsWithEpg = channels.Where(channel => channel.HasPrograms).Count();
        Log.Info($"Grabbing round {grabbingRoundNumber + 1} finished. {channelsWithEpg} channels have programs.");

        grd.endTime = DateTime.Now;
        Log.Info($"Grabber round #{grabbingRoundNumber} finished for {grd.TotalDurationFormatted}");

        if (channels.Count() == channelsWithEpg)
          break;

        grabbingRoundNumber++;
      }
    }

    private static void RunPostProcessScript()
    {
      try
      {
        var process = new Process();
        var startInfo = new ProcessStartInfo();
        startInfo.CreateNoWindow = false;
        startInfo.UseShellExecute = false;
        startInfo.WindowStyle = ProcessWindowStyle.Normal;
        startInfo.FileName = Arguments.postprocessScript;
        startInfo.Arguments = Arguments.postprocessArguments;
        startInfo.RedirectStandardError = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardInput = false;
        process.StartInfo = startInfo;
        process.OutputDataReceived += (a, b) => Log.Info(b.Data);
        process.ErrorDataReceived += (a, b) => Log.Error(b.Data);

        Log.Info(String.Format("Starting post process command {0} with arguments {1}", startInfo.FileName, startInfo.Arguments));
        process.Start();
        //String output = process.StandardOutput.ReadToEnd();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        process.WaitForExit(1000 * 60 * 15);
        Log.Info("Finished post process command");
      }
      catch(Exception ex)
      {
        Log.Error(ex.ToString());
      }
    }

  }
}
