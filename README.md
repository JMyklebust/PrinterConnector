# PrinterConnector
This application connects and disconnects shared printers in a Windows enviroment based on a XML configuration file that define the printers.  
This can be particularily useful in VDI enviroments where you move the sessions around and might have different printers available nearby.  
>_Note that the design currently only accounts for single sessions (basically VDI) on the Citrix platform, terminal servers are not handled properly yet._  
_Nor is this tested on other types of VDI plattforms_  
_It does however work fine direcly on regular clients (laptops, desktops)._

Application is built with .Net 8 and meant to run on Windows OS only.

## Building
Simple build script can be found at [build.bat](build.bat).  
To build this application you need the .Net 8.0 SDK [Dotnet 8.0 Downloads](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).  

### Build commands
To do a normal build just do:  
``dotnet publish -c Release -r win-x64 --self-contained true -p:PublishDir=.\bin\Release\publish``
If you want a version that does not open a console window everytime the program runs you can compile with with ``-p:TargetType=WinExe``:  
``dotnet publish -c Release -r win-x64 --self-contained true -p:PublishDir=.\bin\Release\publish-hidden -p:TargetType=WinExe``

The "hidden" version is convenient to run as part of a scheduled task, this way the program can work in the background without showing up for the user.

## Running
Simply double click to launch, it will by default be looking for `configuration.xml` in the current working directory.  
Also have a look at [ScheduledTaskSample.ps1](ScheduledTaskSample.ps1) to how you could configure it to run as a scheduled task on login and workstation unlock.

## Logging
Currently logging path is not changeable. The program will try to write it's log to `C:\temp\printerlog.log`.  
Failing that it will try to create it at `%TEMP%\printerlog.log`.  
The logfile is replaced for each run.

## Allowed servers and the AllowPrivateNetworkOnly flag
At compile time we decide if we allow trying to connect to non-private IP-address spaces.  
If this constant `AllowPrivateNetworkOnly` is set to `true` we do the following:
After we do a server name lookup, check the returned IP-addresses:
If the IP-address converted to string starts with:
- 10.*
- 172.16.*
- 192.168.*
- fd* (IPv6 private space prefix)

Then we consider the DNS lookup valid. If the IP-address does not start with either of these, then we consider the DNS lookup as "failed".  
The intention of this is to reduce the chance using this application as a means of connecting to a malicious printer server.

## Configuration
The application is configured via the file `configuration.xml`.  
**Important note!** The XML tags are case sensitive, and generally written in lowercase.

### Simple configuration
Printers are defined in the tag `<printers>` under the root tag `<printerconnector>` and a definition is defined by the element `<printerdef>` containg minimum a `<name>` element which defines the printer.
An example of minimum configuration:
```xml
<?xml version="1.0" encoding="utf-8" ?>
<printerconnector>
	<printers>
		<printerdef>
			<name>\\printer.server.FQDN\myprinter</name>
		</printerdef>
	</printers>
</printerconnector>
```
**Note!** Prefer using FQDN in the printer name.
If you add just the hostname, the program will automatically try to append the current computer domain to the name.

### Advanced configuration
More advanced configuration is also possible.

#### Group membership
One can restrict mapping based on group membership:
```xml
<?xml version="1.0" encoding="utf-8" ?>
<printerconnector>
	<printers>
		<printerdef>
			<name>\\printer.server.FQDN\myprinter</name>
			<adgroup>domain\printergroup</adgroup>
		</printerdef>
	</printers>
</printerconnector>
```

#### Current computer/connected Citrix client hostname
Restrict based on the hostname of the current computer (or in case of a Citrix VDI-session, the client that is used to connect):
```xml
<?xml version="1.0" encoding="utf-8" ?>
<printerconnector>
	<printers>
		<printerdef>
			<name>\\printer.server.FQDN\myprinter</name>
			<computers>hostname</computers>
		</printerdef>
	</printers>
</printerconnector>
```  

#### Current IP/connected Citrix client IP
It's also possible to use IP address as condition (same as with hostname, it will check the Citrix VDI-session details):
```xml
<?xml version="1.0" encoding="utf-8" ?>
<printerconnector>
	<printers>
		<printerdef>
			<name>\\printer.server.FQDN\myprinter</name>
			<ipaddress>10.10.10.10</ipaddress>
		</printerdef>
	</printers>
</printerconnector>
```  
IP addresses can be in the formats:  
- Single IP `10.10.10.10`
- IP Range with CDIR notation `10.10.10.0/24`
- From-To range `10.10.10.1-10.10.10.20`

Note that it is possible to put in a "wrong" notation for a CDIR like this; `10.10.10.10/24`.  
The program will try to convert this into the correct range `10.10.10.0/24`, but you might end up with an unexpected range.  
Use a subnet calculator, or use to from-to notation instead.

#### Combination
These can also be combined:
```xml
<?xml version="1.0" encoding="utf-8" ?>
<printerconnector>
	<printers>
		<printerdef>
			<name>\\printer.server.FQDN\myprinter</name>
			<adgroup>domain\printergroup</adgroup>
			<computers>hostname</computers>
		</printerdef>
	</printers>
</printerconnector>
```
Note: If you combine `computername` with `ipaddress` filter the program will use it as an OR filter.  
That means if either `computername` or `ipaddress` match, the mapping will be done.
This also means that if neither of these match, the printer will be removed.

#### Arrays in connection filters
Both the `adgroup`, `computer` and `ipaddress` tags accept a comma (`,`) seperated list of items.
```xml
<?xml version="1.0" encoding="utf-8" ?>
<printerconnector>
	<printers>
		<printerdef>
			<name>\\printer.server.FQDN\myprinter</name>
			<adgroup>domain\printergroup, domain\anothergroup</adgroup>
			<computers>hostname, computer2</computers>
			<ipaddress>10.10.10.10, 10.10.11.0/24</ipaddress>
		</printerdef>
	</printers>
</printerconnector>
```

#### Configure default printer
You can define a printer to be set as default using the tag ``setdefaultprinter``.  
```xml
<printerconnector>
	<printers>
		<printerdef>
			<name>\\printer.server.FQDN\myprinter</name>
			<setdefaultprinter weight="1">true</setdefaultprinter>
		</printerdef>
	</printers>
</printerconnector>
```
You can mark several printers as default printer, the criteria for selection is following:
- The printer is evaluated as a printer to connect (according to other conditions).
- Try to set the default printer in the order they appear in the config file, unless a weight is set specificially on any of them.
- If the ``weight`` attribute is set, then try the higher numbers first (if no weight is set then they have a default weight of 0).
- Try to set default printers in order until one succeeds.

### Important
- Currently you can only have one `printerdef` per printername, if you add multiple entries with the same name only the fist will be used.  
- The `adgroup`, `computer` and `ipaddress` defintions will also remove the printer if the user's session no longer match the conditions. 



## Copyright
```
Copyright (C) 2024 Jens-Kristian Myklebust

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
```