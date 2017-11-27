using System;
using System.Xml.Serialization;
using System.Runtime.Serialization;

namespace wgmulti
{

  [DataContract()]
  public class Proxy
  {
    [XmlAttribute(), DataMember(Order = 1)]
    public String user { get; set; }

    [XmlAttribute(), DataMember(Order = 2)]
    public String password { get; set; }

    [DataMember(Order = 3), XmlText]
    public String server { get; set; }

    public Proxy()
    {
    }
  }


  //public class Credentials
  //{
  //  public String user = "";
  //  public String password = "";
  //  public String site = "";
  //  public Credentials() { }
  //}
}
