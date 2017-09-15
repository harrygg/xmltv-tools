using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace wgmulti
{
  /// <summary>
  /// Create grabbers (copy ini and config to temp location) for each group
  /// </summary>
  public class Grabber
  {
    public Config config;
    //Config rootConfig;
    public String id;
    public String currentChannel;
    public String localDir = "";
    //String iniFilePath;
    public bool enabled = false;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="group"></param>
    public Grabber(ChannelGroup group)
    {
      //this.rootConfig = rootConfig;
      id = group.id;

      if (group.channels.Count == 0)
      {
        enabled = false;
        return;
      }

      // If all channels have update type None (possible when grabbing from backup siteinis), disable the grabber
      if (group.channels.Where(ch => (ch.update == "")).ToList<Channel>().Count == group.channels.Count)
      {
        enabled = false;
        return;
      }

      try
      {
        // save ini files to temp dirs
        foreach (var channel in group.channels)
        {
          localDir = GetLocalDir(channel);
          if (!channel.GetActiveSiteIni().Save(localDir))
            channel.enabled = false;
          // If we are grouping by siteini and there was no siteini found (channel will be inactive) 
          // we exit and don't enable the grabber.
          if (Arguments.groupChannelsBySiteIni && !channel.enabled)
            return;
          // If we are grouping by siteini we copy the first ini file and exit.
          if (Arguments.groupChannelsBySiteIni)
            break;
        }
        
        config = (Config)Program.rootConfig.Clone(localDir);
        config.channels = group.channels.Where(ch => (ch.enabled == true)).ToList<Channel>();
        config.Save(); // Save config and postprocess config files
        enabled = true;
      }
      catch (Exception ex)
      {
        Console.WriteLine("Grabber ERROR | {0}" + ex.Message);
        enabled = false;
      }
    }

    /// <summary>
    /// Creates and gets the name of the temp folder of the current grabber. 
    /// If GroupChannelsBySiteIni is false and MaxChannelsInGroup is 1, 
    /// we create a dir with the name of the channel else we use the siteini id
    /// </summary>
    /// <param name="channel"></param>
    /// <returns></returns>
    String GetLocalDir(Channel channel)
    {
      var localDir = Path.Combine(Arguments.grabingTempFolder, id);
      if (!Arguments.groupChannelsBySiteIni && Arguments.maxChannelsInGroup == 1)
        localDir = Path.Combine(Arguments.grabingTempFolder, channel.name);

      if (!Directory.Exists(localDir))
        Directory.CreateDirectory(localDir);

      return localDir;
    }

    internal void ParseOutput()
    {
      Console.WriteLine("Grabber {0} | Parsing XML output", id.ToUpper());
      //Parse xml file and assign programs to channels
      var filePath = config.outputFilePath;
      if (config.postProcess.run)
        filePath = config.postProcess.fileName;

      if (!File.Exists(filePath))
      {
        Console.WriteLine("ERROR! Epg file not found {0}", filePath);
        return;
      }
      var xmltv = new Xmltv(filePath);
      var i = 0;
      // Iterate the channels in the EPG file, add their programmes to the active channels
      xmltv.channels.ForEach(xmltvChannel => {
        try
        {
          // If channel is offset channel, get it's parent first
          var channel = Program.rootConfig.GetActiveChannels().First(c => c.name.Equals(xmltvChannel.Element("display-name").Value));

          if (channel.xmltvPrograms.Count == 0) // Update only if channel hasn't been populated from previous run
          {
            channel.xmltvChannel = xmltvChannel;
            channel.xmltvPrograms = xmltv.programmes.Where(p => p.Attribute("channel").Value == channel.xmltv_id).ToList();
            i += channel.xmltvPrograms.Count;

            Console.WriteLine("Grabber {0} | {1} | {2} programms grabbed",
              id.ToUpper(),
              channel.name,
              channel.xmltvPrograms.Count);
          }
        }
        catch { }
      });

        if (i == 0)
          Console.WriteLine("Grabber {0} | No programs grabbed!!!", id.ToUpper());
    }
  }
}
