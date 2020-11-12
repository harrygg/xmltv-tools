using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace xmltv_time_modify
{
  public class Configuration
  {
    public String inputXml = "epg.xml";
    public String defaultOutputXml = "epg_corrected.xml";
    public String outputXml = "epg_corrected.xml";
    public String channels;
    public String correction = "local";
    public bool convertToLocal = true;
    public bool applyCorrectionToAll = true;
    public bool displayHelp = false;
    public bool removeOffset = false;

    public Dictionary<String, String> channelsToModify = new Dictionary<String, String>();

    public Configuration(string[] args)
    {
      foreach(var arg in args)
      {
        var _arg = arg.Substring(1);

        if (_arg == "h" || _arg == "?")
          displayHelp = true;

        else if (_arg.StartsWith("in:"))
        {
          inputXml = _arg.Replace("in:", "");
          if (!File.Exists(inputXml))
            throw new ArgumentException("Input file does not exist");
        }

        else if (_arg.StartsWith("out:"))
          outputXml = _arg.Replace("out:", "");
        

        else if (_arg.StartsWith("correction:"))
        {
          correction = _arg.Replace("correction:", "");
          if (correction.ToLower() != "local")
            convertToLocal = false;
        }

        else if (_arg.StartsWith("channels:"))
        {
          channels = _arg.Replace("channels:", "");
        }

        else if (_arg == ("ro"))
        {
          removeOffset = true;
        }

      }

      if (channels != null && channels.ToLower() != "all")
      {
        applyCorrectionToAll = false;
        if (channels.EndsWith(".xml"))
        {
          Console.WriteLine("Loading list of channels from file " + channels);
          channelsToModify = GetChannels(channels);
        }
        else if (channels.EndsWith(".ini"))
        {
          channelsToModify = ParseIni(channels);
        }
        else
        {
          Console.WriteLine("" + channels);
          channels = channels.Replace("\"", "");
          string[] names = channels.Split(',');
          foreach (var name in names)
            channelsToModify.Add(name.Trim(), correction);
        }
      }
    }

    private Dictionary<string, string> ParseIni(string filePath)
    {
      var channels = new Dictionary<string, string>();
      int counter = 0;
      string line;
      var file = new StreamReader(filePath);
      while ((line = file.ReadLine()) != null)
      {
        if (line.StartsWith("#"))
          continue;
        var channelInfo = line.Split('=');
        channels.Add(channelInfo[0].Trim(), channelInfo[1].Trim());  
        counter++;
      }
      return channels;
    }

    private Dictionary<String, String> GetChannels(String channelsXml)
    {
      var channels = new Dictionary<String, String>();
      if (!File.Exists(channelsXml))
        throw new ArgumentException(channelsXml + " does not exist");
      
      XmlDocument doc = new XmlDocument();
      doc.Load(channelsXml);
      XmlNode node = doc.SelectSingleNode("channels");
      foreach (XmlNode childNode in node.ChildNodes)
      {
        if (childNode.NodeType != XmlNodeType.Comment)
        {
          try
          {
            var id = childNode.Attributes["id"].Value.Trim();
            channels.Add(id, childNode.Attributes["correction"].Value.Trim());
          }
          catch (Exception e)
          {
            Console.WriteLine(e.Message);
          }
        }
      }

      return channels;
    }
  }
}
