using System;
using System.IO;
using System.Xml.Linq;

namespace wgmulti
{

  /// <summary>
  /// mdb runs a movie database grabber
  /// rex re-allocates xmltv elements
  /// </summary>
  public class PostProcess : ICloneable
  {
    public enum Type { MDB, REX };
    public Type type = Type.MDB;
    public bool grab = true;
    public bool run = false;
    public String fileName = "epg.xml";
    XElement settings;

    public PostProcess()
    {
      settings = new XElement("settings");
    }

    /// <summary>
     /// Constructor called when loading configuration from an XML file
     /// </summary>
     /// <param name="el"></param>
    public PostProcess(XElement el)
    {
      if (el == null)
        return;

      var attr = el.Attribute("grab");
      if (attr != null && attr.Value != "")
        grab = Utils.StringToBool(attr.Value);

      attr = el.Attribute("run");
      if (attr != null && attr.Value != "")
        run = Utils.StringToBool(attr.Value);

      if (el.Value.ToLower() != "" && el.Value.ToLower() != Type.MDB.ToString().ToLower())
        type = Type.REX;

      // Load settings from file
      if (run)
        Load();
    }

    /// <summary>
    /// Method to create a copy of this object
    /// The 'settings' element must be a new object
    /// </summary>
    /// <returns></returns>
    public Object Clone()
    {
      var cloned = (PostProcess)this.MemberwiseClone();
      cloned.settings = new XElement(settings);
      return cloned;
    }

    public String GetConfigFileName()
    {
      return type.ToString().ToLower() + ".config.xml";
    }
    public String GetRelativeFilePath()
    {
      return Path.Combine(GetFolderName(), GetConfigFileName());
    }

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
        var path = GetRelativeFilePath();
        if (!String.IsNullOrEmpty(folderName))
          path = Path.Combine(folderName, GetRelativeFilePath());

        settings = XDocument.Load(path).Element("settings");
        fileName = settings.Element("filename").Value;
      }
      catch (Exception ex)
      {
        Log.Error(ex.ToString());
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

      // Update the output XML file name so that it contains the correct grabber dir
      fileName = Config.epgFileName; // reset inherited file name to epg.xml
      if (!String.IsNullOrEmpty(folderName))
        fileName = Path.Combine(folderName, GetFolderName(), fileName);

      if (settings.Element("filename") != null)
        settings.Element("filename").Value = fileName;
      else
        settings.Add(new XElement("filename", fileName));

      XDocument xdoc = new XDocument(new XDeclaration("1.0", "utf-8", null), settings);

      // Create post process dir if it doesn't exist
      var dir = GetFolderName();
      if (!String.IsNullOrEmpty(folderName))
        dir = Path.Combine(folderName, dir);
      if (!Directory.Exists(dir))
        Directory.CreateDirectory(dir);

      // Get full path of config file and save it to grabber work folder
      var path = Path.Combine(dir, GetConfigFileName());
      xdoc.Save(path);
    }

    public override String ToString()
    {
      return String.Format("Run: {0}, Grab: {1}, Type: {2}", run, grab, type.ToString());
    }

    public XElement ToXElement()
    {
      var el = new XElement("postprocess");

      var val = run ? "y" : "n";
      el.Add(new XAttribute("run", val));

      val = grab ? "y" : "n";
      el.Add(new XAttribute("grab", val));

      el.Value = type.ToString().ToLower();

      return el;
    }
  }

}
