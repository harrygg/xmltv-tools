using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace wgmulti
{
  public class Channel
  {
    public String update { get; set; }
    public String name { get; set; }
    public String xmltv_id { get; set; }
    public List<SiteIni> siteinis { get; set; }
    public SiteIni siteini;
    public int siteiniIndex = 0;
    public int? offset { get; set; }
    public String same_as { get; set; }
    public String period { get; set; }
    public String include { get; set; }
    public String exclude { get; set; }
    public String site_channel { get; set; }
    public Boolean enabled { get; set; }
    public Boolean active = true; //Channel is deactivated if during grabbing it has no more siteinis
    public List<Channel> offset_channels { get; set; }
    public XElement xml;
    public Xmltv xmltv = new Xmltv();
    public String url;
    public String icon;

    public Channel()
    {
      enabled = true;
    }

    public Channel(String name, String xmltvId, SiteIni siteIni, String updateType = "")
    {
      siteini = siteIni;
      if (siteinis == null)
      { 
        siteinis = new List<SiteIni>();
        siteinis.Add(siteini);
      }
      this.name = name;
      xmltv_id = xmltvId;
      update = updateType;
      enabled = true;
    }

    public XElement ToXElement()
    {

      var xEl = new XElement("channel", name, 
        new XAttribute("xmltv_id", xmltv_id),
        new XAttribute("update", update ?? "i")
      );

      siteini = GetActiveSiteIni();
      if (siteini != null)
      {
        xEl.Add(new XAttribute("site", siteini.name));
        xEl.Add(new XAttribute("site_id", siteini.site_id));
      }
      if (offset != null)
        xEl.Add(new XAttribute("offset", offset));
      if (same_as != null)
        xEl.Add(new XAttribute("same_as", same_as)); 
      if (period != null)
        xEl.Add(new XAttribute("period", period));
      if (include != null)
        xEl.Add(new XAttribute("include", include));
      if (exclude != null)
        xEl.Add(new XAttribute("exclude", exclude));
      if (site_channel != null)
        xEl.Add(new XAttribute("site_channel", site_channel));

      return xEl;
    }

    public SiteIni GetActiveSiteIni()
    {
      try
      {
        return siteinis[siteiniIndex];
      }
      catch
      {
        //Console.WriteLine("ERROR!!! {0}", name);
        //Console.WriteLine(ex.ToString());
        return null;
      }
    }


    public bool CopyChannelXml(Xmltv _xmltv, String channel_id = null)
    {
      try
      {
        // Create a new XElement to force copy by value
        XElement _xmlChannel = null;
        if (channel_id != null)
        {
          try
          {
            // If channel is not found catch the exception
            _xmlChannel = new XElement(_xmltv.channels.Where(c => c.Attribute("id").Value == channel_id).First());
          } 
          catch (Exception)
          {
            return false;
          }
          
        }
        else
          _xmlChannel = new XElement(_xmltv.channels[0]);

        _xmlChannel.Attribute("id").Value = xmltv_id;
        _xmlChannel.Element("display-name").Value = name;

        foreach (var el in _xmlChannel.Elements())
        {
          if (el.Name == "url" || el.Name == "icon")
            el.Remove();
        }
        xmltv.channels.Add(_xmlChannel);
        return true;
      }
      catch (Exception ex)
      {
        Log.Error(String.Format("#{0} | {1}", Program.currentSiteiniIndex + 1, ex.ToString()));
        return false;
      }
    }

    public void CopyProgramsXml(Xmltv _xmltv, String channel_id = null)
    {
      // If offset is null and convert to local times is true, then keep the offset null
      offset = (offset == null && !Arguments.convertTimesToLocal) ? 0 : offset;
      // If channel_id is null we are copying all programms from master channel
      if (channel_id == null) 
        channel_id = _xmltv.programmes[0].Attribute("channel").Value;

      _xmltv.programmes.ForEach(program => {
        var channel_name = program.Attribute("channel").Value;
        var start_time = program.Attribute("start").Value;
        var end_time = program.Attribute("stop").Value;

        if (channel_name == channel_id && Program.masterConfig.Dates.Contains(start_time.Substring(0, 8)))
        {
          program = new XElement(program); // Clone to a copy of the object
          program.Attribute("channel").Value = xmltv_id; // Rename all 'channel' tags to reflect the new channel
          program.Attribute("start").Value = Utils.AddOffset(start_time, offset); //If offset is null we will apply local time shift
          program.Attribute("stop").Value = Utils.AddOffset(end_time, offset);

          // Copy only the titles
          if (offset != null && offset > 0) // if it's an offset channel remove all tags other then the 'title'
          {
            foreach (var el in program.Elements())
            {
              if (el.Name != "title")
                el.Remove();
            }
          }
          xmltv.programmes.Add(program);
        }
      });
    }

    

    public override String ToString()
    {
      var output = name;
      if (GetActiveSiteIni() != null)
        output += ", " + GetActiveSiteIni().name + " (" + siteiniIndex + ")";
      output += ", " + xmltv.programmes.Count + " pr.";
      output += ", " + active.ToString();
      return output;
    }
  }
}
