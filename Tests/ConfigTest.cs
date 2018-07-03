using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using wgmulti;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Xml.Linq;

namespace Tests
{
  [TestClass]
  public class ConfigTest
  {
    [TestMethod]
    public void Serialize_To_Json_File()
    {
      var conf = CreateDefaultConfigObject();
      var content = conf.Serialize(true);
      File.WriteAllText("Config.json", content);
    
      CheckMandatoryElementsInJsonConfig("Config.json");
    }

    [TestMethod]
    public void Deserialize_From_Json_File()
    {
      // Create the env. and the configuration file WebGrab++.config.xml
      var te = new TestEnvironment(ppType: PostProcessType.REX);

      // Read config object from file
      var conf = Config.DeserializeFromFile(te.configFileJson);

      VerifyJsonDeserializedData(conf);
    }

    [TestMethod]
    public void Deserialize_From_Json_File_Minimal()
    {
      var conf = Config.DeserializeFromFile(@"..\..\Test files\wgmulti.config.minimal.json");

      if (conf.retry.attempts != 6)
        Assert.Fail("Retry attempts are not default 6! Actual: " + conf.retry.attempts);

      if (conf.channels.Count != 1)
        Assert.Fail("Channel count is wrong! Actual: " + conf.channels.Count);

      if (conf.period.days != 0)
        Assert.Fail("Timespan period is not default! Actual: " + conf.period.days);

      if (conf.postProcess.run)
        Assert.Fail("Default post process is not false! Actual: " + conf.postProcess.run);

      if (!conf.channels[0].Enabled)
        Assert.Fail("Channel 1 is not enabled!");

      if (conf.channels[0].update != UpdateType.None)
        Assert.Fail("Channel 1 default update is not NONE!");
    }


    [TestMethod]
    public void Deserialize_From_Json_No_Siteini()
    {
      var conf = Config.DeserializeFromFile(@"..\..\Test files\wgmulti.config.minimal.nositeini.json");
      conf.InitSiteinis();
      if (conf.channels[0].active)
        Assert.Fail("Channel 1 is not deactivated!");
    }

    [TestMethod]
    public void Deserialize_From_Xml_File_Minimal()
    {
      var conf = Config.DeserializeFromFile(@"..\..\Test files\WebGrab++.config.minimal.xml");

      if (conf.channels.Count != 1)
        Assert.Fail("Channel count is wrong!");

      if (conf.period.days != 0)
        Assert.Fail("timespan period is not default!");

      if (conf.postProcess.run)
        Assert.Fail("default post process is not false");

    }


        [TestMethod]
        public void Deserialize_From_Xml_EPG_Absolute_Path()
        {
            var conf = Config.DeserializeFromFile(@"..\..\Test files\WebGrab++.config.absolute-path.xml");

            if (conf.outputFilePath != "C:\\Temp\\demoepg.xml")
                Assert.Fail("epg.xml path is wrong!");
        }

        [TestMethod]
    public void Deserialize_From_Json_Copy_Grab_Type()
    {
      // Create the env. and the configuration file WebGrab++.config.xml
      var te = new TestEnvironment(ppType: PostProcessType.REX, copyChannel: true);
      // Read config object from file
      var conf = Config.DeserializeFromFile(te.configFileJson);
      VerifyJsonDeserializedData(conf, true);
    }

    [TestMethod]
    public void Deserialize_From_Json_File_Global_Siteinis()
    {
      var te = new TestEnvironment();
      Arguments.configDir = te.configFolder;
      // Create the env. and the configuration file WebGrab++.config.xml
      File.Copy(@"..\..\Test files\wgmulti.config.siteinis.timespan.json",
        te.configFileJson, true);

      // Read config object from file
      var conf = Config.DeserializeFromFile(te.configFileJson);
       conf.InitSiteinis();

      if (conf.siteinis == null)
        Assert.Fail("Config has no global siteinis!");

      if (conf.siteinis[0].timespan != 2)
        Assert.Fail("Global siteini has no valid timestamp. Expected 2, actual " + conf.siteinis[0].timespan);

      if (conf.channels[0].GetActiveSiteIni().timespan != 2)
        Assert.Fail("Local siteini has no valid timestamp. Expected 2, actual " + conf.channels[0].GetActiveSiteIni().timespan);

      //VerifyJsonDeserializedData(conf);
    }

