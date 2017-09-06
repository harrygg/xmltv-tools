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
        config.Save();
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
      var localDir = Path.Combine(Program.rootConfig.tempDir, id);
      if (!Arguments.groupChannelsBySiteIni && Arguments.maxChannelsInGroup == 1)
        localDir = Path.Combine(Program.rootConfig.tempDir, channel.name);

      if (!Directory.Exists(localDir))
        Directory.CreateDirectory(localDir);

      return localDir;
    }
  }
}
