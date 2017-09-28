using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace wgmulti
{
  public class Report
  {
    public Report(){}

    public String fileName = "wgmulti.results.json";
    public List<String> channels = new List<String>();
    public List<ActiveChannel> activeChannels = new List<ActiveChannel>();
    public List<String> emptyChannels = new List<String>();
    public int total = 0;
    public int missing = 0;
    public String generationTime = String.Empty;
    public String generatedOn = String.Empty;
    public String fileSize = String.Empty;
    public String md5hash = String.Empty;

    public void Save(String path)
    {
      var file = Path.Combine(path, fileName);
      if (this.total == 0)
        this.total = channels.Count;
      if (this.missing == 0)
        this.missing = emptyChannels.Count;

      var serializer = new JavaScriptSerializer();
      var json = serializer.Serialize(this);
      File.WriteAllText(file, json);
      Console.WriteLine("Report saved to {0}", file);
    }

    public ActiveChannel GetChannel(String name)
    {
      try
      {
        return activeChannels.First(channel => channel.name.Equals(name));
      }
      catch(Exception)
      {
        return null;
      }
    }
  }

  public class ActiveChannel
  {
    public String name = String.Empty;
    public String siteini = String.Empty;
    public int siteiniIndex = 0;
    public bool hasEpg = false;
    public String firstShowStartsAt = String.Empty;
    public String lastShowStartsAt = String.Empty;
    public int programsCount = 0;

    public ActiveChannel()
    {
    }

    public ActiveChannel(string name)
    {
      this.name = name;
    }
  }
}
