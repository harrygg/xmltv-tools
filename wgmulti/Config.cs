using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Text.RegularExpressions;

namespace wgmulti
{
  [DataContract(Namespace = "")]
  [XmlRoot(ElementName = "settings")]
  public class Config
  {
    [XmlIgnore]
    public const String configFileName = "WebGrab++.config.xml";

    public const String jsonConfigFileName = "wgmulti.config.json";
    [XmlIgnore]
    public String configFilePath = String.Empty;

    [XmlIgnore]
    public String jsonConfigFilePath = String.Empty;

    [XmlIgnore]
    public String folder = String.Empty;

    [DataMember(Name = "filename", Order = 5, IsRequired = true), XmlElement("filename")]
    public String outputFilePath = "";

    public static String epgFileName = null;

    [DataMember(EmitDefaultValue = false, Order = 9), XmlElement]
    public Proxy proxy { get; set; }
    //public List<Credentials> credentials { get; set; }

    [DataMember(Order = 4), XmlElement]
    public String mode = String.Empty;

    [DataMember(EmitDefaultValue = false, Order = 8), XmlElement("user-agent")]
    public String userAgent { get; set; }

    [DataMember(Order = 9), XmlIgnore]
    public bool logging { get; set; }

    [XmlElement("logging")]
    public String Logging
    {
      get { return logging ? "on" : "off"; }
      set { logging = value == "on" || value == "y" || value == "yes" || value == "true"; }
    }

    [DataMember(Order = 7), XmlElement]
    public String skip = "noskip";

    [DataMember(Order = 2, Name = "timespan"), XmlElement("timespan")]
    public Period period { get; set; }

    [DataMember(Order = 6), XmlElement]
    public String update = String.Empty;

    [DataMember(Order = 1), XmlElement("retry")]
    public Retry retry { get; set; }

    [DataMember(Order = 8, Name = "postprocess"), XmlElement("postprocess")]
    public PostProcess postProcess { get; set; }

    [DataMember(Order = 10, IsRequired = true), XmlIgnore]
    public List<Channel> channels
    {
      get;
      set;
    }

    [XmlElement("channel")]
    public List<Channel> Channels
    {
      get {
        //return GetChannels(includeOffset: true, onlyActive: false).ToList();
        return channels;
      }
      set { channels = value; }
    }

    [XmlIgnore]
    public int activeChannels = 0;

    [XmlIgnore]
    public IEnumerable<IGrouping<String, Channel>> grabbers;

    [DataMember(EmitDefaultValue = false, Order = 12), XmlIgnore]
    public List<SiteIni> siteinis { get; set; }

    [IgnoreDataMember, XmlIgnore]
    public List<SiteIni> Siteinis
    {
      get { return siteinis == null ? new List<SiteIni>() : siteinis; }
      set { }
    }

    [XmlIgnore, DataMember(EmitDefaultValue = false)]
    public double? offset { get; set; }

    /// <summary>
    /// Returns a list of dates in yyyyMMdd format used when copying EPG
    /// </summary>
    [XmlIgnore, IgnoreDataMember]
    List<String> _dates;

    [XmlIgnore, IgnoreDataMember]
    public List<String> Dates {
      get {
        if (_dates != null && _dates.Count != 0)
          return _dates;
        else
          _dates = new List<String>();

        var today = DateTime.Now;
        for (var i = 0 - period.pastdays; i < period.days + 1; i++)
          _dates.Add(today.AddDays(i).ToString("yyyyMMdd"));
        return _dates;
      }
      set { }
    }
    

