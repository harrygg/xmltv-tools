using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Xml.Linq;

namespace wgmulti
{
  public class Config
  {
    XDocument xmlConfig;
    public const String dateFormat = "yyyyMMddHHmmss zzz";
    public const String configFileName = "WebGrab++.config.xml";
    public String configFilePath = "";
    const String LogFileName = "WebGrab++.Log.txt";
    public String LogFilePath = "";
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
    public List<String> disabled_siteinis { get; set; }

    /// <summary>
    /// Returns a list of dates in yyyyMMdd format used when copying EPG
    /// </summary>
    List<String> _dates = new List<String>();
    public List<String> Dates {
      get {
        if (_dates.Count != 0)
          return _dates;

        var today = DateTime.Now;
        for (var i = 0; i < timeSpan.days + 1; i++)
          _dates.Add(today.AddDays(i).ToString("yyyyMMdd"));
        return _dates;
      }
    }

    public Config()
    {
      postProcess = new PostProcess(); //Default postProcess object
    }
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
      LogFilePath = Path.Combine(folder, LogFileName);
      outputFilePath = Path.Combine(folder, epgFileName);
    }

    public void LoadSettingsFromXmlFile(String file = "", bool loadChannels = true)
    {
      if (file == "")
        file = configFilePath;

      if (!File.Exists(file))
        throw new FileNotFoundException("Unable to find config file: " + file);
      
      Log.Debug("Loading configuration from " + file);

      xmlConfig = XDocument.Load(file);
      var settings = xmlConfig.Element("settings");
      Log.Debug("settings node loaded");

      var epgFilePath = settings.Element("filename").Value;
      if (epgFilePath == "")
      {
        outputFilePath = Path.Combine(folder, epgFileName);
      }
      else
      {
        outputFilePath = Path.IsPathRooted(epgFilePath) ? epgFilePath : Path.Combine(folder, epgFilePath);
      }
      Log.Debug("filename set to " + outputFilePath);

      if (settings.Element("mode") != null)
        mode = settings.Element("mode").Value;

      if (settings.Element("user-agent") != null)
        userAgent = settings.Element("user-agent").Value;

      if (settings.Element("update") != null)
        update = settings.Element("update").Value;

      skip = new Skip(settings.Element("skip"));
      timeSpan = new Timespan(settings.Element("timespan"));
      proxy = new Proxy(settings.Element("proxy"));
      logging = Utils.StringToBool(settings.Element("logging"));
      retry = new Retry(settings.Element("retry"));
      postProcess = new PostProcess(settings.Element("postprocess"));
      Log.Debug("Is postprocess enabled: {0}", postProcess.run);

      if (loadChannels)
        channels = GetChannelsFromXml();
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
              Log.Warning("Skippping channel without \"site\" and \"same_as\" attributes");
              continue;
            }
            if (same_as != null)
              channel.same_as = same_as;

            channels.Add(channel);
          }
          catch
          {
            Log.Error("Unable to parse channel");
            Log.Error(c.ToString());
          }
        }
      }
      catch (Exception ex)
      {
        Log.Error(ex.ToString());
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
            settings.Add(c.ToXElement());
        }
      }
      catch (Exception e)
      {
        Log.Error(e.ToString());
      }
      return settings;
    }

    /// <summary>
    /// Saves a XML config file to a folder
    /// </summary>
    /// <param name="outputDir">The folder where the config file should be saved</param>
    /// <returns></returns>
    public String Save(String outputDir = null, bool toJson = false)
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

      try
      {
        if (!toJson)
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
        else
        {
          var jsonConfigPath = Path.Combine(Arguments.configDir, "Converted_" + Arguments.jsonConfigFileName);
          var js = new JavaScriptSerializer().Serialize(this);
          File.WriteAllText(jsonConfigPath, js);
        }

      }
      catch (Exception ex)
      {
        Log.Error(ex.ToString());
        return null;
      }
      return _filePath;
    }

    /// <summary>
    /// Clone a configuration object. 
    /// Don't forget to call Save()
    /// </summary>
    /// <param name="outputFolder">Folder where the XML will be saved</param>
    /// <returns></returns>
    public Config Clone(String outputFolder)
    {
      var newConfig = (Config) this.MemberwiseClone();

      if (this.postProcess.run)
        newConfig.postProcess = (PostProcess)this.postProcess.Clone();

      newConfig.SetAbsPaths(outputFolder);
      return newConfig;
    }



    /// <summary>
    /// Removes all disabled channels 
    /// Adds 'site' attribute to 'same_as' channels so that the grouping works
    /// </summary>
    /// <returns>A list of channels enabled for grabbing</returns>
    public void RemoveDisabledChannels()
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
            channel.active = false;
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
    /// Returns enabled channels by various criteria
    /// </summary>
    /// <param name="includeOffset">Include offset channels</param>
    /// <param name="onlyActive">Include only active channels</param>
    /// <param name="withoutPrograms">Include only channels that have no programs</param>
    /// <returns></returns>
    public IEnumerable<Channel> GetChannels(bool includeOffset = false, bool onlyActive = false)
    {
      foreach (var channel in channels)
      {
        if (!channel.enabled || (onlyActive && !channel.active))
          continue;

        yield return channel;

        if (!includeOffset || (includeOffset && channel.offset_channels == null))
          continue;

        foreach (var offset_channel in channel.offset_channels)
        {
          if (offset_channel.enabled)
            yield return offset_channel;
        }
      }
    }

    public Channel GetChannelByName(String name)
    {
      try
      {
        return GetChannels().First(c => c.name.Equals(name));
      }
      catch (Exception)
      {
      }
      return null;
    }

    public int EmptyChannelsCount()
    {
       return GetChannels().Where(channel => channel.xmltv.programmes.Count == 0).ToList().Count;
    }

    public Xmltv GetEpg()
    {
      Xmltv epg = new Xmltv();
      try
      {
        // Combine all xml guides into a single one
        Log.Info("Merging channel guides, creating offset channel guides");

        foreach (var channel in GetChannels(includeOffset: true))
        {
          if (channel.xmltv.programmes.Count > 0)
          {
            epg.programmes.AddRange(channel.xmltv.programmes);
            epg.postProcessedProgrammes.AddRange(channel.xmltv.postProcessedProgrammes);
            epg.channels.Add(channel.xmltv.channels[0]);
          }
        }
      }
      catch (Exception ex)
      {
        Log.Error("Unable to merge EPGs");
        Log.Error(ex.ToString());
      }
      return epg;
    }

    public override string ToString()
    {
      return "Master channels: " + channels.Count.ToString() + ", Active channels (incl. Offset): " + GetChannels(true).Count();
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
