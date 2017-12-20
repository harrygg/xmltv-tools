using System;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace wgmulti
{
  [DataContract(Namespace = "")]
  public class Channel
  {
    [DataMember(Order = 1, IsRequired = true), XmlText]
    public String name { get; set; }

    [DataMember(Order = 2, IsRequired = true), XmlAttribute]
    public String xmltv_id { get; set; }

    [DataMember(EmitDefaultValue = false, Order = 3), XmlAttribute]
    public String update { get; set; }

    [IgnoreDataMember, XmlAttribute]
    public String site
    {
      get
      {
        if (siteinis != null && siteinis.Count > 0)
          return siteiniIndex == -1 ? siteinis[siteiniIndex + 1].name : siteinis[siteiniIndex].name;
        return "";
      }

      set {
        if (siteinis == null)
          siteinis = new List<SiteIni>();
        if (siteinis.Count > 0)
        {
          var idx = siteiniIndex == -1 ? siteiniIndex + 1 : siteiniIndex;
          siteinis[idx].name = value;
        }
        else
        {
          var siteini = new SiteIni(value);
          siteinis.Add(siteini);
        }
      }
    }

    [IgnoreDataMember, XmlAttribute()]
    public String site_id
    {
      get
      {
        var idx = siteiniIndex == -1 ? 0 : siteiniIndex;
        return (siteinis != null && siteinis.Count > 0) ? siteinis[idx].site_id : "";
      }
      set
      {
        if (siteinis == null)
          siteinis = new List<SiteIni>();
        var idx = siteiniIndex == -1 ? 0 : siteiniIndex;
        if (siteinis.Count > 0)
          siteinis[idx].site_id = value;
        else
        {
          var siteini = new SiteIni();
          siteini.site_id = value;
          siteinis.Add(siteini);
        }
      }
    }

    [DataMember(EmitDefaultValue = false, Order = 4), XmlIgnore]
    public List<SiteIni> siteinis { get; set; }

    [IgnoreDataMember, XmlIgnore]
    public bool HasSiteinis
    {
      get { return siteinis != null && siteinis.Count > 0; }
    }

    [DataMember(EmitDefaultValue = false, Order = 5), XmlIgnore]
    public double? offset { get; set; }

    [XmlAttribute("offset")]
    public String Offset
    {
      get { return offset.HasValue ? offset.ToString() : null; }
      set
      {
        if (!String.IsNullOrEmpty(value))
        {
					double i;
          if (double.TryParse(value, out i))
            offset = i;
        }
      }
    }

    [DataMember(EmitDefaultValue = false, Order = 7), XmlAttribute]
    public String same_as { get; set; }

    [DataMember(EmitDefaultValue = false, Order = 8), XmlAttribute]
    public String period { get; set; }

    [DataMember(EmitDefaultValue = false, Order = 9), XmlAttribute]
    public String include { get; set; }

    [DataMember(EmitDefaultValue = false, Order = 10), XmlAttribute]
    public String exclude { get; set; }

    [DataMember(Name = "enabled", Order = 4, EmitDefaultValue = false), XmlIgnore]
    public bool? enabled = true;

    [IgnoreDataMember, XmlIgnore]
    public bool Enabled
    {
      get { return enabled == null || enabled == true; }
      set { enabled = value; }
    }

    [XmlIgnore]
    public int siteiniIndex = -1;

    //Channel is deactivated if during grabbing it has no more siteinis
    [XmlIgnore]
    public Boolean active = true;
    
    [DataMember(EmitDefaultValue = false, Order = 11), XmlIgnore]
    public List<Channel> timeshifts { get; set; }

    [IgnoreDataMember, XmlIgnore]
    public Xmltv xmltv = new Xmltv();

    [IgnoreDataMember, XmlIgnore]
    public Channel parent;

    public bool IsTimeshifted
    {
      get { return parent != null; }
    }

    public bool HasChildren
    {
      get { return timeshifts != null && timeshifts.Count > 0; }
    }

    public bool HasPrograms
    {
      get { return xmltv != null && xmltv.programmes.Count > 0; }
    }

    public Channel()
    {
    }

    /// <summary>
    /// Set default channel values
    /// </summary>
    /// <param name="c"></param>
    [OnDeserialized]
    void OnDeserialized(StreamingContext c)
    {
      active = true;
      siteiniIndex = -1;
      enabled = (enabled == null || enabled == true) ? true : false;
      xmltv = new Xmltv();
    }

    public void SetActiveSiteIni()
    {
      bool found = false;
      for (var i = siteiniIndex + 1; i < siteinis.Count; i++)
      {
        if (!found && siteinis[i].Enabled)
        {
          siteiniIndex = i;
          found = true;
          String.Format("Channel '{0}' has no programs. Using to grabber {1}",
              name, GetActiveSiteIni().name.ToUpper());
        }
      }

      if (!found)
      {
        Log.Info(String.Format("No alternative siteinis found for '{0}'. Channel deactivated.", name));
        active = false;
      }
    }

    public SiteIni GetActiveSiteIni()
    {
      try
      {
        var idx = siteiniIndex == -1 ? 0 : siteiniIndex;
        return siteinis[idx];
      }
      catch
      {
        Log.Error("GetActiveSiteIni() error for channel '" + name + "'! siteiniIndex: " + siteiniIndex);
        return new SiteIni();
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

        if (Arguments.removeExtraChannelAttributes)
        {
          foreach (var el in _xmlChannel.Elements())
          {
            if (el.Name == "url" || el.Name == "icon")
              el.Remove();
          }
        }
        xmltv.channels.Add(_xmlChannel);
        return true;
      }
      catch (Exception ex)
      {
        Log.Error(String.Format("#{0} | {1}", Application.grabbingRound + 1, ex.ToString()));
        return false;
      }
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
