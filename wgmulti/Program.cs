using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace wgmulti
{
  public class Program
  {
    public static Config rootConfig;
    public static Report report = new Report();
    public static List<String> failedChannelIds = new List<String>();
    public static List<Grabber> grabbersGroupParallel = new List<Grabber>(Arguments.maxAsyncProcesses);
    public static List<Grabber> grabbersGroup2 = new List<Grabber>();
    static List<String> outputEpgFiles = new List<String>();
    static List<String> logs = new List<String>();

    static void Main(string[] args)
    {
      Console.WriteLine("#####################################################");
      Console.WriteLine("#                                                   #");
      Console.WriteLine("#        wgmulti for WebGrab++ by Harry_GG          #");
      Console.WriteLine("#                                                   #");
      Console.WriteLine("#####################################################");
      Console.WriteLine("");      
      var platform = Environment.OSVersion.Platform;
      Console.WriteLine(" System: {0}", platform);
      Console.WriteLine(" Arguments: {0}", Arguments.cmdArgs);
      Console.WriteLine(" ConfigDir: {0}", Arguments.configDir);
      Console.WriteLine(" MaxAsyncProcesses: {0}", Arguments.maxAsyncProcesses);
      Console.WriteLine(" GroupChannelsBySiteIni: {0}", Arguments.groupChannelsBySiteIni);
      Console.WriteLine(" MaxChannelsInGroup: {0}", Arguments.maxChannelsInGroup);
      Console.WriteLine(" ConvertTimesToLocal: {0}", Arguments.convertTimesToLocal);
      Console.WriteLine(" ShowConsole: {0}", Arguments.showConsole);
      Console.WriteLine("");
      Console.WriteLine("-----------------------------------------------------");
      Console.WriteLine("");
      Console.WriteLine("Execution started at: " + DateTime.Now);
      Stopwatch stopWatch = new Stopwatch();
      stopWatch.Start();

      try
      {
        // Read main configuration file
        rootConfig = new Config(Arguments.configDir);

        if (!rootConfig.grabbingEnabled)
        {
          Console.WriteLine("Grabbing disabled in configuration. Enable by setting the postprocess \"grab\" value to on");
          return;
        }

        // Group grabbers (create grabbersGroupParallel, grabbersGroup2 and the list of outputEpgFiles)
        // Each grabber contains one or more channels combined on a different criteria
        CreateGrabberGroups();

        if (grabbersGroupParallel.Count == 0)
        {
          Console.WriteLine("No active grabbing created. Exiting!");
          return;
        }

        // TODO
        Console.WriteLine("Starting grabbers asynchronously, {0} at a time", Arguments.maxAsyncProcesses);
        Parallel.ForEach(grabbersGroupParallel, (grabber) =>
        {
          StartWegGrabInstance(grabber);
        });

        // Combine all xml guides
        MergeEpgs();
        
        // Combine all log files
        if (Arguments.combineLogFiles)
          MergeLogs();
      }
      catch (Exception ex)
      {
        if (ex.ToString().Contains("annot find the"))
          Console.WriteLine("ERROR! WebGrab+Plus.exe not found and not executable!");
        else
          Console.WriteLine(ex.ToString());
      }

      stopWatch.Stop();
      TimeSpan ts = stopWatch.Elapsed;
      var elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
      Console.WriteLine("wgmulti execution time: " + elapsedTime);

      // Print failed channels
      if (report.missingIds.Count() > 0)
      {
        Console.WriteLine("\r\nChannels with no EPG data:");
        report.missingIds.ForEach(id => {
          Console.WriteLine(id);
       });
      }

      // Save report
      report.generationTime = elapsedTime;
      if (Arguments.generateReport)
        report.Save(rootConfig.folder);
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
      // Group channels
      var channelGroups = CreateChannelGroups(rootConfig.channels);

      if (Arguments.randomStartOrder && channelGroups.Count() > 1)
        channelGroups = channelGroups.OrderBy(item => rnd.Next()).ToList();

      int i = 0;
      // Create grabbers (copy ini and config to temp location) for each group
      foreach (var group in channelGroups)
      {
        var grabber = new Grabber(rootConfig, group.id, group.channels);
        if (grabber.enabled)
        {
          i++;
          if (i <= Arguments.maxAsyncProcesses)
            grabbersGroupParallel.Add(grabber);
          else
            grabbersGroup2.Add(grabber);

          outputEpgFiles.Add(GetOutputPath(grabber));
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
    static List<ChannelGroup> CreateChannelGroups(List<Channel> channels)
    {
      var channelGroups = new List<ChannelGroup>();
      try
      { 
        if (Arguments.groupChannelsBySiteIni)
        {
          channelGroups = (from c in channels group c by c.site into grouped
                           select new ChannelGroup(grouped.Key, grouped.ToList<Channel>())).ToList();
        }
        else
        {
          // Channels are not grouped by siteini, meaning that 
          // A single group can have channels from various sites
          var n = 0;
          while (channels.Any())
          {
            var groupName = Arguments.maxChannelsInGroup > 1 ? "group" + (++n).ToString() : channels[0].xmltvId;
            var group = new ChannelGroup(groupName);

            group.channels.AddRange(channels.Take(Arguments.maxChannelsInGroup));
            Channel lastAddedChannel = null;
            try { lastAddedChannel = channels[Arguments.maxChannelsInGroup - 1]; } catch { }
            channels = channels.Skip(Arguments.maxChannelsInGroup).ToList();

            // If any of the left channels have "same_as" or "period" attribute 
            // and "same_as" or 'xmltvId" is equal to the previous channel ones, add it to the group
            while (channels.Any())
            {
              if ((!String.IsNullOrEmpty(channels[0].sameAs) && channels[0].sameAs == lastAddedChannel.name)
                || (!String.IsNullOrEmpty(channels[0].period) && channels[0].xmltvId == lastAddedChannel.xmltvId))
              {
                group.channels.AddRange(channels.Take(1));
                channels = channels.Skip(1).ToList();
              }
              else
                break;
            }
            channelGroups.Add(group);
          }
        }
        Console.WriteLine("Split channels into {0} groups", channelGroups.Count());
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
    public static string GetOutputPath(Grabber grabber)
    {
      if (grabber.config.postProcessEnabled)
        return grabber.config.postProcessOutputFilePath;
      else
        return grabber.config.outputFilePath;
    }

    /// <summary>
    /// Concatenates all EPGs into a single one
    /// Saves it in the config directory
    /// </summary>
    /// <param name="epgFiles">List of all local EPG files to be merged</param>
    /// <param name="outputFile">File for output.</param>
    public static void MergeEpgs(List<String> epgFiles = null, String outputFile = null)
    {
      if (outputFile == null)
        outputFile = rootConfig.postProcessEnabled ? rootConfig.postProcessOutputFilePath : rootConfig.outputFilePath;

      if (epgFiles == null)
        epgFiles = outputEpgFiles;

      var xmltv = new Xmltv();
      Console.WriteLine("\nMerging EPGs, master EPG will be saved in " + outputFile);
      epgFiles.ForEach( epgFile => {
        try
        { 
          var tempXmltv = new Xmltv(epgFile);
          report.missingIds.AddRange(tempXmltv.missingChannelIds);
          report.presentIds.AddRange(tempXmltv.presentChannelIds);
          xmltv.Merge(tempXmltv);
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex.Message);
        }
      });

      xmltv.RemoveOrphans();
      if (Arguments.convertTimesToLocal)
        xmltv.ConvertToLocalTime();

      xmltv.Save(outputFile);
      Console.WriteLine("EPG saved to {0}", outputFile);
    }

    static void StartWegGrabInstance(Grabber grabber)
    {
      var process = new Process();
      var startInfo = new ProcessStartInfo();
      startInfo.CreateNoWindow = false;
      startInfo.UseShellExecute = Arguments.showConsole;
      startInfo.WindowStyle = ProcessWindowStyle.Normal;
      startInfo.FileName = "WebGrab+Plus.exe";
      startInfo.Arguments = String.Format("\"{0}\"", grabber.config.folder);
      process.StartInfo = startInfo;

      if (!Arguments.showConsole)
      {
        process.StartInfo.RedirectStandardOutput = true;
        process.OutputDataReceived += new DataReceivedEventHandler((sender, e) => DataReceived(sender, e, grabber));
      }

      process.Start();
      Console.WriteLine("Starting process {0} {1}", startInfo.FileName, startInfo.Arguments);

      process.EnableRaisingEvents = true;
      process.Exited += new EventHandler(SingleGrabberExited);
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

    static void SingleGrabberExited(object sender, EventArgs e)
    {
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
  }
}
