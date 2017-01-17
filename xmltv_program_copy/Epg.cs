using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace xmltv_program_copy
{
  public class Epg
  {
    public XElement tv = new XElement("tv");
    public List<XElement> channels = new List<XElement>();
    public List<XElement> programmes = new List<XElement>();
    public XDocument doc = new XDocument(new XDeclaration("1.0", "utf-8", null));
    String outputFile;

    public Epg()
    {
      doc.Add(tv);
    }
    public void Save(String outputFile)
    {
      tv.Add(channels.ToArray());
      tv.Add(programmes.ToArray());
      doc.Save(outputFile);
    }

  }
}
