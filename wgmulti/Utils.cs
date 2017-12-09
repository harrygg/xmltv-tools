using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace wgmulti
{
  public class Utils
  {


    /// <summary>
    /// Creates and gets the name of the temp folder of the current grabber/copier. 
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static String CreateGrabberTempDir(String name)
    {
      try
      {
        var localDir = Path.Combine(Arguments.grabingTempFolder, name);
        if (!Directory.Exists(localDir))
          Directory.CreateDirectory(localDir);
        return localDir;
      }
      catch (Exception)
      {
        Log.Error("Unable to create temp dir " + name);
        return null;
      }
    }

    /// <summary>
    /// Adds offset to given date time.
    /// </summary>
    /// <param name="dateTimeString">xmltv date i.e. 20170109110000 +0200</param>
    /// <param name="offset">offset value. If null we will convert to local time</param>
    /// <returns>modified date as string i.e. 20170109123000 +0200</returns>
    public static String AddOffset(String dateTimeString, double? offset = null)
    {
      var result = "";
      var dateFormat = "yyyyMMddHHmmss zzz";
      try
      {
        if (offset == null) //Convert to local time
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
          }
          dto = dto.AddHours(Convert.ToDouble(offset));
          result = dto.ToString(dateFormat);
        }
      }
      catch (Exception e)
      {
        Log.Error(e.Message);
        result = dateTimeString;
      }
      return result.Replace(":", "");
    }

    ///// <summary>
    ///// Converts an XML element to a boolean. Useful when we don't know if the element exist
    ///// </summary>
    ///// <param name="el"></param>
    ///// <returns></returns>
    //public static bool StringToBool(XElement el)
    //{
    //  if (el != null)
    //    return StringToBool(el.Value);
    //  return false;
    //}

    ///// <summary>
    ///// Converts various stirngs to boolean
    ///// </summary>
    ///// <param name="val"></param>
    ///// <returns></returns>
    //public static bool StringToBool(String val)
    //{
    //  val = val.ToLower();
    //  return (val == "y" || val == "yes" || val == "true" || val == "on");
    //}

    public static List<String> GetIniFilesInDir(String path)
    {
      if (iniFilesInDir == null)
        iniFilesInDir = new List<string>();

      iniFilesInDir.AddRange(GetFilesToDepth(path));
      iniFilesInDir.AddRange(GetFilesToDepth(Path.Combine(path, "siteini.user"), 6));
      iniFilesInDir.AddRange(GetFilesToDepth(Path.Combine(path, "siteini.pack"), 6));

      return iniFilesInDir;
    }

    static List<String> iniFilesInDir;

    static List<String> GetFilesToDepth(String path, int depth = 0)
    {
      if (!Directory.Exists(path))
        return new List<String>();

      var iniFiles = Directory.EnumerateFiles(path, "*.ini").ToList();
      if (depth > 0)
      {
        var folders = Directory.EnumerateDirectories(path);
        foreach (var folder in folders)
          iniFiles.AddRange(GetFilesToDepth(folder, depth - 1));
      }
      return iniFiles;
    }
  }
}
