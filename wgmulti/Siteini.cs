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

    [DataMember(Name = "grab_type", EmitDefaultValue = false), XmlIgnore]
    public String Type
    {
      get { return type == GrabType.COPY ? "copy" : null; }
      set { type = value == "scrub" || value == null ? GrabType.SCRUB : GrabType.COPY; }
    }

    [XmlIgnore, DataMember(EmitDefaultValue = false)]
    bool? enabled { get; set; }

    [XmlIgnore, IgnoreDataMember]
    public bool Enabled
    {
      get { return enabled.HasValue ? (bool)enabled : true; }
      set { enabled = value; }
    }

    [XmlIgnore, DataMember(EmitDefaultValue = false)]
    public double? offset { get; set; }

    [XmlIgnore, DataMember(EmitDefaultValue = false)]
    public int? timespan { get; set; }

    [XmlIgnore, IgnoreDataMember]
    public int Timespan
    {
      get { return timespan.HasValue ? (int)timespan : 0; }
      set { timespan = value; }
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
    /// If not found in the current dir, searches recusively depth=4
    /// </summary>
    /// <returns></returns>
    public String GetPath(String iniMasterFolder = null)
    {
      if (String.IsNullOrEmpty(iniMasterFolder))
        iniMasterFolder = Application.masterConfig.folder;

      try
      {
        if (!String.IsNullOrEmpty(path))
        {
          if (path.StartsWith("http") || 
              path.StartsWith("ftp") || 
              File.Exists(path))
            return path;

          Log.Warning("Siteini has 'path' attribute pointing to a non existing file: " + path + ". Searching for another one.");
        }

        var files = Utils.GetIniFilesInDir(iniMasterFolder);
        foreach (var file in files)
        {
          if (Path.GetFileName(file) == GetName())
          {
            this.path = file;
            return path;
          }
        }
      }
      catch (Exception ex)
      {
        Log.Error(ex.Message);
      }
      return null;
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
        Log.Error(String.Format("#{0} GRABBER {1} | {2}", Application.grabbingRound + 1, name.ToUpper(), ex.Message));
        return false;
      }
    }


    public override string ToString()
    {
      return name + ", " + Enabled + ", " + (path ?? "");
    }
  }
}
