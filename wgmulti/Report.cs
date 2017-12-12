using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System.Security.Cryptography;

namespace wgmulti
{
  public class Report
  {
    public Report(){}

    public List<ChannelInfo> channels = new List<ChannelInfo>();
    public List<String> emptyChannels = new List<String>();
    public int total = 0;
    public int channelsWithoutEpg = 0;
    public int channelsWithEpg = 0;
    public String generationTime = String.Empty;
    public String generatedOn = String.Empty;
    public String fileSize = String.Empty;
    public String md5hash = String.Empty;

    public void Generate(Config config)
    {
      try
      {
        foreach (var channel in config.GetChannels(includeOffset: true))
        {
          total += 1;
          var channelInfo = new ChannelInfo(channel.name);
          if (channel.HasPrograms)
          {
            channelsWithEpg += 1;
            channelInfo.siteiniIndex = channel.siteiniIndex;
            channelInfo.programsCount = channel.xmltv.programmes.Count;
            channelInfo.firstShowStartsAt = channel.xmltv.programmes[0].Attribute("start").Value;
            channelInfo.lastShowStartsAt = channel.xmltv.programmes[channelInfo.programsCount - 1].Attribute("stop").Value;

            if (!channel.IsTimeshifted)
              channelInfo.siteiniName = channel.GetActiveSiteIni().name;
            else
              channelInfo.siteiniName = channel.parent.GetActiveSiteIni().name;
          }
          else
          {
            channelsWithoutEpg += 1;
            emptyChannels.Add(channel.name);
          }
          channels.Add(channelInfo);
        }

        fileSize = GetFileSize(config.outputFilePath);
        md5hash = GetMD5Hash(config.outputFilePath);
        generatedOn = DateTime.Now.ToString();

        // Output names of channels with no EPG
        var n = 0;
        Log.Info("Channels with no EPG: ");
        foreach (var name in emptyChannels)
          Log.Info(String.Format("{0}. {1}", ++n, name));
        if (n == 0)
          Log.Info("None!");

        var ts = Application.stopWatch.Elapsed;
        generationTime = String.Format("{0:00}:{1:00}:{2:00}", ts.Hours, ts.Minutes, ts.Seconds);
      }
      catch (Exception ex)
      {
        Log.Error(ex.Message);
      }
    }

    String GetMD5Hash(String file)
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

    String GetFileSize(String file)
    {
      try
      {
        var fi = new FileInfo(file);
        return fi.Length.ToString();
      }
      catch (Exception ex)
      {
        Log.Error(ex.ToString());
        return String.Empty;
      }
    }

    public void Save()
    {
      try
      {
        if (!String.IsNullOrEmpty(Arguments.reportFolder) && !Directory.Exists(Arguments.reportFolder))
          Directory.CreateDirectory(Arguments.reportFolder);

        var serializer = new JavaScriptSerializer();
        var json = serializer.Serialize(this);
        File.WriteAllText(Arguments.reportFilePath, json);
        Console.WriteLine("Report saved to " + Arguments.reportFilePath);
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.ToString());
      }
    }
  }

  public class ChannelInfo
  {
    public String name = String.Empty;
    public String siteiniName = String.Empty;
    public int siteiniIndex = 0;
    public String firstShowStartsAt = String.Empty;
    public String lastShowStartsAt = String.Empty;
    public int programsCount = 0;

    public ChannelInfo(String name)
    {
      this.name = name;
    }
  }
}
