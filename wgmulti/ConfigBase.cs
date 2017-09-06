using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace wgmulti
{
  public class Settings
  {
    public String filename { get; set; }
    public Proxy proxy { get; set; }
    //public List<Credentials> credentials { get; set; }
    public String mode { get; set; }
    public String useragent { get; set; }
    public Boolean logging { get; set; }
    public Skip skip { get; set; }
    public Timespan timespan { get; set; }
    public String update { get; set; }
    public Retry retry { get; set; }
    public PostProcess postprocess { get; set; }
    public List<Channel> channels { get; set; }
  }
}
