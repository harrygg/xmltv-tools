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
    public static int currentSiteiniIndex = 0;
    public static Report report = new Report();
    public static Xmltv epg = new Xmltv();

    /// <summary>
    /// Start the main application
    /// </summary>
    /// <param name="configDir">Overwrite the configuration directory. (when testing)</param>
    public static void Run(String configDir = null)
    {
      if (configDir != null)
        Arguments.configDir = configDir;

      var stopWatch = new Stopwatch();
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

        DisableMissingSiteInis();

        // Grab channel programs
        DoGrabbing();

        // Create the combined xmltv EPG
        epg = masterConfig.GetChannelGuides();

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
      var ts = stopWatch.Elapsed;

      report.fileSize = epg.GetFileSize();
      report.md5hash = epg.GetMD5Hash();

      GenerateReport();
    }

    static void DisableMissingSiteInis()
    {
      List<SiteIni> disabledSiteinis = new List<SiteIni>();

      foreach (var channel in masterConfig.GetChannels())
      {
        foreach (var siteini in channel.siteinis)
        {
          if (masterConfig.DisabledSiteiniNamesList.Contains(siteini.name))
          {
            Log.Error(String.Format("Siteini {0} disabled in JSON configuraiton!", siteini.GetName()));
            siteini.enabled = false;
          }
          else
          {
            siteini.path = siteini.GetPath();
            if (!String.IsNullOrEmpty(siteini.path))
            {
              siteini.enabled = true;
            }
            else
            {
              masterConfig.DisabledSiteiniNamesList.Add(siteini.name);
              Log.Error(String.Format("Siteini {0} not found! Siteini disabled!", siteini.GetName()));
            }
          }
        }
      }
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
          if (channel.update != UpdateType.None)
            channel.SetActiveSiteIni();

          // Channel could be deactivated if no active siteini is set
          if (channel.active)
            doGrabbing = true;
        }

        if (!doGrabbing)
          break;

        // Each grabber contains one or more channels grouped by Siteini
        IEnumerable<Grabber> grabberGroups;
        grabberGroups = (from channel in masterConfig.GetChannels(onlyActive: true)
                         group channel by channel.GetActiveSiteIni().name into grouped
                         select new Grabber(grouped.Key, grouped.ToList()))
                        .Where(grabber => grabber.enabled);

        if (grabberGroups.Count() == 0)
        {
          Log.Info("No active grabbers created. Exiting!");
          break;
        }

        if (Arguments.randomStartOrder && grabberGroups.Count() > 1)
        {
          grabberGroups = grabberGroups.OrderBy(
            item => new Random().Next()).ToList();
        }

        Log.Line();
        Log.Info(String.Format("---------------- Grabbing Round #{0} ------------------", currentSiteiniIndex + 1));
        Log.Line();
        Log.Info(String.Format("#{0} Starting {1} grabbers asynchronously, {2} at a time",
          currentSiteiniIndex + 1, grabberGroups.Count(), Arguments.maxAsyncProcesses));

        var i = 1;
        var options = new ParallelOptions { MaxDegreeOfParallelism = Arguments.maxAsyncProcesses };

        Parallel.ForEach(grabberGroups, options, grabber => {
          grabber.number = i++;
          Log.Debug("Grab() started for " + grabber.id.ToUpper());
          grabber.Grab();
        });

        Log.Line();
        var emptyChnnels = masterConfig.EmptyChannelsCount();
        Log.Info(String.Format("Grabbing round #{0} finished. {1} channels grabbed.", currentSiteiniIndex + 1, (masterConfig.GetChannels(includeOffset: true).Count() - emptyChnnels)));

        // If there are no more channels without programs
        if (emptyChnnels == 0)
          break;

        currentSiteiniIndex++;
      }
    }

    static void GenerateReport()
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

      if (Arguments.generateReport)
        report.Save();
    }
  }
}
