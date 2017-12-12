using System;
using System.Xml.Serialization;
using System.Runtime.Serialization;

namespace wgmulti
{
  [DataContract]
  public class Retry
  {
    [DataMember(Name = "timeOut", Order = 5), XmlAttribute("time-out")]
    public int timeOut = 10;
 
    [DataMember(Name = "channelDelay", Order = 2), XmlAttribute("channel-delay")]
    public int channelDelay = 0;
  
    [DataMember(Name = "indexDelay", Order = 3), XmlAttribute("index-delay")]
    public int indexDelay = 0; 
 
    [DataMember(Name = "showDelay", Order = 4), XmlAttribute("show-delay")]
    public int showDelay = 0;

    [DataMember(Order = 1), XmlText]
    public int attempts = 6;

    public Retry()
    {
    }

    public override String ToString()
    {
      return 
        String.Format("Attempts:{0}, time-out:{1}, channel-delay:{2}, index-delay:{3}, show-delay:{4}", attempts, timeOut, channelDelay, indexDelay, showDelay, attempts);
    }
  }
}
