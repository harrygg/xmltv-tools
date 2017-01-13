using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace xmltv_program_copy
{
  class Program
  {
    static String inputXml = "epg.xml";
    static String outputXml = "epg_copy.xml";
    static String configXml = "";
    static bool unifyIds = true;

    static void Main(string[] args)
    {
      Console.WriteLine("#####################################################");
      Console.WriteLine("#         xmltv_program_copy by Harry_GG            #");
      Console.WriteLine("#---------------------------------------------------#");

      inputXml = (args.Length > 0) ? args[0] : inputXml;
      Console.WriteLine("Input EPG file: {0}", inputXml);

      outputXml = (args.Length > 1) ? args[1] : inputXml;
      Console.WriteLine("Output EPG file: {0}", outputXml);

      configXml = (args.Length > 2) ? args[2] : configXml;
      if (configXml != "")
      {
        Console.WriteLine("Using config file: {0}", configXml);
      }
      else
      {
        Console.WriteLine("No config file specified.");
        return;
      }

      XDocument xmlContent;
      try
      {
        xmlContent = XDocument.Load(configXml);
      }
      catch (Exception ex)
      {
        Console.WriteLine("Unable to read config file");
        Console.WriteLine(ex.ToString());
        return;
      }

      ///Get list of channels that will be copied
      var channelsToCopy = (from c in xmlContent.Element("root").Element("channels").Elements("channel") select c).ToList();

      ///Get the elements that will be removed from channel and programme tags
      var el = xmlContent.Element("root").Element("elements").Element("remove");
      var globalElementsToRemove = el != null ? el.Value.Split(',').ToList() : new List<string>();

      ///Get the elements that will be removed from channel and programme tags
      var globalElementsToKeep = new List<string>() { "display-name", "title" }; //Unremovable elements
      el = xmlContent.Element("root").Element("elements").Element("keep");
      if (el != null)
        globalElementsToKeep.AddRange(el.Value.Split(',').ToList());

      ///Get the elements that will be added to the channel tag
      el = xmlContent.Element("root").Element("elements").Element("add");
      var globalElementsToAdd = new List<XElement>();
      if (el != null)
        globalElementsToAdd = el.Descendants().ToList();

      ///Read the input EPG
      try
      {
          xmlContent = GetInputDocument(inputXml);
      }
      catch (Exception ex)
      {
        Console.WriteLine("Unable to read guide");
        Console.WriteLine(ex.ToString());
        return;
      }

      //Output document
      var tv = new XElement("tv");
      var eChannels = new List<XElement>();
      var eProgrammes = new List<XElement>();
      var epg = new XDocument(new XDeclaration("1.0", "utf-8", null), tv);

      foreach (var channelToCopy in channelsToCopy)
      {
        var name = channelToCopy.Attribute("name").Value;
        var attr = channelToCopy.Attribute("display-name");
        var newName = (attr != null && attr.Value != "") ? attr.Value : name; 

        //Add local rules
        el = channelToCopy.Element("keep");
        var localElementsToKeep = el != null ? el.Value.Split(',').ToList() : new List<String>();
        localElementsToKeep.AddRange(globalElementsToKeep);

        el = channelToCopy.Element("remove");
        var localElementsToRemove = el != null? el.Value.Split(',').ToList() : new List<String>();
        localElementsToRemove.AddRange(globalElementsToRemove);
        var removeAll = localElementsToRemove.Contains("all");

        el = channelToCopy.Element("add");
        var localElementsToAdd = el != null ? el.Descendants().ToList() : new List<XElement>();
        localElementsToAdd.AddRange(globalElementsToAdd);

        XElement channel = null; 
        try
        {
          channel = (from c in xmlContent.Elements("tv").Elements("channel")
                         where c.Element("display-name").Value == name
                         select c).First();
        } catch {
          Console.WriteLine("Channel with name \"{0}\" was not found in the guide", name);
        }

        if (channel != null)
        { 
          var programmes = (from c in xmlContent.Elements("tv").Elements("programme")
            where c.Attribute("channel").Value == channel.Attribute("id").Value select c).ToList();

          if (programmes.Count() > 0)
          {
            ///Modify display-name if new value is provided
            var channelName = channel.Element("display-name").Value;
            if (channelName != newName)
            {
              channel.Element("display-name").Value = newName;
              Console.WriteLine("Modifying channel name. Replaced \"{0}\" with \"{1}\"", channelName, newName);
              channelName = newName;
            }

            ///Unify display-name and channel id
            if (unifyIds)
            {
              if (channel.Attribute("id").Value != channelName)
                channel.Attribute("id").Value = channelName;
              programmes.ForEach(p => p.Attribute("channel").Value = channelName);
            }

            ///Remove elements from channel node
            channel.Descendants().Where(c => (removeAll || localElementsToRemove.Contains(c.Name.LocalName))
                && !localElementsToKeep.Contains(c.Name.LocalName)).Remove();

            ///Remove elements from programmes node
            programmes.ForEach(p => RemoveElements(ref p, localElementsToRemove, localElementsToKeep));

            ///Add elements to channel tag
            localElementsToAdd.ForEach(c => channel.Add(c));

            localElementsToRemove.Clear();
            localElementsToKeep.Clear();
            localElementsToAdd.Clear();

            Console.WriteLine("Adding channel {0} with {1} programmes", name, programmes.Count);
            eChannels.Add(channel);
            eProgrammes.AddRange(programmes);
          }
          else
          {
            Console.WriteLine("Channel {0} has no programmes and will be skipped", name);
          }
        }
        else
          Console.WriteLine("Channel {0} was not found in guide", name);
      }

      tv.Add(eChannels.ToArray());
      tv.Add(eProgrammes.ToArray());
      epg.Save(outputXml);
    }

    private static XDocument GetInputDocument(string file)
    {
      if (file.StartsWith("http") || file.StartsWith("ftp"))
      {
        Console.WriteLine("Starting download...");
        var name = DownloadFile(file);
        if (name == null)
          Console.WriteLine("Unable to download file {0}. Please download manually!", name);
        else
        {
          Console.WriteLine("\nFile downloaded! Saved to {0}", name);
          file = name;
        }
      }


      if (file.EndsWith("gz"))
      {
        Console.WriteLine("Starting decompress...");
        var name = Decompress(file);
        if (name == null)
          Console.WriteLine("Unable to decompress {0}. Please unzip manually.", file);
        else
        {
          file = name;
          Console.WriteLine("File decompressed to {0}", name);
        }
      }
      return XDocument.Load(file);
    }

    private static String DownloadFile(string file)
    {
      try
      { 
        var fileName = Path.GetFileName(file);

        WebClient client = new WebClient();
        client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(client_DownloadProgressChanged);
        //client.DownloadFileCompleted += new AsyncCompletedEventHandler(client_DownloadCompleted);
        client.DownloadFileAsync(new Uri(file), fileName);

        while (client.IsBusy)
        {
          System.Threading.Thread.Sleep(100);
        }

        return fileName;
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.ToString());
        return null;
      }
    }


    static void client_DownloadCompleted(object sender, AsyncCompletedEventArgs e)
    {
      n = "";
      Console.WriteLine("\nFile downloaded!");
    }
    static string n = "+";
    static void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
      Console.Write(n);
    }

    private static string Decompress(string fileName)
    {
      String output = null;
      using (FileStream inStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
      {
        using (GZipStream zipStream = new GZipStream(inStream, CompressionMode.Decompress))
        {
          using (FileStream outStream = new FileStream(fileName.Replace(".gz", ""), FileMode.Create, FileAccess.Write))
          {
            byte[] tempBytes = new byte[4096];
            int i;
            while ((i = zipStream.Read(tempBytes, 0, tempBytes.Length)) != 0)
            {
              outStream.Write(tempBytes, 0, i);
            }
          }
        }
      }
      output = fileName.Replace(".gz", "");
      return output;
    }

    static void RemoveElements(ref XElement programme, List<String> elementsToRemove, List<String> elementsToKeep)
    {
      programme.Descendants().Where(x => (elementsToRemove.Contains("all") || elementsToRemove.Contains(x.Name.LocalName)) && !elementsToKeep.Contains(x.Name.LocalName)).Remove();
    }
  }
}
