using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace xmltv_program_copy
{
  public class Config
  {
    public List<String> keepElements = new List<String>() { "display-name", "title" };
    public List<String> removeElements = new List<String>();
    public List<XElement> addElements = new List<XElement>();
    public List<XElement> channels = null;


    public Config(String fileName)
    {
      XDocument xmlContent;
      try
      {
        xmlContent = XDocument.Load(fileName);
      }
      catch (Exception ex)
      {
        Console.WriteLine("Unable to read config file");
        Console.WriteLine(ex.ToString());
        return;
      }

      ///Get list of channels that will be copied
      if (xmlContent.Element("root").Element("channels") != null)
        channels = (from c in xmlContent.Element("root").Element("channels").Elements("channel") select c).ToList();
      else
        channels = (from c in xmlContent.Element("root").Elements("channel") select c).ToList();

      if (channels.Count() == 0)
      {
        Console.WriteLine("Config file doesn't contain any <channel> elements. Exiting!");
        return;
      }

      var globalRules = xmlContent.Element("root").Element("globalRules");
      if (globalRules != null)
      {
        if (globalRules.Element("remove") != null)
        {
          Console.WriteLine("The following elements will be removed {0}", globalRules.Element("remove"));
          removeElements = globalRules.Element("remove").Value.Split(',').ToList();
        }

        ///Get the elements that will be removed from channel and programme tags
        if (globalRules.Element("keep") != null)
        {
          Console.WriteLine("The following elements will be kept {0}", globalRules.Element("keep"));
          keepElements.AddRange(globalRules.Element("keep").Value.Split(',').ToList());
        }

        ///Get the elements that will be added to the channel tag
        if (globalRules.Element("add") != null)
        {
          addElements = globalRules.Element("add").Descendants().ToList();
          if (addElements.Count() > 0)
          {
            Console.WriteLine("The following elements will be added:");
            Console.WriteLine(String.Join("\n", from e in addElements select e.ToString()));
          }
        }
      }
    }
  }
}
