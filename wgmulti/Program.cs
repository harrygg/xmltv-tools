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
      Log.Info("Path to WebGrab+Plus.exe: " + Arguments.wgPath);
      if (!File.Exists(Arguments.wgPath))
      {
        Log.Error("WebGrab+Plus.exe does not exist! Exiting.");
        return;
      }
      if (Arguments.IsLinux())
      {
        if (!TestWebGrabExe())
          return;
      }

      var versionInfo = FileVersionInfo.GetVersionInfo(Arguments.wgPath);
      Log.Info(String.Format("{0} version: {1}", Arguments.wgexe, versionInfo.ProductVersion));
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

    static bool TestWebGrabExe()
    {
      try
      {
        Log.Debug("Running on Unix based system! Testing if WebGrab+Plus.exe exists and has execute permissions...");
        var process = new Process();
        var startInfo = new ProcessStartInfo();
        startInfo.CreateNoWindow = false;
        startInfo.UseShellExecute = false;
        startInfo.WindowStyle = ProcessWindowStyle.Normal;
        startInfo.FileName = Arguments.wgPath;
        startInfo.Arguments = String.Format("--help");
        startInfo.RedirectStandardOutput = true;
        process.StartInfo = startInfo;
        process.Start();
        process.WaitForExit(1000 * 1);
        return true;
      }
      catch (System.ComponentModel.Win32Exception)
      {
        Log.Error("WebGrab+Plus.exe is either missing or not executable!");
        Console.WriteLine("1. Make sure that it exists and the 'WebGrabFolder' setting in wgmulti.exe.config is set correctly. If 'WebGrabFolder' is blank, WebGrab+Plus.exe must be placed in the same directory as wgmulti.exe");
        Console.WriteLine("2. Make sure 'WebGrab+Plus.exe' is executable (run 'chmod +x WebGrab+Pluse.exe'");
        return false;
      }
      catch (Exception ex)
      {
        Log.Error(ex.Message);
        return false;
      }
    }
  }
}
