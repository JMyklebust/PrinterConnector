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
The application can be configured with either toml or xml files.  
Generally it's reccomended to use the toml format, and xml is considered as legacy configuration for this application.  
Old instuctions for the xml format is here [READMEXML.md](READMEXML.md)

The default configuration filename is `configuration.toml`.  
The application can take a filepath as argument to allow you to use multiple different configurations if nessecary.  


### Simple configuration
A basic configuration can look like this:
```toml
# Main settings
fix-missing-fqdn-names=false
detailed-logging=true

# Printer configuration
[printer.'commonprinter']
name='\\printserver.example.com\commonprinter'
```

The absolute minimum valid configuration is this:
```toml
[printer.'commonprinter']
name='\\printserver.example.com\commonprinter'
```
Or this:
```toml
printer.'commonprinter'.name='\\printserver.example.com\commonprinter'
```

Every printer added needs a toml table under the table 'printer'
So multiple printes can be defined like this:
```toml
[printer.'commonprinter']
name='\\printserver.example.com\commonprinter'
[printer.'extraprinter']
name='\\printserver.example.com\someotherprinter'
```
Or this:
```toml
printer.'commonprinter'.name='\\printserver.example.com\commonprinter'
printer.'extraprinter'.name='\\printserver.example.com\someotherprinter'
```
It's generally recommened to keep to one syntax and not mix the two.  
It's also reccomended to use single quotes, since the character `\` is an escape character in the toml specification.  
Using string literals is simpler since we are working with Windows paths mainly.

> **Note!** Prefer using FQDN in the printer name.  
If you add just the hostname, the program will automatically try to append the current computer domain to the name.  


> **Note2!** The toml table name is just for the configuration, make sure the printer "name" attribute is unique.  
If it's not unique then the next printer with the same name will be ignored.  
Example:
```toml
[printer.'commonprinter']
name='\\printserver.example.com\samename'
[printer.'extraprinter']
name='\\printserver.example.com\samename'
```
Since both 'commonprinter' and 'extraprinter' has the same name, only 'commonprinter' will be processed.

### Advanced configuration
It's possible to add filters to printer mappings for more complex setup. Generally these options are toml arrays.
So they need to be enclosed in `[ ]`

> NOTE! Using the `adgroup`, `computer` and `ipaddress` defintions will also remove the printer if the user's session no longer match the conditions. 

#### Group membership
One can restrict mapping based on group membership using the the `adgroup` property.
```toml
[printer.'commonprinter']
name='\\printserver.example.com\commonprinter'
adgroup=['domain\printergroup','domain\printergroup2']
```

#### Current computer/connected Citrix client hostname
Restrict based on the hostname of the current computer (or in case of a Citrix VDI-session, the client that is used to connect) using the `computers` property.
```toml
[printer.'commonprinter']
name='\\printserver.example.com\commonprinter'
computers=['hostname','hostname2']
```

#### Current IP/connected Citrix client IP
Restrict based on the ip of the current computer (or in case of a Citrix VDI-session, the  ip of the client that is used to connect) using the `ipaddress` property.
```toml
[printer.'commonprinter']
name='\\printserver.example.com\commonprinter'
ipaddress=['10.10.10.10','10.10.20.0/24','10.10.30.1-10.10.30.20']
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
```toml
[printer.'commonprinter']
name='\\printserver.example.com\commonprinter'
adgroup=['domain\printergroup']
computers=['hostname']
```
Note: If you combine `computers` with `ipaddress` filter the program will use it as an OR filter.  
That means if either `computers` or `ipaddress` match, the mapping will be done.
This also means that if neither of these match, the printer will be removed.

#### Configure default printer
You can define a printer to be set as default using the property ``setdefaultprinter``.  
```toml
[printer.'commonprinter']
name='\\printserver.example.com\commonprinter'
setdefaultprinter=true
```

If multiple default printers are possible you can define priority using the weight property:
```toml
[printer.'commonprinter']
name='\\printserver.example.com\commonprinter'
setdefaultprinter=true
defaultprinterweight=1
```
> Notes on priority for default printer:
> - The printer is evaluated as a printer to connect (according to other conditions).
> - Try to set the default printer in the order they appear in the config file, unless a weight is set specificially on any of them.
> - If the ``defaultprinterweight`` property is set, then try the higher numbers first (if no weight is set then they have a default weight of 0).
> - Try to set default printers in order until one succeeds.


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