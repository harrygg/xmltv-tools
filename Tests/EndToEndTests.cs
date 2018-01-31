using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using wgmulti;

namespace Tests
{
  [TestClass]
  public class EndToEndTests
  {
    [TestMethod]
    public void Run_Internal_JSON_Config()
    {
      var te = new TestEnvironment(ppType: PostProcessType.NONE);

      // Set arguments here otherwise the app is using:
      //"C:\\PROGRAM FILES (X86)\\MICROSOFT VISUAL STUDIO 14.0\\COMMON7\\IDE\\COMMONEXTENSIONS\\MICROSOFT\\TESTWINDOW\\vstest.executionengine.x86.exe.Config"

      Arguments.grabingTempFolder = Path.Combine(Path.GetTempPath(), "wgmulti_tests");
      Arguments.configDir = Arguments.grabingTempFolder;
      Arguments.webGrabFolder = Environment.GetEnvironmentVariable("wgpath");
      Arguments.useJsonConfig = true;

      Application.Run(te.configFolder);

      CheckElementsAfterNoPostProcess(te);
    }

    [TestMethod]
    public void Run_Internal_JSON_Config_Test_Offset_Added_To_Parent_Channel()
    {
      var te = new TestEnvironment(ppType: PostProcessType.NONE);

      Arguments.grabingTempFolder = Path.Combine(Path.GetTempPath(), "wgmulti_tests");
      Arguments.configDir = Arguments.grabingTempFolder;
      Arguments.webGrabFolder = Environment.GetEnvironmentVariable("wgpath");
      Arguments.useJsonConfig = true;

      // Create new config and overwrite the wgmilti.config.json
      Config conf = new Config();
      var siteini = new SiteIni("siteini", "channel_1");
      var channel = new Channel();
      channel.name = "Канал 1";
      channel.xmltv_id = "Канал 1 ID";
      channel.update = UpdateType.Incremental;
      channel.siteinis = new List<SiteIni>() { siteini };
      channel.offset = 5.5;
      conf.channels.Add(channel);

      var timeshifted = new Channel();
      timeshifted.name = "Канал 1 +1";
      timeshifted.xmltv_id = "Канал 1 +1 ID";
      timeshifted.offset = 1;
      timeshifted.same_as = "Канал 1 ID";
      channel.timeshifts = new List<Channel>();
      channel.timeshifts.Add(timeshifted);

      var content = conf.Serialize(true);
      File.WriteAllText(te.configFileJson, content);

      Application.Run(te.configFolder);

      var root = XDocument.Load(te.outputEpg);
      var tv = root.Element("tv");

      Func<String, String> GetStartHour = delegate (String id)
      {
        try
        {
          return tv.Elements("programme")
          .Where(p => p.Attribute("channel").Value
          .Equals(id))
          .First().Attribute("start").Value.Substring(8, 4);
        }
        catch { return ""; }
      };

      // Select the start time of first program for 'Канал 1' and first program of 'Channel2ID'
      var time = GetStartHour(channel.xmltv_id);
      if (time !="0530")
        Assert.Fail(String.Format("Start time of first program in channel {0} is {1}, 0500 was expected since channel has offset=5.5", channel.name, time));

      // Select the start time of first timeshifted program for 'Канал 1 +1' and first program of 'Channel2ID'
      time = GetStartHour(timeshifted.xmltv_id);
      if (time != "0630")
        Assert.Fail(String.Format("Start time of first program in channel {0} is {1}, 0600 was expected since channel has offset=1 and the parent channel has offset=5.5", channel.name, time));
    }

