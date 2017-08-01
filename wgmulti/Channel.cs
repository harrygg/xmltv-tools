using System;
using System.Xml.Linq;

namespace wgmulti
{
  public class Channel
  {
    public String update { get; set; }
    public String iniPath { get; set; }
    public String Site
    {
      get { return this.site ?? this.getFileName(); }
    }

    private string getFileName()
    {
      throw new NotImplementedException();
    }

    public String site { get; set; }
    public String site_id { get; set; }
    public String xmltv_id { get; set; }
    public String name { get; set; }
    public String offset { get; set; }
    public String same_as { get; set; }
    public String period { get; set; }
    public String include { get; set; }
    public String exclude { get; set; }
    public String site_channel { get; set; }
    public bool active = true;
    public String siteIni = ".ini";

    public Channel(){}
    public Channel(String site, String name, String siteId, String xmltvId, String updateType = "i")
    {
      this.site = site;
      this.name = name;
      this.site_id = siteId;
      this.xmltv_id = xmltvId;
      this.update = updateType;
      this.siteIni = site + siteIni;
    }

    public XElement ToXElement()
    {
      var xEl = new XElement("channel", name, 
        new XAttribute("site", site), 
        new XAttribute("xmltv_id", xmltv_id),
        new XAttribute("site_id", site_id), 
        new XAttribute("update", update)
      );

      if (offset != null)
        xEl.Add(new XAttribute("offset", offset));
      if (same_as != null)
        xEl.Add(new XAttribute("same_as", same_as)); 
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
