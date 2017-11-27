using System;
using System.Runtime.Serialization;

namespace wgmulti
{

  [DataContract]
  public class Period
  {
    [DataMember(Name = "days")]
    public int days = 0;

    [DataMember(Name = "time")]
    public String time = "";

    public Period()
    {
    }

    public override String ToString()
    {
      return days.ToString();
    }
  }

}
