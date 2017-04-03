using System;
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
    public String sameAs = null;
    public String period = null;
    public String include = null;
    public String exclude = null;
    public String site_channel = null;
    public bool active = true;
    public String siteIni = ".ini";

    public Channel(String site, String name, String siteId, String xmltvId, String updateType = "i")
    {
      this.site = site;
      this.name = name;
      this.siteId = siteId;
      this.xmltvId = xmltvId;
      this.updateType = updateType;
      this.siteIni = site + siteIni;
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
      if (sameAs != null)
        xEl.Add(new XAttribute("same_as", sameAs)); 
      if (period != null)
        xEl.Add(new XAttribute("period", period));
      if (include != null)
        xEl.Add(new XAttribute("include", include));
      if (include != null)
        xEl.Add(new XAttribute("exclude", exclude));
      if (include != null)
        xEl.Add(new XAttribute("site_channel", site_channel));

      return xEl;
    }
  }
}
