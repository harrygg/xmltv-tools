using System;
using System.IO;
using System.Text;

namespace wgmulti
{
  public class Log
  {
    static String datetimeFormat = "yyyy-MM-dd HH:mm:ss.fff";
    static String filename = "wgmulti.log.txt";
    static readonly TextWriter tw;
    /// <summary>
    /// Initialize a new instance of SimpleLogger class.
    /// Log file will be created automatically if not yet exists, else it can be either a fresh new file or append to the existing file.
    /// Default is create a fresh new Log file.
    /// </summary>
    /// <param name="append">True to append to existing Log file, False to overwrite and create new Log file</param>
    static Log()
    {
      if (File.Exists(Log.filename))
        File.Delete(Log.filename);
      FileStream fs = File.Create(filename);
      fs.Close();
      tw = TextWriter.Synchronized(File.AppendText(filename));
    }

    static public void Debug(String text, bool prependDate = true)
    {
      WriteFormattedLog(LogLevel.DEBUG, text, prependDate);
    }

    static public void Error(String text, bool prependDate = true)
    {
      WriteFormattedLog(LogLevel.ERROR, text, prependDate);
    }

    static public void Info(String text, bool prependDate = true)
    {
      WriteFormattedLog(LogLevel.INFO, text, prependDate);
    }

    static public void Warning(String text, bool prependDate = true)
    {
      WriteFormattedLog(LogLevel.WARN, text, prependDate);
    }

    /// <summary>
    /// Format a Log message based on Log level
    /// </summary>
    /// <param name="level">Log level</param>
    /// <param name="text">Log message</param>
    static void WriteFormattedLog(LogLevel level, String text, bool prependDate = true)
    {
      var now = prependDate ? DateTime.Now.ToString(datetimeFormat) + " " : "";
      var pretext = now + "[" + level.ToString() + "] ";

      WriteLine(pretext + text);
    }

    /// <summary>
    /// Write a line of formatted Log message into a Log file
    /// </summary>
    /// <param name="text">Formatted Log message</param>
    /// <param name="append">True to append, False to overwrite the file</param>
    /// <exception cref="System.IO.IOException"></exception>
    static void WriteLine(string text, bool append = true)
    {
      try
      {
        if (String.IsNullOrEmpty(text))
          return;

        Console.WriteLine(text);

        tw.Write(text + "\n");
        tw.Flush();
        //using (StreamWriter Writer = new StreamWriter(filename, append, Encoding.UTF8))
        //Writer.WriteLine(text);
      }
      catch
      {
        throw;
      }
    }

    public override string ToString()
    {
      return filename;
    }

    public static void Line()
    {
      Log.Info("-----------------------------------------------------");
    }
  }

  /// <summary>
  /// Supported Log level
  /// </summary>
  [Flags]
  public enum LogLevel
  {
    ERROR,
    WARN,
    INFO,
    DEBUG
  }
}
