using System;
using System.Diagnostics;
using System.IO;

namespace NunitTests
{
  class Utils
  {

  }
  public class TestEnvironment
  {
    public String configFolder = "";
    public String configFolderName = "wgmulti_tests";
    String wgFolder = Environment.GetEnvironmentVariable("WGPATH", EnvironmentVariableTarget.Machine);
    public String testReportFilePath;
    public String configFileXml = "WebGrab++.config.xml";
    public String configFileJson = "wgmulti.config.json";
    String ppConfigFile = "";
    String postProcessName = "rex";
    bool postprocessEnabled = false;
    public String outputEpg = "epg.xml";
    public String outputEpgAfterPostProcess = "";
    public String temp = Path.GetTempPath();
    String SiteiniFolder
    {
      get { return Path.Combine(configFolder, "siteini.user");  }
      set { }
    }
    const String exe = "wgmulti.exe";
    public String WgmultiExe { get { return Path.Combine(configFolder, exe); } }
    public String WgmultiConfig { get { return Path.Combine(configFolder, exe + ".config"); } }
    public static String nunitAssets;
    public static String serverEpgFolder;

    /// <summary>
    /// Create the temp dir
    /// Save the WebGrab XML config
    /// Save the Wgmulti JSON config
    /// Save the dummy.ini grabbing file
    /// Save the
    /// Start the http service listening on localhost:19801
    /// </summary>
    /// <param name="postProcessName"></param>
    public TestEnvironment(PostProcessType ppType = PostProcessType.NONE, bool copyChannel = false, String subdir = "")
    {
      //var guid = Guid.NewGuid().ToString().Substring(8);

      var path = "";
      if (String.IsNullOrEmpty(wgFolder))
        throw new Exception("%WGPATH% variable does not exist!");

      nunitAssets = Environment.GetEnvironmentVariable("nunitassetsfolder", EnvironmentVariableTarget.Machine);
      if (String.IsNullOrEmpty(nunitAssets))
        throw new Exception("%testbindebug% variable not defined!");


      //Create temp config dir with appropriate files inside
      configFolder = Path.Combine(temp, configFolderName, subdir);
      configFileXml = Path.Combine(configFolder, configFileXml);
      configFileJson = Path.Combine(configFolder, configFileJson);
      outputEpg = Path.Combine(configFolder, outputEpg);
      testReportFilePath = Path.Combine(configFolder, "wgmulti.report.json");

      Destroy(); //Clean up 

      if (!Directory.Exists(configFolder))
        Directory.CreateDirectory(configFolder);


      // If post processing is enabled, create the directory and config files
      if (ppType != PostProcessType.NONE)
      {
        var ppDir = Path.Combine(configFolder, postProcessName);

        postProcessName = ppType.ToString().ToLower();
        ppConfigFile = Path.Combine(ppDir, postProcessName + ".config.xml");
        postprocessEnabled = true;

        if (!Directory.Exists(ppDir))
          Directory.CreateDirectory(ppDir);

        outputEpgAfterPostProcess = Path.Combine(ppDir, "epg.xml");
        var xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<settings>\r\n  <filename>epg.xml</filename>\r\n  <desc>{'title'...}</desc>\r\n</settings>";
        File.WriteAllText(ppConfigFile, xml);
      }



      // Save global XML config
      path = Path.Combine(nunitAssets, "WebGrab++.config.xml");
      var buff = File.ReadAllText(path);
      buff = buff.Replace("epg.xml", outputEpg);
      if (postprocessEnabled)
        buff = buff.Replace("postprocess run=\"n\"", "postprocess run =\"y\"");
      File.WriteAllText(configFileXml, buff);

      //Save global JSON config
      path = Path.Combine(nunitAssets, "wgmulti.config.json");
      buff = File.ReadAllText(path);
      buff = buff.Replace("epg.xml", outputEpg.Replace("\\", "\\\\"));
      if (postprocessEnabled)
        buff = buff.Replace("run\":false", "run\":true");

      var siteini = "";
      // If copyChannel is enabled use grab type COPY otherwise use a regular siteini
      if (copyChannel)
      {
        siteini = "{\r\n          \"name\": \"Channel3Epg\",\r\n          \"site_id\": \"Channel 3 ID\"\r\n        }";
        buff = buff.Replace("##siteini##", siteini);

        var globalSiteini = "{\r\n          \"grab_type\": \"copy\",\r\n          \"name\": \"Channel3Epg\",\r\n          \"path\": \"http://localhost/epg/epg-cet.xml.gz\"\r\n        }";
        buff = buff.Replace("siteinis\": []", "siteinis\": [" + globalSiteini + "]");

        // Get server EPG folder - where we will extract the newly generated raw EPG content for COPY type grabbing
        serverEpgFolder = Environment.GetEnvironmentVariable("serverEpgFolder", EnvironmentVariableTarget.Machine);
        if (String.IsNullOrEmpty(serverEpgFolder))
          throw new Exception("%testbindebug% variable not defined!");

        var epg = File.ReadAllText(Path.Combine(nunitAssets, "epg-cet.xml"));
        var now = DateTime.Now;
        var today = now.ToString("yyyyMMdd");
        now = now.AddDays(1);
        var tomorrow = now.ToString("yyyyMMdd");
        epg = epg.Replace("##TODAY##", today).Replace("##TOMORROW##", tomorrow);
        File.WriteAllText(Path.Combine(serverEpgFolder, "epg-cet.xml"), epg);
        var gzippedFile = Path.Combine(serverEpgFolder, "epg-cet.xml.gz");
        if (File.Exists(gzippedFile))
          File.Delete(gzippedFile);

        var bytes = File.ReadAllBytes(Path.Combine(serverEpgFolder, "epg-cet.xml"));
        using (FileStream fs = new FileStream(gzippedFile, FileMode.CreateNew))
        using (var zipStream = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Compress, false))
        {
          zipStream.Write(bytes, 0, bytes.Length);
        }
      }
      else
      {
        siteini = "{\r\n          \"name\": \"siteini\",\r\n          \"site_id\": \"channel_1\"\r\n          }";
        buff = buff.Replace("##siteini##", siteini);
      }

