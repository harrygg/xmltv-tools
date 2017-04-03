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
    List<XElement> channels = new List<XElement>();
    List<XElement> programmes = new List<XElement>();
    public List<String> missingChannelIds = new List<String>();
    public List<String> presentChannelIds = new List<String>();

    public Xmltv(String file = null)
    {
      if (file != null)
      {
        root = XDocument.Load(file);
        tv = root.Element("tv");
        generatorName = tv.Attribute("generator-info-name") != null ? tv.Attribute("generator-info-name").Value : String.Empty;
        generatorUrl = tv.Attribute("generator-info-url") != null ? tv.Attribute("generator-info-url").Value : String.Empty;
        channels = (from e in tv.Elements("channel") select e).ToList();
        programmes = (from e in tv.Elements("programme") select e).ToList();
      }
    }


    public void Merge(Xmltv xmltv)
    {
      if (generatorName == "" && generatorUrl == "")
      {
        generatorName = xmltv.generatorName;
        generatorUrl = xmltv.generatorUrl;
      }
      channels.AddRange(xmltv.channels);
      programmes.AddRange(xmltv.programmes);
    }


    public void Save(String outputFile = null)
    {
      if (outputFile == null)
        outputFile = file;

      tv.Add(new XAttribute("generator-info-name", generatorName));
      tv.Add(new XAttribute("generator-info-url", generatorUrl));
      tv.Add(channels.ToArray());
      tv.Add(programmes.ToArray());
      root.Add(tv);
      root.Save(outputFile);
    }

    /// <summary>
    /// Remove all orphan channels and programmes
    /// </summary>
    public void RemoveOrphans()
    {
      var channelIds = (from e in tv.Elements("channel") select e.Attribute("id").Value).ToList();
      var programmesIds = (from e in tv.Elements("programme") select e.Attribute("channel").Value).ToList();

      tv.Descendants("programme").Where(p => !channelIds.Contains(p.Attribute("channel").Value)).Remove();
      missingChannelIds.AddRange(tv.Descendants("channel")
        .Where(c => !programmesIds.Contains(c.Attribute("id").Value))
        .Select(c => c.Element("display-name").Value).ToList());

      tv.Descendants("channel").Where(c => !programmesIds.Contains(c.Attribute("id").Value)).Remove();
      presentChannelIds.AddRange(tv.Descendants("channel").Select(c => c.Element("display-name").Value).ToList());
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
  }
}
