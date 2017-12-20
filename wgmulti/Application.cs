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
    public static int grabbingRound = 0;
    public static Report report = new Report();
    public static Xmltv epg = new Xmltv();
    public static Stopwatch stopWatch = new Stopwatch();
    /// <summary>
    /// Start the main application
    /// </summary>
    /// <param name="configDir">Overwrite the configuration directory. (when testing)</param>
    public static void Run(String configDir = null)
    {
      if (configDir != null)
        Arguments.configDir = configDir;

      stopWatch.Start();

      try
      {
        var configFilePath = Arguments.useJsonConfig ? Config.jsonConfigFileName : Config.configFileName;
        configFilePath = Path.Combine(Arguments.configDir, configFilePath);
        masterConfig = Config.DeserializeFromFile(configFilePath);

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

        // Create the combined xmltv EPG
        epg = masterConfig.GetChannelsGuides();

        // Save post process EPG file
        if (masterConfig.postProcess.run)
          epg.Save(masterConfig.postProcess.fileName, true);

        // Save main EPG file
        epg.Save(masterConfig.outputFilePath);

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

      stopWatch.Stop();

      report.Generate(masterConfig);
      if (Arguments.generateReport)
        report.Save();
    }

    static void DoGrabbing()
    {
      // Try grabbing all empty channels using the next available siteini 
      // until there are no more site inis or all channels have programmes
      Log.Line();
      var doGrabbing = true;

      while (doGrabbing)
      {
        doGrabbing = false;

        // Get only the currently active channels every time
        foreach (var channel in masterConfig.GetChannels(onlyActive: true))
        {
          if (!channel.HasPrograms)
            channel.SetActiveSiteIni();

          // Channel could be deactivated if no active siteini is set
          if (channel.active)
            doGrabbing = true;
        }

        if (!doGrabbing)
          break;

        // Each grabber contains one or more channels grouped by Siteini
        var i = 1;
        List<Grabber> grabberGroups = (
          from channel in masterConfig.GetChannels(onlyActive: true)
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

        Log.Line();
        Log.Info(String.Format("----------------- Grabbing Round {0} ------------------", grabbingRound + 1));
        Log.Line();
        Log.Info(String.Format("Round {0} Starting {1} grabbers asynchronously, {2} at a time",
          grabbingRound + 1, grabberGroups.Count(), Arguments.maxAsyncProcesses));


        var options = new ParallelOptions { MaxDegreeOfParallelism = Arguments.maxAsyncProcesses };

        Parallel.ForEach(grabberGroups, options, grabber => { grabber.Grab(); });

        Log.Line();

        var countedChannels = masterConfig.GetChannelsCount();
        Log.Info(String.Format("Grabbing round {0} finished. {1} channels have programs.", grabbingRound + 1, countedChannels["withPrograms"]));

        if (countedChannels["withoutPrograms"] == 0)
          break;

        grabbingRound++;
      }
    }
  }
}
