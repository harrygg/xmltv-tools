using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace wgmulti
{
  public enum GrabType { SCRUB, COPY}
  public class SiteIni
  {
    public SiteIni(String site, String siteId = null)
    {
      this.name = site;
      if (siteId != null)
        site_id = siteId;
    }
    public SiteIni() { }
    public String name { get; set; }
    public String GetName() { return name + ".ini"; }
    public String site_id { get; set; }
    public String path { get; set; }
    public String grab_type { get; set; }

    /// <summary>
    /// Gets the path of the source INI file.
    /// If not found in the current dir, searches recusively depth=6
    /// </summary>
    /// <returns></returns>
    public String GetPath()
    {
      if (!String.IsNullOrEmpty(path))
        return path;

      var files = GetFilesToDepth(Program.masterConfig.folder, 4);
      foreach (var file in files)
      {
        if (Path.GetFileName(file) == GetName())
        {
          path = file;
          Log.Debug(String.Format("{0} | {1} | Found siteini {2}", Program.currentSiteiniIndex + 1, name.ToUpper(), path));
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
        Log.Error(String.Format("#{0} GRABBER {1} | {2}", Program.currentSiteiniIndex + 1, name.ToUpper(), ex.Message));
        return false;
      }
    }

    static IList<String> GetFilesToDepth(String path, int depth)
    {
      var files = Directory.EnumerateFiles(path).ToList();
      if (depth > 0)
      {
        var folders = Directory.EnumerateDirectories(path);
        foreach (var folder in folders)
          files.AddRange(GetFilesToDepth(folder, depth - 1));
      }
      return files;
    }

    public override string ToString()
    {
      return name;
    }
  }
}
