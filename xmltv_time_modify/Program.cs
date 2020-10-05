using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace xmltv_time_modify
{
  public class Program
  {
    static Version version = Assembly.GetExecutingAssembly().GetName().Version;
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
      Console.WriteLine($"Version: {version}");
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

      Console.WriteLine($"xmltv_time_modify v. {version} - a tool for modifies XMLTV timings by Hristo Genev");
      Console.WriteLine("");
      Console.WriteLine("Arguments:");
      Console.WriteLine("");
      Console.WriteLine("  /h              Displays the help.");
      Console.WriteLine("  /in             Provide the path to the input XMLTV guide. Default is epg.xml");
      Console.WriteLine("  /out            Provide the path to the output XMLTV guide where all changes will be saved. \n" +
                        "                  Default is epg_corrected.xml");
      Console.WriteLine("  /correction     Specify the correction to be applied. Accepts value in hours or offset. \n" +
                        "                  Example values: local, utc, +1, -1, +1,5 (or +1.5), +02:00, -05:00 etc.\n" +
                        "                  Default is local, which automatically converts the timings to user's current timezone.");
      Console.WriteLine("  /channels       Provide a list of channels to be corrected or a path to an XML file containing the list of channels.\n" +
                        "                  For example: \n" +
                        "                  /channels:all - applies the correction to all channels.\n" +
                        "                  /channels:channels.ini - reads the channels and time corrections from a file 'channels.ini'.\n" +
                        "                  /channels:channels.xml - reads the channels and time corrections from a file 'channels.xml'.\n" +
                        "                  /channels:\"Channel1ID, Channel2ID, Channel3ID\" - applies the correction to the given channel ids. \n" +
                        "                  Default is 'all'.");
      Console.WriteLine("  /ro             Removes the offset. For instance \"20200924061000 +0000\" becomes \"20200924061000\".\n" +
                        "                  Default is false. If the input datetime string has no offset, it will be appended.");
      Console.WriteLine("");
      Console.WriteLine("Example usage (for questions feel free to contact harrygg@gmail.com):");
      Console.WriteLine("Running without provided arguments is equivalent to:");
      Console.WriteLine("xmltv_time_modify.exe /in:epg.xml /out:epg_corrected.xml /channels:all /correction:local");
      Console.WriteLine("");
      Console.WriteLine("Use none-default input and output XML files:");
      Console.WriteLine("xmltv_time_modify /in:guide.xml /out:guide_fixed.xml");
      Console.WriteLine("");
      Console.WriteLine("Apply global correction of +1 hours to all channels:");
      Console.WriteLine("xmltv_time_modify /correction:+1");
      Console.WriteLine("");
      Console.WriteLine("Apply global correction to UTC to all channels:");
      Console.WriteLine("xmltv_time_modify /correction:utc");
      Console.WriteLine("");
      Console.WriteLine("Modify all channels timings to timezone -05:00:");
      Console.WriteLine("xmltv_time_modify /correction:-05:00");
      Console.WriteLine("");
      Console.WriteLine("Read the channels and their correction values from a INI file:");
      Console.WriteLine("xmltv_time_modify /channels:channels.ini");
      Console.WriteLine("The file channels.ini should contain the channel ids and their correction values separated with \"=\".");
      Console.WriteLine("Example content:");
      Console.WriteLine("Channel1Id=+1");
      Console.WriteLine("Channel2Id=+02:00");
      Console.WriteLine("Channel3Id=-1,5");
      Console.WriteLine("");
      Console.WriteLine("Read the channels and their correction values from a XML file:");
      Console.WriteLine("xmltv_time_modify /channels:channels.xml");
      Console.WriteLine("The file channels.xml should have the following syntax:");
      Console.WriteLine("Example content:");
      Console.WriteLine("<channels>");
      Console.WriteLine("    <channel id=\"Channel1Id\" correction=\"+1\" />");
      Console.WriteLine("    <channel id=\"Channel2Id\" correction=\"+02:00\" />");
      Console.WriteLine("    <channel id=\"Channel3Id\" correction=\"-1,5\" />");
      Console.WriteLine("</channels>");
      Console.WriteLine("");
      Console.WriteLine("Provide a list of channels to apply correction of -2 hours:");
      Console.WriteLine("xmltv_time_modify /channels:\"Channel1Id, Channel2Id, Channel3Id\" /correction:-2");
      Console.WriteLine("");
      Console.WriteLine("Provide a list of channels to change their timings to +01:00 timezone");
      Console.WriteLine("xmltv_time_modify /channels:\"Channel1Id, Channel2Id, Channel3Id\" /correction:+01:00");
      Console.WriteLine("");

    }
  }

}
