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
    static List<String> outputFiles = new List<String>();

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

      // Read main configuration file
      rootConfig = new Config(Arguments.configDir);

      if (!rootConfig.grabbingEnabled)
      {
        Console.WriteLine("Grabbing disabled in configuration. Enable by setting the postprocess grab value to on");
        return;
      }

      // Group channels and create grabbersGroupParallel, grabbersGroup2 and outputFiles
      CreateGrabberGroups();

      try
      {
        Console.WriteLine("Starting grabbers asynchronously, {0} at a time", Arguments.maxAsyncProcesses);
        Parallel.ForEach(grabbersGroupParallel, (grabber) =>
        {
          StartWegGrabInstance(grabber);
        });

        // Combine all xml guides
        Console.WriteLine("Concatenating guids from groups");
        var outputXml = rootConfig.postProcessEnabled ? rootConfig.postProcessOutputFilePath : rootConfig.outputFilePath;
        Concat(outputFiles, outputXml);
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

      //Print failed channels if any
      Console.WriteLine("\r\nNo programmes were found for the following channels:");
      if (report.missingIds.Count() > 0)
      { 
        report.missingIds.ForEach(id => {
          Console.WriteLine(id);
       });
      }

      //Save report
      report.generationTime = elapsedTime;
      report.Save(rootConfig.folder);
    }

    static Random rnd = new Random();

    static void CreateGrabberGroups()
    {
      // Group channels
      var channelGroups = GetChannelGroupsFromConfig(rootConfig.channels);
      if (Arguments.randomStartOrder)
        channelGroups = channelGroups.OrderBy(item => rnd.Next()).ToList();

      int i = 0;
      // Create grabbers (copy ini and config) for each group
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

          outputFiles.Add(GetOutputPath(grabber));
          Console.WriteLine("Grabber {0} initialized", grabber.id.ToUpper());
        }
      }
    }


    /// <summary>
    /// Groups channels depending on the GroupChannelsBySiteIni - true or false property
    /// </summary>
    /// <param name="channels"></param>
    /// <returns></returns>
    static List<ChannelGroup> GetChannelGroupsFromConfig(List<Channel> channels)
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
          var i = 0;
          while (channels.Any())
          {
            var groupName = Arguments.maxChannelsInGroup > 1 ? "group" + (++i).ToString() : channels[i].xmltvId;
            var group = new ChannelGroup(groupName);
            group.channels.AddRange(channels.Take(Arguments.maxChannelsInGroup).ToList());
            channels = channels.Skip(Arguments.maxChannelsInGroup).ToList();
            channelGroups.Add(group);
          }
        }
        Console.WriteLine("Splitted channels into {0} groups", channelGroups.Count());
      }
      catch(Exception ex)
      {
        Console.WriteLine(ex.ToString());
      }
      return channelGroups;
    }

    /// <summary>
    /// Gets output file path depending on whether the postprocess operations are enabled or not
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
    /// <param name="epgFiles"></param>
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

      epgFiles.ForEach(epgFile => {
        try
        {
          var xml = XDocument.Load(epgFile);
          var tv = xml.Elements("tv").ToList();

          AddMetaData(ref eTv, xml);
          RemoveOrphanElements(ref tv);

          var channels = (from e in tv.Elements("channel") select e).ToList();
          var programmes = (from e in tv.Elements("programme") select e).ToList();

          report.total += channels.Count;
          eChannels.AddRange(channels);

          if (Arguments.convertTimesToLocal)
            programmes.ForEach(p => ModifyTimings(ref p));
          eProgrammes.AddRange(programmes);
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex.Message);
        }
      });

      var epg = new XDocument(new XDeclaration("1.0", "utf-8", null), eTv);
      eTv.Add(eChannels.ToArray());
      eTv.Add(eProgrammes.ToArray());
      epg.Save(outputFile);
      Console.WriteLine("EPG saved to {0}", outputFile);
    }

    static void AddMetaData(ref XElement tv, XDocument xml)
    {
      if (!tv.HasAttributes)
      {
        var temp = xml.Element("tv").Attribute("generator-info-name");
        if (temp != null)
          tv.Add(new XAttribute("generator-info-name", temp.Value));

        temp = xml.Element("tv").Attribute("generator-info-name");
        if (temp != null)
          tv.Add(new XAttribute("generator-info-url", temp.Value));
      }
    }

    /// <summary>
    /// Remove all channel elements that have no associated programme elements
    /// Remove all programmes that have no channel
    /// </summary>
    /// <param name="tv"></param>
    public static void RemoveOrphanElements(ref List<XElement> tv)
    {
      var channelIds = (from e in tv.Elements("channel") select e.Attribute("id").Value).ToList();
      var programmesIds = (from e in tv.Elements("programme") select e.Attribute("channel").Value).ToList();

      //Remove all orphan channels and programmes
      tv.Descendants("programme").Where(p => !channelIds.Contains(p.Attribute("channel").Value)).Remove();
      //Save the names of all channels with missing programmes
      report.missingIds = tv.Descendants("channel")
        .Where(c => !programmesIds.Contains(c.Attribute("id").Value))
        .Select(c => c.Element("display-name").Value).ToList<String>();

      tv.Descendants("channel").Where(c => !programmesIds.Contains(c.Attribute("id").Value)).Remove();
      //Save the names of all grabbed channels
      report.presentIds = tv.Descendants("channel").Select(c => c.Element("display-name").Value).ToList();

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
