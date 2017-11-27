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
    public static List<Grabber> grabberGroups;
    public static int currentSiteiniIndex = 0;
    public static Report report = new Report();

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

      var epg = new Xmltv();

      try
      {
        var configFilePath = Arguments.useJsonConfig ? Config.jsonConfigFileName : Config.configFileName;
        masterConfig = Config.DeserializeFromFile(Path.Combine(Arguments.configDir, configFilePath));

        // Export json config if we are using xml config
        if (Arguments.exportJsonConfig)
          masterConfig.Save(Path.Combine(Arguments.configDir, "exported_wgmulti.config.json"));

        if (!masterConfig.postProcess.grab)
        {
          Log.Info("Grabbing disabled. Enable by setting the postprocess 'grab' value to 'on'");
          return;
        }

        if (masterConfig.postProcess.run)
          masterConfig.postProcess.Load(masterConfig.folder);

        masterConfig.activeChannels = masterConfig.GetChannels(includeOffset: true, onlyActive: false).Count();
        Log.Info(String.Format("Config contains {0} channels for grabbing", masterConfig.activeChannels));

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
      var ts = stopWatch.Elapsed;

      GenerateReport();
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
        {
          grabberGroups = grabberGroups.OrderBy(
            item => new Random().Next()).ToList();
        }

        Log.Line();
        Log.Info(String.Format("---------------- Grabbing Round #{0} ------------------", currentSiteiniIndex + 1));
        Log.Line();
        Log.Info(String.Format("#{0} Starting {1} grabbers asynchronously, {2} at a time",
          currentSiteiniIndex + 1, grabberGroups.Count, Arguments.maxAsyncProcesses));

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