    [TestMethod]
    public void Serialize_To_Xml_File()
    {
      var conf = CreateDefaultConfigObject();
      var content = conf.Serialize();
      File.WriteAllText("Config.xml", content);

      var root = XDocument.Load("Config.xml");

      var channels = root.Element("settings").Elements("channel").ToList();
      var cha1 = channels[0];
      var cha2 = channels[1];
      var cha3 = channels[2];
      var cha4 = channels.Where(c=>c.Value=="Channel 4");
      if (cha4.Any())
        Assert.Fail("Channel 4 exists but it should have been disabled as it does not have a valid siteini");

      if (channels.Count < 3)
        Assert.Fail("Document does not contain less than the expected 3 channels. Contains {0}", channels.Count);

      if (cha1.Value != "Канал 1")
        Assert.Fail("Channel 1 has different name than expected!");

      if (cha1.Attribute("xmltv_id").Value != "Канал 1 ID")
        Assert.Fail("Channel 1 has different xmltv_id than expected!");

      if (cha1.Attribute("site_id").Value != "channel_1")
        Assert.Fail("Channel 1 has different site_id than expected!");

      if (cha1.Attribute("site").Value != "siteini")
        Assert.Fail("Channel 1 has different site_id than expected!");

      if (cha1.Attribute("update").Value != "i")
        Assert.Fail("Channel 1 has different update than expected!");

      if (cha2.Attribute("update") != null)
        Assert.Fail("Channel 1 +1 has update but it shouldn't!");

      if (cha2.Attribute("xmltv_id").Value != "Канал 1 +1 ID")
        Assert.Fail("Channel 1 +1 has different xmltv_id than expected!");

      if (cha2.Attribute("offset").Value != "1")
        Assert.Fail("Channel 1 +1 has different offset than expected!");

      if (cha2.Attribute("same_as").Value != cha1.Attribute("xmltv_id").Value)
        Assert.Fail("Channel 1 +1 has different same_as value than parrent!");

      if (cha3.Attribute("site").Value != "siteiniCET")
        Assert.Fail("Channel 2 has different 'site' than expected!");

      if (cha3.Value != "Channel 2")
        Assert.Fail("Channel 2 has different name than expected! Actual: {0}", cha2.Value);

      if (cha3.Attribute("xmltv_id").Value != "Channel 2 ID")
        Assert.Fail("Channel 2 has different xmltv_id than expected!");

      if (cha3.Attribute("site_id").Value != "channel_2")
        Assert.Fail("Channel 2 has different site_id than the expected 'channel_2'. Actual: {0}", cha3.Attribute("site_id").Value);

      if (!content.Contains("channel_3\" include=\"III\" exclude=\"EEE\">"))
        Assert.Fail("channel_3 does not have correct include and exclude entries");

      if (!content.Contains("time-out=\"15"))
        Assert.Fail("time-out value is not 15");

      if (!content.Contains("\">5</retry>"))
        Assert.Fail("time-out value is not 15");

      if (!content.Contains(">mdb<"))
        Assert.Fail("Post process is not mdb");

      if (!content.Contains("timespan>0"))
        Assert.Fail("Timespan is not correct");

      if (!content.Contains("settings>\r\n"))
        Assert.Fail("Missing settings>\r\n element");

      if (!content.Contains(">automatic<"))
        Assert.Fail("Proxy value not matching expectations!");
    }

    [TestMethod]
    public void Deserialize_From_Xml_File()
    {
      // Create the env. and the configuration file WebGrab++.config.xml
      var te = new TestEnvironment(ppType: PostProcessType.REX);

      // Read config object from file
      var conf = Config.DeserializeFromFile(te.configFileXml);

      var cha1 = conf.channels[0];
      var cha2 = conf.channels[1];

      if (2 != conf.period.days)
        Assert.Fail("timespan does not have vlaue of 2");

      if (1 != conf.period.pastdays)
        Assert.Fail("timespan's keeppastdays attribute does not have vlaue of 1");

      if ("12:00" != conf.period.time)
        Assert.Fail("timespan's oneshowonly attribute does not have vlaue of 12:00");

      if (!conf.postProcess.run)
        Assert.Fail("Postproces run is not enabled");

      if (2 != conf.channels.Count)
        Assert.Fail("Channels in the config file are {0}, expected 2", conf.channels.Count);

      if ("12,1" != conf.skip)
        Assert.Fail("Skip value is no 12,1");

      if ("Канал 1" != cha1.name)
        Assert.Fail("First channel name is not Канал 1");

      if ("Канал 1 +1" != cha1.timeshifts[0].name)
        Assert.Fail("First timeshifted channel name is not Канал 1 +1");

      if (cha1.xmltv_id != cha1.timeshifts[0].same_as)
        Assert.Fail("Timeshifted same_as value doesn't match parent channel value");

      if (!cha1.active)
        Assert.Fail("Channel 1 is not active!");

      if (!cha1.Enabled)
        Assert.Fail("Channel 1 is not enabeld");

      if (!cha2.Enabled)
        Assert.Fail("Channel 2 is not enabeld");

      if (1 != cha1.siteinis.Count)
        Assert.Fail("Channel 1 siteinis is not 1, it is {0}", cha1.siteinis.Count);

      if ("III" != cha2.include)
        Assert.Fail("Channel 2 include value is not expected. Actual: {0}", cha2.include);

      if ("automatic" != conf.proxy.server)
        Assert.Fail("proxy server value is not expected. Actual {0}", conf.proxy.server);
    }