    [TestMethod]
    public void Run_JSON_Config_Test_Offset_Global()
    {
      var te = new TestEnvironment(ppType: PostProcessType.NONE);

      Arguments.grabingTempFolder = Path.Combine(Path.GetTempPath(), "wgmulti_tests");
      Arguments.configDir = Arguments.grabingTempFolder;
      Arguments.webGrabFolder = Environment.GetEnvironmentVariable("wgpath");
      Arguments.useJsonConfig = true;

      File.Copy(@"..\..\Test files\wgmulti.config.global.siteinis.offset.json", te.configFileJson, true);

      Application.Run(te.configFolder);

      var root = XDocument.Load(te.outputEpg);
      var tv = root.Element("tv");

      Func<String, String> GetStartHour = delegate (String id)
      {
        try
        {
          return tv.Elements("programme")
          .Where(p => p.Attribute("channel").Value
          .Equals(id))
          .First().Attribute("start").Value.Substring(8, 4);
        }
        catch { return ""; }
      };

      var time = GetStartHour("Канал 1 ID");
      if (time != "0100")
        Assert.Fail(String.Format("Start time of first program in Канал 1 is {0}, 0100 was expected", time));

      time = GetStartHour("Канал 1 +1 ID");
      if (time != "0200")
        Assert.Fail(String.Format("Start time of first program in channel Канал 1 +1 ID is {0}, 0200 was expected since channel has offset=1 added to the parent offset.", time));

      time = GetStartHour("Channel2ID");
      if (time != "0200")
        Assert.Fail(String.Format("Start time of first program in channel Channel2ID is {0}, 0200 was expected since channel has offset=2.", time));

      time = GetStartHour("Channel 3 ID");
      if (time != "0300")
        Assert.Fail(String.Format("Start time of first program in channel Channel 3 ID is {0}, 0300 was expected since channel offset should overwrite global", time));

      time = GetStartHour("Channel 4 ID");
      if (time != "0100")
        Assert.Fail(String.Format("Start time of first program in channel Channel 4 ID is {0}, 0100 was expected since channel local siteini offset should be ignored.", time));

      time = GetStartHour("Channel 5 ID");
      if (time != "0500")
        Assert.Fail(String.Format("Start time of first program in channel Channel 5 ID is {0}, 0500 was expected since channel has global siteini offset.", time));
    }

    [TestMethod]
    public void Run_Internal_JSON_Config_REX_PostProcess()
    {
      var te = new TestEnvironment(ppType: PostProcessType.REX);

      Arguments.grabingTempFolder = Path.Combine(Path.GetTempPath(), "wgmulti_tests");
      Arguments.configDir = Arguments.grabingTempFolder;
      Arguments.webGrabFolder = Environment.GetEnvironmentVariable("wgpath");
      Arguments.useJsonConfig = true;

      Application.Run(te.configFolder);

      ChecksAfterPostProcess(te);
    }

    [TestMethod]
    public void Run_Internal_JSON_Config_Different_Output_Filename()
    {
      var te = new TestEnvironment(ppType: PostProcessType.NONE);
      Arguments.grabingTempFolder = Path.Combine(Path.GetTempPath(), "wgmulti_tests");
      Arguments.configDir = Arguments.grabingTempFolder;
      Arguments.webGrabFolder = Environment.GetEnvironmentVariable("wgpath");
      Arguments.useJsonConfig = true;

      // Change output filename
      var content = File.ReadAllText(te.configFileJson);
      content = content.Replace("epg.xml", "guide.xml");
      File.WriteAllText(te.configFileJson, content);
      te.outputEpg = Path.Combine(te.configFolder, "guide.xml");

      Application.Run(te.configFolder);

      CheckElementsAfterNoPostProcess(te);
    }


    [TestMethod]
    public void Run_Internal_XML_Config_Different_Output_Filename()
    {
      var te = new TestEnvironment(ppType: PostProcessType.NONE);
      Arguments.grabingTempFolder = Path.Combine(Path.GetTempPath(), "wgmulti_tests");
      Arguments.configDir = Arguments.grabingTempFolder;
      Arguments.webGrabFolder = Environment.GetEnvironmentVariable("wgpath");
      Arguments.useJsonConfig = false;

      // Change output filename
      var content = File.ReadAllText(te.configFileXml);
      content = content.Replace("epg.xml", "guide.xml");
      File.WriteAllText(te.configFileXml, content);
      te.outputEpg = Path.Combine(te.configFolder, "guide.xml");

      Application.Run(te.configFolder);

      CheckElementsAfterNoPostProcess(te);
    }

    [TestMethod]
    public void Run_External_JSON_Config()
    {
      var te = new TestEnvironment(ppType: PostProcessType.NONE);
      te.RunWgmulti(useJsonConfig: true);

      CheckElementsAfterNoPostProcess(te);
    }


    [TestMethod]
    public void Run_External_JSON_Config_REX_PostProcess()
    {
      var te = new TestEnvironment(ppType: PostProcessType.REX);
      te.RunWgmulti(useJsonConfig: true);
      ChecksAfterPostProcess(te);
    }


    [TestMethod]
    public void Run_External_XML_Config_REX_PostProcess()
    {
      var te = new TestEnvironment(ppType: PostProcessType.REX);
      te.RunWgmulti(useJsonConfig: false);

      ChecksAfterPostProcess(te);

    }


    [TestMethod]
    public void Run_External_XML_Config()
    {
      var te = new TestEnvironment(ppType: PostProcessType.NONE);
      te.RunWgmulti(useJsonConfig: false);

      CheckElementsAfterNoPostProcess(te);
    }

