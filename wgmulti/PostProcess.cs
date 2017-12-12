using System;
using System.IO;
using System.Runtime.Serialization;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace wgmulti
{
  /// <summary>
  /// mdb runs a movie database grabber
  /// rex re-allocates xmltv elements
  /// </summary>
 [DataContract()]
  public class PostProcess
  {
    [DataContract]
    public enum Type
    {
      [XmlEnum(Name = "mdb")]
      MDB,
      [XmlEnum(Name = "rex")]
      REX
    };

    [IgnoreDataMember, XmlIgnore]
    public Type type = Type.MDB;

    [DataMember(Name = "type"), XmlText]
    public String PPType
    {
      get { return type == Type.MDB ? "mdb" : "rex"; }
      set { type = value == "mdb" ? Type.MDB : Type.REX; }
    }

    [DataMember, XmlIgnore]
    public bool grab = true;

    [XmlAttribute("grab"), IgnoreDataMember]
    public String Grab
    {
      get { return grab ? "y" : "n"; }
      set { grab = value == "y" || value == "yes" || value == "on" || value == "true"; }
    }

    [DataMember, XmlIgnore]
    public bool run = false;

    [XmlAttribute("run"), IgnoreDataMember]
    public String Run
    {
      get { return run ? "y" : "n"; }
      set { run = value == "y" || value == "yes" || value == "on" || value == "true"; }
    }

    [IgnoreDataMember, XmlIgnore]
    public String fileName = "epg.xml";

    [IgnoreDataMember, XmlIgnore]
    public String ConfigFileName
    {
      get { return GetFolderName() + ".config.xml"; }
      set { }
    }

    /// <summary>
    /// the abs path to the rex or mdb folder
    /// </summary>
    [IgnoreDataMember, XmlIgnore]
    public String configDir = String.Empty;

    [IgnoreDataMember, XmlIgnore]
    public String ConfigFilePath
    {
      get { return Path.Combine(configDir, ConfigFileName); }
      set { }
    }

    [IgnoreDataMember, XmlIgnore]
    XElement settings;

    public PostProcess()
    {
      settings = new XElement("settings");
    }

    /// <summary>
    /// Method to create a copy of this object
    /// The 'settings' element must be a new object
    /// </summary>
    /// <returns></returns>
    public PostProcess Clone(String outputFolder)
    {
      var cloned = (PostProcess)this.MemberwiseClone();
      cloned.configDir = Path.Combine(outputFolder, GetFolderName());
      cloned.settings = new XElement(settings);
      cloned.fileName = Path.Combine(cloned.configDir, "epg.xml");
      cloned.settings.Element("filename").Value = cloned.fileName;
      return cloned;
    }



    public String GetRelativeFilePath()
    {
      return Path.Combine(GetFolderName(), ConfigFileName);
    }

    /// <summary>
    /// Returns the current active folder name rex or mdb
    /// </summary>
    /// <returns></returns>
    public String GetFolderName()
    {
      return type.ToString().ToLower();
    }

    /// <summary>
    /// Load XML configuration from file
    /// </summary>
    /// <param name="folderName"></param>
    public void Load(String folderName = null)
    {
      try
      {
        if (!String.IsNullOrEmpty(ConfigFilePath) && ConfigFilePath.EndsWith(".xml"))
        {
          settings = XDocument.Load(ConfigFilePath).Element("settings");
          fileName = settings.Element("filename").Value;
          if (!Path.IsPathRooted(fileName))
            fileName = Path.Combine(Path.GetDirectoryName(ConfigFilePath), fileName);

          Log.Debug("Post process config successfully loaded!");
        }
        else
        {
          Log.Error("Could not load postprocess settings from file " + ConfigFilePath);
          run = false;
        }
      }
      catch (Exception ex)
      {
        Log.Error(ex.Message);
        Log.Debug(ex.ToString());
        Log.Error("Post process disabled!");
        run = false;
      }
    }

    /// <summary>
    /// The post process XML config file is always saved into a sub folder with the PP type name 
    /// i.e. rex\rex.config.xml or mdb\mdb.config.xml
    /// </summary>
    /// <param name="folderName"></param>
    public void Save(String folderName = null)
    {
      XDocument xdoc = new XDocument(new XDeclaration("1.0", "utf-8", null), settings);

      if (!Directory.Exists(configDir))
        Directory.CreateDirectory(configDir);

      // Get full path of config file and save it to grabber work folder
      xdoc.Save(ConfigFilePath);
    }

    public override String ToString()
    {
      return String.Format("Run: {0}, Grab: {1}, Type: {2}", run, grab, type.ToString());
    }
  }

}
