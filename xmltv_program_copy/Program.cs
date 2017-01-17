using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Xml.Linq;

namespace xmltv_program_copy
{
  class Program
  {
    static String inputXml = "epg.xml";
    static String outputXml = "epg_copy.xml";
    static String configXml = "";
    static XDocument inputEpg = null;
    static Epg outputEpg = new Epg();
    static Config config = null;
    static bool unifyIds = true;

    static void Main(string[] args)
    {
      inputXml = (args.Length > 0) ? args[0] : inputXml;
      outputXml = (args.Length > 1) ? args[1] : inputXml;
      configXml = (args.Length > 2) ? args[2] : configXml;

      Console.WriteLine("#####################################################");
      Console.WriteLine("#         xmltv_program_copy by Harry_GG            #");
      Console.WriteLine("#---------------------------------------------------#");   
      Console.WriteLine("Input EPG file: {0}", inputXml);
      Console.WriteLine("Output EPG file: {0}", outputXml);

      if (configXml != "")
      {
        Console.WriteLine("Using config file: {0}", configXml);
      }
      else
      {
        Console.WriteLine("No config file specified.");
        return;
      }

      config = new Config(configXml);
      if (config.channels == null)
        return;

      ///Read the input EPG
      inputEpg = GetInputDocument(inputXml);
      if (inputEpg == null)
        return;

      //Output document
      config.channels.ForEach(channel => CopyChannel(channel));
      outputEpg.Save(outputXml);
    }

    static void CopyChannel(XElement channelToCopy)
    {
      var name = channelToCopy.Attribute("name").Value;
      var attr = channelToCopy.Attribute("display-name");
      var newName = (attr != null && attr.Value != "") ? attr.Value : name;

      //Get local modification rules and add global ones 
      var elementsToKeep = GetLocalRules(channelToCopy, "keep");
      elementsToKeep.AddRange(config.keepElements);

      var elementsToRemove = GetLocalRules(channelToCopy, "remove");
      elementsToRemove.AddRange(config.removeElements);

      var temp = channelToCopy.Element("add");
      var elementsToAdd = temp != null ? temp.Descendants().ToList() : new List<XElement>();
      elementsToAdd.AddRange(config.addElements);

      var channel = GetChannel(name);
      if (channel != null)
      {
        var programmes = GetProgrammes(channel.Attribute("id").Value);
        if (programmes != null)
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
          channel.Descendants()
            .Where(c => 
              !elementsToKeep.Contains(c.Name.LocalName) && 
              (elementsToRemove.Contains("all") || elementsToRemove.Contains(c.Name.LocalName))
              ).Remove();

          ///Remove elements from programmes node
          programmes.ForEach(p => RemoveElements(ref p, elementsToRemove, elementsToKeep));

          ///Add elements to channel tag
          elementsToAdd.ForEach(c => channel.Add(c));
          elementsToRemove.Clear();
          elementsToKeep.Clear();
          elementsToAdd.Clear();

          Console.WriteLine("Adding channel {0} with {1} programmes", name, programmes.Count);
          outputEpg.channels.Add(channel);
          outputEpg.programmes.AddRange(programmes);
        }
      }
      else
        Console.WriteLine("Channel {0} was not found in guide", name);
    }

    static XElement GetChannel(String name)
    {
      try
      {
        return (from c in inputEpg.Elements("tv").Elements("channel")
                   where c.Element("display-name").Value == name select c).First();
      }
      catch
      {
        Console.WriteLine("Channel with name \"{0}\" was not found in the guide", name);
        return null;
      }
    }

    static List<XElement> GetProgrammes(String channelId)
    {
      try
      {
        return (from c in inputEpg.Elements("tv").Elements("programme")
                where c.Attribute("channel").Value == channelId
                select c).ToList();
      }
      catch (Exception ex)
      {
        Console.WriteLine("Error getting programme for channel {0}", channelId);
        Console.WriteLine(ex.ToString());
        return null;
      }
    }

    static List<String> GetLocalRules(XElement channelToCopy, String elementName)
    {
      XElement temp = channelToCopy.Element(elementName);
      var elements = temp != null ? temp.Value.Split(',').ToList() : new List<String>();
      return elements;
    }

    static XDocument GetInputDocument(string file)
    {
      try
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
      catch (Exception ex)
      {
        Console.WriteLine(ex.ToString());
        return null;
      }
    }

    static String DownloadFile(string file)
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