    [TestMethod]
    public void Deserialize_From_Xml_File_Nested_Channels()
    {
      // Read config object from file
      var conf = Config.DeserializeFromFile("..\\..\\Test files\\WebGrab++.config-nested.xml");
      if (conf.channels.Count != 2)
        Assert.Fail("channels count is not correct");
    }


    [TestMethod]
    public void Convert_XML_Config_To_JSON()
    {
      // Create the env. and the configuration file WebGrab++.config.xml
      var te = new TestEnvironment(ppType: PostProcessType.REX);

      // Read config object from file
      var conf = Config.DeserializeFromFile(te.configFileXml);
      var file = Path.Combine(te.configFolder, "exported_wgmulti.config.json");

      File.WriteAllText(
        Path.Combine(te.configFolder, file), conf.Serialize(true));

      CheckMandatoryElementsInJsonConfig(file);
    }


    [TestMethod]
    public void Clone_JSON_Config_To_XML()
    {
      // Read config object from file
      var conf = Config.DeserializeFromFile(@"..\..\Test files\wgmulti.config.multi.siteinis.json");
      var conf2 = (Config)conf.Clone(Directory.GetCurrentDirectory());
      File.WriteAllText("Exported.WebGrab++.config.xml", conf2.Serialize());

      //Check(file);
    }


    [TestMethod]
    public void Convert_Big_JSON_Config_To_XML()
    {
      var conf = Config.DeserializeFromFile(@"..\..\Test files\wgmulti.config.big.json");
      File.WriteAllText("WebGrab.Big.xml", conf.Serialize());
    }


    [TestMethod]
    public void Convert_Big_XML_Config_To_JSON()
    {
      var conf = Config.DeserializeFromFile(@"webgrab.config.test.big.xml");
      File.WriteAllText("webgrab.config.test.big.json", conf.Serialize(true));
    }

    [TestMethod]
    public void Test_DisableMissingSiteinis()
    {

      var conf = Config.DeserializeFromFile(@"..\..\Test files\wgmulti.config.disabled.siteinis.json");
      conf.InitSiteinis();

      if (conf.channels[0].siteinis[0].Enabled)
        Assert.Fail("Channel 1 first siteini is enabled");

      if (!conf.channels[1].siteinis[0].Enabled)
        Assert.Fail("Channel 2 siteini should be enabled");

      if (conf.channels[2].siteinis[0].Enabled)
        Assert.Fail("Channel 3 first siteini is enabled");

      if (conf.channels[2].siteinis[1].Enabled)
        Assert.Fail("Channel 3 second siteini is enabled");

      if (conf.channels[3].siteinis[0].Enabled)
        Assert.Fail("Channel 4 siteini 1 is enabled");

      if (conf.channels[3].siteinis[1].Enabled)
        Assert.Fail("Channel 4 siteini 2 is enabled");
    }

