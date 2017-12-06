using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace wgmulti
{

  /// <summary>
  /// Create grabbers (copy ini and config to temp location) for each group
  /// </summary>
  public class Grabber
  {
    public Config config;
    public String id;
    public String currentChannel;
    public String localDir = String.Empty;
    public String path = String.Empty;
    public bool enabled = false;
    public int number = 0;
    public GrabType type = GrabType.SCRUB;
    public List<Channel> channels = new List<Channel>();
    public Grabber(String id, List<Channel> channels)
    {

      this.id = id; // Set it first in order for WriteLog to work

      try
      {
        if (channels.Count == 0)
        {
          WriteLog("Grabber has no channels. Grabber disabled!", LogLevel.ERROR);
          return;
        }

        this.channels = channels;

        // If all channels have update type None (possible when grabbing from backup siteinis), disable the grabber
        if (channels.Where(ch => (ch.update == UpdateType.None)).Count() == channels.Count)
        {
          WriteLog("No channels require an update! Grabber disabled!", LogLevel.WARN);
          return;
        }

        // Make sure the temp dir exists
        localDir = Utils.CreateLocalDir(id);

        if (localDir == null)
        {
          WriteLog("Unable to create local temp dir! Grabber disabled!", LogLevel.ERROR);
          return;
        }

        if (channels[0].GetActiveSiteIni().type == GrabType.COPY)
          type = GrabType.COPY;

        config = (Config)Application.masterConfig.Clone(localDir);
        config.channels = channels.Where(ch => (ch.enabled == true)).ToList<Channel>();

        if (type == GrabType.COPY)
        {
          path = channels[0].GetActiveSiteIni().path;
          if (String.IsNullOrEmpty(path))
          {
            WriteLog("Path attribute does not exist in Siteini configuraiton. Grabber disabled!", LogLevel.ERROR);
            return;
          }
        }
        else 
        {
          // If grabbing type is Scrub, copy siteinis to temp folder
          if (!channels[0].GetActiveSiteIni().Save(localDir))
          {
            WriteLog("Siteini not saved to temp folder. Grabber disabled!", LogLevel.ERROR);
            return;
          }
          // Save config and postprocess config files
          if (!config.Save())
          {
            WriteLog("Unable to save config file in " + config.configFilePath, LogLevel.ERROR);
            return;
          }
          WriteLog("Saved config file in " + config.configFilePath, LogLevel.DEBUG);
        }
      }
      catch (Exception ex)
      {
        WriteLog(String.Format("{0} Grabber disabled!", ex.ToString()), LogLevel.ERROR);
        return;
      }

      // If no errors enable the grabber
      WriteLog("Grabber enabled!", LogLevel.DEBUG);
      enabled = true;
    }

    public void Grab()
    {
      WriteLog("GrabType is " + type.ToString(), LogLevel.DEBUG);
      if (type == GrabType.SCRUB)
        ScrubData();
      else
        CopyData();
    }

    void ScrubData()
    {
      try
      {
        var process = new Process();
        var startInfo = new ProcessStartInfo();
        startInfo.CreateNoWindow = false;
        startInfo.UseShellExecute = Arguments.showConsole;
        startInfo.WindowStyle = ProcessWindowStyle.Normal;
        startInfo.FileName = Arguments.wgPath;
        startInfo.Arguments = String.Format("\"{0}\"", config.folder);
        process.StartInfo = startInfo;

        if (!Arguments.showConsole)
        {
          process.StartInfo.RedirectStandardOutput = true;
          process.OutputDataReceived += new DataReceivedEventHandler((sender, e) => DataReceived(sender, e));
        }

        WriteLog(String.Format("Starting a new instance of {0} with argument {1}", startInfo.FileName, startInfo.Arguments));
        process.Start();

        process.EnableRaisingEvents = true;
        process.Exited += new EventHandler((sender, e) => ScrubbingFinished(sender, e));
        if (!Arguments.showConsole)
          process.BeginOutputReadLine();

        process.WaitForExit(1000 * 60 * Arguments.processTimeout);
      }
      catch (Exception ex)
      {
        WriteLog(ex.ToString(), LogLevel.ERROR);
      }
    }

    void CopyData()
    {
      ///If we are downloading the xmltv, GetRemoteFile will download and extract
      if (path.StartsWith("http") || path.StartsWith("ftp"))
      {
        var result = GetRemoteFile(path, config.outputFilePath);
        if (result == null)
        {
          WriteLog("GetRemoteFile() failed!", LogLevel.ERROR);
          return;
        }
      }
      //ScrubbingFinished(null, null);
      ParseOutput();
    }


    void ParseOutput()
    {
      //Parse xml file and assign programs to channels
      WriteLog("Parsing XML output of " + config.outputFilePath, LogLevel.DEBUG);
      var grabberXmltv = new Xmltv(config.outputFilePath);
      if (grabberXmltv == null)
      {
        WriteLog("Parsing XML ERROR!", LogLevel.ERROR);
        return;
      }

      // Get postprocessed data
      Xmltv postProcessedXmltv = null;
      if (type == GrabType.SCRUB && config.postProcess.run)
      {
        postProcessedXmltv = new Xmltv(config.postProcess.fileName);
        if (postProcessedXmltv == null)
          WriteLog("Parsing post processed XML ERROR!", LogLevel.ERROR);
      }

      var i = 0;
      // Loop through all channels in grabber's config and get their programs
      channels.ForEach(
          channel => {
            if (channel.xmltv.programmes.Count > 0)
            {
              channel.update = UpdateType.None;
            }
            else
            {
              var channel_id = "";
              try
              {
                channel_id = (type == GrabType.COPY) ? channel.GetActiveSiteIni().site_id : channel.xmltv_id;
                if (type == GrabType.COPY)
                  WriteLog(String.Format("Copying data for channel {0} by id {1}", channel.name, channel_id));

                if (!channel.CopyChannelXml(grabberXmltv, channel_id))
                  WriteLog(String.Format("No XML channel {0} with id {1} not found!", channel.name, channel_id), LogLevel.ERROR);

                channel.xmltv.programmes = grabberXmltv.GetProgramsById(
                  channel_id, channel.offset, channel.xmltv_id);
                i += channel.xmltv.programmes.Count;

                if (channel.xmltv.programmes.Count == 0)
                {
                  WriteLog(String.Format("No XML programms found for channel {0} with id '{1}' found in EPG!",
                    channel.name, channel_id), LogLevel.ERROR);
                }
                else
                {
                  WriteLog(String.Format(" {0} - {1} programs grabbed", channel.name, channel.xmltv.programmes.Count));

                  // Copy post processed data
                  if (type == GrabType.SCRUB && config.postProcess.run && postProcessedXmltv != null)
                    channel.xmltv.postProcessedProgrammes = postProcessedXmltv.GetProgramsById(channel_id, channel.offset, channel.xmltv_id);

                  //If channel has offset channels, copy and offset the programs
                  if (channel.timeshifts != null)
                  {
                    foreach (var timeshifted in channel.timeshifts)
                    {
                      WriteLog("Generating program for offset channel " + timeshifted.name);

                      timeshifted.CopyChannelXml(channel.xmltv);
                      //offset_channel.CopyProgramsXml(channel.xmltv);

                      // Copy programs from the parent channel xmltv. Apply the offset and rename
                      timeshifted.xmltv.programmes = channel.xmltv.GetProgramsById(
                        channel.xmltv_id,
                        timeshifted.offset,
                        timeshifted.xmltv_id);

                      // Copy post processed programs from the parent channel post processed programms
                      if (type == GrabType.SCRUB && config.postProcess.run && postProcessedXmltv != null)
                      {
                        timeshifted.xmltv.postProcessedProgrammes = channel.xmltv.GetProgramsById(
                          channel.xmltv_id,
                          timeshifted.offset,
                          timeshifted.xmltv_id,
                          true);
                      }
                      WriteLog(String.Format(" {0} - {1} programs grabbed", timeshifted.name, timeshifted.xmltv.programmes.Count));
                    }
                  }
                }
              }
              catch (Exception)
              {
                //WriteLog(ex.ToString());
                WriteLog(String.Format("NO programs for channel '{0}' with id '{1}'", channel.name, channel_id), LogLevel.ERROR);
              }
            }
          });

      if (i == 0)
        WriteLog("No programs grabbed!!!");
    }

    void DataReceived(object sender, DataReceivedEventArgs e)
    {
      if (!String.IsNullOrEmpty(e.Data))
      {
        if (Application.masterConfig.channels.Count == 1 || (Arguments.maxAsyncProcesses == 1 && Arguments.maxChannelsInGroup == 1))
        {
          Log.Error(e.Data);
          return;
        }

        var name = id.ToUpper();
        var reg = new Regex(@"xmltv_id=(.*?)\)");
        try { currentChannel = reg.Matches(e.Data)[0].Groups[1].Value; } catch { }

        if (e.Data.Contains("started") || e.Data.Contains("finished"))
          WriteLog(e.Data.Replace(name, ""));

        if (e.Data.Contains("xmltv_id") && !e.Data.Contains("Could find existing channel"))
          WriteLog("starting " + e.Data.Replace("(   ", "").Replace("   ) " + name + " -- chan. (xmltv_id=", " ").Replace(") --", " "));

        if (e.Data.ToLower().Contains("error"))
          WriteLog(String.Format("{0} {1}", currentChannel, e.Data), LogLevel.ERROR);

        if (e.Data.Contains("no shows in indexpage"))
          WriteLog(String.Format("no shows in indexpage for channel {0}", currentChannel), LogLevel.ERROR);

        if (e.Data.Contains("no index page data"))
          WriteLog(String.Format("{0}", e.Data), LogLevel.ERROR);

        if (e.Data.Contains("unable to update channel"))
          WriteLog(String.Format("{0} {1}", currentChannel, e.Data), LogLevel.ERROR);
      }
    }

    void ScrubbingFinished(object sender, EventArgs e)
    {
      ParseOutput();
    }

    /// <summary>
    /// String representation of the Grabber object used for debugging
    /// </summary>
    /// <returns></returns>
    public override String ToString()
    {
      return String.Format("{0} | {1} | {2}", id.ToUpper(), enabled, channels.Count);
    }


    String GetRemoteFile(String file, String outFile)
    {
      var dir = Path.GetDirectoryName(outFile);
      var name = DownloadFile(file, dir);
      if (name == null)
      {
        WriteLog("Unable to download file " + file, LogLevel.ERROR);
        return null;
      }

      if (name.EndsWith("gz") || name.EndsWith("zip"))
      {
        var _name = Decompress(name, outFile);
        if (_name == null)
        {
          WriteLog("Unable to decompress " + name, LogLevel.ERROR);
          return null;
        }
        name = _name;
      }
      return name;
    }

    String DownloadFile(String fileUrl, String tempDir)
    {
      try
      {
        var fileName = Path.GetFileName(fileUrl);
        fileName = Path.Combine(tempDir, fileName);
        var doDownload = true;
        //Check if file exists and is fresh
        if (File.Exists(fileName))
        {
          var fi = new FileInfo(fileName);
          if (fi.LastWriteTime > DateTime.Now.AddHours(-12)) // if file ia less than 12 hours old
            doDownload = false;
        }

        if (doDownload)
        {
          Log.Info("-----------------------------------------------------");
          WriteLog("Starting download of " + fileUrl);
          WebClient client = new WebClient();
          client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/61.0.3163.100 Safari/537.36");
          //client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(client_DownloadProgressChanged);
          //client.DownloadFileCompleted += new AsyncCompletedEventHandler(client_DownloadCompleted);
          //client.DownloadFileAsync(new Uri(fileUrl), fileName);
          client.DownloadFile(new Uri(fileUrl), fileName);
          var fi = new FileInfo(fileName);
          WriteLog(String.Format("File downloaded! Size {0} bytes, Saved to {1}", fi.Length, fileName));
        }
        else
          WriteLog(String.Format("File {0} is new and will not be downloaded!", fileName), LogLevel.WARN);

        //while (client.IsBusy)
        //{
        //  System.Threading.Thread.Sleep(100);
        //}

        return fileName;
      }
      catch (Exception ex)
      {
        Log.Error(ex.ToString());
        return null;
      }
    }
    //public void client_DownloadCompleted(object sender, AsyncCompletedEventArgs e)
    //{
    //  n = "";
    //  Console.WriteLine("\nFile downloaded!");
    //}
    //static string n = "+";
    //public void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    //{
    //  Console.Write(n);
    //}

    String Decompress(String fileName, String outFile)
    {
      if (!File.Exists(fileName))
      {
        WriteLog(String.Format("No file to extract! Missing {0}", fileName), LogLevel.ERROR);
        return null;
      }

      // If file exists check the when is the last time we modified it.
      if (File.Exists(outFile))
      {
        var gzFileInfo = new FileInfo(fileName);
        var extractedFileInfo = new FileInfo(outFile);
        // If downloaded file is older than the extracted file do not extract
        if (gzFileInfo.LastWriteTime < extractedFileInfo.LastWriteTime)
        {
          WriteLog("File already decompressed " + outFile, LogLevel.WARN);
          return outFile;
        }
      }

      try
      {
        WriteLog("Starting decompress...");
        using (FileStream inStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
        {
          using (GZipStream zipStream = new GZipStream(inStream, CompressionMode.Decompress))
          {
            using (FileStream outStream = new FileStream(outFile, FileMode.Create, FileAccess.Write))
            {
              byte[] tempBytes = new byte[4096];
              int i;
              while ((i = zipStream.Read(tempBytes, 0, tempBytes.Length)) != 0)
                outStream.Write(tempBytes, 0, i);
            }
          }
        }
        WriteLog("File extracted successfully! " + outFile);
        return outFile;
      }
      catch (Exception ex)
      {
        WriteLog(ex.ToString());
        return null;
      }
    }

    void WriteLog(String msg, LogLevel level = LogLevel.INFO)
    {
      msg = String.Format("{0}.{1} | {2} | {3}", Application.currentSiteiniIndex + 1, number, id.ToUpper(), msg);

      if (level == LogLevel.INFO)
        Log.Info(msg);
      else if (level == LogLevel.ERROR)
        Log.Error(msg);
      else if (level == LogLevel.DEBUG)
        Log.Debug(msg);
      else if (level == LogLevel.WARN)
        Log.Warning(msg);
    }
  }
}
