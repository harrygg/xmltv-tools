using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace NunitTests
{
  class Verify
  {
    internal static void AllElementsExistAfterNoPostProcess(TestEnvironment te)
    {
      // Verify that the output EPG file exists
      if (!File.Exists(te.outputEpg))
        Assert.Fail("Output file does not exist: " + te.outputEpg);

      // Verify 3 channel tags exsits, more than 0 programmes tags and 0 desc tags
      CheckMandatoryElementsExist(te.outputEpg);

      // Verify that the first program of 'Channel1' starts 1 hour before the program of 'Channel1 +1'
      Timeshifted_Channel_Has_Different_StartTime(te.outputEpg);

      // Verify that the Channel2 programs times are converted to EEST time
      CetTimeIsConvertedToLocal(te.outputEpg);
    }


    internal static void AllElementsExistAfterRexPostProcess(TestEnvironment te)
    {
      // Verify that the input config file exists
      if (!File.Exists(te.WgmultiConfig))
        Assert.Fail("File does not exist: " + te.WgmultiConfig);

      // Verify that the output EPG file exists
      if (!File.Exists(te.outputEpg))
        Assert.Fail("File does not exist: " + te.outputEpg);

      // Verify 3 channel tags exsits, more than 0 programmes tags and 0 desc tags
      CheckMandatoryElementsExist(te.outputEpg);

      // Verify that the output EPG file exists
      if (!File.Exists(te.outputEpgAfterPostProcess))
        Assert.Fail("File does not exist: " + te.outputEpgAfterPostProcess);

      CheckMandatoryElementsExist(te.outputEpgAfterPostProcess, true);

      // Verify that the first program of 'Channel1' starts 1 hour before the program of 'Channel1 +1'
      Timeshifted_Channel_Has_Different_StartTime(te.outputEpg);

      // Verify that the Channel2 programs times are converted to EEST time
      CetTimeIsConvertedToLocal(te.outputEpg);
    }


    internal static void AllElementsExistAfterPostProcess(TestEnvironment te)
    {
      // Verify that the output EPG file exists
      if (!File.Exists(te.outputEpg))
        Assert.Fail("File does not exist: " + te.outputEpg);

      // Verify 3 channel tags exsits, more than 0 programmes tags and 0 desc tags
      CheckMandatoryElementsExist(te.outputEpg);

      // Verify that the output EPG file exists
      if (!File.Exists(te.outputEpgAfterPostProcess))
        Assert.Fail("File does not exist: " + te.outputEpgAfterPostProcess);

      CheckMandatoryElementsExist(te.outputEpgAfterPostProcess, true);

      // Verify that the first program of 'Channel1' starts 1 hour before the program of 'Channel1 +1'
      Timeshifted_Channel_Has_Different_StartTime(te.outputEpg);

      // Verify that the Channel2 programs times are converted to EEST time
      CetTimeIsConvertedToLocal(te.outputEpg);
    }

    internal static void Timeshifted_Channel_Has_Different_StartTime(String filename)
    {
      try
      {
        var root = XDocument.Load(filename);
        var tv = root.Element("tv");

        // Select the start time of first program for 'Channel1' and first program of 'Channel1 +1'
        // The difference must be 1
        var p1 = GetProgramStartHour(tv, "Канал 1 ID");
        var p2 = GetProgramStartHour(tv, "Канал 1 +1 ID");

        if (p1 + 1 != p2)
          Assert.Fail("Parent and offset program start times are not correct");
      }
      catch (Exception ex)
      {
        Assert.Fail("Parent and offset program start times are not correct");
        Assert.Fail(ex.Message);
      }
    }

    internal static void CetTimeIsConvertedToLocal(String filename)
    {
      try
      {
        var root = XDocument.Load(filename);
        var tv = root.Element("tv");

        var p1 = GetProgramTimeZone(tv, "Channel2ID");

        root = XDocument.Load(Path.Combine(Path.GetDirectoryName(filename), "siteiniCET", "epg.xml"));
        tv = root.Element("tv");
        var p2 = GetProgramTimeZone(tv, "Channel2ID");

        if (!(p1 == "0200" && p2 == "0100"))
          Assert.Fail("Error during verification of local time conversion");
      }
      catch (Exception ex)
      {
        Assert.Fail(ex.Message);
      }
    }

    internal static String ChannelHasStartHour(String filename, String channelId, String startHour)
    {
      try
      {
        var root = XDocument.Load(filename);
        var el = root.Element("tv");

        return el.Elements("programme")
          .Where(p => p.Attribute("channel").Value
          .Equals(channelId)).First().
          Attribute("start").Value.Substring(8, 2);
      }
      catch { return ""; }
    }

    internal static int GetProgramStartHour(XElement el, String id)
    {
      try
      {
        return Convert.ToInt16(el.Elements("programme")
        .Where(p => p.Attribute("channel").Value
        .Equals(id))
        .First().Attribute("start").Value.Substring(8, 2));
      }
      catch { return -2; }
    }

    internal static String GetProgramTimeZone(XElement el, String id)
    {
      try
      {
        return el.Elements("programme")
        .Where(p => p.Attribute("channel").Value.Equals(id))
        .First().Attribute("start").Value.Substring(16, 4);
      }
      catch { return ""; }
    }



    internal static bool ProgramForChannelDoesnotExist(XElement el, String id)
    {
      try { return el.Elements("programme").First(p => p.Attribute("channel").Value.Equals(id)) == null; }
      catch { return true; }
    }

    static void CheckMandatoryElementsExist(String filename, bool includePostProcessed = false)
    {
      var root = XDocument.Load(filename);
      var tv = root.Element("tv");

      try
      {
        if (ProgramForChannelDoesnotExist(tv, "Канал 1 ID"))
          Assert.Fail("Missing program with channel id 'Канал 1 ID'");

        if (ProgramForChannelDoesnotExist(tv, "Канал 1 +1 ID"))
          Assert.Fail("Missing program with channel id 'Канал 1 +1 ID'");

        if (ProgramForChannelDoesnotExist(tv, "Channel2ID"))
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
            Assert.Fail("Postprocess is not enabled but 'desc' element exists in the epg data!");
        }
      }
      catch (Exception ex)
      {
        Assert.Fail(ex.Message);
      }
    }
  }
}
