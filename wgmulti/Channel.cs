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
        return (siteinis != null && siteinis.Count > 0) ? 
          siteinis[0].name : ""; }

      set {
        if (siteinis == null)
          siteinis = new List<SiteIni>();
        if (siteinis.Count > 0)
          siteinis[0].name = value;
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
      get { return (siteinis != null && siteinis.Count > 0) ? siteinis[0].site_id : ""; }
      set
      {
        if (siteinis == null)
          siteinis = new List<SiteIni>();
        if (siteinis.Count > 0)
          siteinis[0].site_id = value;
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

    [XmlIgnore]
    public int siteiniIndex = 0;

    [DataMember(EmitDefaultValue = false, Order = 5), XmlIgnore]
    public int? offset { get; set; }

    [XmlAttribute("offset")]
    public String Offset
    {
      get { return offset.HasValue ? offset.ToString() : null; }
      set
      {
        if (!String.IsNullOrEmpty(value))
        {
          int i;
          if (int.TryParse(value, out i))
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

    //Channel is deactivated if during grabbing it has no more siteinis
    [XmlIgnore]
    public Boolean active = true;
    
    [DataMember(EmitDefaultValue = false, Order = 11), XmlIgnore]
    public List<Channel> timeshifted { get; set; }

    [IgnoreDataMember, XmlIgnore]
    public Xmltv xmltv;

    public Channel()
    {
    }

    [OnDeserialized]
    void OnDeserialized(StreamingContext c)
    {
      active = true;
      enabled = (enabled == null || enabled == true) ? true : false;
      xmltv = new Xmltv();
    }

    public SiteIni GetActiveSiteIni()
    {
      try
      {
        return siteinis[siteiniIndex];
      }
      catch
      {
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
        Log.Error(String.Format("#{0} | {1}", Application.currentSiteiniIndex + 1, ex.ToString()));
        return false;
      }
    }

    //public void CopyProgramsXml(Xmltv _xmltv, String channel_id = null)
    //{
    //  // If offset is null and convert to local times is true, then keep the offset null
    //  offset = (offset == null && !Arguments.convertTimesToLocal) ? 0 : offset;
    //  // If channel_id is null we are copying all programms from master channel
    //  if (channel_id == null) 
    //    channel_id = _xmltv.programmes[0].Attribute("channel").Value;

    //  _xmltv.programmes.ForEach(program => {
    //    var channel_name = program.Attribute("channel").Value;
    //    var start_time = program.Attribute("start").Value;
    //    var end_time = program.Attribute("stop").Value;

    //    if (channel_name == channel_id && Program.masterConfig.Dates.Contains(start_time.Substring(0, 8)))
    //    {
    //      program = new XElement(program); // Clone to a copy of the object
    //      program.Attribute("channel").Value = xmltv_id; // Rename all 'channel' tags to reflect the new channel
    //      program.Attribute("start").Value = Utils.AddOffset(start_time, offset); //If offset is null we will apply local time shift
    //      program.Attribute("stop").Value = Utils.AddOffset(end_time, offset);

    //      // Copy only the titles
    //      if (offset != null && offset > 0) // if it's an offset channel remove all tags other then the 'title'
    //      {
    //        foreach (var el in program.Elements())
    //        {
    //          if (el.Name != "title")
    //            el.Remove();
    //        }
    //      }
    //      xmltv.programmes.Add(program);
    //    }
    //  });
    //}

    

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
