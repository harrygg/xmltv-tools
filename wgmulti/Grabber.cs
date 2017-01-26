using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace wgmulti
{
  public class Grabber
  {
    public Config config;
    Config rootConfig;
    public String id;
    public String currentChannel;
    String localDir = "";
    String iniFilePath;
    public bool enabled = false;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rootConfig"></param>
    /// <param name="id"></param>
    /// <param name="channels"></param>
    public Grabber(Config rootConfig, String id, List<Channel> channels)
    {
      this.rootConfig = rootConfig;
      this.id = id;

      try
      {
        foreach (var channel in channels)
        {
          var sourceIni = Path.Combine(rootConfig.folder, channel.siteIni); 
          CopyIni(sourceIni, channel);

          // If we are grouping by site ini we copy the first ini file and exit.
          if (Arguments.groupChannelsBySiteIni)
            break;
        }
        
        config = (Config)rootConfig.Clone(localDir);
        config.channels = channels.Where(ch => (ch.active == true)).ToList<Channel>();
        config.Save();
        enabled = true;
      }
      catch (Exception ex)
      {
        Console.WriteLine("Grabber ERROR | {0}" + ex.Message);
        enabled = false;
      }
    }

    void CopyIni(String sourceIni, Channel channel)
    {
      try
      {
        localDir = GetLocalDir(channel);
        iniFilePath = Path.Combine(localDir, channel.siteIni);
        File.Copy(sourceIni, iniFilePath, true);
      }
      catch (FileNotFoundException)
      {
        Console.WriteLine("Grabber {0} | ERROR | Ini file does not exist. Grabbing will be skipped!\n{1}", id.ToUpper(), sourceIni);
        channel.active = false;
      }
      catch (Exception ex)
      {
        Console.WriteLine("Unable to copy source ini file to grabber dir");
        Console.WriteLine(ex.ToString());
        channel.active = false;
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
      var localDir = Path.Combine(rootConfig.tempDir, id);
      if (!Arguments.groupChannelsBySiteIni && Arguments.maxChannelsInGroup == 1)
        localDir = Path.Combine(rootConfig.tempDir, channel.name);

      if (!Directory.Exists(localDir))
        Directory.CreateDirectory(localDir);

      return localDir;
    }
  }
}
