using System;
using System.Collections.Generic;
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
          // correct to given offset i.e. +01:00
          if (correction.Contains(":"))
          {
            var ts = TimeSpan.Parse(correction.Trim('+'), null);
            dto = dto.Add(ts);
            //dto = dto.ToOffset(ts);
          }
          // correct with given hours
          else
          {
            dto = DateTimeOffset.ParseExact(intputDateTime, dateFormat, null);
            dto = dto.AddHours(Convert.ToDouble(correction));
          }
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

    public static bool HasOffset(String datetimeString)
    {
      if (datetimeString.Trim().Contains(" ") 
        || datetimeString.Trim().Contains(":") 
        || datetimeString.Trim().Contains("-") 
        || datetimeString.Trim().Contains("+"))
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
