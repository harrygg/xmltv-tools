using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wgmulti
{
  public class Grabber
  {
    public Config config;
    public String id;
    public String currentChannel;
    String iniFile;
    String iniFilePath;
    public bool enabled = false;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="mainConfig"></param>
    /// <param name="name"></param>
    /// <param name="channels"></param>
    public Grabber(Config mainConfig, String name, List<Channel> channels)
    {
      try
      {
        id = name;
        iniFile = id + ".ini";
        var sourceIni = Path.Combine(mainConfig.folder, iniFile);
        if (File.Exists(sourceIni))
        {
          String localDir = Path.Combine(mainConfig.tempDir, id);
          if (!Directory.Exists(localDir))
          {
            Directory.CreateDirectory(localDir);
          }
          iniFilePath = Path.Combine(localDir, iniFile);
          File.Copy(sourceIni, iniFilePath, true);

          config = (Config)mainConfig.Clone(localDir);
          config.channels = channels;
          config.Save();
          enabled = true;
        }
        else
          Console.WriteLine("Grabber {0} | ERROR | Ini file does not exist. Grabbing will be skipped!\n{1}", id.ToUpper(), sourceIni);
      }
      catch (Exception)
      {
        //Log error
        //enabled = false;
      }
    }
  }
}
