using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace wgmulti
{
  public class Channel
  {
    public String update { get; set; }
    public String name { get; set; }
    public String xmltv_id { get; set; }
    public List<SiteIni> siteinis { get; set; }
    public SiteIni siteini;
    public int? offset { get; set; }
    public String same_as { get; set; }
    public String period { get; set; }
    public String include { get; set; }
    public String exclude { get; set; }
    public String site_channel { get; set; }
    public Boolean enabled { get; set; }
    /// <summary>
    /// Channel is deactivated if during grabbing it has 
    /// no more siteinis and there are no programms grabbed
    /// </summary>
    public Boolean isActive = true;
    public int activeSiteIni = 0;
    public List<Channel> offset_channels { get; set; }
    public XElement xmltvChannel;
    public List<XElement> xmltvPrograms = new List<XElement>();
    //public String url;

    public Channel()
    {
      // set defaults

      //siteinis = new List<SiteIni>(); // init empty list so we don't get exceptions when there are no siteinis (in case of 'same_as' channels)
      //offset_channels = new List<Channel>();
      enabled = true;
    }

    /// <summary>
    /// Creates a channel object
    /// </summary>
    /// <param name="name"></param>
    /// <param name="xmltvId"></param>
    /// <param name="siteIni"></param>
    /// <param name="updateType"></param>
    public Channel(String name, String xmltvId, SiteIni siteIni, String updateType = "")
    {
      siteini = siteIni;
      if (siteinis == null)
      { 
        siteinis = new List<SiteIni>();
        siteinis.Add(siteini);
        isActive = false;
      }
      this.name = name;
      xmltv_id = xmltvId;
      update = updateType;
      enabled = true;
    }

    public override string ToString()
    {
      var output = name;
      if (GetActiveSiteIni() != null)
        output += ", " + GetActiveSiteIni().name;
      return output;
    }
    public XElement ToXElement()
    {

      var xEl = new XElement("channel", name, 
        new XAttribute("xmltv_id", xmltv_id),
        new XAttribute("update", update ?? "i")
      );

      siteini = GetActiveSiteIni();
      if (siteini != null)
      {
        xEl.Add(new XAttribute("site", siteini.name));
        xEl.Add(new XAttribute("site_id", siteini.site_id));
      }
      if (offset != null)
        xEl.Add(new XAttribute("offset", offset));
      if (same_as != null)
        xEl.Add(new XAttribute("same_as", same_as)); 
      if (period != null)
        xEl.Add(new XAttribute("period", period));
      if (include != null)
        xEl.Add(new XAttribute("include", include));
      if (exclude != null)
        xEl.Add(new XAttribute("exclude", exclude));
      if (site_channel != null)
        xEl.Add(new XAttribute("site_channel", site_channel));

      return xEl;
    }

    public XElement GetXmltvChannel(bool getUrl = true)
    {
      if (getUrl)
        return xmltvChannel;
      
      xmltvChannel.Element("url").Remove();
      return xmltvChannel;
    }

    public SiteIni GetActiveSiteIni()
    {
      try
      {
        return siteinis[activeSiteIni];
      }
      catch
      {
        return null;
      }
    }


    /// <summary>
    /// Copies source siteini file from local to destination grabber folder
    /// </summary>
    /// <param name="grabber"></param>
    public void CopyIni(Grabber grabber)
    {
      String sIniFilePath = GetActiveSiteIni().GetPath();
      var nIniFilePath = Path.Combine(grabber.localDir, GetActiveSiteIni().GetName());

      try
      {
        File.Copy(sIniFilePath, nIniFilePath, true);
      }
      catch (FileNotFoundException)
      {
        Console.WriteLine("Grabber {0} | ERROR | Ini file does not exist. Grabbing will be skipped!\n{1}", 
          grabber.id, sIniFilePath);
        enabled = false;
      }
      catch (Exception ex)
      {
        Console.WriteLine("Unable to copy source ini file to grabber dir");
        Console.WriteLine(ex.ToString());
        enabled = false;
      }
    }

    public static Channel GetParent(String channelName)
    {
      Channel _parent = null; 
      try
      {
        if (new Regex(@"\+\d+").Match(channelName).Success)
        {
          return null;
        }

      }
      catch(Exception ex)
      {
        Console.WriteLine(ex.ToString());
        return null;
      }
      return _parent; 
    }
  }

  public class SiteIni
  {
    public SiteIni(String site, String siteId = null)
    {
      this.name = site;
      if (siteId != null)
        site_id = siteId;
    }
    public SiteIni() {}
    public String name { get; set; }
    public String GetName() { return name + ".ini"; }
    public String site_id { get; set; }
    public String path { get; set; }

    /// <summary>
    /// Gets the path of the source INI file.
    /// If not found in the current dir, searches recusively depth=6
    /// </summary>
    /// <returns></returns>
    public String GetPath()
    {
      if (!String.IsNullOrEmpty(path))
        return path;

      var files = GetFilesToDepth(Program.rootConfig.folder, 4);
      foreach (var f in files)
      { 
        if (f.EndsWith(GetName()))
        {
          path = f;
          Console.WriteLine("Grabber {0} | Found siteini {1}", name.ToUpper(), path);
          break;
        }
      }

      if (String.IsNullOrEmpty(path))
        path = GetName();

      return path;
    }

    /// <summary>
    /// Copies source siteini file from local to destination grabber folder
    /// </summary>
    /// <param name="grabber"></param>
    public bool Save(String destDir)
    {
      try
      {
        var source = GetPath();
        var file = Path.Combine(destDir, GetName());
        File.Copy(source, file, true);
        return true;
      }
      catch (Exception ex)
      {
        Console.WriteLine("Grabber {0} | ERROR {1}", name.ToUpper(), ex.Message);
        return false;
      }
    }

    private static IList<String> GetFilesToDepth(String path, int depth)
    {
      var files = Directory.EnumerateFiles(path).ToList();

      if (depth > 0)
      {
        var folders = Directory.EnumerateDirectories(path);

        foreach (var folder in folders)
        {
          files.AddRange(GetFilesToDepth(folder, depth - 1));
        }
      }

      return files;
    }

    public override string ToString()
    {
      return name;
    }
  }
}
