# xmltv-tools

The XMLTV-Tools project consists of several programs for grabbing and modifying XMLTV files.

* wgmulti.exe - a tool for automated starting of multiple instances of WebGrab+Plus.exe:
* xmltv_time_modify.exe - a tool for modifying xmltv timings
* xmltv_program_copy.exe - a tool for copying channels programmes from xmltv while adding/removing certain elements 
* xmltv_merge.exe - a tool for merging xmltv files, discarding duplicated channels

## wgmulti

wgmulti.exe is a simple accelarator for WebGrab+Plus.exe that can start multiple instances of the WebGrab process and later concatenates the xml output. 

Benefits:

* Faster overal grabbing process. 
Done by splitting the WebGrab master configuration WebGrab++.config.xml file into multiple configuration files. By default (see below for details) a single configuration file is created for every site ini in the master WebGrab++.config.xml file. 
For instance, if you grab programmes for 20 channels - 10 from site1.com and 10 from site2.com, you will end up with 2 WebGrab++.config.xml with 10 channels each. 
Multiple WebGrab+Plus instances are then started in parallel for each configuration file. 
After the grabbing finsihes, the resulting xml files are merged into a single file.


* Automatically modifies programmes' timings to local time.
If your local time is +01:00 and you are grabbing a +00:00 channel then timings will be converted from:

``` <programme start="20170101083000 +0000" stop="20170101093000 +0000" channel="Channel 1"> ```  
to:  
``` <programme start="20170101093000 +0100" stop="20170101103000 +0100" channel="Channel 1"> ```



### Installation

1. Download the files wgmulti.exe and wgmulti.exe.config and place them into your WebGrab+Plus folder (It's the one where the WebGrab+Plus.exe exists).
2. Run by starting the wgmulti.exe.  
If started without arguments, the tool will load the configuration from WebGrab's default directory. On Windows systems it is %APPDATA%\ServerCare\WebGrab\, on Linux it's the current working folder.  
The configuration folder can be passed as an argument:  
``` wgmulti.exe <path-to-configuration-folder> ```

### Configuration

The wgmulti.exe.config file contains some configurable parameters:
* maxAsyncProcesses - the number of maximum instances of WebGrab+ the program can start
* configDir - default configuration directory
* showWebGrabConsole - Having this option to "True" will open a command line output window for each instance of WebGrab. 
* convertTimesToLocal - If set to "False" will leave the programmes timinings unmodified. Otherwise will convert them to the local time of the machine where wgmulti is executed.
* GroupChannelsBySiteIni - criteria for grouping channels. Defaults to true, meaning that it splits the master WebGrab++.config.xml into multiple files for each siteini. If false, WebGrab++.config.xml will be created fo a number of channels - depending on the MaxChannelsInGroup setting.
MaxChannelsInGroup - works only if GroupChannelsBySiteIni is set to false. The number of channels that should be added to each newly created WebGrab++.config.xml.


## xmltv_program_copy

A program for copying and modifying elements from an XMLTV file. 
The program can:

* Replace channel names
* Remove certain sub-elements of the &lt;channel&gt; and the &lt;programme&gt;
* Add new sub-elements i.e. &lt;icon&gt;
* Remove channels that have no associated programmes

### Installation
To use the tool you need to pass the following arguments:

``` xmltv_program_copy.exe <input-xml-file> <output-xml-file> <template-xml-file>```

Example:
xmltv_program_copy.exe epg.xml epg2.xml channels.xml

Example channels.xml template file:
```
<?xml version="1.0" encoding="utf-8"?>
<root>
  <!-- Anything put in the elements tag has global effect -->
  <globalRules>
      <!-- any xmltv element name added inside the remove tag will be removed. 'all' means remove all sub-elements. If added it will remove all sub-elements from <channel> and <programme> tags. It will leave only 'display-name' and 'title'  --> 
      <remove>all</remove>
      <!-- any xmltv element name added to the <keep> element will be preserved --> 
      <keep>sub-title,icon</keep>
      <!-- any xmltv element added to the <add> element will be added to <channel> or <programme> elements--> 
      <add>
        <url>http://xmltvtools/</url>
      </add>
  </globalRules>
  <!-- Anything added insided a channel tag has local effect -->  
  <channels>
    <!-- 
    will copy the program for channel with name "TEST 1", while renaming it to "Channel 1" 
    will keep the 'desc' element for this channel's programme
    will add 'icon' element for this channel
    -->  
    <channel name="TEST 1" display-name="Channel 1">
      <keep>desc</keep>
      <add>
        <icon src="http://logos.com/test1.png" />
      </add>
    </channel>
    <!-- 
      will copy the program for channel with name "TEST 2" 
      will keep the 'desc', 'sub-title', 'credits' and 'country' elements for this channel's programme
      will add 'icon' element for this channel
    -->      
    <channel name="TEST 2">
      <keep>desc,sub-title,credits,country</keep>
      <add>
        <icon src="http://logos.com/test2.png" />
      </add>
    </channel>
    <!-- 
      will copy the program for channel with name "TEST 3" 
      all xmltv elements for channel's programme will be deleted leaving only the show 'title'
    -->      
    <channel name="TEST 3"></channel>
  </channels>
</root>

```

## xmltv_time_modify

The tool modifies the timings for channel programmes in an xmltv guide. 

``` xmltv_time_modify <input-xml-file> [input-xml-file] [config-file] ```

Examples:
If you provide only an input EPG file, the tool will convert all programme timings to local time and save the changes in the input file.
 ``` xmltv_time_modify.exe epg.xml ``` 

If you provide an input and output EPG files, the tool will convert all programme timings to local time and save the changes in the output file. 
 ``` xmltv_time_modify.exe epg.xml epg2.xml ``` 

If you provide an input and output EPG files and settings XML file, the tool will correct all programme timings as per the settings in the chans2correct.xml. 
 ``` xmltv_time_modify.exe epg.xml epg2.xml chans2correct.xml ``` 

## xmltv_merge

A tool for merging multiple XMLTV files. It creates a single EPG file "merged_epg.xml". It will discard any duplicate channels if they are already added.

 ``` xmltv_merge.exe <input-xml-file> <input-xml-file> [input-xml-file] ```


