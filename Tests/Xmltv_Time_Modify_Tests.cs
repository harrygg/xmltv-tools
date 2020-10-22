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
    readonly static string inputArgument = "/in:Guide.xml";
    readonly static string outputArgument = "/out:Guide_corrected.xml";
    readonly static string inputXml = "Guide.xml";
    readonly static string outputXml = "Guide_corrected.xml";
    readonly static string defaultInputXml = "epg.xml";
    readonly static string defaultOutputXml = "epg_corrected.xml";
    readonly static string channelsLocalCorrectionFile = "channelsLocalCorrection.xml";
    readonly static string channelsVaiousCorrectionFile = "channelsVaiousCorrection.xml";
    readonly static string iniFile = "channelsLocalCorrection.ini";
    readonly static string channel1Id = "Channel1Id";
    readonly static string channel2Id = "Channel2Id";
    readonly static string channel3Id = "Channel3Id";
    readonly static string channel4Id = "Channel 4 Id";
    static bool isDaylight = false;
    List<String> filesToCleanUp;

    // XML Content
    readonly static string xmlContentChannelsLocalCorrection = $"<channels>\n\t<channel id=\"{channel1Id}\" correction=\"local\" />" +
        $"\n\t<channel id=\"{channel2Id}\" correction=\"local\" />\n</channels>";
    readonly static string xmlContentChannelsVaiousCorrection = $"<channels>\n\t<channel id=\"{channel1Id}\" correction=\"+1\" />" +
        $"\n\t<channel id=\"{channel2Id}\" correction=\"02:00\" />" +
        $"\n\t<channel id=\"{channel3Id}\" correction=\"-1,5\" />\n</channels>";
    readonly static string iniContent = $"{channel1Id}=+1\n{channel2Id}=+02:00\n{channel3Id}=-1,5\n#{channel4Id}=-1,5";

    [TestInitialize]
    public void SetUp() 
    {
      filesToCleanUp = new List<String>();
      isDaylight = TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now);

      File.WriteAllText(inputXml, "");
      filesToCleanUp.Add(inputXml);
      File.WriteAllText(channelsLocalCorrectionFile, xmlContentChannelsLocalCorrection);
      filesToCleanUp.Add(channelsLocalCorrectionFile);
      File.WriteAllText(channelsVaiousCorrectionFile, xmlContentChannelsVaiousCorrection);
      filesToCleanUp.Add(channelsVaiousCorrectionFile);
      File.WriteAllText(iniFile, iniContent);
      filesToCleanUp.Add(iniFile);

    }

    [TestCleanup]
    public void TearDown()
    {
      GC.Collect();
      GC.WaitForPendingFinalizers();

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
      Assert.IsFalse(config.removeOffset);
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
    public void ArgumentsProvideRemoveOffset()
    {
      string[] args = new string[] { "/ro" };
      var config = new Configuration(args);

      Assert.IsTrue(config.removeOffset);
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
    public void ArgumentsProvideChannelsFromXmlFile()
    {
      string[] args = new string[] { "/channels:channelsLocalCorrection.xml" };
      var config = new Configuration(args);
      Assert.IsTrue(config.channelsToModify.Count > 1);
      Assert.IsFalse(config.applyCorrectionToAll);
    }

    [TestMethod]
    public void ArgumentsProvideChannelsFromIniFile()
    {

      string[] args = new string[] { $"/channels:{iniFile}" };
      var config = new Configuration(args);
      Assert.IsTrue(config.channelsToModify.Count == 3);
      Assert.IsFalse(config.applyCorrectionToAll);
    }

    [TestMethod]
    public void ArgumentsProvideChannelsListFromArguments()
    {
      string[] args = new string[] { $"/channels:\"{channel1Id}, {channel2Id}, {channel4Id}\"" };
      var config = new Configuration(args);
      Assert.IsTrue(config.channelsToModify.Count > 1);
      int count = config.channelsToModify.Where(c => c.Key == channel2Id || c.Key == channel4Id).Count();
      Assert.IsTrue(count == 2);
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

      args = new string[] { $"/correction:+01:45" };
      config = new Configuration(args);
      Assert.AreEqual(config.correction, "+01:45");

      args = new string[] { $"/correction:-2:15" };
      config = new Configuration(args);
      Assert.AreEqual(config.correction, "-2:15");
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
      //Input datetime is 20200924051000

      RunExe($"/correction:+1");
      //Program.Main(new String[] { $"/correction:+1" });
      var actualStartTime = GetFirstShowStartTimeForChannel(channel1Id, defaultOutputXml);
      Assert.AreEqual("20200924061000 +0100", actualStartTime);
      
      actualStartTime = GetFirstShowStartTimeForChannel(channel3Id, defaultOutputXml);
      Assert.AreEqual("20200924061000 +0000", actualStartTime);

      RunExe($"/correction:+1:15");
      //Program.Main(new String[] { $"/correction:+1:15" });
      actualStartTime = GetFirstShowStartTimeForChannel(channel1Id, defaultOutputXml);
      Assert.AreEqual("20200924062500 +0100", actualStartTime);

      RunExe($"/correction:-01:45");
      //Program.Main(new String[] { $"/correction:-1:45" });
      actualStartTime = GetFirstShowStartTimeForChannel(channel1Id, defaultOutputXml);
      Assert.AreEqual("20200924032500 +0100", actualStartTime);

      RunExe($"/correction:-01:10");
      //Program.Main(new String[] { $"/correction:-01:10" });
      actualStartTime = GetFirstShowStartTimeForChannel(channel1Id, defaultOutputXml);
      Assert.AreEqual("20200924040000 +0100", actualStartTime);
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
    public void E2E_RunExe_ApplyCorrectionFromIniFile()
    {
      var out_E2E_RunExe_ApplyCorrectionFromIniFile = "E2E_RunExe_ApplyCorrectionFromIniFile.xml";
      filesToCleanUp.Add(out_E2E_RunExe_ApplyCorrectionFromIniFile);

      RunExe($"/channels:{iniFile} /out:{out_E2E_RunExe_ApplyCorrectionFromIniFile}");

      var actualStartTime = GetFirstShowStartTimeForChannel(channel1Id, out_E2E_RunExe_ApplyCorrectionFromIniFile);
      Assert.AreEqual("20200924061000 +0100", actualStartTime);

      actualStartTime = GetFirstShowStartTimeForChannel(channel2Id, out_E2E_RunExe_ApplyCorrectionFromIniFile);
      Assert.AreEqual("20200924071000 +0000", actualStartTime);

      actualStartTime = GetFirstShowStartTimeForChannel(channel3Id, out_E2E_RunExe_ApplyCorrectionFromIniFile);
      Assert.AreEqual("20200924034000 +0000", actualStartTime);
    }

    [TestMethod]
    public void E2E_RunExe_ApplyCorrectionFromXmlFile()
    {
      var E2E_RunExe_ApplyCorrectionFromXmlFile = "E2E_RunExe_ApplyCorrectionFromXmlFile.xml";
      filesToCleanUp.Add(E2E_RunExe_ApplyCorrectionFromXmlFile);

      RunExe($"/channels:{channelsVaiousCorrectionFile} /out:{E2E_RunExe_ApplyCorrectionFromXmlFile}");
      //Program.Main(new String[] { $"/channels:{channelsVaiousCorrectionFile}", $"/out:{ E2E_RunExe_ApplyCorrectionFromXmlFile }" });

      var actualStartTime = GetFirstShowStartTimeForChannel(channel1Id, E2E_RunExe_ApplyCorrectionFromXmlFile);
      Assert.AreEqual("20200924061000 +0100", actualStartTime);

      actualStartTime = GetFirstShowStartTimeForChannel(channel2Id, E2E_RunExe_ApplyCorrectionFromXmlFile);
      Assert.AreEqual("20200924071000 +0000", actualStartTime);

      actualStartTime = GetFirstShowStartTimeForChannel(channel3Id, E2E_RunExe_ApplyCorrectionFromXmlFile);
      Assert.AreEqual("20200924034000 +0000", actualStartTime);
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
    public void E2E_RunExe_OffsetRemove()
    {
      var outFile = "E2E_RunExe_OffsetRemove.xml";
      filesToCleanUp.Add(outFile);

      RunExe($"/out:{outFile} /ro");
      //Program.Main(new String[] { $"/out:{outFile}", "/ro" });

      var actualStartTime = GetFirstShowStartTimeForChannel(channel1Id, outFile);
      Assert.IsFalse(actualStartTime.Contains(" "));
    }


    [TestMethod]
    public void E2E_RunExe_OffsetAdd()
    {
      var inFile = "E2E_RunExe_OffsetAdd_In";
      var outFile = "E2E_RunExe_OffsetAdd_Out.xml";
      File.WriteAllText(inFile, "<?xml version=\"1.0\" encoding=\"utf-8\"?><tv><channel id=\"Channel1Id\">" +
        "<display-name lang=\"bg\">Channel 1</display-name></channel><programme channel=\"Channel1Id\" " + 
        "start=\"20200924051000\" stop=\"20200924060000\"><title lang=\"bg\">Боен клуб</title>" +
        "</programme></tv>");

      filesToCleanUp.Add(inFile);
      filesToCleanUp.Add(outFile);

      RunExe($"/in:{inFile} /out:{outFile}");
      //Program.Main(new String[] { $"/out:{inFile}", $"/out:{outFile}" });

      var actualStartTime = GetFirstShowStartTimeForChannel(channel1Id, outFile);
      Assert.IsTrue(actualStartTime.Contains(" "));

    }

    [TestMethod]
    public void E2E_RunExe_DisplayHelp()
    {
      RunExe($"-h");
      //Program.Main(new String[] { $"-h" });
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
      Assert.IsTrue(Utils.HasOffset("+0300"));
      Assert.IsTrue(Utils.HasOffset("-0300"));
      Assert.IsTrue(Utils.HasOffset("05:00"));
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