    void VerifyJsonDeserializedData(Config conf, bool isCopyEnabled = false)
		{
			var cha1 = conf.channels[0];
			var cha2 = conf.channels[1];
			var cha3 = conf.channels[2];
			var cha4 = conf.channels[3];

			if (isCopyEnabled)
			{
				if (String.IsNullOrEmpty(cha1.siteinis[0].path))
					Assert.Fail("Channe 1 siteini have no 'path' element!");

				if (cha1.siteinis[0].type != GrabType.COPY)
					Assert.Fail("Channe 1 siteini type is not 'COPY'!");
			}

			if (cha4.active)
				Assert.Fail("Channel 4 is active but it should have been disactivated as it does not have a valid siteini");

			if (2 != conf.period.days)
				Assert.Fail("timespan does not have vlaue of 2");

			if (String.Empty != conf.period.time)
				Assert.Fail("timespan does not have an empty time value");

      if (1 != conf.period.pastdays)
        Assert.Fail("timespan's passdays attribute does not have vlaue of 1");

      if (!conf.postProcess.run)
				Assert.Fail("Postproces run is not enabled");

			if (conf.channels.Count < 3)
				Assert.Fail("Channels in the config file are less than 3, actual: {0}",
				 conf.channels.Count);

			if ("12,1" != conf.skip)
				Assert.Fail("Skip value is no 12,1");

			if ("Канал 1" != cha1.name)
				Assert.Fail("First channel name is not Канал 1");

			if ("Канал 1 +1" != cha1.timeshifts[0].name)
				Assert.Fail("First timeshifted channel name is not Канал 1 +1");

			if (cha1.xmltv_id != cha1.timeshifts[0].same_as)
				Assert.Fail("Timeshifted same_as value doesn't match parent channel value");

			if (!cha1.active)
				Assert.Fail("Channel 1 is not active!");

			if (!cha1.Enabled)
				Assert.Fail("Channel 1 is not enabeld");

			if (!cha2.Enabled)
				Assert.Fail("Channel 2 is not enabeld");

			if (1 != cha1.siteinis.Count)
				Assert.Fail("Channel 1 siteinis is not 1, it is {0}", cha1.siteinis.Count);

			if (2 != cha3.siteinis.Count)
				Assert.Fail("Channel 3 siteinis is not 2, it is {0}", cha3.siteinis.Count);

			if ("III" != cha3.include)
				Assert.Fail("Channel 3 include value is not expected. Actual: {0}", cha3.include);

			if ("automatic" != conf.proxy.server)
				Assert.Fail("proxy server value is not expected. Actual {0}", conf.proxy.server);

		}

		void CheckMandatoryElementsInJsonConfig(String file)
		{
			var content = File.ReadAllText(file);

			if (!content.Contains("\"name\": \"Канал 1\""))
				Assert.Fail("Name 'Канал 1' not found in json");

			if (!content.Contains("\"name\": \"Канал 1 +1\""))
				Assert.Fail("Name 'Канал 1 +1' not found in json");

			if (!content.Contains("\"xmltv_id\": \"Канал 1 +1 ID\""))
				Assert.Fail("xmltv_id 'Канал 1 +1 ID' not found in json");

			if (!content.Contains("\"logging\":"))
				Assert.Fail("logging property not found in json");

			if (!content.Contains("\"timeshifts\":"))
				Assert.Fail("timeshift property not found in json");

			if (!content.Contains("\"type\": \""))
				Assert.Fail("type property not found in json");

			if (!content.Contains("\"automatic"))
				Assert.Fail("Proxy setting automatic not found in json");

      if (!content.Contains("retry time-out=\"20\" channel-delay=\"1\" index-delay=\"1\" show-delay=\"1\">4</retry>"))
        Assert.Fail("Retry settings are wrong");
		}

		public Config CreateDefaultConfigObject()
    {
      Config conf = new Config(); // Default config values
      conf.retry.timeOut = 15;
      conf.retry.attempts = 5;
      conf.proxy = new Proxy();
      conf.proxy.user = "";
      conf.proxy.password = "";
      conf.proxy.server = "automatic";

      var siteini = new SiteIni("siteini", "channel_1");
      var channel = new Channel();
      channel.name = "Канал 1";
      channel.xmltv_id = "Канал 1 ID";
      channel.update = UpdateType.Incremental;
      channel.siteinis = new List<SiteIni>() { siteini };
      conf.channels.Add(channel);

      var timeshifted = new Channel();
      timeshifted.name = "Канал 1 +1";
      timeshifted.xmltv_id = "Канал 1 +1 ID";
      timeshifted.offset = 1;
      timeshifted.same_as = "Канал 1 ID";
      channel.timeshifts = new List<Channel>();
      channel.timeshifts.Add(timeshifted);

      channel = new Channel();
      siteini = new SiteIni("siteiniCET", "channel_2");
      channel.name = "Channel 2";
      channel.xmltv_id = "Channel 2 ID";
      channel.update = UpdateType.Incremental;
      channel.siteinis = new List<SiteIni>() { siteini };
      conf.channels.Add(channel);

      channel = new Channel();
      siteini = new SiteIni("siteini", "channel_3");
      channel.name = "Channel 3";
      channel.xmltv_id = "Channel 3 ID";
      channel.update = UpdateType.Incremental;
      channel.siteinis = new List<SiteIni>() { siteini };
      channel.include = "III";
      channel.exclude = "EEE";
      conf.channels.Add(channel);

      channel = new Channel();
      channel.name = "Channel 4";
      channel.xmltv_id = "Channel 4 ID";
      channel.update = UpdateType.Incremental;
      channel.Enabled = false;

      conf.channels.Add(channel);

      return conf;
    }
  }
}
