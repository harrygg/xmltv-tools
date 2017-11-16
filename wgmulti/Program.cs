using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace wgmulti
{
  public class Program
  {
    public const String wgexe = "WebGrab+Plus.exe";
    public static String WgPath = Path.Combine(Arguments.webGrabFolder, wgexe);
    public static Config masterConfig;
    public static List<Grabber> grabberGroups;
    public static int currentSiteiniIndex = 0;
    static Random rnd = new Random();
    public static List<String> runningGrabbers = new List<String>();
    static void Main(string[] args)
    {

      Stopwatch stopWatch = new Stopwatch();
      Xmltv epg = new Xmltv();
      Report report = new Report();
      

      try
      {
        Log.Info("#####################################################");
        Log.Info("#                                                   #");
        Log.Info("#      Wgmulti.exe for WebGrab+Plus by Harry_GG     #");
        Log.Info("#                                                   #");
        Log.Info("#####################################################");
        Log.Info("System: " + Environment.OSVersion.Platform);
        Log.Info("Working Directory: " + Directory.GetCurrentDirectory());
        Log.Info("Config Directory: " + Arguments.configDir);
        Log.Info("Arguments: " + Arguments.cmdArgs);
        var versionInfo = FileVersionInfo.GetVersionInfo(WgPath);
        Log.Info(String.Format("{0} version: {1}", wgexe, versionInfo.ProductVersion));
        if (Arguments.buildConfigFromJson)
        {
          Log.Info("Build config from JSON file: " + Arguments.buildConfigFromJson);
          Log.Info("JsonConfigFileName: " + Arguments.jsonConfigFileName);
        }
        else
          Log.Info("ConvertXmlConfigToJson: " + Arguments.convertXmlConfigToJson);
        Log.Info("MaxAsyncProcesses: " + Arguments.maxAsyncProcesses);
        Log.Info("CombineLogFiles: " + Arguments.combineLogFiles);
        Log.Info("ConvertTimesToLocal: " + Arguments.convertTimesToLocal);
        Log.Info("GenerateResultsReport: " + Arguments.generateReport);
        Log.Info("ShowConsole: " + Arguments.showConsole);
        Log.Info("-----------------------------------------------------");
        Log.Info("Execution started at: " + DateTime.Now);

        stopWatch.Start();

        masterConfig = InitConfig();

        Log.Info(String.Format("Config contains {0} channels for grabbing", masterConfig.activeChannels));

        if (!masterConfig.postProcess.grab)
        {
          Log.Info("Grabbing disabled in configuration. Enable by setting the postprocess 'grab' value to 'on'");
          return;
        }

        // Grab channel programs
        DoGrabbing();

        // Create the combined xmltv EPG
        epg = masterConfig.GetEpg();

        // Save EPG file
        if (masterConfig.postProcess.run)
          epg.Save(masterConfig.postProcess.fileName, true);
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
          Log.Error(ex.ToString());
        return;
      }

      stopWatch.Stop();
      TimeSpan ts = stopWatch.Elapsed;

      //***************************************
      // Generate report with all grabbing info
      GenerateReport(report);
      
      // Output names of channels with no EPG
      var n = 0;
      Log.Info("Channels with no EPG: ");

      foreach (var name in report.emptyChannels)
        Log.Info(String.Format("{0}. {1}", ++n, name));

      if (n == 0)
        Log.Info("None!");

      // Calculate EPG file size and md5 hash
      report.fileSize = epg.GetFileSize();
      report.md5hash = epg.GetMD5Hash();
      report.generationTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
      report.generatedOn = DateTime.Now.ToString();

      Log.Info("wgmulti finished at: " + report.generatedOn);
      Log.Info("wgmulti execution time: " + report.generationTime);

      // Save report 
      if (Arguments.generateReport)
        report.Save(Arguments.reportFolder);

      Log.Info("-----------------------------------------------------");
      Log.Info("Total channels: " + report.total);
      Log.Info("With EPG: " + report.channelsWithEpg);
      Log.Info("Without EPG: " + report.emptyChannels.Count);
      Log.Info("EPG size: " + report.fileSize);
      Log.Info("EPG md5 hash: " + report.md5hash);
      Log.Info(String.Format("Report saved to: {0}", Path.Combine(Arguments.reportFolder, Arguments.reportFileName)));
      Log.Info("-----------------------------------------------------");

    }






    static void DoGrabbing()
    {
      // Try grabbing all empty channels using the next available siteini 
      // until there are no more site inis or all channels have programmes
      Log.Info("-----------------------------------------------------");
      var doGrabbing = true;

      while (doGrabbing)
      {
        doGrabbing = false;

        // Call GetChannels to get only the active channels every time as channels are deactivated
        foreach (var channel in masterConfig.GetChannels(onlyActive: true))
        {
          // If there is EPG for this channel, disable scrubbing
          if (channel.xmltv.programmes.Count > 0)
          {
            channel.update = UpdateType.None;
            continue;
          }

          if (channel.siteinis == null)
          {
            Log.Error(String.Format("Channel '{0}' has no siteinis. Channel deactivated!", channel.name));
            channel.active = false;
            continue;
          }

          // If the current index (0 based) is greater than or equal to the number of siteinis, deactivate the channel
          if (currentSiteiniIndex + 1 > channel.siteinis.Count)
          {
            Log.Info(String.Format("Channel '{0}' has no programs. No alternative siteinis found. Channel deactivated.", channel.name));
            channel.active = false;
            continue;
          }

          channel.siteiniIndex = currentSiteiniIndex;
          doGrabbing = true;

          if (currentSiteiniIndex > 0) // we are not in the first grabbing round
            Log.Info(String.Format("Channel '{0}' has no programs. Switching to grabber {1}",
              channel.name, channel.GetActiveSiteIni().name.ToUpper()));
        }

        if (!doGrabbing)
          break;

        // Each grabber contains one or more channels grouped by Siteini
        grabberGroups = (from channel in masterConfig.GetChannels(onlyActive: true)
                         group channel by channel.GetActiveSiteIni().name into grouped
                         select new Grabber(grouped.Key, grouped.ToList()))
                        .Where(grabber => grabber.enabled)
                        .ToList();

        if (grabberGroups.Count == 0)
        {
          Log.Info("No active grabbers created. Exiting!");
          break;
        }

        if (Arguments.randomStartOrder && grabberGroups.Count() > 1)
          grabberGroups = grabberGroups.OrderBy(item => rnd.Next()).ToList();

        Log.Info("-----------------------------------------------------");
        Log.Info(String.Format("---------------- Grabbing Round #{0} ------------------", currentSiteiniIndex + 1));
        Log.Info("-----------------------------------------------------");
        Log.Info(String.Format("#{0} Starting {1} grabbers asynchronously, {2} at a time",
          currentSiteiniIndex + 1, grabberGroups.Count, Arguments.maxAsyncProcesses));

        var i = 1;
        var options = new ParallelOptions { MaxDegreeOfParallelism = Arguments.maxAsyncProcesses };

        Parallel.ForEach(grabberGroups, options, grabber => {
          grabber.number = i++;
          Log.Debug("Grab() started for " + grabber.id.ToUpper());
          grabber.Grab();
        });

        Log.Info("-----------------------------------------------------");
        var emptyChnnels = masterConfig.EmptyChannelsCount();
        Log.Info(String.Format("Grabbing round #{0} finished. {1} channels grabbed.", currentSiteiniIndex + 1, (masterConfig.GetChannels(includeOffset: true).Count() - emptyChnnels)));

        // If there are no more channels without programs
        if (emptyChnnels == 0)
          break;

        currentSiteiniIndex++;
        runningGrabbers.Clear();
      }
    }

    static void GenerateReport(Report report)
    {
      if (Arguments.generateReport)
      {
        var lastSiteIni = "";
        foreach (var channel in masterConfig.GetChannels(includeOffset: true))
        {
          report.total += 1;
          var channelInfo = new ChannelInfo(channel.name);
          if (channel.xmltv.programmes.Count > 0)
          {
            report.channelsWithEpg += 1;
            channelInfo.programsCount = channel.xmltv.programmes.Count;
            channelInfo.firstShowStartsAt = channel.xmltv.programmes[0].Attribute("start").Value;
            channelInfo.lastShowStartsAt = channel.xmltv.programmes[channelInfo.programsCount - 1].Attribute("stop").Value;
            //In case of offset channels, they don't have siteini
            try
            { 
              channelInfo.siteiniName = channel.GetActiveSiteIni().name;
              lastSiteIni = channelInfo.siteiniName;
            }
            catch
            {
              if (channel.offset != null)
                channelInfo.siteiniName = lastSiteIni;
            }
            channelInfo.siteiniIndex = channel.siteiniIndex;
          }
          else
          {
            report.channelsWithoutEpg += 1;
            report.emptyChannels.Add(channel.name);
          }
          report.channels.Add(channelInfo);
        }
      }
    }

    internal static Config InitConfig()
    {
      Config config = null;

      // Check whether we have a config xml or json file
      if (Arguments.buildConfigFromJson)
      {
        var jsonConfigPath = Path.Combine(Arguments.configDir, Arguments.jsonConfigFileName);

        var js = new JavaScriptSerializer();
        config = js.Deserialize<Config>(File.ReadAllText(jsonConfigPath));

        config.SetAbsPaths(Arguments.configDir);

        if (config.postProcess.run)
          config.postProcess.Load(config.folder);
      }
      else
      {
        // Read xml configuration file
        config = new Config(Arguments.configDir);

        if (Arguments.convertXmlConfigToJson)
        {
          config.Save(Arguments.configDir, true);
          Log.Info("WebGrab XML config converted to " + Arguments.jsonConfigFileName);
        }
      }

      // Remove all disabled channels from the list
      config.RemoveDisabledChannels();

      return config;
    }
  }
}