    public Config()
    {
      postProcess = new PostProcess(); //Default postProcess object
      retry = new Retry();
      channels = new List<Channel>();
      period = new Period();
    }
    /// <summary>
    /// Create a config object. If a path to config.xml file is provided, settings will be loaded.
    /// Otherwise a default config file will be created
    /// </summary>
    /// <param name="path">Directory where the configuration exists or will be created</param>
    public Config(String path = null)
    {
      //postProcess = new PostProcess(); //init postprocess default values

      //if (path == null)
      //  folder = Arguments.grabingTempFolder;
      //else
      //{
      //  folder = path.EndsWith(".xml") ? new FileInfo(path).Directory.FullName : path;
      //  if (!Path.IsPathRooted(folder))
      //    throw new ArgumentException("Config folder path must be an absolute path");
      //}
      //SetAbsPaths(folder);
    }

    /// <summary>
    /// called after cloning a config
    /// </summary>
    /// <param name="configFolder"></param>
    public void SetAbsPaths(String configFolder)
    {
      folder = configFolder; //when called from Clone()
      configFilePath = Path.Combine(folder, configFileName);
      outputFilePath = Path.Combine(folder, epgFileName);
      jsonConfigFilePath = Path.Combine(folder, jsonConfigFileName);
      if (postProcess.run && !Path.IsPathRooted(postProcess.configDir))
        postProcess.configDir = Path.Combine(folder, postProcess.GetFolderName());
    }

    public String Serialize(bool toJson = false)
    {
      if (!toJson)
        return SerializeToXml();
      return SerializeToJson();
    }

