using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace wgmulti
{
  public class Program
  {
    const String wgexe = "WebGrab+Plus.exe";
    static String WgPath = Path.Combine(Arguments.webGrabFolder, wgexe);
    public static Config masterConfig;
    public static List<Grabber> grabbersGroupParallel;
    public static List<Grabber> grabbersGroup2 = new List<Grabber>();
    public static int currentSiteiniIndex = 0;
    static List<String> logFiles = new List<String>();

    static void Main(string[] args)
    {

      Stopwatch stopWatch = new Stopwatch();
      Xmltv epg = new Xmltv();
      Report report = new Report();

      try
      {
        Console.WriteLine("\n#####################################################");
        Console.WriteLine("#                                                   #");
        Console.WriteLine("#      Wgmulti.exe for WebGrab+Plus by Harry_GG     #");
        Console.WriteLine("#                                                   #");
        Console.WriteLine("#####################################################\n");
        Console.WriteLine(" System: {0}", Environment.OSVersion.Platform);
        Console.WriteLine(" Working Directory: {0}", Directory.GetCurrentDirectory());
        Console.WriteLine(" Config Directory: {0}", Arguments.configDir);
        Console.WriteLine(" Arguments: {0}", Arguments.cmdArgs);
        var versionInfo = FileVersionInfo.GetVersionInfo(WgPath);
        Console.WriteLine(" {0} version: {1}", wgexe, versionInfo.ProductVersion);

        if (Arguments.buildConfigFromJson)
        {
          Console.WriteLine(" Build config from JSON file: {0}", Arguments.buildConfigFromJson);
          Console.WriteLine(" JsonConfigFileName: {0}", Arguments.jsonConfigFileName);
        }

        Console.WriteLine(" MaxAsyncProcesses: {0}", Arguments.maxAsyncProcesses);
        //Console.WriteLine(" GroupChannelsBySiteIni: {0}", Arguments.groupChannelsBySiteIni);
        //Console.WriteLine(" MaxChannelsInGroup: {0}", Arguments.maxChannelsInGroup);
        Console.WriteLine(" CombineLogFiles: {0}", Arguments.combineLogFiles);
        Console.WriteLine(" ConvertTimesToLocal: {0}", Arguments.convertTimesToLocal);
        Console.WriteLine(" GenerateResultsReport: {0}", Arguments.generateReport);
        Console.WriteLine(" ShowConsole: {0}", Arguments.showConsole);
        Console.WriteLine("\n-----------------------------------------------------\n");
        Console.WriteLine("Execution started at: " + DateTime.Now);

        stopWatch.Start();

        masterConfig = InitConfig();

        Console.WriteLine("Cofig contains {0} channels for grabbing", masterConfig.activeChannels);

        if (!masterConfig.postProcess.grab)
        {
          Console.WriteLine("Grabbing disabled in configuration. Enable by setting the postprocess 'grab' value to 'on'");
          return;
        }

        // Try grabbing all empty channels using the next available siteini 
        // until there are no more site inis or all channels have programmes
        Console.WriteLine("-----------------------------------------------------");
        var doGrabbing = true;
        
        while (doGrabbing)
        {
          //if (siteiniIndex > 0)
          //  Console.WriteLine("Checking for empty channels...");

          doGrabbing = false;
          //Call GetActiveChannels everytime as channels are disabled
          foreach (var channel in masterConfig.GetActiveChannels())
          {
            // Get channel in report and add its statistics
            //var reportChannel = report.GetChannel(channel.name);

            if (channel.xmltvPrograms.Count > 0)
            {
              // there is EPG for this channel so disable updates for next round
              channel.update = UpdateType.None;
              continue;
            }

            if (channel.offset != null) //skip offset channels
              continue;

            if (channel.siteinis == null)
            {
              Console.WriteLine("Channel '{0}' has no siteinis. Channel disabled.", channel.name);
              channel.isActive = false;
              continue;
            }

            // If there are more siteinis, take the next one
            if (channel.siteinis.Count > currentSiteiniIndex)
            {
              channel.siteiniIndex = currentSiteiniIndex;
              doGrabbing = true;

              if (currentSiteiniIndex > 0) // we are not in the first grabbing round
                Console.WriteLine("Channel '{0}' has no programs. Switching to grabber {1}",
                  channel.name, channel.GetActiveSiteIni().name.ToUpper());
            }
            else
            {
              Console.WriteLine("Channel '{0}' has no programs. No alternative siteinis found. Channel deactivated.", channel.name);
              channel.isActive = false;
            }
          }

          if (!doGrabbing)
            break;

          // Group grabbers (create grabbersGroupParallel, grabbersGroup2)
          // Each grabber contains one or more channels combined on a different criteria
          CreateGrabberGroups();

          if (grabbersGroupParallel.Count == 0)
          {
            Console.WriteLine("No active grabbing created. Exiting!");
            return;
          }

          Console.WriteLine("-----------------------------------------------------");
          Console.WriteLine("---------------- Grabbing Round #{0} ------------------", currentSiteiniIndex + 1);
          Console.WriteLine("-----------------------------------------------------");
          Console.WriteLine("#{0} Starting {1} grabbers asynchronously, {2} at a time",
            currentSiteiniIndex + 1, grabbersGroupParallel.Count + grabbersGroup2.Count, Arguments.maxAsyncProcesses);

          Parallel.ForEach(grabbersGroupParallel, (grabber) =>
          {
            StartWegGrabInstance(grabber);
          });

          if (masterConfig.GetActiveChannels().Where(channel => channel.xmltvPrograms.Count == 0).ToList().Count == 0)
          {
            doGrabbing = false;
            break;
          }
          currentSiteiniIndex++;
        }

        //******************************
        // Create the combined xmltv EPG
        //
        epg = masterConfig.GetEpg();

        if (Arguments.convertTimesToLocal)
        {
          Console.WriteLine("Converting program times to local time");
          epg.ConvertToLocalTime();
        }

        epg.Save(masterConfig.outputFilePath);

        //******************************
        // Combine all log files
        //
        if (Arguments.combineLogFiles)
          MergeLogs();
      }
      catch (FileNotFoundException fnfe)
      {
        Console.WriteLine("ERROR! {0}", fnfe.ToString());
        return;
      }
      catch (Exception ex)
      {
        if (ex.ToString().Contains("annot find the")) //Could come from Linux based OS
          Console.WriteLine("ERROR! WebGrab+Plus.exe not found or not executable!");
        else
          Console.WriteLine(ex.ToString());
        return;
      }

      stopWatch.Stop();
      TimeSpan ts = stopWatch.Elapsed;

      //***************************************
      // Generate report with all grabbing info
      //
      GenerateChannelsReport(report);
      
      // Output names of channels with no EPG
      var n = 0;
      Console.Write("Channels with no EPG: ");
      foreach (var name in report.emptyChannels)
      {
        Console.WriteLine("{0}. {1}", ++n, name);
      }
      if (n == 0)
        Console.WriteLine("None!");

      // Calculate EPG file size and md5 hash
      report.fileSize = epg.GetFileSize();
      report.md5hash = epg.GetMD5Hash();
      report.generationTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
      report.generatedOn = DateTime.Now.ToString();

      Console.WriteLine("wgmulti finished at: " + report.generatedOn);
      Console.WriteLine("wgmulti execution time: " + report.generationTime);

      // Save report 
      if (Arguments.generateReport)
        report.Save(Arguments.reportFolder);

      Console.WriteLine("-----------------------------------------------------");
      Console.WriteLine("Total channels: {0}", report.total);
      Console.WriteLine("With EPG: {0}", report.channelsWithEpg);
      Console.WriteLine("Without EPG: {0}", report.emptyChannels.Count);
      Console.WriteLine("EPG size: {0}", report.fileSize);
      Console.WriteLine("EPG md5 hash: {0}", report.md5hash);
      Console.WriteLine("Report saved to: {0}", Path.Combine(Arguments.reportFolder, Arguments.reportFileName));
      Console.WriteLine("-----------------------------------------------------");

      //Console.ReadLine();
    }

    static void GenerateChannelsReport(Report report)
    {
      if (Arguments.generateReport)
      {
        var lastSiteIni = "";
        foreach (var channel in masterConfig.GetEnabledChannels())
        {
          report.total += 1;
          var channelInfo = new ChannelInfo(channel.name);
          if (channel.xmltvPrograms.Count > 0)
          {
            report.channelsWithEpg += 1;
            channelInfo.programsCount = channel.xmltvPrograms.Count;
            channelInfo.firstShowStartsAt = channel.xmltvPrograms[0].Attribute("start").Value;
            channelInfo.lastShowStartsAt = channel.xmltvPrograms[channelInfo.programsCount - 1].Attribute("stop").Value;
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

    static Config InitConfig()
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
      }

      // Remove all disabled channels from the list
      config.PurgeChannels();

      return config;
    }

    /// <summary>
    /// Merge all local log files into a single master log file
    /// placed in WebGrab's configuration folder
    /// </summary>
    /// <param name="logFiles">List of files to be merged</param>
    /// <param name="logFilePath">Output file</param>
    static void MergeLogs(List<String> logFiles = null, String logFilePath = null)
    {
      try
      {
        if (logFiles == null)
          logFiles = Program.logFiles;

        if (logFilePath == null)
          logFilePath = masterConfig.logFilePath;

        Console.WriteLine("Concatenating log files.");
        var outStream = File.Create(logFilePath);
        logFiles.ForEach(log =>
        {
          try
          {
            var inStream = File.OpenRead(log);
            inStream.CopyTo(outStream);
          }
          catch (Exception ex)
          {
            Console.Write(ex.Message);
          }
        });
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.ToString());
      }
    }

    static Random rnd = new Random();

    /// <summary>
    /// Create group of grabbers that will be run in a single instance of WebGrab
    /// </summary>
    static void CreateGrabberGroups()
    {
      // Create the list here so that it is reset when creating backup groups
      grabbersGroupParallel = new List<Grabber>(Arguments.maxAsyncProcesses);
      // Group channels
      var channelGroups = CreateChannelGroups();

      if (Arguments.randomStartOrder && channelGroups.Count() > 1)
        channelGroups = channelGroups.OrderBy(item => rnd.Next()).ToList();

      int i = 0;
      // Create grabbers (copy ini and config to temp location) for each group
      foreach (var group in channelGroups)
      {
        var grabber = new Grabber(group);
        if (grabber.enabled)
        {
          i++;
          if (i <= Arguments.maxAsyncProcesses)
            grabbersGroupParallel.Add(grabber);
          else
            grabbersGroup2.Add(grabber);

          if (!logFiles.Contains(grabber.config.logFilePath))
            logFiles.Add(grabber.config.logFilePath);
          Console.WriteLine("Grabber {0} initialized", grabber.id.ToUpper());
        }
      }
    }


    /// <summary>
    /// Groups group of channels depending on the GroupChannelsBySiteIni - true or false property
    /// </summary>
    /// <param name="channels"></param>
    /// <returns></returns>
    static List<ChannelGroup> CreateChannelGroups()
    {
      var channelGroups = new List<ChannelGroup>();
      try
      { 
        //if (Arguments.groupChannelsBySiteIni)
        //{
        channelGroups = (from channel in masterConfig.channels where channel.isActive
                           group channel by channel.GetActiveSiteIni().name into grouped
                           select new ChannelGroup(grouped.Key, grouped.ToList<Channel>()))
                           .ToList();
        //}
        //else
        //{
        //  // Channels are not grouped by siteini, meaning that 
        //  // A single group can have channels from various sites
        //  var n = 0;
        //  while (rootConfig.channels.Any())
        //  {
        //    var groupName = Arguments.maxChannelsInGroup > 1 ? "group" + (++n).ToString() : rootConfig.channels[0].xmltv_id;
        //    var group = new ChannelGroup(groupName);

        //    group.channels.AddRange(rootConfig.channels.Take(Arguments.maxChannelsInGroup));
        //    Channel lastAddedChannel = null;
        //    try { lastAddedChannel = rootConfig.channels[Arguments.maxChannelsInGroup - 1]; } catch { }
        //    rootConfig.channels = rootConfig.channels.Skip(Arguments.maxChannelsInGroup).ToList();

        //    // If any of the left channels have "same_as" or "period" attribute 
        //    // and "same_as" or 'xmltvId" is equal to the previous channel ones, add it to the group
        //    while (rootConfig.channels.Any())
        //    {
        //      if ((!String.IsNullOrEmpty(rootConfig.channels[0].same_as) && rootConfig.channels[0].same_as == lastAddedChannel.name)
        //        || (!String.IsNullOrEmpty(rootConfig.channels[0].period) && rootConfig.channels[0].xmltv_id == lastAddedChannel.xmltv_id))
        //      {
        //        group.channels.AddRange(rootConfig.channels.Take(1));
        //        rootConfig.channels = rootConfig.channels.Skip(1).ToList();
        //      }
        //      else
        //        break;
        //    }
        //    channelGroups.Add(group);
        //  }
        //}
        Console.WriteLine("Splitting channels into {0} groups", channelGroups.Count());
      }
      catch(Exception ex)
      {
        Console.WriteLine(ex.ToString());
      }
      return channelGroups;
    }

    /// <summary>
    /// Gets output file path depending on whether the postprocess operation is enabled or not
    /// </summary>
    /// <param name="grabber"></param>
    /// <returns></returns>
    /*public static string GetOutputPath(Grabber grabber)
    {
      if (grabber.config.postProcess.run)
        return grabber.config.postProcessOutputFilePath;
      else
        return grabber.config.outputFilePath;
    }*/
    

    static void StartWegGrabInstance(Grabber grabber)
    {
      var process = new Process();
      var startInfo = new ProcessStartInfo();
      startInfo.CreateNoWindow = false;
      startInfo.UseShellExecute = Arguments.showConsole;
      startInfo.WindowStyle = ProcessWindowStyle.Normal;
      startInfo.FileName = WgPath;
      startInfo.Arguments = String.Format("\"{0}\"", grabber.config.folder);
      process.StartInfo = startInfo;

      if (!Arguments.showConsole)
      {
        process.StartInfo.RedirectStandardOutput = true;
        process.OutputDataReceived += new DataReceivedEventHandler((sender, e) => DataReceived(sender, e, grabber));
      }

      process.Start();
      Console.WriteLine("#{0} Grabber {1} | Starting new instance of {2} with argument\n{3}",
        currentSiteiniIndex + 1, grabber.id.ToUpper(), startInfo.FileName, startInfo.Arguments);

      process.EnableRaisingEvents = true;
      process.Exited += new EventHandler((sender, e) => SingleGrabberExited(sender, e, grabber));
      if (!Arguments.showConsole)
        process.BeginOutputReadLine();
      process.WaitForExit(1000 * 60 * Arguments.processTimeout);
    }

    static void DataReceived(object sender, DataReceivedEventArgs e, Grabber grabber)
    {
      if (!String.IsNullOrEmpty(e.Data))
      {
        
        //TODO check if channels are grouped by ini
        if (masterConfig.channels.Count == 1 || (Arguments.maxAsyncProcesses == 1 && Arguments.maxChannelsInGroup == 1))
        {
          Console.WriteLine(e.Data);
          return;
        }
        var name = grabber.id.ToUpper();
        var reg = new Regex(@"xmltv_id=(.*?)\)");
        try { grabber.currentChannel = reg.Matches(e.Data)[0].Groups[1].Value; } catch { }
        if (e.Data.Contains("started") || e.Data.Contains("finished"))
        {
          Console.WriteLine("#{0} Grabber {1} | {2}", currentSiteiniIndex + 1, name, e.Data.Replace(name, ""));
        }
        if (e.Data.Contains("xmltv_id") && !e.Data.Contains("Could find existing channel"))
        {
          Console.WriteLine("#{0} Grabber {1} | starting {2}", currentSiteiniIndex + 1, name, e.Data.Replace("(   ", "").Replace("   ) " + name + " -- chan. (xmltv_id=", " ").Replace(") --", " "));
        }
        if (e.Data.ToLower().Contains("error"))
        {
          Console.WriteLine("#{0} Grabber {1} | ERROR! {2} {3}", currentSiteiniIndex + 1, name, grabber.currentChannel, e.Data);
        }

        if (e.Data.Contains("no shows in indexpage"))
        {
          Console.WriteLine("#{0} Grabber {1} | ERROR! no shows in indexpage for channel {1}", currentSiteiniIndex + 1, name, grabber.currentChannel);
        }
        if (e.Data.Contains("no index page data"))
        {
          Console.WriteLine("#{0} Grabber {1} | ERROR! {2}", currentSiteiniIndex + 1, name, e.Data);
        }
        if (e.Data.Contains("unable to update channel"))
        {
          Console.WriteLine("#{0} Grabber {1} | ERROR! {2} {3}", currentSiteiniIndex + 1, name, grabber.currentChannel, e.Data);
        }
      }
    }

    static void SingleGrabberExited(object sender, EventArgs e, Grabber currentGrabber)
    {
      currentGrabber.ParseOutput();

      Grabber temp = null;
      lock (grabbersGroup2)
      {
        if (grabbersGroup2.Count > 0)
        {
          temp = grabbersGroup2[0];
          grabbersGroup2.Remove(temp);
        }
      }
      if (temp != null)
        StartWegGrabInstance(temp);
    }
  }


  public class ChannelGroup
  {
    public String id = "";
    public List<Channel> channels = new List<Channel>();

    public ChannelGroup(String id, List<Channel> channels = null)
    {
      this.id = id;
      if (channels != null)
        this.channels = channels;
    }
    public override string ToString()
    {
      return id + " (" + channels.Count + ")";
    }
  }
}
