using System;
using System.Diagnostics;
using System.IO;

namespace Tests
{
  public class TestEnvironment
  {
    public String configFolder = "";
    public String configFolderName = "wgmulti_tests";
    //public Config config;
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
    public String wgmultiConfig = "wgmulti.exe.config";
    public String wgExe = "WebGrab+Plus.exe";
    public String wgDll = "xmltv.dll";
    /// <summary>
    /// Create the temp dir
    /// Save the WebGrab XML config
    /// Save the Wgmulti JSON config
    /// Save the dummy.ini grabbing file
    /// Save the
    /// Start the http service listening on localhost:19801
    /// </summary>
    /// <param name="postProcessName"></param>
    public TestEnvironment(PostProcessType ppType = PostProcessType.NONE, bool copyChannel = false)
    {
      var path = "";
      if (String.IsNullOrEmpty(wgFolder))
        throw new Exception("%WGPATH% variable does not exist!");

      File.Copy(Path.Combine(wgFolder, wgExe), Path.Combine(Directory.GetCurrentDirectory(), wgExe));
      File.Copy(Path.Combine(wgFolder, wgDll), Path.Combine(Directory.GetCurrentDirectory(), wgDll));

      //Create temp config dir with appropriate files inside
      configFolder = Path.Combine(temp, configFolderName);
      configFileXml = Path.Combine(configFolder, configFileXml);
      configFileJson = Path.Combine(configFolder, configFileJson);
      outputEpg = Path.Combine(configFolder, outputEpg);
      

      Destroy(); //Clean up 

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
        var xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><settings>\r\n  <filename>epg.xml</filename>\r\n  <desc>{'title'...}</desc>\r\n</settings>";
        File.WriteAllText(ppConfigFile, xml);
      }

      //outputEpg = Path.Combine(configFolder, outputEpg);

      if (!Directory.Exists(configFolder))
        Directory.CreateDirectory(configFolder);
      testReportFilePath = Path.Combine(configFolder, "wgmulti.report.json");


      //ws = new HttpServer(5);
      //ws.Start(19801);


      // Save global XML config
      path = (@"..\..\Test Files\WebGrab++.config.xml");
      var buff = File.ReadAllText(path);
      buff = buff.Replace("epg.xml", outputEpg);
      buff = buff.Replace("rex", postProcessName);
      buff = buff.Replace("run=\"n", "run=\"" + postprocessEnabled.ToString().ToLower());
      File.WriteAllText(configFileXml, buff);

      //Save global JSON config
      path = (@"..\..\Test Files\wgmulti.config.json");
      buff = File.ReadAllText(path);
      buff = buff.Replace("epg.xml", outputEpg.Replace("\\", "\\\\"));
      buff = buff.Replace("rex", postProcessName);
      buff = buff.Replace("run\":false",  "run\":" + postprocessEnabled.ToString().ToLower());

      // If copyChannel is enabled use grab type COPY otherwise use a regular siteini
      if (copyChannel)
      {
        var siteini = "{\r\n          \"name\": \"siteini\",\r\n          \"site_id\": \"channel_1\"\r\n        }";

        var siteini_copy = "{\r\n          \"name\": \"Channel3Epg\",\r\n          \"site_id\": \"Channel 3 ID\"\r\n        }";

        buff = buff.Replace(siteini, siteini_copy);

        var globalSiteini = "{\r\n          \"grab_type\": \"copy\",\r\n          \"name\": \"Channel3Epg\",\r\n          \"path\": \"http://localhost/epg/epg-cet.xml.gz\"\r\n        }";
        buff = buff.Replace("siteinis\": [ ]", "siteinis\": [" + globalSiteini + "]");

        var epg = File.ReadAllText(@"..\..\Test Files\epg-cet.xml");
        var now = DateTime.Now;
        var today = now.ToString("yyyyMMdd");
        now = now.AddDays(1);
        var tomorrow = now.ToString("yyyyMMdd");
        epg = epg.Replace("##TODAY##", today).Replace("##TOMORROW##", tomorrow);
        File.WriteAllText(@"C:\wamp64\www\epg\epg-cet.xml", epg);
        var gzippedFile = @"C:\wamp64\www\epg\epg-cet.xml.gz";
        if (File.Exists(gzippedFile))
          File.Delete(gzippedFile);

        var bytes = File.ReadAllBytes(@"C:\wamp64\www\epg\epg-cet.xml");
        using (FileStream fs = new FileStream(gzippedFile, FileMode.CreateNew))
        using (var zipStream = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Compress, false))
        {
          zipStream.Write(bytes, 0, bytes.Length);
        }
      }

      File.WriteAllText(configFileJson, buff);

      //config = new Config(configFolder);

      // Create 2 test ini files. First save the EEST site ini
      buff = File.ReadAllText(@"..\..\Test Files\siteini.ini");
      File.WriteAllText(Path.Combine(configFolder, "siteini.ini"), buff);
      // Save CET siteini
      buff = File.ReadAllText(@"..\..\Test Files\siteiniCET.ini");
      File.WriteAllText(Path.Combine(configFolder, "siteiniCET.ini"), buff);

      File.Copy(@"..\..\Test Files\siteiniEmpty.ini", Path.Combine(configFolder, "siteiniEmpty.ini"));

      File.Copy(@"..\..\Test Files\siteini2.ini", Path.Combine(configFolder, "siteini2.ini"));

      Debug.WriteLine("Environment created in {0}", configFolder);
    }

    public void Destroy()
    {
      if (Directory.Exists(configFolder))
      {
        Directory.Delete(configFolder, true);
        Debug.WriteLine("Existing TestEnvironment in {0} deleted!", configFolder);
      }
    }


    public void RunWgmulti(bool useJsonConfig = true)
    {

      //Create wgmulti.exe.config file
      CreateWGRunTimeConfig(true);

      var startInfo = new ProcessStartInfo();
      startInfo.UseShellExecute = false;
      startInfo.FileName = @"C:\Users\hristo\source\repos\xmltv-tools\Tests\bin\Debug\wgmulti.exe";
      startInfo.FileName = "wgmulti.exe";
      startInfo.UseShellExecute = false;
      

      var process = new Process();
      process.StartInfo = startInfo;
      process.Start();
      process.WaitForExit(1000 * 60 * 2); //5 minutes
    }

    public void CreateWGRunTimeConfig(bool useJsonConfig = true)
    {
      var xml = "<?xml version =\"1.0\" encoding=\"utf-8\"?>\r\n<configuration>\r\n  <startup>\r\n    <supportedRuntime version=\"v4.0\" sku=\".NETFramework,Version=v4.5\" />\r\n  </startup>\r\n  <appSettings>\r\n    " + 
        "<add key=\"WebGrabFolder\" value=\"" + wgFolder + "\" />\r\n    " +
        "<add key=\"UseJsonConfig\" value=\"" + useJsonConfig.ToString().ToLower() + "\" />\r\n    " + 
        "<add key=\"ConfigDir\" value=\"" + configFolder + "\" />\r\n    " + 
        "<add key=\"GrabingTempFolder\" value=\"" + configFolder + "\" />\r\n  " +
        "</appSettings>\r\n</configuration>";

      File.WriteAllText(wgmultiConfig, xml);
    }
  }

  public enum PostProcessType { NONE, REX, MDB }
}
