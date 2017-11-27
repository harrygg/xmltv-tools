using System;
using System.IO;
using System.Xml.Serialization;
using System.Runtime.Serialization;

namespace wgmulti
{
  public enum GrabType { SCRUB, COPY}

  [DataContract]
  public class SiteIni
  {

    [XmlAttribute, DataMember(Order = 1)]
    public String name { get; set; }

    /// <summary>
    /// Gets the name with INI extension
    /// </summary>
    /// <returns></returns>
    public String GetName() { return name + ".ini"; }

    [XmlAttribute, DataMember(Order = 1)]
    public String site_id { get; set; }

    [XmlIgnore, DataMember(EmitDefaultValue = false)]
    public String path { get; set; }

    [XmlIgnore, IgnoreDataMember]
    public GrabType type { get; set; }

    [DataMember(Name = "type", EmitDefaultValue = false), XmlIgnore]
    public String Type
    {
      get { return type == GrabType.COPY ? "copy" : null; }
      set { type = value == "scrub" || value == null ? GrabType.SCRUB : GrabType.COPY; }
    }

    public SiteIni(String site, String siteId = null)
    {
      this.name = site;
      if (siteId != null)
        site_id = siteId;
    }
    public SiteIni()
    {
    }


    /// <summary>
    /// Gets the path of the source INI file.
    /// If not found in the current dir, searches recusively depth=6
    /// </summary>
    /// <returns></returns>
    public String GetPath()
    {
      if (!String.IsNullOrEmpty(path))
        return path;

      var files = Utils.GetFilesToDepth(Application.masterConfig.folder, 4);
      foreach (var file in files)
      {
        if (Path.GetFileName(file) == GetName())
        {
          path = file;
          Log.Debug(String.Format("{0} | {1} | Found siteini {2}", Application.currentSiteiniIndex + 1, name.ToUpper(), path));
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
        Log.Error(String.Format("#{0} GRABBER {1} | {2}", Application.currentSiteiniIndex + 1, name.ToUpper(), ex.ToString()));
        return false;
      }
    }


    public override string ToString()
    {
      return name;
    }
  }
}
