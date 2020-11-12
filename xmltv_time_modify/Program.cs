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

      var modifiedPrograms = 0;
      if (config.applyCorrectionToAll)
      {
        var groups = programs.GroupBy(p => p.Attribute("channel").Value);
        
        foreach (var groupOfPrograms in groups)
        {
          groupOfPrograms.ToList().ForEach(
              program => Utils.ModifyProgramTimings(ref program, ref modifiedPrograms, config.correction, config.removeOffset));
          Console.WriteLine($"{modifiedPrograms} programs modified for channel '{groupOfPrograms.Key}'. Applied correction: '{config.correction}'");
          modifiedPrograms = 0;
        }
      }
      else
      {
        foreach (var channel in config.channelsToModify)
        {
          var _programs = programs.Where(p => p.Attribute("channel").Value == channel.Key).ToList(); 
          _programs.ForEach(
            program => Utils.ModifyProgramTimings(ref program, ref modifiedPrograms, channel.Value, config.removeOffset)
            );
          Console.WriteLine($"{modifiedPrograms} programs modified for channel '{channel.Key}'. Applied correction: '{channel.Value}'");
          modifiedPrograms = 0;
        }
      }

      // Save new EPG
      Console.WriteLine($"Saving corrected EPG to: {config.outputXml}");
      try
      {
        Console.WriteLine($"Saving output xml to: {config.outputXml}");
        xmlContent.Save(config.outputXml);
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
        Console.WriteLine($"Saving output xml to default location: {config.defaultOutputXml}");
        xmlContent.Save(config.defaultOutputXml);
      }
      Console.WriteLine($"Done! Finished at {DateTime.Now}");

    }


    static void DisplayHelp()
    {

      Console.WriteLine($"xmltv_time_modify v. {version} - a tool for modifying XMLTV timings by Hristo Genev");
      Console.WriteLine("");
      Console.WriteLine("Arguments:");
      Console.WriteLine("");
      Console.WriteLine("  /h              Display help.");
      Console.WriteLine("  /in             Provide the path to the input XMLTV file. Defaults to epg.xml");
      Console.WriteLine("  /out            Provide the path to the output XMLTV file where all changes will be saved.\n" +
                        "                  Defaults to epg_corrected.xml");
      Console.WriteLine("  /correction     Specify the correction to be applied globally to all channels.\n" +
                        "                  Default is local, which automatically converts the timings to user current timezone.\n" +
                        "                  Other acceptable values:\n" +
                        "                  /correction:local - convert to user local time\n" +
                        "                  /correction:utc - convert to UTC\n" +
                        "                  /correction:+1 - adds 1 hour\n" +
                        "                  /correction:-1,5 (or -1.5) - subtracts 1 and a half hours\n" +
                        "                  /correction:+02:15 - adds 2 hours and 15 minutes\n" +
                        "                  /correction:-05:30 - subtracts 5 hours and 30 minutes");
      Console.WriteLine("  /channels       Provide a list of channels to be corrected or a path to a file containing\n" +
                        "                  the list of channels and their correction values. Acceptable values are: \n" +
                        "                  /channels:all - applies the correction to all channels. This is the default.\n" +
                        "                  /channels:\"Channel1ID, Channel2ID, Channel 3 ID\" - applies the correction to the given channel ids.\n" +
                        "                  /channels:C:\\EPG\\channels_and_corrections.ini - reads the channels and time corrections from a file.");
      Console.WriteLine("                  The file channels_and_corrections.ini should contain the channel ids and their correction values\n" +
                        "                  separated by \"=\". Prepending # to the channel name disables the channel correction.");
      Console.WriteLine("                  Example file content:");
      Console.WriteLine("                  Channel1Id=+1");
      Console.WriteLine("                  Channel2Id=+02:45");
      Console.WriteLine("                  Channel 3 Id=-1,5");
      Console.WriteLine("                  #Channel4Id=-01:10 #will be skipped");
      Console.WriteLine("  /ro             Removes the offset. For instance \"20200924061000 +0000\" becomes \"20200924061000\".\n" +
                        "                  Default is false. If the input datetime string has no offset, it will be appended.");
      Console.WriteLine("");
      Console.WriteLine("Example usage (for questions feel free to contact hristo[dot]genev@gmail[dot]com):");
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
      Console.WriteLine("Subtrack 5 hours and 15 minutes from all channels:");
      Console.WriteLine("xmltv_time_modify /correction:-05:15");
      Console.WriteLine("");
      Console.WriteLine("Read the channels and their correction values from a file with an INI format:");
      Console.WriteLine("xmltv_time_modify /channels:channels_and_corrections.ini");

      //Console.WriteLine("Read the channels and their correction values from a XML file:");
      //Console.WriteLine("xmltv_time_modify /channels:channels_and_corrections.xml");
      //Console.WriteLine("The file channels_and_corrections.xml should have the following syntax:");
      //Console.WriteLine("Example content:");
      //Console.WriteLine("<channels>");
      //Console.WriteLine("    <channel id=\"Channel1Id\" correction=\"+1\" />");
      //Console.WriteLine("    <channel id=\"Channel2Id\" correction=\"+02:15\" />");
      //Console.WriteLine("    <channel id=\"Channel 3 Id\" correction=\"-1,5\" />");
      //Console.WriteLine("</channels>");
      Console.WriteLine("");
      Console.WriteLine("Provide a list of channels to apply correction of -2 hours:");
      Console.WriteLine("xmltv_time_modify /channels:\"Channel1Id, Channel2Id, Channel3Id\" /correction:-2");
      //Console.WriteLine("");
    }
  }

}
