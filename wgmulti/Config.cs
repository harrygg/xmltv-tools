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
    public String fileName = "WebGrab++.config.xml";
    public String filePath = String.Empty;
    public String folder = String.Empty;
    public String outputFilePath = "epg.xml";
    public String epgFileName = "epg.xml";
    public String proxy = String.Empty;
    public String mode = "m,nomark";
    public String userAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/54.0.2840.99 Safari/537.36";
    public String logging = "on";
    public String skip = "noskip";
    public String timeSpan = "0";
    public String updateType = String.Empty;
    public String retry = "4";
    public String retryTimeOut = "20";
    public String retryChannelDelay = "5";
    public String retryIndexDelay = "1";
    public String retryShowDelay = "1";
    public bool postProcessEnabled = false;
    public String postProcessName = "mdb";
    public String postProcessRun = "n";
    public String postProcessGrab = "y";
    public bool grabbingEnabled = true;
    String postProcessConfigFileName = ".config.xml";
    public String postProcessConfigFilePath = "";
    public String postProcessOutputFilePath = "";
    public XElement postProcessSettings = null;
    public List<Channel> channels;
    public IEnumerable<IGrouping<String, Channel>> grabbers;
    public String tempDir = "";

    /// <summary>
    /// Create a config object. If a path to config.xml file is provided, settings will be loaded.
    /// Otherwise a default config file will be created
    /// </summary>
    /// <param name="path">Directory where the configuration exists or will be created</param>
    public Config(String path = "")
    {
      if (!Path.IsPathRooted(path))
        throw new ArgumentException("Config path must be an absolute path");

      folder = path.EndsWith(".xml") ? new FileInfo(path).Directory.FullName : path;

      tempDir = Path.Combine(Path.GetTempPath(), "wgmulti");

      SetAbsPaths(folder);

      LoadSettingsFromFile(filePath);
    }

    /// <summary>
    /// called after cloning a config
    /// </summary>
    /// <param name="configFolder"></param>
    public void SetAbsPaths(String configFolder)
    {
      folder = configFolder; //when called from Clone
      filePath = Path.Combine(folder, fileName);
      outputFilePath = Path.Combine(folder, epgFileName);
      if (postProcessEnabled)
      {
        postProcessConfigFilePath = Path.Combine(folder, postProcessName, postProcessName + postProcessConfigFileName);
        postProcessOutputFilePath = Path.Combine(folder, postProcessName, epgFileName);
      }
    }

    public void LoadSettingsFromFile(String file = "", bool loadChannels = true)
    {
      if (file == "")
        file = filePath;

      if (!File.Exists(file))
      {
        Log("Configuration file " + file + " not found! Exiting...");
        return;
      }
      
      xmlConfig = XDocument.Load(file);
      var settings = xmlConfig.Element("settings");
      try {
        var filename = settings.Element("filename").Value;
        outputFilePath = Path.IsPathRooted(filename) ? filename : Path.Combine(folder, filename);
      } catch {}
      try { mode = settings.Element("mode").Value; } catch {}
      try { proxy = settings.Element("proxy").Value; } catch {}
      try { userAgent = settings.Element("user-agent").Value; } catch { }
      try { logging = settings.Element("logging").Value; } catch {}
      try { skip = settings.Element("skip").Value; } catch {}
      try { timeSpan = settings.Element("timespan").Value;} catch {}
      try { updateType = settings.Element("update").Value; } catch {}
      try {
        var retryEl = settings.Element("retry");
        retry = retryEl.Value;
        try { retryTimeOut = retryEl.Attribute("time-out").Value; } catch { }
        try { retryChannelDelay = retryEl.Attribute("channel-delay").Value; } catch { }
        try { retryIndexDelay = retryEl.Attribute("index-delay").Value; } catch { }
        try { retryShowDelay = retryEl.Attribute("show-delay").Value; } catch { }
      }
      catch {}
      try {
        var postProcessEl = settings.Element("postprocess"); 
        postProcessName = postProcessEl.Value;
        try {
          postProcessRun = postProcessEl.Attribute("run").Value.ToLower();
          postProcessGrab = postProcessEl.Attribute("grab").Value.ToLower();
          grabbingEnabled = (postProcessGrab == "y" || postProcessGrab == "yes" || postProcessGrab == "true" || postProcessGrab == "on");
          postProcessEnabled = (postProcessName != "" && (postProcessRun == "y" || postProcessRun == "yes" || postProcessRun == "true" || postProcessRun == "on"));
          if (postProcessEnabled)
          {
            var postProcessDir = Path.Combine(folder, postProcessName);

            //if (!Directory.Exists(postProcessDir))
            //  Directory.CreateDirectory(postProcessDir);

            postProcessConfigFilePath = Path.Combine(postProcessDir, postProcessName + postProcessConfigFileName);
            if (File.Exists(postProcessConfigFilePath))
            {
              //Overwrite EPG xml that will be later concatenated
              var ppConfig = XDocument.Load(postProcessConfigFilePath);
              postProcessSettings = ppConfig.Element("settings");
              //var outFile = postProcessSettings.Element("filename").Value;
              //if (!Path.IsPathRooted(outFile))
              postProcessOutputFilePath = Path.Combine(postProcessDir, epgFileName);
              //postProcessSettings.Element("filename").Value = postProcessOutputFilePath;
            }
            else //If file is not found disable postprocess
            {
              postProcessRun = "n";
            }
          }
        } catch { }
        try { postProcessGrab = postProcessEl.Attribute("grab").Value; } catch { }
      }
      catch { }

      if (loadChannels)
        channels = GetChannels();
    }
    
    /// <summary>
    /// Creates a list of channels from XML config file
    /// </summary>
    /// <returns></returns>
    List<Channel> GetChannels()
    {
      channels = new List<Channel>();
      try
      {
        var cs = from c in xmlConfig.Elements("settings").Elements("channel") select c;
        if (cs.Count() == 0)
          cs = from c in xmlConfig.Elements("settings").Elements("channels").Elements("channel") select c;
        foreach (XElement c in cs)
        {
          try
          {
            String site = String.Empty;
            try
            {
              site = c.Attribute("site").Value;
            }
            catch
            {
              Console.WriteLine("Skipping channel without \"site\" attribute!");
              Console.WriteLine(c.ToString());
              continue;
            };
            var siteId = c.Attribute("site_id").Value;
            var xmltvId = c.Attribute("xmltv_id").Value;
            var name = c.Value;
            var update = c.Attribute("update") != null ? c.Attribute("update").Value : "";
            var offset = c.Attribute("offset") != null ? c.Attribute("offset").Value : "";
            var sameAs = c.Attribute("same_as") != null ? c.Attribute("same_as").Value : "";

            var channel = new Channel(site, name, siteId, xmltvId, offset, sameAs, update);
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

    public bool Save(String outputFile = "", bool saveChannels = true)
    {

      XDocument xdoc;
      try
      {
        var settings = new XElement("settings");
        var retryEl = new XElement("retry", new XAttribute("time-out", retryTimeOut), new XAttribute("channel-delay", retryChannelDelay), new XAttribute("index-delay", retryIndexDelay), new XAttribute("show-delay", retryShowDelay));
        retryEl.Value = retry;
        var postProcessEl = new XElement("postprocess", new XAttribute("run", postProcessRun), new XAttribute("grab", postProcessGrab));
        postProcessEl.Value = postProcessName;

        xdoc = new XDocument(new XDeclaration("1.0", "utf-8", null), settings);
        settings.Add(
          new XElement("filename", Path.Combine(filePath, outputFilePath)),
          new XElement("proxy", proxy),
          new XElement("mode", mode),
          new XElement("user-agent", userAgent),
          new XElement("logging", logging),
          new XElement("skip", skip),
          new XElement("timespan", timeSpan),
          new XElement("update", updateType)
        );
        settings.Add(retryEl);
        settings.Add(postProcessEl);

        if (saveChannels && channels != null)
        { 
          foreach (Channel c in channels)
            settings.Add(c.ToXElement());
        }

        if (String.IsNullOrEmpty(outputFile))
          outputFile = filePath;
        Console.WriteLine("Creating config file in {0}", outputFile);

        var folder = new FileInfo(outputFile).Directory.FullName;
        if (!Directory.Exists(folder))
          Directory.CreateDirectory(folder);
        xdoc.Save(outputFile);

        if (postProcessEnabled)
        {
          // Always overwrite filename value
          postProcessSettings.Element("filename").Value = postProcessOutputFilePath;
          xdoc = new XDocument(new XDeclaration("1.0", "utf-8", null), postProcessSettings);

          folder = new FileInfo(postProcessConfigFilePath).Directory.FullName;
          if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
          xdoc.Save(postProcessConfigFilePath);
        }

        return true;
      }
      catch (Exception e)
      {
        Log(e.ToString());
        return false;
      }  
    }

    static void Log(string v)
    {
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
      var newConfig = (Config) this.MemberwiseClone();
      newConfig.SetAbsPaths(outputFolder);

      return newConfig;
    }
  }
}
