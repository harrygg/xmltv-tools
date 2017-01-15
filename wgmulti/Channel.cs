using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace wgmulti
{
  public class Channel
  {
    public String updateType = "";
    public String site = "";
    public String siteId = "";
    public String xmltvId = "";
    public String name = "";
    public String offset = "";
    public String sameAs = "";

    public Channel(String site, String name, String siteId, String xmltvId, String offset, String sameAs, String updateType = "i")
    {
      this.site = site;
      this.name = name;
      this.siteId = siteId;
      this.xmltvId = xmltvId;
      this.offset = offset;
      this.sameAs = sameAs;
      this.updateType = updateType;
    }

    public XElement ToXElement()
    {
      var xEl = new XElement("channel", name, 
        new XAttribute("site", site), 
        new XAttribute("xmltv_id", xmltvId),
        new XAttribute("site_id", siteId), 
        new XAttribute("update", updateType)
      );

      if (offset != "")
        xEl.Add(new XAttribute("offset", offset));
      if (sameAs != "")
        xEl.Add(new XAttribute("same_as", sameAs)); 

      return xEl;
    }
  }
}
