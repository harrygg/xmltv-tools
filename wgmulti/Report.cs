using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace wgmulti
{
   public class Report
  {
    public static List<GrabbingRoundStats> grabbingRoundsData = new List<GrabbingRoundStats>();
    public static int totalChannels = 0;
    public static List<Channel> emptyChannels;
    public static int channelsWithoutEpg = 0;
    public static int channelsWithEpg = 0;
    public static DateTime executionStartTime;
    public static DateTime executionEndTime;
    public static String TotalDurationFormatted = String.Empty;
    public static String generatedOn = String.Empty;
    public static String fileSize = String.Empty;
    public static String md5hash = String.Empty;
    public static string wgVersionInfo;
    public static Version wgmultiVersion;
    public static DateTime wgmultiBuildDate;

    public static Config ActiveConfig { get; internal set; }
    
    static String GenerateHtml()
    {
      Log.Debug("Started HTML report generation");
      var html = new StringBuilder();
      var i = 0;

      try
      {
        var channels = ActiveConfig.GetChannels(includeOffset: true).ToList();
        totalChannels = channels.Count();
        channelsWithEpg = channels.Where(c => c.HasPrograms).Count();
        emptyChannels = channels.Where(c => !c.HasPrograms).ToList();
        channelsWithoutEpg = emptyChannels.Count();
        fileSize = GetFileSize(ActiveConfig.outputFilePath);
        md5hash = GetMD5Hash(ActiveConfig.outputFilePath);
        generatedOn = DateTime.Now.ToString();

        html.Append($"<html>\r\n<header>\r\n<title>WGMULTI Report from {generatedOn}</title>\r\n<style>td, th {{padding: 3px; }}\r\n</style>\r\n</header>\r\n<body>");
        html.Append("<h2>General info:</h2>\r\n");
        html.Append($"Total channels: {totalChannels}<br />\r\n");
        html.Append($"With EPG: {channelsWithEpg}<br />\r\n");
        html.Append($"Without EPG: {channelsWithoutEpg}<br />\r\n");
        html.Append($"Grabbing started on: {executionStartTime}<br />\r\n");
        html.Append($"Grabbing ended on: {executionEndTime}<br />\r\n");
        html.Append($"Total execution time: {executionEndTime.Subtract(executionStartTime)}<br />\r\n");
        html.Append($"Grabbing rounds: {Application.grabbingRoundNumber}<br />\r\n");
        html.Append($"wgmulti.exe version: {Report.wgmultiVersion} built on {Report.wgmultiBuildDate}<br />\r\n");
        html.Append($"{ Arguments.wgexe}: { Report.wgVersionInfo}<br />\r\n");
        html.Append($"EPG size: {fileSize}<br />\r\n");
        html.Append($"EPG md5 hash: {md5hash}<br />\r\n");
        html.Append("<a href='https://raw.githubusercontent.com/harrygg/EPG/master/epg.xml.gz'>Download</a><br />\r\n");
        html.Append("<h2>Channels:</h2>\r\n");
        html.Append("<table border='1' cellspacing='0' cellpadding='0'><tr><th></th><th>Name</th>\r\n");
        html.Append("<th>Siteini</th><th>Grabbing round</th><th>Start time</th><th>End time</th><th>Programs</th></tr>\r\n");

        channels.ForEach(channel =>
        {
          i++;
          var color = channel.xmltv.programmes.Count > 0 ? "lime" : "red";
          var attemptedSiteinis = "";
          try
          {
            if (channel.HasPrograms)
              attemptedSiteinis = !channel.IsTimeshifted ? channel.GetActiveSiteIni().name : channel.parent.GetActiveSiteIni().name;
            else
              attemptedSiteinis = string.Join(", ", channel.siteinis.Select(s => s.name));

            html.Append($"<tr><td style='background-color: {color}'>{i}</td>");
            html.Append($"<td>{channel.name}</td><td>{attemptedSiteinis}</td>");
            html.Append($"<td>{channel.siteiniIndex + 1}</td>");
            if (channel.HasPrograms)
            {
              html.Append($"<td>{channel.xmltv.programmes[0].Attribute("start").Value}</td>");
              html.Append($"<td>{channel.xmltv.programmes[channel.xmltv.programmes.Count - 1].Attribute("stop").Value}</td>");
            }
            else
            {
              html.Append($"<td></td><td></td>");
            }
            html.Append($"<td>{channel.xmltv.programmes.Count}</td></tr>\r\n");
          }
          catch (Exception ex)
          {
            html.Append($"<td style='backgorund-color: red;' colspan='5'>{ex.ToString()}</td></tr>\r\n");
          }
        });

        html.Append("</table>\r\n");

        html.Append("<h2 id='rounds'>Grabbing rounds:</h2>\r\n");

        grabbingRoundsData.ForEach( gr => {
          html.Append($"<h3 id='round'>Round #{gr.roundNumber + 1}</h2>\r\n");
          html.Append($"Started at: {gr.startTime}<br />\r\n");
          html.Append($"Ended at: {gr.endTime}<br />\r\n");
          html.Append($"Duration: {gr.TotalDurationFormatted}<br />\r\n");
          html.Append("<table border='1' cellspacing='0' cellpadding='0'>\r\n");
          html.Append("<tr><th>#</th><th>Siteini name</th><th>Grabbing time</th><th>Used for channels</th><th>Total programs grabbed</th></tr>\r\n");
          i = 0;
          //var ordered = gr.grabbingTimes.OrderByDescending(gt => gt.Value);
          foreach (var entry in gr.grabbingTimes)
          {
            try
            {
              List<Channel> channelsForGrabber = channels.Where(channel => 
                channel.GetSiteiniAtIndex(gr.roundNumber) != null && channel.GetSiteiniAtIndex(gr.roundNumber).name == entry.Key).ToList();
              var channelsForGrabberCount = channelsForGrabber.Count();
              var programmesForGrabber = channelsForGrabber.Select(channel => channel.xmltv.programmes.Count).Sum();
              var color = programmesForGrabber == 0 ? "red" : "lime";
              html.Append($"<tr>");
              html.Append($"<td>{++i}</td>");
              html.Append($"<td style='background-color: {color}'>{entry.Key}</td>");
              html.Append($"<td>{TimeSpan.FromSeconds(entry.Value)}</td>");
              html.Append($"<td>{channelsForGrabberCount}</td>");
              html.Append($"<td>{programmesForGrabber}</td>");
              html.Append($"</tr>");
            }
            catch (Exception ex)
            {
              html.Append($"<tr><td>{++i}</td><td style='background-color: red'>{entry.Key}</td><td colspan='3'>{ex.ToString()}</td></tr>\r\n");
            }
          }
          html.Append("</table>\r\n");
        });

        html.Append("</body>\r\n</html>");
        Log.Debug("Finished HTML report generation");
      }
      catch (Exception ex)
      {
        Log.Error(ex.Message);
      }
      return html.ToString();
    }

    static String GetMD5Hash(String file)
    {
      try
      {
        var md5 = MD5.Create();
        var stream = File.OpenRead(file);
        return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", String.Empty).ToLower();
      }
      catch (Exception ex)
      {
        Log.Error(ex.ToString());
        return String.Empty;
      }
    }

    static String GetFileSize(String file)
    {
      try
      {
        string[] suffixes = { "B", "KB", "MB", "GB" };
        int s = 0;
        long size = new FileInfo(file).Length;

        while (size >= 1024)
        {
          s++;
          size /= 1024;
        }
        return String.Format("{0} {1}", size, suffixes[s]);
      }
      catch (Exception ex)
      {
        Log.Error(ex.ToString());
        return String.Empty;
      }
    }

    public static void Save()
    {
      try
      {
        if (!String.IsNullOrEmpty(Arguments.reportFolder) && !Directory.Exists(Arguments.reportFolder))
          Directory.CreateDirectory(Arguments.reportFolder);

        //var serializer = new JavaScriptSerializer();
        //var json = serializer.Serialize(this);
        //File.WriteAllText(Arguments.reportFilePath, json);
        //Console.WriteLine("Report saved to " + Arguments.reportFilePath);

        //if (Arguments.saveStandaloneReports)
        //{
        //  Console.WriteLine("Saving separate report files");
        //  foreach (var c in channelInfos)
        //  {
        //    File.WriteAllText(Path.Combine(Arguments.reportFolder, c.id + ".report.json"), c.ToString());
        //  }
        //}

        // Save HTML report
        File.WriteAllText(Path.Combine(Arguments.reportFolder, Arguments.reportFileName), GenerateHtml());

      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.ToString());
      }
    }
  }

  public class GrabbingRoundStats
  {
    public Dictionary<string, int> grabbingTimes = new Dictionary<string, int>();
    public DateTime startTime;
    public DateTime endTime;
    public int roundNumber = 0;
    public TimeSpan TotalDuration { get { return endTime.Subtract(startTime); } }
    public string TotalDurationFormatted { get { return TotalDuration.ToString(@"hh\:mm\:ss"); } }
    public GrabbingRoundStats(int roundNumber)
    {
      this.roundNumber = roundNumber;
      startTime = DateTime.Now;
    }
  }
}