      File.WriteAllText(configFileJson, buff);

      // Create 2 test ini files. First save the EEST site ini
      if (!Directory.Exists(SiteiniFolder))
        Directory.CreateDirectory(SiteiniFolder);

      File.Copy(
        Path.Combine(nunitAssets, "siteini.ini"), 
        Path.Combine(SiteiniFolder, "siteini.ini"),
        true);

      // Save CET siteini
      File.Copy(
        Path.Combine(nunitAssets, "siteiniCET.ini"), 
        Path.Combine(SiteiniFolder, "siteiniCET.ini"),
        true);

      File.Copy(
        Path.Combine(nunitAssets, "siteiniEmpty.ini"),
        Path.Combine(SiteiniFolder, "siteiniEmpty.ini"),
        true);

      File.Copy(
        Path.Combine(nunitAssets, "siteini2.ini"),
        Path.Combine(SiteiniFolder, "siteini2.ini"),
        true);
    }

    public void Destroy()
    {
      if (File.Exists(outputEpg))
        File.Delete(outputEpg);

      if (File.Exists(configFileJson))
        File.Delete(outputEpg);

      if (File.Exists(configFileXml))
        File.Delete(outputEpg);
    }

    public void RunWgmulti()
    {
      var startInfo = new ProcessStartInfo();
      startInfo.UseShellExecute = false;
      startInfo.FileName = WgmultiExe;

      var process = new Process();
      process.StartInfo = startInfo;
      process.Start();
      process.WaitForExit(1000 * 60 * 2); //5 minutes
    }

    public void CreateWGRunTimeConfig(bool useJsonConfig = true)
    {
      // Copy wgmulti.exe to temp folder
      var exePath = Path.Combine(nunitAssets, exe);
      if (!File.Exists(exePath))
        throw new FileNotFoundException(exePath + " not found!");

      var newExe = Path.Combine(configFolder, exe);

      File.Copy(exePath, newExe, true);

      if (!File.Exists(newExe))
        throw new FileNotFoundException(newExe + " not found!");

      // Create wgmulti.exe.config file
      var xml = "<?xml version =\"1.0\" encoding=\"utf-8\"?>\r\n<configuration>\r\n  <startup>\r\n    <supportedRuntime version=\"v4.0\" sku=\".NETFramework,Version=v4.5\" />\r\n  </startup>\r\n  <appSettings>\r\n    " +
        "<add key=\"WebGrabFolder\" value=\"" + wgFolder + "\" />\r\n    " +
        "<add key=\"UseJsonConfig\" value=\"" + useJsonConfig.ToString().ToLower() + "\" />\r\n    " +
        "<add key=\"ConfigDir\" value=\"" + configFolder + "\" />\r\n    " +
        "<add key=\"GrabingTempFolder\" value=\"" + configFolder + "\" />\r\n  " +
        "</appSettings>\r\n</configuration>";

      File.WriteAllText(WgmultiConfig, xml);
    }
  }

  public enum PostProcessType { NONE, REX, MDB }
}
