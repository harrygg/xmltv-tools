using System;
using System.Collections.Generic;
using System.IO;

namespace wgmulti
{
  public class Report
  {
    public Report()
    {
    }
    String name = "wgmultiresults.json";
    public List<String> presentIds = new List<String>();
    public List<String> missingIds = new List<String>();
    public int total = 0;
    public String generationTime = String.Empty;

    public void Save(String path)
    {
      using (var f = new StreamWriter(Path.Combine(path, name)))
      {
        f.Write("{");
        f.Write("\"total\":" + total + ",");
        var ids = String.Empty;
        if (presentIds.Count > 0)
          ids = "\"" + String.Join("\",\"", presentIds.ToArray()) + "\"";
        f.Write("\"present_ids\":["+ids+"], ");

        if (missingIds.Count > 0)
          ids = "\"" + String.Join("\",\"", missingIds.ToArray()) + "\"";
        else
          ids = ""; //reset in case they were set above

        f.Write("\"missing_ids\":[" + ids + "], ");
        f.Write("\"missing\":" + missingIds.Count + ", ");
        f.Write("\"generationTime\":\"{0}\"", generationTime);
        f.Write("}");
      }
    }
  }
}
