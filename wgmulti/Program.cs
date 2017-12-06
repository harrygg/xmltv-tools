using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace wgmulti
{
  public class Program
  {
    public static void Main(string[] args)
    {
      if (args.Count() == 2)
        Arguments.configDir = args[1];

      Log.Info("#####################################################");
      Log.Info("#                                                   #");
      Log.Info("#      Wgmulti.exe for WebGrab+Plus by Harry_GG     #");
      Log.Info("#                                                   #");
      Log.Info("#####################################################");
      Log.Info("System: " + Environment.OSVersion.Platform);
      Log.Info("Working Directory: " + Directory.GetCurrentDirectory());
      Log.Info("Config Directory: " + Arguments.configDir);
      Log.Info("Grabbing Temp Directory: " + Arguments.grabingTempFolder);
      //var versionInfo = FileVersionInfo.GetVersionInfo(Arguments.wgPath);
      //Log.Info(String.Format("{0} version: {1}", Arguments.wgexe, versionInfo.ProductVersion));
      if (Arguments.useJsonConfig)
      {
        Log.Info("Use JSON config file: " + Arguments.useJsonConfig);
        Log.Info("JsonConfigFileName: " + Config.jsonConfigFileName);
      }
      else
        Log.Info("ConvertXmlConfigToJson: " + Arguments.exportJsonConfig);
      Log.Info("MaxAsyncProcesses: " + Arguments.maxAsyncProcesses);
      Log.Info("CombineLogFiles: " + Arguments.combineLogFiles);
      Log.Info("ConvertTimesToLocal: " + Arguments.convertTimesToLocal);
      Log.Info("GenerateResultsReport: " + Arguments.generateReport);
      Log.Info("ShowConsole: " + Arguments.showConsole);
      Log.Line();
      Log.Info("Execution started at: " + DateTime.Now);

      Application.Run();

      Log.Line();
      Log.Info("Total channels: " + Application.report.total);
      Log.Info("With EPG: " + Application.report.channelsWithEpg);
      Log.Info("Without EPG: " + Application.report.emptyChannels.Count);
      Log.Info("EPG size: " + Application.report.fileSize);
      Log.Info("EPG md5 hash: " + Application.report.md5hash);
      Log.Info("Report saved to: " + Arguments.reportFilePath);
      Log.Line();
    }
  }
}
