using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace wgmulti
{

  [DataContract]
  public class Decryptkey
  {

    [DataMember(Order = 1), XmlAttribute("site")]
    public String site = "";

    [DataMember(Order = 2), XmlText]
    public String key = "";

    // Parameterless constructor to prevent System.InvalidOperationException during serialization
    public Decryptkey()
    {
    }

    public Decryptkey(String name, String key)
    {
      site = name;
      this.key = key;
    }
    public override String ToString()
    {
      return $"Site: {site}, key: {key}";
    }
  }
}