    [TestMethod]
    public void Run_Internal_XML_Config()
    {
      var te = new TestEnvironment(ppType: PostProcessType.NONE);

      Arguments.grabingTempFolder = Path.Combine(Path.GetTempPath(), "wgmulti_tests");
      Arguments.configDir = Arguments.grabingTempFolder;
      Arguments.webGrabFolder = Environment.GetEnvironmentVariable("wgpath");
      Arguments.useJsonConfig = false;
      Arguments.exportJsonConfig = true;

      Application.Run(te.configFolder);

      CheckElementsAfterNoPostProcess(te);
    }


    [TestMethod]
    public void Run_Internal_XML_Config_REX_PostProcess()
    {
      var te = new TestEnvironment(ppType: PostProcessType.REX);
      Arguments.grabingTempFolder = Path.Combine(Path.GetTempPath(), "wgmulti_tests");
      Arguments.configDir = Arguments.grabingTempFolder;
      Arguments.webGrabFolder = Environment.GetEnvironmentVariable("wgpath");
      Arguments.useJsonConfig = false;
      Arguments.exportJsonConfig = true;

      Application.Run(te.configFolder);

      ChecksAfterPostProcess(te);

    }

    [TestMethod]
    public void Run_External_JSON_Config_Copy_Channel()
    {
      var te = new TestEnvironment(ppType: PostProcessType.NONE, copyChannel: true);
      te.RunWgmulti(useJsonConfig: true);

      CheckMandatoryElementsExist(te.outputEpg);
      Debug.WriteLine("Channel copied and renamed!");
    }

    ////TODO
    //[TestMethod]
    //public void Run_External_JSON_Config_Copy_Channel_Offset_Minus_One()
    //{
    //  var te = new TestEnvironment(ppType: PostProcessType.NONE, copyChannel: true);
    //  te.RunWgmulti(useJsonConfig: true);

    //  CheckMandatoryElementsExist(te.outputEpg);
    //  Debug.WriteLine("Channel copied and renamed!");
    //}

    [TestMethod]
    public void Run_External_JSON_Config_REX_Copy_Channel()
    {
      var te = new TestEnvironment(ppType: PostProcessType.REX, copyChannel: true);
      te.RunWgmulti(useJsonConfig: true);

      CheckMandatoryElementsExist(te.outputEpg);
    }

    [TestMethod]
    public void Run_Internal_JSON_Config_REX_Copy_Channel()
    {
      var te = new TestEnvironment(ppType: PostProcessType.REX, copyChannel: true);
      Arguments.grabingTempFolder = Path.Combine(Path.GetTempPath(), "wgmulti_tests");
      Arguments.configDir = Arguments.grabingTempFolder;
      Arguments.webGrabFolder = Environment.GetEnvironmentVariable("wgpath");
      Arguments.useJsonConfig = true;

      Application.Run(te.configFolder);

      CheckMandatoryElementsExist(te.outputEpg);
    }

    [TestMethod]
    public void Run_Internal_JSON_Config_Test_Grab_From_Second_Siteini()
    {
      var te = new TestEnvironment(ppType: PostProcessType.NONE);
      Arguments.grabingTempFolder = Path.Combine(Path.GetTempPath(), "wgmulti_tests");
      Arguments.configDir = Arguments.grabingTempFolder;
      Arguments.webGrabFolder = Environment.GetEnvironmentVariable("wgpath");
      Arguments.reportFilePath = Path.Combine(Arguments.configDir, "report.json");
      Arguments.useJsonConfig = true;
      File.Copy(@"..\..\Test files\wgmulti.config.multi.siteinis.json",
        Path.Combine(Arguments.configDir, "wgmulti.config.json"), true);

      Application.Run(te.configFolder);

      CheckMandatoryElementsExist(te.outputEpg);
    }

    [TestMethod]
    public void Run_Internal_JSON_Config_Test_Disabled_Siteini()
    {
      var te = new TestEnvironment(ppType: PostProcessType.NONE);
      Arguments.grabingTempFolder = Path.Combine(Path.GetTempPath(), "wgmulti_tests");
      Arguments.configDir = Arguments.grabingTempFolder;
      Arguments.webGrabFolder = Environment.GetEnvironmentVariable("wgpath");
      Arguments.useJsonConfig = true;
      File.Copy(@"..\..\Test files\wgmulti.config.multi.disabled.siteini.json",
        Path.Combine(Arguments.configDir, "wgmulti.config.json"), true);

      Application.Run(te.configFolder);

      CheckMandatoryElementsExist(te.outputEpg);
    }

