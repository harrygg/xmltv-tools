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
    static String wgexe = "WebGrab+Plus.exe";
    public static Config rootConfig;
    public static Report report = new Report();
    public static List<String> failedChannelIds = new List<String>();
    public static List<Grabber> grabbersGroupParallel;
    public static List<Grabber> grabbersGroup2 = new List<Grabber>();
    static List<String> logs = new List<String>();
    public static Xmltv epg = new Xmltv();

    static void Main(string[] args)
    {
      Stopwatch stopWatch = new Stopwatch();
      try
      {
        Console.WriteLine("\n#####################################################");
        Console.WriteLine("#                                                   #");
        Console.WriteLine("#        wgmulti for WebGrab++ by Harry_GG          #");
        Console.WriteLine("#                                                   #");
        Console.WriteLine("#####################################################\n");
        Console.WriteLine(" System: {0}", Environment.OSVersion.Platform);
        Console.WriteLine(" Arguments: {0}", Arguments.cmdArgs);
        Console.WriteLine(" ConfigDir: {0}", Arguments.configDir);

        var versionInfo = FileVersionInfo.GetVersionInfo(wgexe);
        Console.WriteLine(" {0} version: {1}", wgexe, versionInfo.ProductVersion);

        if (Arguments.buildConfigFromJson)
        {
          Console.WriteLine(" Build config from JSON file: {0}", Arguments.buildConfigFromJson);
          Console.WriteLine(" JsonConfigFileName: {0}", Arguments.jsonConfigFileName);
        }

        Console.WriteLine(" MaxAsyncProcesses: {0}", Arguments.maxAsyncProcesses);
        Console.WriteLine(" GroupChannelsBySiteIni: {0}", Arguments.groupChannelsBySiteIni);
        //Console.WriteLine(" MaxChannelsInGroup: {0}", Arguments.maxChannelsInGroup);
        Console.WriteLine(" ConvertTimesToLocal: {0}", Arguments.convertTimesToLocal);
        Console.WriteLine(" ShowConsole: {0}", Arguments.showConsole);
        Console.WriteLine("\n-----------------------------------------------------\n");
        Console.WriteLine("Execution started at: " + DateTime.Now);


        stopWatch.Start();


        rootConfig = InitConfig();
        report.total = rootConfig.activeChannels;
        Console.WriteLine("Cofig contains {0} channels for grabbing", rootConfig.activeChannels);

        if (!rootConfig.postProcess.grab)
        {
          Console.WriteLine("Grabbing disabled in configuration. Enable by setting the postprocess 'grab' value to 'on'");
          return;
        }

        // Try grabbing all empty channels using the next available siteini 
        // until there are no more site inis or all channels have programmes
        Console.WriteLine("-----------------------------------------------------");
        var doGrabbing = true;
        var siteiniIndex = 0;
        while (doGrabbing)
        {
          //if (siteiniIndex > 0)
          //  Console.WriteLine("Checking for empty channels...");

          doGrabbing = false;
          //Call GetActiveChannels everytime as channels are disabled
          foreach (var channel in rootConfig.GetActiveChannels())
          {
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
            if (channel.siteinis.Count > siteiniIndex)
            {
              channel.activeSiteIni = siteiniIndex;
              doGrabbing = true;

              if (siteiniIndex > 0) // we are not in the first grabbing round
                Console.WriteLine("Channel '{0}' has no programs. Switching to grabber {1}",
                  channel.name, channel.GetActiveSiteIni().name.ToUpper());
            }
            else
            {
              Console.WriteLine("Channel '{0}' has no programs. No alternative siteinis found. Channel disabled.", channel.name);
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
          Console.WriteLine("---------------- Grabbing Round #{0} ------------------", siteiniIndex + 1);
          Console.WriteLine("-----------------------------------------------------");
          Console.WriteLine("Starting {0} grabbers asynchronously, {1} at a time",
            grabbersGroupParallel.Count + grabbersGroup2.Count, Arguments.maxAsyncProcesses);

          Parallel.ForEach(grabbersGroupParallel, (grabber) =>
          {
            StartWegGrabInstance(grabber);
          });

          if (rootConfig.GetActiveChannels().Where(channel => channel.xmltvPrograms.Count == 0).ToList().Count == 0)
          {
            doGrabbing = false;
            break;
          }
          siteiniIndex++;
        }

        // Create the combined xmltv EPG
        MergeEPGs();

        if (Arguments.convertTimesToLocal)
          epg.ConvertToLocalTime();

        epg.Save(rootConfig.outputFilePath);

        // Combine all log files
        //if (Arguments.combineLogFiles)
        //  MergeLogs();
      }
      catch (FileNotFoundException fnfe)
      {
        Console.WriteLine("ERROR! {0}", fnfe.Message);
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

      /// Output names of channels with no EPG
      OutputEmptyChannels();

      // Calculate EPG file size and md5 hash
      report.fileSize = epg.GetFileSize();
      report.md5hash = epg.GetMD5Hash();

      Console.WriteLine("-----------------------------------------------------");
      Console.WriteLine("Total channels: {0}", report.total);
      Console.WriteLine("With EPG: {0}", report.channels.Count);
      Console.WriteLine("Without EPG: {0}", report.emptyChannels.Count);
      Console.WriteLine("EPG size: {0}", report.fileSize);
      Console.WriteLine("EPG md5 hash: {0}", report.md5hash);
      Console.WriteLine("-----------------------------------------------------");

      stopWatch.Stop();
      TimeSpan ts = stopWatch.Elapsed;
      report.generationTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
      report.generatedOn = DateTime.Now.ToString();
      Console.WriteLine("wgmulti finished at: " + report.generatedOn);
      Console.WriteLine("wgmulti execution time: " + report.generationTime);

      // Save report    
      if (Arguments.generateReport)
        report.Save(Arguments.configDir);

      //Console.ReadLine();
    }

    static Config InitConfig()
    {
      Config config = null;

      // Check whether we have a config xml or json file
      if (Arguments.buildConfigFromJson)
      {
        var jsonConfigPath = Path.Combine(Arguments.configDir, Arguments.jsonConfigFileName);
        if (File.Exists(jsonConfigPath))
        {
          var js = new JavaScriptSerializer();
          config = js.Deserialize<Config>(File.ReadAllText(jsonConfigPath));
          config.SetAbsPaths(Arguments.configDir);
        }
      }
      else
      {
        // Read xml configuration file
        config = new Config(Arguments.configDir);
      }

      // Remove all disabled channels from the list
      config.SetActiveChannels();

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
      if (logFiles == null)
        logFiles = logs;

      if (logFilePath == null)
        logFilePath = rootConfig.logFilePath;

      Console.WriteLine("Concatenating log files.");
      using (var outStream = File.Create(logFilePath))
      {
        logFiles.ForEach(log =>
        {
          try
          { 
            using (var inStream = File.OpenRead(log))
              inStream.CopyTo(outStream);
          }
          catch (Exception ex)
          {
            Console.Write(ex.Message);
          }
        });
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

          //outputEpgFiles.Add(GetOutputPath(grabber));
          logs.Add(grabber.config.logFilePath);
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
        channelGroups = (from channel in rootConfig.channels where channel.isActive
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
      startInfo.FileName = wgexe;
      startInfo.Arguments = String.Format("\"{0}\"", grabber.config.folder);
      process.StartInfo = startInfo;

      if (!Arguments.showConsole)
      {
        process.StartInfo.RedirectStandardOutput = true;
        process.OutputDataReceived += new DataReceivedEventHandler((sender, e) => DataReceived(sender, e, grabber));
      }

      process.Start();
      Console.WriteLine("Grabber {0} | Starting new instance of {1} with argument\n{2}", 
        grabber.id.ToUpper(), startInfo.FileName, startInfo.Arguments);

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
        if (rootConfig.channels.Count == 1 || (Arguments.maxAsyncProcesses == 1 && Arguments.maxChannelsInGroup == 1))
        {
          Console.WriteLine(e.Data);
          return;
        }
        var name = grabber.id.ToUpper();
        var reg = new Regex(@"xmltv_id=(.*?)\)");
        try { grabber.currentChannel = reg.Matches(e.Data)[0].Groups[1].Value; } catch { }
        if (e.Data.Contains("started") || e.Data.Contains("finished"))
        {
          Console.WriteLine("Grabber {0} | {1}", name, e.Data.Replace(name, ""));
        }
        if (e.Data.Contains("xmltv_id") && !e.Data.Contains("Could find existing channel"))
        {
          Console.WriteLine("Grabber {0} | starting {1}", name, e.Data.Replace("(   ", "").Replace("   ) " + name + " -- chan. (xmltv_id=", " ").Replace(") --", " "));
        }
        if (e.Data.ToLower().Contains("error"))
        {
          Console.WriteLine("Grabber {0} | ERROR! {1} {2}", name, grabber.currentChannel, e.Data);
        }

        if (e.Data.Contains("no shows in indexpage"))
        {
          Console.WriteLine("Grabber {0} | ERROR! no shows in indexpage for channel {1}", name, grabber.currentChannel);
          failedChannelIds.Add(grabber.currentChannel);
        }
        if (e.Data.Contains("no index page data"))
        {
          Console.WriteLine("Grabber {0} | ERROR! {1}", name, e.Data);
          failedChannelIds.Add(grabber.currentChannel);
        }
        if (e.Data.Contains("unable to update channel"))
        {
          Console.WriteLine("Grabber {0} | ERROR! {1} {2}", name, grabber.currentChannel, e.Data);
        }
      }
    }

    static void SingleGrabberExited(object sender, EventArgs e, Grabber currentGrabber)
    {
      ParseXml(currentGrabber);

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

    static void ParseXml(Grabber currentGrabber)
    {
      Console.WriteLine("Grabber {0} | Parsing XML output", currentGrabber.id.ToUpper());
      //Parse xml file and assign programs to channels
      var xmltv = new Xmltv(currentGrabber.config.outputFilePath);
      var i = 0;
      // Iterate the channels in the EPG file, add their programmes to the active channels
      xmltv.channels.ForEach(xmltvChannel => {
        try
        {
          // If channel is offset channel, get it's parent first
          var channel = rootConfig.GetActiveChannels().First(c => c.name.Equals(xmltvChannel.Element("display-name").Value));

          if (channel.xmltvPrograms.Count == 0) // Update only if channel hasn't been populated from previous run
          {
            channel.xmltvChannel = xmltvChannel;
            channel.xmltvPrograms = xmltv.programmes.Where(p => p.Attribute("channel").Value == channel.xmltv_id).ToList();
            i += channel.xmltvPrograms.Count;

            Console.WriteLine("Grabber {0} | {1} | {2} programms grabbed",
              currentGrabber.id.ToUpper(),
              channel.name,
              channel.xmltvPrograms.Count);
          }
        }
        catch { }
      });

      if (i == 0)
        Console.WriteLine("Grabber {0} | No programs grabbed!!!", currentGrabber.id.ToUpper());
    }


    static void OutputEmptyChannels()
    {
      var n = 0;
      Console.WriteLine("Channels with no EPG:");
      foreach (var name in report.emptyChannels)
      {
        Console.WriteLine("{0}. {1}", ++n, name);
      }
      if (n == 0)
        Console.WriteLine("None");
    }

    static void MergeEPGs()
    {
      try
      {
        // Combine all xml guides into a single one
        Console.WriteLine("Saving EPG XML file");

        foreach (var channel in rootConfig.channels)
        {
          if (channel.xmltvPrograms.Count > 0)
          {
            epg.programmes.AddRange(channel.xmltvPrograms);
            epg.channels.Add(channel.xmltvChannel);
            report.channels.Add(channel.name);
          }
          else
          {
            report.emptyChannels.Add(channel.name);
          }
          epg.allChannels.Add(channel.xmltvChannel);
        };
        Console.WriteLine("Report empty channels: {0}", report.emptyChannels.Count);
      }
      catch (Exception ex)
      {
        Console.WriteLine("Unable to merge EPGs");
        Console.WriteLine(ex.ToString());
      }
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
