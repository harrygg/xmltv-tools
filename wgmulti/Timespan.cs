using System;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace wgmulti
{
  [DataContract]
  public class Period
  {
    [DataMember(Order = 1), XmlText]
    public int days = 0;

    [DataMember(Order = 2), XmlAttribute("oneshowonly")]
    public String time = "";

    [DataMember(Order = 3), XmlAttribute("keeppastdays")]
    public int pastdays = 0;

    public Period()
    {
    }

    public override String ToString()
    {
      return "Days: " + days.ToString() + (pastdays > 0 ? ", keep last " + pastdays.ToString() : "");
    }
  }

}
