using System;
using System.Configuration;
using System.IO;

namespace wgmulti
{
  public class Arguments
  {
    public static String configDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public static String webGrabFolder = "";
    public static String grabingTempFolder = String.Empty;
    public static bool useJsonConfig;
    public static bool exportJsonConfig = true;
    public static Double timeOffset = 0;
    public static bool help = false;
    public static bool convertTimesToLocal = true;
    public static int maxAsyncProcesses = 10;
    public static bool groupChannelsBySiteIni = true;
    public static int maxChannelsInGroup = 10;
    public static int processTimeout = 240;
    public static bool showConsole = false;
    public static bool randomStartOrder = true;
    public static bool generateReport = true;
    static String reportFileName = "wgmulti.report.json";
    public static String reportFolder = "";
    public static String reportFilePath = "";
    public static bool combineLogFiles = true;
    public static bool removeChannelsWithNoProgrammes = true;
    public static bool persistantGrabbing = true;
    public static bool debug = false;
    public static bool copyOnlyTitleForOffsetChannel = false;
    public static bool removeExtraChannelAttributes = false;
    public const String wgexe = "WebGrab+Plus.exe";
    public static String wgPath = "";

    public static bool IsLinux()
    {
      int p = (int)Environment.OSVersion.Platform;
      return (p == 4 || p == 6 || p == 128) ? true : false;
    }

    static Arguments()
    {
      var val = ConfigurationManager.AppSettings["configDir"] ?? @"WebGrab+Plus";

      if (val == @"WebGrab+Plus")
      {
        if (IsLinux())
          configDir = Directory.GetCurrentDirectory();
        else
          configDir = Path.Combine(configDir, val);
      }
      else
        configDir = val;


      val = ConfigurationManager.AppSettings["Debug"] ?? "false";
      debug = Convert.ToBoolean(val);

      webGrabFolder = ConfigurationManager.AppSettings["WebGrabFolder"] ?? webGrabFolder;
      wgPath = Path.Combine(Arguments.webGrabFolder, Arguments.wgexe);

      val = ConfigurationManager.AppSettings["UseJsonConfig"] ?? "true";
      useJsonConfig = Convert.ToBoolean(val);

      val = ConfigurationManager.AppSettings["ExportJsonConfig"] ?? "true";
      exportJsonConfig = Convert.ToBoolean(val) && !useJsonConfig;

      val = ConfigurationManager.AppSettings["GroupChannelsBySiteIni"] ?? "true";
      groupChannelsBySiteIni = Convert.ToBoolean(val);

      val = ConfigurationManager.AppSettings["RandomStartOrder"] ?? "true";
      randomStartOrder = Convert.ToBoolean(val);

      val = ConfigurationManager.AppSettings["GenerateResultsReport"] ?? "true";
      generateReport = Convert.ToBoolean(val);

      val = ConfigurationManager.AppSettings["CombineLogFiles"] ?? "true";
      combineLogFiles = Convert.ToBoolean(val);

      val = ConfigurationManager.AppSettings["ConvertTimesToLocal"] ?? "true";
      convertTimesToLocal = Convert.ToBoolean(val);

      val = ConfigurationManager.AppSettings["MaxAsyncProcesses"] ?? "10";
      maxAsyncProcesses = val != "0" ? Convert.ToInt32(val) : 10;

      val = ConfigurationManager.AppSettings["MaxChannelsInGroup"] ?? "10";
      maxChannelsInGroup = val != "0" ? Convert.ToInt32(val) : 10;

      val = ConfigurationManager.AppSettings["ProcessTimeout"] ?? "240";
      processTimeout = Convert.ToInt32(val);

      val = ConfigurationManager.AppSettings["RemoveChannelsWithNoProgrammes"] ?? "true";
      removeChannelsWithNoProgrammes = Convert.ToBoolean(val);

      val = ConfigurationManager.AppSettings["CopyOnlyTitleForOffsetChannel"] ?? copyOnlyTitleForOffsetChannel.ToString().ToLower();
      copyOnlyTitleForOffsetChannel = Convert.ToBoolean(val);

      val = ConfigurationManager.AppSettings["RemoveExtraChannelAttributes"] ?? removeExtraChannelAttributes.ToString().ToLower();
      removeExtraChannelAttributes = Convert.ToBoolean(val);
 
      //if (!IsLinux())
      //{ 
        val = ConfigurationManager.AppSettings["ShowWebGrabConsole"] ?? "false";
        showConsole = Convert.ToBoolean(val);
      //}

      val = ConfigurationManager.AppSettings["GrabingTempFolder"] ?? Path.Combine(Path.GetTempPath(), "wgmulti");
      if (String.IsNullOrEmpty(val)) // If grabingTempFolder is empty in config file 
        val = Arguments.configDir;
      grabingTempFolder = val;

      val = ConfigurationManager.AppSettings["ReportFileName"] ?? reportFileName;
      reportFileName = val;

      val = ConfigurationManager.AppSettings["ReportFolder"] ?? reportFolder;
      reportFolder = val;

      reportFilePath = Path.Combine(reportFolder, reportFileName);
    }
  }
}
