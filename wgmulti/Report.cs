using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace wgmulti
{
  public class Report
  {
    public Report(){}

    String fileName = "wgmulti.results.json";
    public List<String> channels = new List<String>();
    public List<String> emptyChannels = new List<String>();
    public int total = 0;
    public int missing = 0;
    public String generationTime = String.Empty;

    public void Save(String path)
    {
      var file = Path.Combine(path, fileName);
      this.total = channels.Count;
      this.missing = emptyChannels.Count;

      var serializer = new JavaScriptSerializer();
      var json = serializer.Serialize(this);
      File.WriteAllText(file, json);
      Console.WriteLine("Report saved to {0}", file);
    }
  }
}
