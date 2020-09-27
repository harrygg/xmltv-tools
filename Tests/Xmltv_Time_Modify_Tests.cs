using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using xmltv_time_modify;
using System.Diagnostics;
using System.Collections.Generic;

namespace Tests
{
  [TestClass]
  public class Xmltv_Time_Modify_Tests
  {
    readonly string inputArgument = "/in:Guide.xml";
    readonly string outputArgument = "/out:Guide_corrected.xml";
    readonly string inputXml = "Guide.xml";
    readonly string outputXml = "Guide_corrected.xml";
    readonly string defaultInputXml = "epg.xml";
    readonly string defaultOutputXml = "epg_corrected.xml";
    readonly string channelsLocalCorrection = "channelsLocalCorrection.xml";
    readonly string channelsVaiousCorrection = "channelsVaiousCorrection.xml";
    readonly string channel1Id = "Channel1Id";
    readonly string channel2Id = "Channel2Id";
    readonly string channel3Id = "Channel3Id";
    bool isDaylight = false;
    List<String> filesToCleanUp;

    [TestInitialize]
    public void SetUp() 
    {
      filesToCleanUp = new List<String>();
      isDaylight = TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now);

      File.WriteAllText(inputXml, "");
      File.WriteAllText(channelsLocalCorrection, "<channels>\n\t<channel id=\"ChannelId1\" correction=\"local\" />" +
        "\n\t<channel id=\"ChannelId2\" correction=\"local\" />\n</channels>");
      File.WriteAllText(channelsVaiousCorrection, "<channels>\n\t<channel id=\"ChannelId1\" correction=\"+1\" />" +
        "\n\t<channel id=\"ChannelId2\" correction=\"+2\" />\n</channels>");
    }

    [TestCleanup]
    public void TearDown()
    {
      foreach (var file in filesToCleanUp)
        if (File.Exists(file))
          File.Delete(file);
    }

    [TestMethod]
    public void ArgumentsProvideNone_ResultsInDefaultValues()
    {
      string[] args = new string[] { };
      var config = new Configuration(args);

      Assert.AreEqual(config.inputXml, defaultInputXml);
      Assert.AreEqual(config.outputXml, defaultOutputXml);
      Assert.AreEqual(config.correction, "local");
      Assert.IsTrue(config.applyCorrectionToAll);
      Assert.IsTrue(config.convertToLocal);
    }

