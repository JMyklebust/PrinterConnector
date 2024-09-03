# XML Configuration

The application can be configured via XML files, but it's reccomended to migrate to toml configs.  
Use [ConfigConverter.ps1](ConfigConverter.ps1) to convert any xml files over to the toml format.  

The default xml config file is `configuration.xml`.  
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

