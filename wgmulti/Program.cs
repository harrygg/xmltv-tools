using System;
using System.IO;
using System.Diagnostics;

namespace wgmulti
{
  public class Program
  {
    public static void Main(string[] args)
    {

      if (!Arguments.ParseArguments(args))
        return;

      Log.Info("#####################################################");
      Log.Info("#                                                   #");
      Log.Info("#      Wgmulti.exe for WebGrab+Plus by Harry_GG     #");
      Log.Info("#                                                   #");
      Log.Info("#####################################################");
      Log.Info($"System: {Environment.OSVersion.Platform}");
      Log.Info($"Working Directory: {Directory.GetCurrentDirectory()}");
      Log.Info($"Config Directory: {Arguments.configDir}");
      Log.Info($"Grabbing Temp Directory: {Arguments.grabingTempFolder}");
      Log.Info($"Path to WebGrab+Plus.exe: {Arguments.wgPath}");

      if (!File.Exists(Arguments.wgPath))
      {
        Log.Error("WebGrab+Plus.exe does not exist! Exiting.");
        return;
      }

      if (Arguments.IsLinux())
        if (!VerifyWebGrabExe())
          return;

      Report.wgVersionInfo = FileVersionInfo.GetVersionInfo(Arguments.wgPath).ProductVersion;
      Log.Info($"{Arguments.wgexe}: {Report.wgVersionInfo}");
      Report.wgmultiVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
      Report.wgmultiBuildDate = new DateTime(2000, 1, 1).AddDays(Report.wgmultiVersion.Build).AddSeconds(Report.wgmultiVersion.Revision * 2);
      Log.Info($"wgmulti.exe version: {Report.wgmultiVersion} built on {Report.wgmultiBuildDate}");

      if (Arguments.useJsonConfig)
      {
        Log.Info($"Use JSON config file: {Arguments.useJsonConfig}");
        Log.Info($"JsonConfigFileName: {Arguments.jsonConfigFileName}");
      }
      else
      {
        Log.Info($"ConvertXmlConfigToJson: {Arguments.exportJsonConfig}");
      }

      Log.Info($"MaxAsyncProcesses: {Arguments.maxAsyncProcesses}");
      Log.Info($"CombineLogFiles: {Arguments.combineLogFiles}");
      Log.Info($"ConvertTimesToLocal: {Arguments.convertTimesToLocal}");
      Log.Info($"GenerateResultsReport: {Arguments.generateReport}");
      Log.Info($"SaveStandaloneGuides: {Arguments.saveStandaloneGuides}");
      Log.Info($"ShowConsole: {Arguments.showConsole}");
      Log.Line();
      Log.Info($"Execution started at: {DateTime.Now}");

      Application.Run();

      Log.Line();
      Log.Info($"Total channels: {Report.totalChannels}");
      if (Report.totalChannels > 0)
      {
        Log.Info($"With EPG: {Report.channelsWithEpg}");
        Log.Info($"Without EPG: {Report.channelsWithoutEpg}");
        Log.Info($"EPG size: {Report.fileSize}");
        Log.Info($"EPG md5 hash: {Report.md5hash}");
        if (Arguments.generateReport)
        {
          Log.Info($"Report saved to: {Arguments.reportFilePath}");
          Log.Info($"HTML report saved to: {Arguments.reportFilePath}");
        }

        // Output names of channels with no EPG
        var n = 0;
        Log.Info("Channels with no EPG:");
        Report.emptyChannels.ForEach(channel => { Log.Info($"{++n}. {channel.name}"); });
        if (n == 0)
          Log.Info("None!");
      }
      Log.Line();
    }

    static bool VerifyWebGrabExe()
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
