using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace xmltv_time_modify
{
  public class Program
  {
    public static void Main(string[] args)
    {

      Configuration config;
      try
      {
        config = new Configuration(args);
      }
      catch(Exception ex)
      {
        Console.WriteLine(ex.ToString());
        DisplayHelp();
        return;
      }

      if (config.displayHelp)
      {
        DisplayHelp();
        return;
      }
      Console.WriteLine("#####################################################");
      Console.WriteLine("#                                                   #");
      Console.WriteLine("#         xmltv_time_modify by Hristo Genev         #");
      Console.WriteLine("#                                                   #");
      Console.WriteLine("#####################################################");
      Console.WriteLine($"System: {Environment.OSVersion.Platform}");
      Console.WriteLine($"Working Directory: {Directory.GetCurrentDirectory()}");
      Console.WriteLine($"Starting at {DateTime.Now}");

      // Load EPG file
      XDocument xmlContent = XDocument.Load(config.inputXml);
      var programs = xmlContent.Elements("tv").Elements("programme");

      if (config.applyCorrectionToAll)
      {
        var groups = programs.GroupBy(p => p.Attribute("channel").Value);
        foreach (var groupOfPrograms in groups)
        {
          groupOfPrograms.ToList().ForEach(
              program => Utils.ModifyProgramTimings(ref program, config.correction, config.removeOffset));
          Console.WriteLine($"{groupOfPrograms.Count()} programs modified for channel '{groupOfPrograms.Key}'. Applied correction: '{config.correction}'");
        }
      }
      else
      {
        foreach (var channel in config.channelsToModify)
        {
          var _programs = programs.Where(p => p.Attribute("channel").Value == channel.Key).ToList(); 
          _programs.ForEach(
            program => Utils.ModifyProgramTimings(ref program, channel.Value, config.removeOffset)
            );
          Console.WriteLine($"{_programs.Count()} programs modified for channel '{channel.Key}'. Applied correction: '{channel.Value}'");
        }
      }

      // Save new EPG
      Console.WriteLine($"Saving corrected EPG to: {config.outputXml}");
      xmlContent.Save(config.outputXml);
      Console.WriteLine($"Done! Finished at {DateTime.Now}");

    }


    static void DisplayHelp()
    {
      Console.WriteLine("xmltv_time_modify - a tool for modifies XMLTV timings");
      Console.WriteLine("");
      Console.WriteLine("Arguments:");
      Console.WriteLine("");
      Console.WriteLine("  /h              Displays help");
      Console.WriteLine("  /in             Provide the path to the input XMLTV guide. Default is epg.xml");
      Console.WriteLine("  /out            Provide the path to the output XMLTV guide where all changes will be saved. \n" +
                        "                  Default is epg_corrected.xml");
      Console.WriteLine("  /correction     Specify the time correction to be applied in hours. \n" +
                        "                  Possible values are: local, utc, +1, -1, +1,5 (or +1.5) etc.\n" +
                        "                  Default is local, which automatically converts the timings to user's current timezone");
      Console.WriteLine("  /channels       Provide a list of channels to be corrected or a path to an XML file\n" +
                        "                  containing the list of channels. For example: \n" +
                        "                  /channels:all - applies the correction to all channels.\n" +
                        "                  /cahnnels:channels.xml - reads the channels and their correction\n" +
                        "                  values from a file 'channels.xml'.\n " +
                        "                  /channels:\"Channel 1,Channel 2, Channel 3\" - applies the correction\n" +
                        "                  to only the specified channels. Default is 'all'");
      Console.WriteLine("  /ro             Removes the offset. For instance \"20200924061000 +0000\" becomes \"20200924061000\".\n" +
                        "                  Default is false. If the input datetime string has no offset, it will be appended");
      Console.WriteLine("");
      Console.WriteLine("Example usage:");
      Console.WriteLine("Running without provided arguments is equivalent to");
      Console.WriteLine("xmltv_time_modify.exe /in:epg.xml /out:epg_corrected.xml /channels:all /correction:local");
      Console.WriteLine("Use none-default input and output XML files");
      Console.WriteLine("xmltv_time_modify /in:guide.xml /out:guide_fixed.xml");
      Console.WriteLine("Apply global correction of +1 hours applied to all channels.");
      Console.WriteLine("xmltv_time_modify /correction:+1");
      Console.WriteLine("Apply global correction to utc to all channels.");
      Console.WriteLine("xmltv_time_modify /correction:utc");
      Console.WriteLine("Read the channels and their correction values from a file.");
      Console.WriteLine("xmltv_time_modify /channels:channels.xml");
      Console.WriteLine("Provide a list of channels to apply correction of -2 hours.");
      Console.WriteLine("xmltv_time_modify /channels:\"Channel 1, Channel 2, Channe 3\" /correction:-2");

    }
  }

}
