using System;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;

namespace wgmulti
{
  public class Xmltv
  {
    XDocument root;
    XElement tv;
    String file = "epg.xml";
    String generatorName = "WebGrab+Plus/w MDB &amp; REX Postprocess -- version  V2.1 -- Jan van Straaten";
    String generatorUrl = "http://www.webgrabplus.com";
    // String generatorAutomationTool = "wgmulti.exe";
    public List<XElement> allChannels = new List<XElement>();
    public List<XElement> channels = new List<XElement>();
    public List<XElement> programmes = new List<XElement>();
    public List<XElement> postProcessedProgrammes = new List<XElement>();
    public List<String> emptyChannelNames = new List<String>();

    public Xmltv(String file = null, String tempDir = null)
    {
      if (file == null) // We create an empty object
        return;

      try
      {
        root = XDocument.Load(file);
        tv = root.Element("tv");

        if (tv.Attribute("generator-info-name") != null)
          generatorName = tv.Attribute("generator-info-name").Value;

        if (tv.Attribute("generator-info-url") != null)
          generatorUrl = tv.Attribute("generator-info-url").Value;

        // if (tv.Attribute("generator-automation-tool") != null)
        //   tv.Add(new XAttribute("generator-info-automation-tool", generatorAutomationTool));

        // Get all programme xml nodes
        programmes = tv.Elements("programme").ToList();
        // Get the channel ids of all programme elements
        var programmesIds = programmes.Select(p => p.Attribute("channel").Value);

        // Get all channel xml nodes
        allChannels = tv.Elements("channel").ToList();
        
        // Get all channel xml nodes that have programmes
        channels.AddRange(tv.Elements("channel").Where(c => programmesIds.Contains(c.Attribute("id").Value)).ToList());

        // Get the names of all channels that have no programmes
        emptyChannelNames.AddRange(
          allChannels.Where(c => !programmesIds.Equals(c.Attribute("id").Value))
          .Select(c => c.Element("display-name").Value)
        .ToList());  
      }
      catch (Exception ex)
      {
        Log.Error(ex.ToString());
        return;
      }
    }

    /// <summary>
    /// Get a list of XElements with programs having the required 'channel' attribute
    /// </summary>
    /// <param name="channel_id">The channel id of the programs to copy</param>
    /// <param name="offset">The offset of programs. If different than null it will be applied to start and end times. If null local conversion will be applied</param>
    /// <param name="newId">The new channel id of the programs. If specified the channel id of the program will be renamed</param>
    /// <returns></returns>
    public List<XElement> GetProgramsById(String channel_id, double? offset = null, String newId = null, bool usePostProcessedData = false)
    {
      var _programs = new List<XElement>();
      // If we are copying from regular programs or from post processed programs
      var channel_programs = usePostProcessedData && postProcessedProgrammes.Count > 0 ? postProcessedProgrammes : programmes;
      // If offset is null and convert to local times is true, then keep the offset null
      offset = (offset == null && !Arguments.convertTimesToLocal) ? 0 : offset;

      try
      {
        channel_programs.ForEach(program =>
        {
          var channel_name = program.Attribute("channel").Value;
          var start_time = program.Attribute("start").Value;
          var end_time = program.Attribute("stop").Value;

          if (channel_name == channel_id && Application.masterConfig.Dates.Contains(start_time.Substring(0, 8)))
          {
            var copiedProgram = new XElement(program); // Clone to a copy of the object
            if (!String.IsNullOrEmpty(newId))
              copiedProgram.Attribute("channel").Value = newId; // Rename all 'channel' tags to reflect the new channel

            //If offset is null we will apply local time shift
            copiedProgram.Attribute("start").Value = Utils.AddOffset(start_time, offset); 
            copiedProgram.Attribute("stop").Value = Utils.AddOffset(end_time, offset);

            // if it's an offset channel remove all tags other then the 'title'
            if (Arguments.copyOnlyTitleForOffsetChannel && offset != null && offset > 0) 
            {
              foreach (var el in copiedProgram.Elements())
              {
                if (el.Name != "title")
                  el.Remove();
              }
            }

            _programs.Add(copiedProgram);
          }
        });
      }
      catch (Exception ex)
      {
        Log.Error(ex.ToString());
      }

      return _programs;
    }

    public void Save(String outputFile = null, bool usePostProcessedPrograms = false)
    {
      tv = new XElement("tv");
      tv.Add(new XAttribute("generator-info-name", generatorName));
      tv.Add(new XAttribute("generator-info-url", generatorUrl));

      if (Arguments.removeChannelsWithNoProgrammes)
        tv.Add(channels.ToArray());
      else
        tv.Add(allChannels.ToArray());

      if (usePostProcessedPrograms)
        tv.Add(postProcessedProgrammes.ToArray());
      else
        tv.Add(programmes.ToArray());

      root = new XDocument(new XDeclaration("1.0", "utf-8", null));
      root.Add(tv);

      if (outputFile == null)
        outputFile = file;
      root.Save(outputFile);
    }
  }
}
