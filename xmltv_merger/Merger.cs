using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace wgmulti.xmltv_merger
{
  public class XmltvMerger
  {
    static void Main(String[] args)
    {
      Console.WriteLine("#####################################################");
      Console.WriteLine("#            xmltv_merger by Harry_GG               #");
      Console.WriteLine("#---------------------------------------------------#");
      var doc = MergeContentFromFiles(args.ToList());
      doc.Save("merged_epg.xml");
      Console.WriteLine("EPG saved to merged_epg.xml");
    }


    public static XDocument MergeContentFromFiles(List<String> files)
    {
      var eTv = new XElement("tv");
      var channelNames = new List<String>();
      //var channelIds = new List<String>();
      var eChannels = new List<XElement>();
      var eProgrammes = new List<XElement>();

      files.ForEach(file => {
        try
        {
          Console.WriteLine("Merging content from " + file);
          var xml = XDocument.Load(file); 
          var tv = xml.Elements("tv").ToList();

          AddMetaData(ref eTv, xml);
          RemoveOrphanElements(ref tv);

          //Separate channels from programmes. Get only the ones we don't already have
          var channels = (from e in tv.Elements("channel")
                          where !channelNames.Contains(e.Element("display-name").Value) select e).ToList();
          eChannels.AddRange(channels);

          //Select only programes that has not already been added
          var programmes = (from p in tv.Elements("programme")
                            where !channelNames.Contains(p.Attribute("channel").Value) select p).ToList();
          eProgrammes.AddRange(programmes);

          //Update the list of channel names
          channelNames.AddRange((from c in channels select c.Element("display-name").Value).ToList());
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex.Message);
          return;
        }
      });

      //Add all channels and then programmes
      eTv.Add(eChannels.ToArray());
      eTv.Add(eProgrammes.ToArray());
      return new XDocument(new XDeclaration("1.0", "utf-8", null), eTv);
    }

    // Removes channels that have no programmes and programmes that have no associated channels
    public static void RemoveOrphanElements(ref List<XElement> tv)
    {
      var channelIds = (from e in tv.Elements("channel") select e.Attribute("id").Value).ToList();
      var programmesIds = (from e in tv.Elements("programme") select e.Attribute("channel").Value).ToList();

      //Remove all orphan channels and programmes
      tv.Descendants("programme").Where(p => !channelIds.Contains(p.Attribute("channel").Value)).Remove();
      tv.Descendants("channel").Where(c => !programmesIds.Contains(c.Attribute("id").Value)).Remove();   
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
  }
}
