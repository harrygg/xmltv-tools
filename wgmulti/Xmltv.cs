using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.Security.Cryptography;
using System.IO;

namespace wgmulti
{
  public class Xmltv
  {
    XDocument root = new XDocument(new XDeclaration("1.0", "utf-8", null));
    XElement tv = new XElement("tv");
    String file = "epg.xml";
    String generatorName = "WebGrab+Plus/w MDB &amp; REX Postprocess -- version  V2.1 -- Jan van Straaten";
    String generatorUrl = "http://www.webgrabplus.com";
    String generatorAutomationTool = "wgmulti.exe";
    public List<XElement> allChannels = new List<XElement>();
    public List<XElement> channels = new List<XElement>();
    public List<XElement> programmes = new List<XElement>();
    public List<String> emptyChannelNames = new List<String>();

    public Xmltv(String file = null)
    {
      if (file == null)
        return;

      root = XDocument.Load(file);
      tv = root.Element("tv");

      if (!(tv.Attribute("generator-info-name") != null))
        generatorName = tv.Attribute("generator-info-name").Value;

      if (!(tv.Attribute("generator-info-url") != null))
        generatorUrl = tv.Attribute("generator-info-url").Value;

      // Get all channel xml nodes
      allChannels = (from e in tv.Elements("channel") select e).ToList();
      // Get the ids of all channel elements
      //var channelIds = (from e in channels select e.Attribute("id").Value).ToList();

      // Get all programme xml nodes
      programmes = (from e in tv.Elements("programme") select e).ToList();
      // Get the channel ids of all programme elements
      var programmesIds = (from e in programmes select e.Attribute("channel").Value).ToList();

      // Get all channel xml nodes that have programmes
      channels.AddRange(allChannels.Where(c => programmesIds.Contains(c.Attribute("id").Value)).ToList());
      // Get the names of all channels that have programmes
      //notEmptyChannelNames.AddRange(channels.Select(c => c.Element("display-name").Value));

      // Get the names of all channels that have no programmes
      emptyChannelNames.AddRange(allChannels.Where(c => !programmesIds.Contains(c.Attribute("id").Value)).Select(c => c.Element("display-name").Value).ToList());
    }

    public void Save(String outputFile = null)
    {
      if (outputFile == null)
        outputFile = file;
      else
        file = outputFile;

      tv.Add(new XAttribute("generator-info-name", generatorName));
      tv.Add(new XAttribute("generator-info-url", generatorUrl));
      tv.Add(new XAttribute("generator-info-automation-tool", generatorAutomationTool));

      if (Arguments.removeChannelsWithNoProgrammes)
        tv.Add(channels.ToArray());
      else
        tv.Add(allChannels.ToArray());

      tv.Add(programmes.ToArray());
      root.Add(tv);
      root.Save(outputFile);
    }

    public void ConvertToLocalTime()
    {
      programmes.ForEach(programme => {
        programme.Attribute("start").Value = ConvertToLocal(programme.Attribute("start").Value);
        programme.Attribute("stop").Value = ConvertToLocal(programme.Attribute("stop").Value);
      });
    }

    public String ConvertToLocal(String dateTimeString)
    {
      var dDateTime = DateTime.ParseExact(dateTimeString, Config.dateFormat, null);
      dateTimeString = dDateTime.ToString(Config.dateFormat);
      return dateTimeString.Replace(":", "");
    }

    public String GetMD5Hash()
    {
      try
      {
        var md5 = MD5.Create();
        var stream = File.OpenRead(file);
        return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", String.Empty).ToLower();
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.ToString());
        return String.Empty;
      }
    }

    public String GetFileSize()
    {
      try
      {
        var fi = new FileInfo(file);
        return fi.Length.ToString();
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.ToString());
        return String.Empty;
      }
    }
  }
}
