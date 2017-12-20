using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Tests
{
  public class ResultChecks
  {
    public static void CheckElementsAfterNoPostProcess(TestEnvironment te)
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
      TimesConvertedToLocal(te.outputEpg);
      Debug.WriteLine("Times are converted to local");
    }


    public static void ChecksAfterPostProcess(TestEnvironment te)
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
      TimesConvertedToLocal(te.outputEpg);
    }

    public static void CheckMandatoryElementsExist(String filename, bool includePostProcessed = false)
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

    public static void OffsetProgramStartTimeIsDifferent(String filename)
    {
      try
      {
        var root = XDocument.Load(filename);
        var tv = root.Element("tv");

        Func<String, int> GetStartHour = delegate (String id)
        {
          try
          {
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


    public static void TimesConvertedToLocal(String filename)
    {
      try
      {
        var root = XDocument.Load(filename);
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

        root = XDocument.Load(Path.Combine(Path.GetDirectoryName(filename), "siteiniCET", "epg.xml"));
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