    String SerializeToXml()
    {
      try
      {
        // OnSerialize is not supported by XmlSerializer
        channels = GetChannels(includeOffset: true, onlyActive: false).ToList();
        var ser = new XmlSerializer(typeof(Config));
        String buff;
        var settings = new XmlWriterSettings();
        settings.Indent = true;

        using (var ms = new MemoryStream())
        using (var writer = XmlWriter.Create(ms, settings))
        {
          ser.Serialize(writer, this);
          buff = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
        }
        
        return buff
          .Replace(" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"", "")
          .Replace(" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"", "");
      }
      catch (Exception ex)
      {
        Log.Error(ex.ToString());
        return null;
      }
    }

    String SerializeToJson()
    {
      try
      {
        var ms = new MemoryStream();
        var sr = new DataContractJsonSerializer(typeof(Config));
        var writer = JsonReaderWriterFactory.CreateJsonWriter(ms, Encoding.UTF8, true, true, "  ");
        sr.WriteObject(writer, this);
        writer.Flush();

        return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
      }
      catch (Exception ex)
      {
        Log.Error(ex.ToString());
        return null;
      }
    }

    public static Config DeserializeFromFile(String configFile)
    {
      if (configFile == null)
        throw new Exception("Specify a config file!");

      Log.Debug("Deserializing config object from file: " + configFile);
      Config conf = configFile.EndsWith(".xml") ? 
        DeserializeXmlFile(configFile) : DeserializeJsonFile(configFile);

      //conf.SetAbsPaths(Arguments.configDir);
      conf.folder = Arguments.configDir;
      if (!Path.IsPathRooted(conf.outputFilePath))
        conf.outputFilePath = Path.Combine(Arguments.configDir, epgFileName);
      if (!Path.IsPathRooted(conf.jsonConfigFilePath))
        conf.jsonConfigFilePath = Path.Combine(Arguments.configDir, jsonConfigFileName);
      if (conf.postProcess.run && !Path.IsPathRooted(conf.postProcess.configDir))
        conf.postProcess.configDir = Path.Combine(Arguments.configDir, conf.postProcess.GetFolderName());

      conf.InitSiteinis();

      if (conf.postProcess.run)
        conf.postProcess.Load();

      Log.Info(String.Format("Config contains {0} channels for grabbing", conf.activeChannels));

      return conf;
    }

    static Config DeserializeXmlFile(String file)
    {
      var ser = new XmlSerializer(typeof(Config));
      var ms = GetMemoryStreamFromFile(file);
      var conf = (Config)ser.Deserialize(ms);

      conf.OnXmlDeserialized();

      return conf;
    }

    void OnXmlDeserialized()
    {
      // XMLSerialization does not support OnDeserialize
      var deflatedChannels = new List<Channel>();
      var previous = new Channel();
      epgFileName = Path.GetFileName(outputFilePath);

      // Add all timeshifted channels as sub channels to their parents
      foreach (var channel in GetChannels(includeOffset: true))
      {
        // Is this a timeshifted channel, add it to the previous channel 
        if (String.IsNullOrEmpty(channel.same_as))
        {
          previous = channel;
          deflatedChannels.Add(previous);
          activeChannels++;
        }
        else
        {
          if (previous.timeshifts == null)
            previous.timeshifts = new List<Channel>();
          previous.timeshifts.Add(channel);
          activeChannels++;
        }
      }

      channels = deflatedChannels;
    }

    static MemoryStream GetMemoryStreamFromFile(String file)
    {
      var sb = new StringBuilder();
      using (StreamReader sr = File.OpenText(file))
      {
        String temp;
        while ((temp = sr.ReadLine()) != null)
        {
          temp = Regex.Replace(temp, @"</*channels\s*>", "", RegexOptions.IgnoreCase);
          sb.Append(temp);
        }
        return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
      }
    }

    static Config DeserializeJsonFile(String file)
    {
      Config conf = null;

      var txt = File.ReadAllText(file);
      using (var ms = new MemoryStream(Encoding.Unicode.GetBytes(txt)))
      {
        var sr = new DataContractJsonSerializer(typeof(Config));
        conf = (Config)sr.ReadObject(ms);
      }
      return conf;
    }

    /// <summary>
    /// Executed only when reading a JSON file
    /// </summary>
    /// <param name="c"></param>
    [OnDeserialized]
    void OnDeserialized(StreamingContext c)
    {
      if (postProcess == null)
        postProcess = new PostProcess();

      if (retry == null)
        retry = new Retry();
      
      if (period == null)
        period = new Period();

      epgFileName = Path.GetFileName(outputFilePath);

      // Set update type to channels with missing update type
      foreach (var channel in GetChannels(includeOffset: true))
      {
        activeChannels++;
        if (channel.update == null && !channel.IsTimeshifted)
        {
          channel.update = UpdateType.None;
          Log.Error("Channel '" + channel.name + "' has no update type. Update set to None!");
          activeChannels--;
        }

        if (channel.siteinis == null && !channel.IsTimeshifted)
        {
          Log.Error("Channel '" + channel.name + "' has no siteinis!");
          channel.siteinis = new List<SiteIni>();
        }
      }
    }

    public void InitSiteinis()
    {
      Log.Info("Searching for siteinis. Disabling missing ones.");
      try
      {
        foreach (var channel in GetChannels(includeOffset: false))
        {
          // Skip channels that have no siteinis
          if (!channel.HasSiteinis)
          {
            Log.Warning("Channel '" + channel.name + "' has no available siteinis. Channel deactivated!");
            channel.active = false;
            continue;
          }

          // Iterate over channel siteinis and enable/disable them or add properties
          foreach (var siteini in channel.siteinis)
          {
            // Search for global siteini 
            var globalSiteini = GetGlobalSiteini(siteini);
            if (globalSiteini != null)
            {
              // If global siteini is not enabled, disable all local siteinis
              if (!globalSiteini.Enabled)
              {
                siteini.Enabled = false;
                Log.Error(String.Format("Siteini {0} disabled in global siteinis configuraiton!", siteini.name));
              }
              else
              {
                siteini.Enabled = true;
                // Global siteini is enabled, check whether it has a valid path (.ini file exists)
                globalSiteini.path = globalSiteini.GetPath(folder);
                if (!String.IsNullOrEmpty(globalSiteini.path))
                {
                  // If path is valid and existing assign it to the local ini path
                  siteini.path = globalSiteini.path;
                  // If there is no overwriting global timespan value use the default period days value from master config
                  siteini.timespan = globalSiteini.timespan ?? period.days;
                  // If we don't have global siteini offset use config offset
                  siteini.offset = globalSiteini.offset ?? offset;
                  // assign the type
                  siteini.type = globalSiteini.type;
                }
                else
                {
                  // No .ini file was found so disable the global and local siteini
                  globalSiteini.Enabled = false;
                  siteini.Enabled = globalSiteini.Enabled;
                }
              }
            }
            else // There is no global siteini for this siteini, so we will add one
            {
              // Get local siteini ini path
              siteini.path = siteini.GetPath(folder);
              if (!String.IsNullOrEmpty(siteini.path))
              {
                // if siteini file exists
                siteini.Enabled = true;
                // Since this is a local siteini and there was no overwriting global siteini use default timespan
                siteini.timespan = period.days;
                // Since there is no global siteini offset, overwrite with config offset
                // Later it could be overwritten by channel offset
                siteini.offset = offset;
              }
              else
              {
                siteini.Enabled = false;
                Log.Error(String.Format("Siteini {0} not found in config folder {1} or siteini.user/siteini.pack sub folders (Depth=6). Siteini will be disabled globally!", siteini.GetName(), folder));
              }
              // Create a global siteini and add it to the list
              Siteinis.Add(siteini);
            }
          }
        }
      }
      catch (Exception ex)
      {
        Log.Error(ex.Message + ": " + ex.ToString());
      }
    }

    private SiteIni GetGlobalSiteini(SiteIni siteini)
    {
      try
      {
        return Siteinis.Where(s => s.name == siteini.name).First();
      }
      catch
      {
        return null;
      }
    }

    /// <summary>
    /// Saves the config to a file
    /// </summary>
    /// <param name="outputDir">The folder where the config file should be saved</param>
    /// <returns></returns>
    public bool Save(String file = null, bool toJson = false)
    {
      var _filePath = configFilePath;
      if (!String.IsNullOrEmpty(file)) //If we want to overwrite during tests
        _filePath = file;

      try
      {
        if (!Directory.Exists(folder))
          Directory.CreateDirectory(folder);

        File.WriteAllText(_filePath, this.Serialize(toJson));

        // Save postprocess configuration file as well
        if (postProcess.run && !toJson)
          postProcess.Save(folder);

      }
      catch (Exception ex)
      {
        Log.Error(ex.ToString());
        return false;
      }
      return true;
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
        newConfig.postProcess = postProcess.Clone(outputFolder);

      newConfig.SetAbsPaths(outputFolder);
      return newConfig;
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
        if (!channel.Enabled || (onlyActive && !channel.active))
          continue;
        
        yield return channel;

        if (!includeOffset || (includeOffset && channel.timeshifts == null))
          continue;

        foreach (var timeshifted in channel.timeshifts)
        {
          timeshifted.parent = channel;
          if (String.IsNullOrEmpty(timeshifted.same_as))
            timeshifted.same_as = timeshifted.parent.xmltv_id;

          if (timeshifted.Enabled)
            yield return timeshifted;
        }
      }
    }

    public Channel GetChannelByName(String name)
    {
      try
      {
        return GetChannels(includeOffset: true).First(c => c.name.Equals(name));
      }
      catch (Exception)
      {
      }
      return null;
    }

    public Dictionary<String, int> GetChannelsCount()
    {
      var res = new Dictionary<String, int>();
      res.Add("withoutPrograms", 0);
      res.Add("withPrograms", 0);

      foreach (var channel in GetChannels(includeOffset: true))
      {
        var key = "withoutPrograms";
        if (channel.HasPrograms)
          key = "withPrograms";

        if (res.ContainsKey(key))
        {
          res[key]++;
        }
        else
        {
          res.Add(key, 1);
        }
      }
      return res;
    }

    public Xmltv GetChannelsGuides()
    {
      Xmltv epg = new Xmltv();
      try
      {
        // Combine all xml guides into a single one
        Log.Info("Merging channel guides");

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