    [TestMethod]
    public void Global_Siteini_With_Different_Timespan()
    {
      var te = new TestEnvironment();
      Arguments.grabingTempFolder = Path.Combine(Path.GetTempPath(), "wgmulti_tests");
      Arguments.configDir = Arguments.grabingTempFolder;
      Arguments.webGrabFolder = Environment.GetEnvironmentVariable("wgpath");
      Arguments.useJsonConfig = true;
      File.Copy(@"..\..\Test files\wgmulti.config.global.siteinis.timestamp.json",
        Path.Combine(Arguments.configDir, "wgmulti.config.json"), true);

      Application.Run(te.configFolder);

      CheckMandatoryElementsExist(te.outputEpg);

      // Check the content of the webgrab file it should contain timespan 2
      var conf = Config.DeserializeFromFile(
        Path.Combine(Arguments.grabingTempFolder, "siteini", "WebGrab++.config.xml"));

      if (conf.period.days != 2)
        Assert.Fail("siteini period is not 2. Actual: " + conf.period.days);

      if (conf.channels[0].siteinis[0].timespan != 2)
        Assert.Fail("siteini period is not 2. Actual: " + conf.channels[0].siteinis[0].timespan);

      conf = Config.DeserializeFromFile(
        Path.Combine(Arguments.grabingTempFolder, "siteiniCET", "WebGrab++.config.xml"));

      if (conf.period.days != 0)
        Assert.Fail("siteini period is not 0. Actual: " + conf.period.days);
    }

    [TestMethod]
    public void Global_Siteini_With_Different_Offset()
    {
      var te = new TestEnvironment();
      Arguments.grabingTempFolder = Path.Combine(Path.GetTempPath(), "wgmulti_tests");
      Arguments.configDir = Arguments.grabingTempFolder;
      Arguments.webGrabFolder = Environment.GetEnvironmentVariable("wgpath");
      Arguments.useJsonConfig = true;
      File.Copy(@"..\..\Test files\wgmulti.config.global.siteinis.offset.json",
        te.configFileJson, true);

      //Application.Run(te.configFolder);

      //CheckMandatoryElementsExist(te.outputEpg);

      // Check the content of the webgrab file it should contain timespan 2
      var conf = Config.DeserializeFromFile(te.configFileJson);

      if (conf.offset != 1)
        Assert.Fail("Config global offset is not 1. Actual: " + conf.offset);

      //if (conf.channels[0].offset != 1)
      //  Assert.Fail("Channel 1 offset is not 1. Actual: " + conf.channels[0].offset);

      if (conf.channels[0].siteinis[0].offset != 1)
        Assert.Fail("siteini offset is not 1. Actual: " + conf.siteinis[0].offset);

      //if (conf.channels[1].offset != 2)
      //  Assert.Fail("Channel 2 offset is not 2. Actual: " + conf.period.days);

      //if (conf.channels[2].offset != 3)
      //  Assert.Fail("Channel 3 offset is not 3. Actual: " + conf.period.days);

      //if (conf.channels[3].offset != 1)
      //  Assert.Fail("Channel 4 offset is not 1. Actual: " + conf.period.days);

      if (conf.channels[3].siteinis[0].offset != 1)
        Assert.Fail("Channel 4 siteini offset is not 1. It's not overwritten. Actual: " + conf.channels[3].siteinis[0].offset);
    }

    [TestMethod]
    public void NOTEST_Build_Env_Only()
    {
      var te = new TestEnvironment(ppType: PostProcessType.NONE);
      Debug.WriteLine("Test Environment is up in {0}", te.configFolder);
    }

    [TestMethod]
    public void NOTEST_Build_Env_CopySiteIni()
    {
      var te = new TestEnvironment(ppType: PostProcessType.NONE, copyChannel: true);
      Debug.WriteLine("Test Environment is up in {0}", te.configFolder);
    }

    void CheckElementsAfterNoPostProcess(TestEnvironment te)
    {
      // Verify that the output EPG file exists
      if (!File.Exists(te.outputEpg))
        Assert.Fail("File does not exist: " + te.outputEpg);

      // Verify 3 channel tags exsits, more than 0 programmes tags and 0 desc tags
      CheckMandatoryElementsExist(te.outputEpg);
      Debug.WriteLine("Mandatory elements exist");

      // Verify that the first program of 'Channel1' starts 1 hour before the program of 'Channel1 +1'
      OffsetProgramStartTimeIsDifferent(te.outputEpg);
      Debug.WriteLine("Offset time is correct");

      // Verify that the Channel2 programs times are converted to EEST time
      TimesConvertedToLocal(te);
      Debug.WriteLine("Times are converted to local");
    }