    [TestMethod]
    public void ArgumentsProvideInputAndOutputXml()
    {
      string[] args = new string[] { inputArgument, outputArgument };
      var config = new Configuration(args);

      Assert.AreEqual(config.inputXml, inputXml);
      Assert.AreEqual(config.outputXml, outputXml);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException),
    "An input XML was specified but does not exist.")]
    public void ArgumentsProvideMissingInputXml_ResultsInException()
    {
      string[] args = new string[] { "/in:Guide123456789.xml" };
      var config = new Configuration(args);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException), "A channels XML was specified but does not exist.")]
    public void ArgumentsProvideChannelsFromMissingFile_ResultsInException()
    {
      string[] args = new string[] { "/channels:channels123456789.xml" };
      var config = new Configuration(args);
    }

    [TestMethod]
    public void ArgumentsProvideChannelsFromFile()
    {
      string[] args = new string[] { "/channels:channelsLocalCorrection.xml" };
      var config = new Configuration(args);
      Assert.IsTrue(config.channelsToModify.Count > 1);
      Assert.IsFalse(config.applyCorrectionToAll);
    }


    [TestMethod]
    public void ArgumentsProvideChannelsListFromArguments()
    {
      string[] args = new string[] { $"/channels:\"{channel1Id},{channel2Id}\"" };
      var config = new Configuration(args);
      Assert.IsTrue(config.channelsToModify.Count > 1);
      Assert.IsFalse(config.applyCorrectionToAll);
    }

    [TestMethod]
    public void ArgumentsProvideChannelsListFromArgumentsAll()
    {
      string[] args = new string[] { $"/channels:all" };
      var config = new Configuration(args);
      Assert.IsTrue(config.applyCorrectionToAll);
    }

    [TestMethod]
    public void ArgumentsProvideCorrectionValueNone_ResultsInDefaultCorrectionValue()
    {
      string[] args = new string[] { };
      var config = new Configuration(args);
      Assert.AreEqual(config.correction, "local");
      Assert.IsTrue(config.convertToLocal);
    }

    [TestMethod]
    public void ArgumentsProvideCorrectionValueLocal_ResultsInDefaultCorrectionValue()
    {
      string[] args = new string[] { "/correction:local" };
      var config = new Configuration(args);
      Assert.AreEqual(config.correction, "local");
      Assert.IsTrue(config.convertToLocal);
    }

    [TestMethod]
    public void ArgumentsProvideCorrectionValueUtc()
    {
      string[] args = new string[] { "/correction:utc" };
      var config = new Configuration(args);
      Assert.AreEqual(config.correction, "utc");
      Assert.IsFalse(config.convertToLocal);
    }

    [TestMethod]
    public void ArgumentsProvideCorrectionValueFromArgument_ResultsInCorrectionAppliedToAll()
    {
      string[] args = new string[] { $"/correction:+1" };
      var config = new Configuration(args);
      Assert.AreEqual(config.correction, "+1");
      Assert.IsFalse(config.convertToLocal);
      Assert.IsTrue(config.applyCorrectionToAll);
    }

    [TestMethod]
    public void ArgumentsProvideCorrectionValueFromArgumentForFirstChannel()
    {
      string[] args = new string[] { $"/correction:+1", $"/channels:{channel1Id}" };
      var config = new Configuration(args);
      Assert.AreEqual(config.correction, "+1");
      Assert.IsFalse(config.convertToLocal);
      Assert.IsFalse(config.applyCorrectionToAll);
      Assert.AreEqual(config.channelsToModify.Count, 1);
      Assert.AreEqual(config.channelsToModify.First().Key, channel1Id);
      Assert.AreEqual(config.channelsToModify.First().Value, config.correction);
    }

    [TestMethod]
    public void Unit_ModifyProgramsTimingsToLocal()
    {
      XElement program = new XElement("programme",
        new XAttribute("channel", "Channel1Id"),
        new XAttribute("start", "20200924233000 +0100"), 
        new XAttribute("stop", "20200925013000 +0100"));

      Utils.ModifyProgramTimings(ref program, "local");

      var expectedValue = isDaylight ? "20200925013000 +0300" : "20200925003000 +0200";
      Assert.AreEqual(expectedValue, program.Attribute("start").Value);
      expectedValue = isDaylight ? "20200925033000 +0300" : "20200925023000 +0200";
      Assert.AreEqual(expectedValue, program.Attribute("stop").Value);
    }

    [TestMethod]
    public void Unit_ModifyProgramsTimingsToLocalRemoveOffset()
    {
      XElement program = new XElement("programme",
        new XAttribute("channel", "Channel1Id"),
        new XAttribute("start", "20200924233000 +0100"),
        new XAttribute("stop", "20200925013000 +0100"));

      Utils.ModifyProgramTimings(ref program, "local", true);

      var expectedValue = isDaylight ? "20200925013000" : "20200925003000";
      Assert.AreEqual(expectedValue, program.Attribute("start").Value);
      expectedValue = isDaylight ? "20200925033000" : "20200925023000";
      Assert.AreEqual(expectedValue, program.Attribute("stop").Value);
    }


    [TestMethod]
    public void E2E_RunExe_ApplyCorrectionDefaultToSomeChannels()
    {
      RunExe( $"/channels:{channel3Id}" );

      var expectedStartTime = GetFirstShowStartTimeForChannel(channel1Id, defaultInputXml);
      var actualStartTime = GetFirstShowStartTimeForChannel(channel1Id, defaultOutputXml);

      Assert.AreEqual(expectedStartTime, actualStartTime);

      expectedStartTime = isDaylight ? "20200924081000 +0300" : "20200924071000 +0200";
      actualStartTime = GetFirstShowStartTimeForChannel(channel3Id, defaultOutputXml);

      Assert.AreEqual(expectedStartTime, actualStartTime);
    }

    [TestMethod]
    public void E2E_RunExe_ApplyCorrectionFromArgumentToAllChannels()
    {
      RunExe($"/correction:+1");
      //Program.Main(new String[] { $"/correction:+1" });

      var actualStartTime = GetFirstShowStartTimeForChannel(channel1Id, defaultOutputXml);
      Assert.AreEqual("20200924061000 +0100", actualStartTime);
      
      actualStartTime = GetFirstShowStartTimeForChannel(channel3Id, defaultOutputXml);
      Assert.AreEqual("20200924061000 +0000", actualStartTime);
    }

    [TestMethod]
    public void E2E_RunExe_ApplyCorrectionFromArgumentToSomeChannels()
    {
      RunExe($"/channels:{channel3Id} /correction:+1");
      //Program.Main(new String[] { $"/channels:{channel3Id}", "/correction:+1" });

      var actualStartTime = GetFirstShowStartTimeForChannel(channel1Id, defaultOutputXml);
      Assert.AreEqual("20200924051000 +0100", actualStartTime);

      actualStartTime = GetFirstShowStartTimeForChannel(channel3Id, defaultOutputXml);
      Assert.AreEqual("20200924061000 +0000", actualStartTime);
    }

    [TestMethod]
    public void E2E_ApplyLocalCorrectionToSomeChannels()
    {
      Program.Main(new String[] { $"/channels:{channel3Id}" });

      var expectedStartTime = GetFirstShowStartTimeForChannel(channel1Id, defaultInputXml);
      var actualStartTime = GetFirstShowStartTimeForChannel(channel1Id, defaultOutputXml);

      Assert.AreEqual(expectedStartTime, actualStartTime);

      expectedStartTime = isDaylight ? "20200924081000 +0300" : "20200924071000 +0200";
      actualStartTime = GetFirstShowStartTimeForChannel(channel3Id, defaultOutputXml);

      Assert.AreEqual(expectedStartTime, actualStartTime);

    }

    [TestMethod]
    public void E2E_RunExe_SavesModifiedEPGAsDifferentFile()
    {
      var outFile = "E2E_RunExe_SavesModifiedEPGAsDifferentFile.xml";
      filesToCleanUp.Add(outFile);

      RunExe($"/out:{outFile}");
      Assert.IsTrue(File.Exists(outFile));

    }

    [TestMethod]
    public void E2E_RunExe_DisplayHelp()
    {
      RunExe($"/?");
      //Program.Main(new String[] { $"/h" });
      Assert.IsTrue(true);

    }

    [TestMethod]
    public void Unit_ApplyOffsetLocal()
    {
      String correction = "local";
      String initialDateTime = "20170109090000 -0100";
      String expectedDateTime = "20170109120000 +0200";

      String actual = Utils.ApplyCorrection(initialDateTime, correction);

      Assert.AreEqual(expectedDateTime, actual);
    }

    [TestMethod]
    public void Unit_ApplyOffsetUtc()
    {
      String correction = "utc";
      String expectedDateTime = "20170109100000 +0000";

      String actual = Utils.ApplyCorrection("20170109090000 -0100", correction);

      Assert.AreEqual(expectedDateTime, actual);
    }

    [TestMethod]
    public void Unit_ApplyOffsetVarious()
    {
      String actual = Utils.ApplyCorrection("20170109120000 +0100", "+1");
      Assert.AreEqual("20170109130000 +0100", actual);

      actual = Utils.ApplyCorrection("20170109120000 +0100", "-1");
      Assert.AreEqual("20170109110000 +0100", actual);

      actual = Utils.ApplyCorrection("20170109120000 +0100", "-1,5");
      Assert.AreEqual("20170109103000 +0100", actual);
    }


    [TestMethod]
    public void Unit_StripOffset()
    {
      Assert.AreEqual("20170109130000", Utils.StripOffset("20170109130000 +0100"));
      Assert.AreEqual("20170109103000", Utils.StripOffset("20170109103000 -0200"));
    }

    [TestMethod]
    public void Unit_HasOffset()
    {
      Assert.IsTrue(Utils.HasOffset("20170109130000 +0100"));
      Assert.IsFalse(Utils.HasOffset("20170109103000"));
    }

    void RunExe(String argumentsAsString)
    {
      Process process = new Process();
      process.StartInfo.UseShellExecute = true;
      process.StartInfo.RedirectStandardOutput = false;
      process.StartInfo.FileName = "cmd.exe";
      process.StartInfo.CreateNoWindow = false;
      process.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
      process.StartInfo.Arguments = $"/K xmltv_time_modify.exe {argumentsAsString}";
      process.Start();
      process.WaitForExit();
    }

    String GetFirstShowStartTimeForChannel(String channelId, String file)
    {
      XDocument doc = XDocument.Load(file);
      var program = doc.Elements("tv").Elements("programme").First(p => p.Attribute("channel").Value == channelId);
      return program.Attribute("start").Value;
    }

  }
}
