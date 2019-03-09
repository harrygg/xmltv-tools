using System;
using System.Configuration;
using System.IO;
using System.Linq;


namespace wgmulti
{
  public class Arguments
  {
    public static String configDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public static String webGrabFolder = "";

    internal static bool ParseArguments(string[] args)
    {
      try
      {
        if (args.Length == 1)
        {
          if (args[0] == "-h" || args[0] == "-?" || args[0] == "--help" || args[0] == "/?" || args[0] == "/h")
          {
            DisplayHelp();
            return false;
          }
          else
          {
            Arguments.configDir = args[0];
          }
        }

        if (args.Length > 1)
        {
          for(var i=0; i < args.Length; i++)
          {
            if (args[i].Contains("configDir"))
              configDir = args[i+1];

            if (args[i].Contains("reportFolder"))
              configDir = args[i + 1];
          }
        }
      }
      catch(Exception ex)
      {
        Log.Error("Error while partins command line arguments");
        Log.Error(ex.ToString());
        return false;
      }
      return true;
    }

    private static void DisplayHelp()
    {
      Console.WriteLine("USAGE:");
      Console.WriteLine("wgmulti <path-to-config-folder>");
      Console.WriteLine("wgmulti -configDir <path-to-config-folder>");
      Console.WriteLine("wgmulti -configDir <path-to-config-folder> -reportFolder <path-to-folder-where-report-will-be-created>");
    }

    public static String grabingTempFolder = String.Empty;
    public static bool useJsonConfig;
    public static bool exportJsonConfig = true;
    public static String jsonConfigFileName = "wgmulti.config.json";
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
    public static bool saveStandaloneReports = false;
    public static bool combineLogFiles = true;
    public static bool removeChannelsWithNoProgrammes = true;
    public static bool persistantGrabbing = true;
    public static bool debug = false;
    public static bool copyOnlyTitleForOffsetChannel = false;
    public static bool removeExtraChannelAttributes = false;
    public const String wgexe = "WebGrab+Plus.exe";
    public static String wgPath = "";
    public static bool saveStandaloneGuides = false;
    public static String standaloneGuidesFolder = "";
    public static bool runPostprocessScript = false;
    public static String postprocessScript = "";
    public static String postprocessArguments = "";

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

      val = ConfigurationManager.AppSettings["JsonConfigFileName"] ?? jsonConfigFileName;
      jsonConfigFileName = val;

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
        val = ConfigurationManager.AppSettings["ShowWebGrabConsole"] ?? showConsole.ToString().ToLower();
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

      val = ConfigurationManager.AppSettings["SaveStandaloneReports"] ?? saveStandaloneReports.ToString();
      saveStandaloneReports = Convert.ToBoolean(val);

      reportFilePath = Path.Combine(reportFolder, reportFileName);

      val = ConfigurationManager.AppSettings["SaveStandaloneGuides"] ?? saveStandaloneGuides.ToString().ToLower();
      saveStandaloneGuides = Convert.ToBoolean(val);

      val = ConfigurationManager.AppSettings["StandaloneGuidesFolder"] ?? standaloneGuidesFolder;
      standaloneGuidesFolder = val;

      val = ConfigurationManager.AppSettings["RunPostprocessScript"] ?? runPostprocessScript.ToString().ToLower();
      runPostprocessScript = Convert.ToBoolean(val);

      val = ConfigurationManager.AppSettings["PostprocessScript"] ?? postprocessScript;
      postprocessScript = val;

      val = ConfigurationManager.AppSettings["PostprocessArguments"] ?? postprocessArguments;
      postprocessArguments = val.Replace("%JsonConfigFileName%", jsonConfigFileName);
    }
  }
}