    void ChecksAfterPostProcess(TestEnvironment te)
    {
      // Verify that the input config file exists
      if (!File.Exists(te.wgmultiConfig))
        Assert.Fail("File does not exist: " + te.wgmultiConfig);

      // Verify that the output EPG file exists
      if (!File.Exists(te.outputEpg))
        Assert.Fail("File does not exist: " + te.outputEpg);

      // Verify 3 channel tags exsits, more than 0 programmes tags and 0 desc tags
      CheckMandatoryElementsExist(te.outputEpg);
      Debug.WriteLine("Mandatory elements exist");

      // Verify that the output EPG file exists
      if (!File.Exists(te.outputEpgAfterPostProcess))
        Assert.Fail("File does not exist: " + te.outputEpgAfterPostProcess);

      CheckMandatoryElementsExist(te.outputEpgAfterPostProcess, true);
      Debug.WriteLine("Mandatory elements in postprocess EPG exist");

      // Verify that the first program of 'Channel1' starts 1 hour before the program of 'Channel1 +1'
      OffsetProgramStartTimeIsDifferent(te.outputEpg);
      Debug.WriteLine("Offset time is correct");

      // Verify that the Channel2 programs times are converted to EEST time
      TimesConvertedToLocal(te);
    }

    void CheckMandatoryElementsExist(String filename, bool includePostProcessed = false)
    {
      var root = XDocument.Load(filename);
      var tv = root.Element("tv");

      Func<String, bool> ElementNotExist = delegate (String id)
      {
        try { return tv.Elements("programme").First(p => p.Attribute("channel").Value.Equals(id)) == null; }
        catch { return true; }
      };

      try
      {
        if (ElementNotExist("Канал 1 ID"))
          Assert.Fail("Missing program with channel id 'Канал 1 ID'");

        if (ElementNotExist("Канал 1 +1 ID"))
          Assert.Fail("Missing program with channel id 'Канал 1 +1 ID'");

        if (ElementNotExist("Channel2ID"))
          Assert.Fail("Missing program with channel id 'Channel2ID'");

        var channelsExist = tv.Elements("channel").Count();
        if (channelsExist < 3)
          Assert.Fail("Channel number is less than 3. Actual: {0}", channelsExist);

        // if includePostProcessed is true, check also if there is a 'desc' element
        var el = tv.Element("programme").Element("desc");
        if (includePostProcessed)
        {
          if (el == null)
            Assert.Fail("Postprocess is enabled but 'desc' element does not exist in the epg data!");
        }
        else
        {
          if (el != null)
            Assert.Fail("Postprocess is not enabled but 'desc' element not exist in the epg data!");
        }
      }
      catch (Exception ex)
      {
        Assert.Fail(ex.Message);
      }
    }

    void OffsetProgramStartTimeIsDifferent(String filename)
    {
      try
      {
        var root = XDocument.Load(filename);
        var tv = root.Element("tv");

        Func<String, int> GetStartHour = delegate (String id)
        {
          try {
            return Convert.ToInt16(tv.Elements("programme")
            .Where(p => p.Attribute("channel").Value
            .Equals(id))
            .First().Attribute("start").Value.Substring(8, 2));
          }
          catch { return -2; }
        };

        // Select the start time of first program for 'Channel1' and first program of 'Channel1 +1'
        // The difference must be 1
        var p1 = GetStartHour("Канал 1 ID");
        var p2 = GetStartHour("Канал 1 +1 ID");

        if (p1 + 1 != p2)
          Assert.Fail("Parent and offset program start times are not correct");
      }
      catch (Exception ex)
      {
        Assert.Fail("Parent and offset program start times are not correct");
        Assert.Fail(ex.Message);
      }
    }


    void TimesConvertedToLocal(TestEnvironment te)
    {
      try
      {
        var root = XDocument.Load(te.outputEpg);
        var tv = root.Element("tv");

        Func<String, String> GetStringDate = delegate (String id)
        {
          try
          {
            return tv.Elements("programme")
          .Where(p => p.Attribute("channel").Value.Equals(id))
          .First().Attribute("start").Value.Substring(16, 4);
          }
          catch { return ""; }
        };

        var p1 = GetStringDate("Channel2ID");

        root = XDocument.Load(Path.Combine(te.configFolder, "siteiniCET", Path.GetFileName(te.outputEpg)));
        tv = root.Element("tv");
        var p2 = GetStringDate("Channel2ID");

        if (!(p1 == "0200" && p2 == "0100"))
          Assert.Fail("Error during verification of local time conversion");
      }
      catch (Exception ex)
      {
        Assert.Fail(ex.Message);
      }
    }


  }
}
