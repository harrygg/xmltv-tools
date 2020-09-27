using System;
using System.Globalization;
using System.Xml.Linq;

namespace xmltv_time_modify
{
  public class Utils
  {
    public static void ModifyProgramTimings(ref XElement programme, String correction, bool removeOffset = false)
    {
      var _time = ApplyCorrection(programme.Attribute("start").Value, correction);
      if (removeOffset)
        _time = StripOffset(_time);
      programme.Attribute("start").Value = _time;

      _time = ApplyCorrection(programme.Attribute("stop").Value, correction);
      if (removeOffset)
        _time = StripOffset(_time);
      programme.Attribute("stop").Value = _time;
    }

    public static String ApplyCorrection(String intputDateTime, String correction)
    {
      var result = "";
      var dateFormat = HasOffset(intputDateTime) ? "yyyyMMddHHmmss K" : "yyyyMMddHHmmss";
      DateTimeOffset dto;

      try
      {
        dto = DateTimeOffset.ParseExact(intputDateTime, dateFormat, null);
        if (correction.ToLower() == "local")
        {
          dto = dto.ToLocalTime();
        } 
        else if (correction.ToLower() == "utc")
        {
          dto = dto.ToUniversalTime();
        }
        else
        {
          dto = DateTimeOffset.ParseExact(intputDateTime, dateFormat, null);
          dto = dto.AddHours(Convert.ToDouble(correction));
        }

        result = dto.ToString("yyyyMMddHHmmss K").Replace(":", "");       
      }
      catch (Exception e)
      {
        Console.WriteLine(e.Message);
        result = intputDateTime;
      }
      return result;
    }

    //Reverses the number (positve to negative)
    public static Double ToHours(String correction)
    {
      var result = (correction.StartsWith("-")) ? correction.Replace("-", "") : "-" + correction.Replace("+", "");
      return Convert.ToDouble(result);
    }

    public static bool HasOffset(String datetimeString)
    {
      if (datetimeString.Contains(" +") || datetimeString.Contains(" -"))
        return true;
      return false;
    }

    public static String StripOffset(String datetimeString)
    {
      if (HasOffset(datetimeString))
        return datetimeString.Split(' ')[0];
      return datetimeString;
    }
  }
}
