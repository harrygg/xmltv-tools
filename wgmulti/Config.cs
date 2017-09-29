using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace wgmulti
{
  public class Config
  {
    XDocument xmlConfig;
    public const String dateFormat = "yyyyMMddHHmmss zzz";
    public const String configFileName = "WebGrab++.config.xml";
    public String configFilePath = "";
    const String logFileName = "WebGrab++.log.txt";
    public String logFilePath = "";
    public String folder = "";
    public String outputFilePath = "epg.xml";
    public static String epgFileName = "epg.xml";
    public Proxy proxy { get; set; }
    //public List<Credentials> credentials { get; set; }
    public String mode { get; set; }
    public String userAgent { get; set; }
    public bool logging { get; set; }
    public Skip skip { get; set; }
    public Timespan timeSpan { get; set; }
    public String update { get; set; }
    public Retry retry { get; set; }
    public PostProcess postProcess { get; set; }
    public List<Channel> channels { get; set; }
    public int activeChannels = 0;
    public IEnumerable<IGrouping<String, Channel>> grabbers;

    public Config() {}
    /// <summary>
    /// Create a config object. If a path to config.xml file is provided, settings will be loaded.
    /// Otherwise a default config file will be created
    /// </summary>
    /// <param name="path">Directory where the configuration exists or will be created</param>
    public Config(String path = null)
    {
      postProcess = new PostProcess(); //init postprocess default values

      if (path == null)
        folder = Arguments.grabingTempFolder;
      else
      { 
        folder = path.EndsWith(".xml") ? new FileInfo(path).Directory.FullName : path;
        if (!Path.IsPathRooted(folder))
          throw new ArgumentException("Config folder path must be an absolute path");
      }
      SetAbsPaths(folder);

      if (path != null)
        LoadSettingsFromXmlFile(configFilePath);
    }

    /// <summary>
    /// called after cloning a config
    /// </summary>
    /// <param name="configFolder"></param>
    public void SetAbsPaths(String configFolder)
    {
      folder = configFolder; //when called from Clone()
      configFilePath = Path.Combine(folder, configFileName);
      logFilePath = Path.Combine(folder, logFileName);
      outputFilePath = Path.Combine(folder, epgFileName);
    }

    public void LoadSettingsFromXmlFile(String file = "", bool loadChannels = true)
    {
      if (file == "")
        file = configFilePath;

      if (!File.Exists(file))
      {
        //Debug("Configuration file " + file + " not found! Exiting...");
        throw new FileNotFoundException("Unable to find config file: " + file);
      }
      Debug("Loading configuration from " + file);

      xmlConfig = XDocument.Load(file);
      var settings = xmlConfig.Element("settings");
      Debug("settings node loaded");

      var epgFilePath = settings.Element("filename").Value;
      if (epgFilePath == "")
      {
        outputFilePath = Path.Combine(folder, epgFileName);
      }
      else
      {
        outputFilePath = Path.IsPathRooted(epgFilePath) ? epgFilePath : Path.Combine(folder, epgFilePath);
      }
      Debug("filename set to " + outputFilePath);

      if (settings.Element("mode") != null)
        mode = settings.Element("mode").Value;

      if (settings.Element("user-agent") != null)
        userAgent = settings.Element("user-agent").Value;

      if (settings.Element("update") != null)
        update = settings.Element("update").Value;

      skip = new Skip(settings.Element("skip"));
      timeSpan = new Timespan(settings.Element("timespan"));
      proxy = new Proxy(settings.Element("proxy"));
      logging = StringToBool(settings.Element("logging"));
      retry = new Retry(settings.Element("retry"));
      postProcess = new PostProcess(settings.Element("postprocess"));
      Console.WriteLine("Is postprocess enabled: {0}", postProcess.run);

      if (loadChannels)
        channels = GetChannelsFromXml();
    }

    public static bool StringToBool(XElement el)
    {
      if (el != null)
        return StringToBool(el.Value);
      return false;
    }

    public static bool StringToBool(String val)
    {
      val = val.ToLower();
      return (val == "y" || val == "yes" || val == "true" || val == "on");
    }

    void SetValue(ref String defaultValue, XElement x, string name, bool fromAttr = false)
    {
      try
      {
        defaultValue = fromAttr ? x.Attribute(name).Value : x.Element(name).Value;
        Debug("\"" + name + "\" value is set to \"" + defaultValue + "\"");
      }
      catch
      {
        Debug("\"" + name + "\" value not found! Using the default value \"" + defaultValue + "\"");
      }
    }

    /// <summary>
    /// Creates a list of channels from XML config file
    /// </summary>
    /// <returns></returns>
    List<Channel> GetChannelsFromXml()
    {
      channels = new List<Channel>();
      try
      {
        var cs = from c in xmlConfig.Elements("settings").Elements("channel") select c;
        if (cs.Count() == 0)
          cs = from c in xmlConfig.Elements("settings").Elements("channels").Elements("channel") select c;

        Channel channel = null; //keep the previous value here in case channel has same_as attribute
        foreach (XElement c in cs)
        {
          try
          {
            //is "site" attr is null get it from the previous channel
            var site = c.Attribute("site") != null ? c.Attribute("site").Value : channel.siteini.name;
            var siteId = c.Attribute("site_id") != null ? c.Attribute("site_id").Value : "";
            var update = c.Attribute("update") != null ? c.Attribute("update").Value : UpdateType.None;

            var xmltvId = c.Attribute("xmltv_id").Value;
            var name = c.Value;

            var siteIni = new SiteIni(site, siteId);
            channel = new Channel(name, xmltvId, siteIni, update);

            // Add channel's additional attributes
            if (c.Attribute("offset") != null)
              channel.offset = Convert.ToInt16(c.Attribute("offset").Value);
            if (c.Attribute("period") != null)
              channel.period = c.Attribute("period").Value;
            if (c.Attribute("include") != null)
              channel.include = c.Attribute("include").Value;
            if (c.Attribute("exclude") != null)
              channel.exclude = c.Attribute("exclude").Value;
            if (c.Attribute("site_channel") != null)
              channel.site_channel = c.Attribute("site_channel").Value;

            //if "site" and "same_as" attr are missing skip this channel
            var same_as = c.Attribute("same_as") != null ? c.Attribute("same_as").Value : null;
            if (String.IsNullOrEmpty(site) && String.IsNullOrEmpty(same_as))
            {
              Console.WriteLine("Skippping channel without \"site\" and \"same_as\" attributes");
              continue;
            }
            if (same_as != null)
              channel.same_as = same_as;

            channels.Add(channel);
          }
          catch
          {
            Console.WriteLine("Unable to parse channel");
            Console.WriteLine(c.ToString());
          }
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.ToString());
      }
      return channels;
    }

    XElement CreateXml()
    {
      var settings = new XElement("settings");

      try
      {
        settings.Add(
          new XElement("filename", outputFilePath),
          new XElement("mode", mode),
          new XElement("user-agent", userAgent),
          new XElement("update", update)
        );

        if (logging)
          settings.Add(new XElement("logging", "on"));

        //if (credentials != null)
        //{
        //  foreach (var c in credentials)
        //    settings.Add(c.ToXElement());
        //}

        if (proxy != null)
          settings.Add(proxy.ToXElement());

        settings.Add(skip.ToXElement());
        settings.Add(timeSpan.ToXElement());
        settings.Add(retry.ToXElement());
        settings.Add(postProcess.ToXElement());

        if (channels != null)
        {
          foreach (var c in channels)
          { 
            settings.Add(c.ToXElement());
            if (c.offset_channels != null)
              foreach (var ch in c.offset_channels)
              {
                if (String.IsNullOrEmpty(ch.same_as))
                {
                  ch.same_as = c.name;
                }
                settings.Add(ch.ToXElement());
              }
          }
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(e.ToString());
      }
      return settings;
    }

    /// <summary>
    /// Saves a XML config file to a folder
    /// </summary>
    /// <param name="outputDir">The folder where the config file should be saved</param>
    /// <returns></returns>
    public bool Save(String outputDir = null)
    {
      var _filePath = "";
      if (!String.IsNullOrEmpty(outputDir)) //If we want to overwrite
        _filePath = Path.Combine(outputDir, configFileName);
      else if (!String.IsNullOrEmpty(configFilePath))
        _filePath = configFilePath;
      else if(!String.IsNullOrEmpty(folder))
        _filePath = Path.Combine(folder, configFileName);
      else
        _filePath = configFileName;

      Debug("Saving config in " + _filePath);

      try
      {
        var settings = CreateXml();
        XDocument xdoc = new XDocument(new XDeclaration("1.0", "utf-8", null), settings);

        folder = new FileInfo(_filePath).Directory.FullName;
        if (!Directory.Exists(folder))
          Directory.CreateDirectory(folder);

        xdoc.Save(_filePath);

        // Save postprocess configuration file as well
        if (postProcess.run)
          postProcess.Save(folder);
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.ToString());
        return false;
      }
      return true;
    }

    public static void Debug(string v)
    {
      if (Arguments.debug)
        Console.WriteLine(v);
    }

    /// <summary>
    /// Clone a configuration object. 
    /// Don't forget to call Save()
    /// </summary>
    /// <param name="outputFolder">Folder where the XML will be saved</param>
    /// <returns></returns>
    public Config Clone(String outputFolder)
    {
      var newConfig = (Config)this.MemberwiseClone();
      newConfig.SetAbsPaths(outputFolder);
      return newConfig;
    }

    /// <summary>
    /// Removes all disabled channels 
    /// Adds 'site' attribute to 'same_as' channels so that the grouping works
    /// </summary>
    /// <returns>A list of channels enabled for grabbing</returns>
    public void PurgeChannels()
    {
      foreach (var channel in channels.ToArray())
      {
        if (!channel.enabled)
        {
          channels.Remove(channel);
        }
        else
        {
          if (channel.siteinis == null || channel.siteinis.Count == 0)
          {
            channel.isActive = false;
          }
          else
          {
            activeChannels++;
            if (channel.offset_channels != null)
            {
              foreach (var offset_channel in channel.offset_channels.ToArray())
              {
                if (!offset_channel.enabled)
                  channel.offset_channels.Remove(offset_channel);
                else
                  activeChannels++;
              }
            }
          }
        }
      }
    }

    /// <summary>
    /// Get enumeration of all currently active channels
    /// TODO - FIX issue - some playlists contain offset channels without active parent channels
    /// if playlist contains offset channel but no parent channel, 
    /// then the parent channel will be inactive and so will be the child offset channel.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<Channel> GetActiveChannels()
    {
      foreach (var channel in channels)
      {
        if (!channel.isActive)
          continue;

        yield return channel;

        if (channel.offset_channels == null)
          continue;

        foreach (var offset_channel in channel.offset_channels)
           yield return offset_channel;
      }
    }

    public IEnumerable<Channel> GetEnabledChannels()
    {
      foreach (var channel in channels)
      {
        if (channel.enabled)
          yield return channel;

        if (channel.offset_channels == null)
          continue;

        foreach (var offset_channel in channel.offset_channels)
          if (offset_channel.enabled)
            yield return offset_channel;
      }
    }

    public Xmltv GetEpg()
    {
      Xmltv epg = new Xmltv();
      try
      {
        // Combine all xml guides into a single one
        Console.WriteLine("Saving EPG XML file");

        foreach (var channel in GetEnabledChannels())
        {
          if (channel.xmltvPrograms.Count > 0)
          {
            epg.programmes.AddRange(channel.xmltvPrograms);
            epg.channels.Add(channel.xmltvChannel);
          }
        };
      }
      catch (Exception ex)
      {
        Console.WriteLine("Unable to merge EPGs");
        Console.WriteLine(ex.ToString());
      }
      return epg;
    }
  }
  public class PostProcess
  {
    /// <summary>
    /// mdb runs a movie database grabber
    /// rex re-allocates xmltv elements
    /// </summary>
    public enum Type { MDB, REX };
    public Type type = Type.MDB;
    /// <summary>
    /// Grabs epg first
    /// </summary>
    public bool grab = true;
    /// <summary>
    /// Runs the postprocess
    /// </summary>
    public bool run = false;
    public String fileName = "epg.xml";

    public XElement settings;
    XDocument xdoc = new XDocument(new XDeclaration("1.0", "utf-8", null));

    public PostProcess() { }

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
        grab = Config.StringToBool(attr.Value);

      attr = el.Attribute("run");
      if (attr != null && attr.Value != "")
        run = Config.StringToBool(attr.Value);

      if (el.Value.ToLower() != "" && el.Value.ToLower() != Type.MDB.ToString().ToLower())
        type = Type.REX;

      // Load settings from file
      if (run)
        Load();
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
        Console.WriteLine(ex.ToString());
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
      // Update the output XML file name so that it reflects the grabber temp dir
      fileName = Config.epgFileName; // reset inherited file name to epg.xml
      if (!String.IsNullOrEmpty(folderName))
        fileName = Path.Combine(folderName, GetFolderName(), fileName);

      settings.Element("filename").Value = fileName; 

      xdoc.Add(settings);

      var path = GetRelativeFilePath();
      if (!String.IsNullOrEmpty(folderName))
        path = Path.Combine(folderName, path);

      xdoc.Save(path);
    }

    public override String ToString()
    {
      var _run = run ? "y" : "n";
      var _grab = grab ? "y" : "n";
      var _type = (type == Type.MDB) ? "mdb" : "rex";
      return String.Format("<postprocess run=\"{0}\" grab=\"{1}\">{2}</postprocess>", _run, _grab, _type);
    }

    public XElement ToXElement()
    {
      var el = new XElement("postprocess");

      var val = run ? "y" : "n";
      el.Add(new XAttribute("run", val));
      val = grab ? "y" : "n";
      el.Add(new XAttribute("grab", val));
      el.Value = (type == Type.MDB) ? "mdb" : "rex";

      return el;
    }
  }

  public class Retry
  {
    /// <summary>
    /// The delay between retries
    /// </summary>
    public int timeOut = 10;
    /// <summary>
    /// The delay between subsequent channels
    /// </summary>
    public int channelDelay = 0;
    /// <summary>
    /// The delay between the grabbing of index pages
    /// </summary>
    public int indexDelay = 0; //
    /// <summary>
    /// The delay between the grabbing of detail show page
    /// </summary>
    public int showDelay = 0;
    /// <summary>
    /// The amount of times the grabber engine should attempt to capture 
    /// a web page before giving up and continuing with the next page
    /// </summary>
    public int value = 4;
    public Retry() { }

    public Retry(XElement el)
    {
      if (el == null)
        return;

      var attr = el.Attribute("time-out");
      if (attr != null && attr.Value != "")
        timeOut = Convert.ToInt32(attr.Value);

      attr = el.Attribute("channel-delay");
      if (attr != null && attr.Value != "")
        channelDelay = Convert.ToInt32(attr.Value);

      attr = el.Attribute("index-delay");
      if (attr != null && attr.Value != "")
        indexDelay = Convert.ToInt32(attr.Value);

      attr = el.Attribute("show-delay");
      if (attr != null && attr.Value != "")
        showDelay = Convert.ToInt32(attr.Value);

      if (el.Value != "")
        value = Convert.ToInt32(el.Value);
    }


    public override String ToString()
    {
      return String.Format("<retry time-out=\"{0}\" channel-delay=\"{1}\" index-delay=\"{2}\" show-delay=\"{3}\">{4}</retry>", timeOut, channelDelay, indexDelay, showDelay, value);
    }
    public XElement ToXElement()
    {
      var el = new XElement("retry",
        new XAttribute("time-out", timeOut),
        new XAttribute("channel-delay", channelDelay),
        new XAttribute("index-delay", indexDelay),
        new XAttribute("show-delay", showDelay));
      el.Value = value.ToString();
      return el;
    }
  }

  public class Timespan
  {
    public int days { get; set; }
    public string time { get; set; }
    public Timespan() { }

    public Timespan(String t = null)
    {
      if (t == null)
        return;

      var temp = t.Split(',');
      days = Convert.ToInt32(temp[0]);
      if (temp.Length > 1)
        time = temp[1];

    }
    public Timespan(XElement el)
    {
      if (el == null)
        return;

      var temp = el.Value.Split(',');
      days = Convert.ToInt32(temp[0]);
      if (temp.Length > 1)
        time = temp[1];
    }

    public XElement ToXElement()
    {
      var el = new XElement("timespan");

      el.Value = days.ToString();
      if (!String.IsNullOrEmpty(time))
        el.Value += "," + time;

      return el;
    }
  }

  public class Skip
  {
    /// <summary>
    ///  if a show takes more than H hours, it's either tellsell or other commercial fluff, 
    ///  or simply a mistake or error, we want to skip such shows.
    /// </summary>
    public int max = 12;
    /// <summary>
    /// if a show is less or equal than m minutes it is probably an announcement, 
    /// in any case not a real show
    /// </summary>
    public int min = 1;
    bool noskip = false;

    public Skip() { }
    public Skip(String t = null)
    {
      if (t == null)
        return;

      if (t == "noskip")
      {
        noskip = true;
        return;
      }

      var temp = t.Split(',');
      max = Convert.ToInt32(temp[0]);
      if (temp.Length > 1)
        min = Convert.ToInt32(temp[1]);
    }

    public Skip(XElement el)
    {
      if (el == null)
        return;

      if (el.Value == "noskip")
      {
        noskip = true;
        return;
      }

      var temp = el.Value.Split(',');
      max = Convert.ToInt32(temp[0]);
      if (temp.Length > 1)
        min = Convert.ToInt32(temp[1]);
    }
    public XElement ToXElement()
    {
      var el = new XElement("skip");

      if (noskip)
        el.Value = "noskip";
      else
      {
        el.Value = String.Format("{0},{1}", max.ToString(), min.ToString());
      }

      return el;
    }
  }

  public class Proxy
  {
    public String user { get; set; }
    public String password { get; set; }
    public String value { get; set; }

    public Proxy() { }

    public Proxy(XElement el)
    {
      if (el == null)
        return;

      var attr = el.Attribute("user");
      if (attr != null)
        user = attr.Value;

      attr = el.Attribute("password");
      if (attr != null)
        password = attr.Value;

      value = el.Value;
    }

    public XElement ToXElement()
    {
      var el = new XElement("proxy");
      if (!String.IsNullOrEmpty(user))
        el.Add(new XAttribute("user", user));
      if (!String.IsNullOrEmpty(password))
        el.Add(new XAttribute("password", password));

      if (!String.IsNullOrEmpty(value))
        el.Value = value;

      return el;
    }
  }

  //public class Credentials
  //{
  //  public String user = "";
  //  public String password = "";
  //  public String site = "";
  //  public Credentials() { }
  //  public Credentials(JObject jo = null)
  //  {
  //    if (jo == null)
  //      return;

  //    if (jo["user"] != null)
  //      user = jo["user"].ToString();

  //    if (jo["password"] != null)
  //      password = jo["password"].ToString();

  //    if (jo["site"] == null)
  //      site = jo["site"].ToString();
  //  }
  //  public Credentials(XElement el)
  //  {
  //    if (el == null)
  //      return;

  //    var attr = el.Attribute("user");
  //    if (attr != null)
  //      user = attr.Value;

  //    attr = el.Attribute("password");
  //    if (attr != null)
  //      password = attr.Value;

  //    site = el.Value;
  //  }

  //  public XElement ToXElement()
  //  {
  //    var el = new XElement("credentials");
  //    if (user != "")
  //      el.Add(new XAttribute("user", user));
  //    if (password != "")
  //      el.Add(new XAttribute("password", password));

  //    el.Value = site;

  //    return el;
  //  }
  //}

  public class UpdateType
  {
    public static String None = "";
    public static String Incremental = "i";
    public static String Light = "l";
    public static String Smart = "s";
    public static String Full = "f";
    public static String Index = "index-only";
  }
}
