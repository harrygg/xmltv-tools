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
    static bool async = true;
    public static List<String> failedChannelIds = new List<String>();
    public static List<Grabber> grabbersGroupParallel = new List<Grabber>(Arguments.maxAsyncProcesses);
    public static List<Grabber> grabbersGroup2 = new List<Grabber>();
    
    static void Main(string[] args)
    {
      Console.WriteLine("#####################################################");
      Console.WriteLine("#                                                   #");
      Console.WriteLine("#        wgmulti for WebGrab++ by Harry_GG          #");
      Console.WriteLine("#                                                   #");
      Console.WriteLine("#####################################################");
      Console.WriteLine("");
      var platform = Environment.OSVersion.Platform;
      Console.WriteLine("wgmulti started on {0}\nArguments: {1}", platform, Arguments.cmdArgs);
      Console.WriteLine("Using config folder: {0}", Arguments.configDir);
      Console.WriteLine("Max WebGrab instances: {0}", Arguments.maxAsyncProcesses);
      Console.WriteLine("Convert programmes' times to local time: {0}", Arguments.convertTimesToLocal);
      Console.WriteLine("Show WebGrab+Plus Console: {0}", Arguments.showConsole);
      Console.WriteLine("");
      Console.WriteLine("Execution started at: " + DateTime.Now);
      Stopwatch stopWatch = new Stopwatch();
      stopWatch.Start();
      
      rootConfig = new Config(Arguments.configDir);
      if (rootConfig.grabbingEnabled)
      {
        var channelGroups = from c in rootConfig.channels group c by c.site;

        Console.WriteLine("Splitting channels into groups according to siteini");
        Console.WriteLine("{0} grabbers will be created", channelGroups.Count());
        int i = 0;
        var outputFiles = new List<String>();

        foreach (var group in channelGroups)
        {
          var grabber = new Grabber(rootConfig, group.Key, group.ToList<Channel>());
          if (grabber.enabled)
          {
            i++;
            if (i <= Arguments.maxAsyncProcesses)
              grabbersGroupParallel.Add(grabber);
            else
              grabbersGroup2.Add(grabber);

            outputFiles.Add(GetOutputPath(grabber));
            Console.WriteLine("Grabber {0} initialized", grabber.id.ToUpper());
          }
        }

        if (grabbersGroupParallel.Count > 0)
        {
          // Start all grabbers synchronously
          if (!async)
          { 
            foreach (var grabber in grabbersGroupParallel)
              StartWegGrabInstance(grabber);
            foreach (var grabber in grabbersGroup2)
              StartWegGrabInstance(grabber);
          }
          else
          {
            // Start all grabbers asyncronously
            Console.WriteLine("Starting grabbers asynchronously, {0} at a time", Arguments.maxAsyncProcesses);
            Parallel.ForEach(grabbersGroupParallel, (grabber) =>
            {
              StartWegGrabInstance(grabber);
            });
          }
          // Combine all xml guides
          var outputXml = rootConfig.postProcessEnabled ? rootConfig.postProcessOutputFilePath : rootConfig.outputFilePath;
          //XmltvMerger.Merge(outputFiles.ToArray(), outputXml);
          Concat(outputFiles, outputXml);
          //Wgtools.XmltvTimeModifier.Modify(outputXml);
        }

        stopWatch.Stop();
        TimeSpan ts = stopWatch.Elapsed;
        var elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
        Console.WriteLine("wgmulti execution time: " + elapsedTime);
        //Save report
        report.generationTime = elapsedTime;
        report.Save(rootConfig.folder);

        if (Arguments.deleteWorkFolder)
        {
          Directory.Delete(rootConfig.tempDir, true);
        }
      }
      else
      {
        Console.WriteLine("Grabbing disabled in configuration. Enable by setting the postprocess grab value to on");
        if (rootConfig.postProcessEnabled)
          Console.WriteLine("There is no point of using wgmulti for only postprocess tasks. ");
      }
      Console.ReadLine();
    }

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
    /// <param name="grabbers"></param>
    /// <param name="outputFile">File for output.</param>
    public static void Concat(List<String> epgFiles, String outputFile = "")
    {
      if (outputFile == "")
        outputFile = rootConfig.outputFilePath;
      Console.WriteLine("");
      Console.WriteLine("Combining output, saving EPG in " + outputFile);
      var eTv = new XElement("tv");
      var eChannels = new List<XElement>();
      var eProgrammes = new List<XElement>();

      foreach (var epgFile in epgFiles)
      {
        if (File.Exists(epgFile))
        { 
          var grabberXml = XDocument.Load(epgFile);
          if (!eTv.HasAttributes)
          {
            eTv.Add(new XAttribute("generator-info-name", grabberXml.Element("tv").Attribute("generator-info-name").Value));
            eTv.Add(new XAttribute("generator-info-url", grabberXml.Element("tv").Attribute("generator-info-url").Value));
          }

          var channels = (from e in grabberXml.Elements("tv").Elements("channel") select e).ToList<XElement>();
          var programmes = (from e in grabberXml.Elements("tv").Elements("programme") select e).ToList<XElement>();

          report.total += channels.Count;
          channels = RemoveEmptyChannels(channels, programmes);
          eChannels.AddRange(channels);

          if (Arguments.convertTimesToLocal)
            programmes.ForEach(p => ModifyTimings(ref p));
          eProgrammes.AddRange(programmes);

        }
      }
      var epg = new XDocument(new XDeclaration("1.0", "utf-8", null), eTv);
      eTv.Add(eChannels.ToArray());
      eTv.Add(eProgrammes.ToArray());
      epg.Save(outputFile);
      Console.WriteLine("EPG saved to {0}", outputFile);
    }

    public static List<XElement> RemoveEmptyChannels(List<XElement> channelsFromGrabber, List<XElement> programmes)
    {
      var channels = new List<XElement>();
      try
      {
        var grabberProgrammesIds = programmes.GroupBy(x => x.Attribute("channel").Value);
        var ids = (from e in grabberProgrammesIds select e.Key).ToArray<String>();
        
        foreach (var channel in channelsFromGrabber)
        {
          var id = channel.Attribute("id").Value;
          if (ids.Contains(id))
          {
            report.presentIds.Add(id);
            channels.Add(channel);
          }
          else
            report.missingIds.Add(id);
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine("ERROR while removing empty channels " + ex.ToString());
        return channelsFromGrabber;
      }
      return channels;
    }

    public static void ModifyTimings(ref XElement programme, Double timeOffset = 0)
    {
      programme.Attribute("start").Value = ConvertToLocal(programme.Attribute("start").Value);
      programme.Attribute("stop").Value = ConvertToLocal(programme.Attribute("stop").Value);

    }

    public static String ConvertToLocal(String dateTimeString)
    {
      var dDateTime = DateTime.ParseExact(dateTimeString, Config.dateFormat, null);
      dateTimeString = dDateTime.ToString(Config.dateFormat);
      return dateTimeString.Replace(":", "");
    }

    static void StartWegGrabInstance(Grabber grabber)
    {
      var process = new Process();
      var startInfo = new ProcessStartInfo();
      startInfo.CreateNoWindow = false;
      startInfo.UseShellExecute = Arguments.showConsole;
      startInfo.WindowStyle = ProcessWindowStyle.Normal;
      startInfo.FileName = "WebGrab+Plus.exe";
      startInfo.Arguments = grabber.config.folder;
      process.StartInfo = startInfo;

      if (!Arguments.showConsole)
      {
        process.StartInfo.RedirectStandardOutput = true;
        process.OutputDataReceived += new DataReceivedEventHandler((sender, e) => DataReceived(sender, e, grabber));
      }

      process.Start();
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
      lock(grabbersGroup2)
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
}
