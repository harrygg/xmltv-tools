using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;

namespace wgmulti
{
  public class Xmltv
  {
    XDocument root = new XDocument(new XDeclaration("1.0", "utf-8", null));
    XElement tv = new XElement("tv");
    String file = "epg.xml";
    String generatorName = "";
    String generatorUrl = "";
    List<XElement> channelElements = new List<XElement>();
    List<XElement> notEmptyChannelElements = new List<XElement>();
    List<XElement> programmeElements = new List<XElement>();
    public List<String> emptyChannels = new List<String>();
    public List<String> notEmptyChannels = new List<String>();

    public Xmltv(String file = null)
    {
      if (file == null)
        return;

      root = XDocument.Load(file);
      tv = root.Element("tv");
      generatorName = tv.Attribute("generator-info-name") != null ? tv.Attribute("generator-info-name").Value : String.Empty;
      generatorUrl = tv.Attribute("generator-info-url") != null ? tv.Attribute("generator-info-url").Value : String.Empty;

      // Get all channel xml nodes
      channelElements = (from e in tv.Elements("channel") select e).ToList();
      // Get the ids of all channel elements
      var channelIds = (from e in channelElements select e.Attribute("id").Value).ToList();

      // Get all programme xml nodes
      programmeElements = (from e in tv.Elements("programme") select e).ToList();
      // Get the channel ids of all programme elements
      var programmesIds = (from e in programmeElements select e.Attribute("channel").Value).ToList();

      // Get all channel xml nodes that have no programmes
      notEmptyChannelElements.AddRange(channelElements.Where(c => programmesIds.Contains(c.Attribute("id").Value)).ToList());
      // Get the names of all channels that have programmes
      notEmptyChannels.AddRange(notEmptyChannelElements.Select(c => c.Element("display-name").Value));

      // Get the names of all channels that have no programmes
      emptyChannels.AddRange(channelElements.Where(c => !programmesIds.Contains(c.Attribute("id").Value)).Select(c => c.Element("display-name").Value).ToList());
    }


    public void Merge(Xmltv xmltv)
    {
      bool excludeEmptyChannels = Arguments.removeChannelsWithNoProgrammes;

      if (generatorName == "" && generatorUrl == "")
      {
        generatorName = xmltv.generatorName;
        generatorUrl = xmltv.generatorUrl;
      }

      if (excludeEmptyChannels)
        channelElements.AddRange(xmltv.notEmptyChannelElements);
      else
        channelElements.AddRange(xmltv.channelElements);
      programmeElements.AddRange(xmltv.programmeElements);
    }


    public void Save(String outputFile = null)
    {
      if (outputFile == null)
        outputFile = file;

      tv.Add(new XAttribute("generator-info-name", generatorName));
      tv.Add(new XAttribute("generator-info-url", generatorUrl));
      tv.Add(channelElements.ToArray());
      tv.Add(programmeElements.ToArray());
      root.Add(tv);
      root.Save(outputFile);
    }

    public void ConvertToLocalTime()
    {
      programmeElements.ForEach(programme => {
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
  }
}
