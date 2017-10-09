using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace wgmulti.xmltv_time_modify
{
  public class TimeModifier
  {
    static String inputXml = "epg.xml";
    static String outputXml = "";
    static String configXml = "";
    static bool convertToLocal = true;

    static void Main(string[] args)
    {
      inputXml = (args.Length > 0) ? args[0] : inputXml;
      Console.WriteLine("Input EPG file: {0}", inputXml);

      outputXml = (args.Length > 1) ? args[1] : inputXml;
      Console.WriteLine("Output EPG file: {0}", outputXml);

      configXml = (args.Length > 2) ? args[2] : configXml;
      if (configXml != "")
      {
        Console.WriteLine("Using config file: {0}", configXml);
        convertToLocal = false;
      }
      else
      {
        Console.WriteLine("No config file specified will convert all times to local time");        
      }

      XDocument xmlFile = ModifyTimingsInFile(inputXml, convertToLocal);
      Console.WriteLine("Done!");
      xmlFile.Save(outputXml);
      //Console.ReadKey();
    }

    public static XDocument ModifyTimingsInFile(String inputXml, bool convertToLocal = true)
    {
      XDocument xmlContent = XDocument.Load(inputXml);
      if (convertToLocal)
      {
        var channelGroups = (from c in xmlContent.Elements("tv").Elements("programme")
          .GroupBy(c => c.Attribute("channel").Value) select c).ToList();

        channelGroups.ForEach(programmes => {
          Console.WriteLine("{0} programmes will be modified for channel {1}", programmes.Count(), programmes.Key);
          programmes.ToList().ForEach(programme => ModifyProgramTimings(ref programme, null));
        });
      }
      else
      {
        foreach (var channel in LoadChannelsFromFile(configXml))
        {
          var programmes = (from c in xmlContent.Elements("tv").Elements("programme")
                            where c.Attribute("channel").Value == channel.Key select c).ToList();

          programmes.ForEach(p => ModifyProgramTimings(ref p, channel.Value));
          Console.WriteLine("{0} programmes modified for channel {1}", programmes.Count(), channel.Key);
        }
      }
      return xmlContent;
    }

    public static void ModifyProgramTimings(ref XElement programme, String time_error = null)
    {
      programme.Attribute("start").Value = ConvertStringTime(programme.Attribute("start").Value, time_error);
      programme.Attribute("stop").Value = ConvertStringTime(programme.Attribute("stop").Value, time_error);
    }

    /// <summary>
    /// Adds offset to given date time.
    /// If time_error is -1, 1 hour will be added
    /// iF time error is 1.5, 1.5 hours will be subtracted
    /// </summary>
    /// <param name="dateTimeString">xmltv date i.e. 20170109110000 +0200</param>
    /// <param name="time_error">time_error taken from XML config file i.e. -1.5</param>
    /// <returns>modified date as string i.e. 20170109123000 +0200</returns>
    public static String ConvertStringTime(String dateTimeString, String time_error = null)
    {
      var result = "";
      var dateFormat = "yyyyMMddHHmmss zzz";
      try
      {
        if (time_error == null) //Convert to local time
        {
          DateTime dt;
          try
          {
            //DateTimeZone localZone = DateTimeZone.SystemDefault;
            dt = DateTime.ParseExact(dateTimeString, dateFormat, CultureInfo.InvariantCulture);
          }
          catch (FormatException)
          {
            dateFormat = dateFormat.Substring(0, dateTimeString.Length);
            dt = DateTime.ParseExact(dateTimeString, dateFormat, null);
          }
          result = dt.ToString(dateFormat);
        }
        else
        {
          DateTimeOffset dto;
          try
          {
            dto = DateTimeOffset.ParseExact(dateTimeString, dateFormat, null);
          }
          catch (FormatException)
          {
            dateFormat = dateFormat.Substring(0, dateTimeString.Length);
            dto = DateTimeOffset.ParseExact(dateTimeString, dateFormat, null);
            dto = dto.AddHours(ToHours(time_error));
          }
          result = dto.ToString(dateFormat);
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(e.Message);
        result = dateTimeString;
      }
      return result.Replace(":", "");
    }

    //Reverses the number (positve to negative)
    public static Double ToHours(String time_error)
    {
      var result = (time_error.StartsWith("-")) ? time_error.Replace("-", "") : "-" + time_error.Replace("+", "");
      return Convert.ToDouble(result);
    }


    static Dictionary<String, String> LoadChannelsFromFile(String configXml)
    {
      var channels = new Dictionary<String, String>();
      if (File.Exists(configXml))
      {
        XmlDocument doc = new XmlDocument();
        doc.Load(configXml);
        XmlNode node = doc.SelectSingleNode("channels");
        foreach (XmlNode childNode in node.ChildNodes)
        {
          if (childNode.NodeType != XmlNodeType.Comment)
          {
            try
            {
              var channelName = childNode.InnerText;
              channels.Add(channelName, childNode.Attributes["time_error"].Value);
            }
            catch (Exception e)
            {
              Console.WriteLine(e.Message);
            }
          }
        }
      }
      else
      {
        Console.WriteLine("Configuration file {0} does not exist!", configXml);
      }
      return channels;
    }
  }
}
