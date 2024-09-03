// Copyright (C) 2024 Jens-Kristian Myklebust
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using Microsoft.Win32;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Xml;
using Tomlet;
using Tomlet.Models;

namespace PrinterConnector;

internal static class PrinterList
{
    internal static readonly List<PrinterConnectDef> printers = [];
    internal static readonly List<PrinterConnectDef> printersToConnect = [];
    internal static readonly List<PrinterConnectDef> printersToRemove = [];
    internal static readonly List<string> printersToSetDefault = [];
    internal static readonly HashSet<string> userGroups = GetUserGroups();
    internal static readonly string computerName = GetHostname();
    internal static readonly string computerDomain = CIMUtils.GetComputerDomain();
    internal static readonly IPAddress computerIPv4 = GetIPv4();
    internal static bool CitrixSession { get; private set; }
    static bool FixMissingFQDNNames { get; set; } = true;

    internal static bool GetXMLConfig(string configPath)
    {
        XmlDocument xml = new();
        try
        {
            xml.Load(configPath);
        }
        catch
        {
            return false;
        }
        XmlNode? rootNode = xml.SelectSingleNode(@"/printerconnector");
        if (rootNode == null)
        {
            return false;
        }

        if (rootNode.SelectSingleNode(@"configuration") != null)
        {
            XmlNode configurationNode = rootNode.SelectSingleNode(@"configuration")!;
            XmlAttributeCollection? configAttributes = configurationNode.Attributes;
            if (configAttributes != null)
            {
                if (configAttributes["fix-missing-fqdn-names"]?.Value.ToLower() == "false")
                {
                    FixMissingFQDNNames = false;
                }
                if (configAttributes["detailed-logging"]?.Value.ToLower() == "true")
                {
                    Program.DetailedLog = true;
                }
            }

        }
        if (rootNode.SelectSingleNode(@"printers") != null)
        {
            foreach (XmlNode node in rootNode.SelectSingleNode(@"printers")!.ChildNodes)
            {
                string name = "undefined";
                bool setDefaultPrinter;
                int defaultPrinterWeight = 0;
                try
                {
                    if (node.LocalName == @"#comment")
                    {
                        continue;
                    }
                    name = node.SelectSingleNode(@"name")!.InnerText;
                    if (bool.TryParse(node.SelectSingleNode(@"setdefaultprinter")?.InnerText, out setDefaultPrinter))
                    {
                        _ = int.TryParse(node.SelectSingleNode(@"setdefaultprinter")?.Attributes.GetNamedItem("weight")?.InnerText, out defaultPrinterWeight);
                    }
                    string[] adgroup = ProcessXMLArrayList(node, @"adgroup");
                    string[] computers = ProcessXMLArrayList(node, @"computers");
                    string[] ipaddress = ProcessXMLArrayList(node, @"ipaddress");

                    if (FixMissingFQDNNames)
                    {
                        // We assume that if there is no dot in the name, it's not a FQDN
                        // In that case we add the computer's domain to the name to make sure we have a FQDN
                        if (!name.Contains('.') && !string.IsNullOrWhiteSpace(computerDomain))
                        {
                            // Add a an entry for the old name, this will be filtered away and end up on the remove list
                            printers.Add(new(name, ["noconnect"], ["noconnect"], [], false, 0));

                            string hostname = name.TrimStart('\\').Split('\\')[0];
                            name = name.Replace(hostname, hostname + "." + computerDomain);
                        }
                    }
                    printers.Add(new(name, adgroup, computers, ipaddress, setDefaultPrinter, defaultPrinterWeight));
                }
                catch
                {
                    Console.WriteLine($"Failed to parse {name}");
                };
            }
        }
        PrepareConnectDisconnectLists();
        DecideDefaultPrinter();
        return true;
    }
    private static string[] ProcessXMLArrayList(XmlNode node, string nodeNameXpath)
    {
        string[] stringArray;
        return stringArray = null != node.SelectSingleNode(nodeNameXpath)
            ? node.SelectSingleNode(nodeNameXpath)!.InnerText.Split(',').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() : ([]);
    }

    internal static bool GetTomlConfig(string configPath)
    {
        TomlDocument document;
        try
        {
            document = TomlParser.ParseFile(configPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse {configPath}");
            return false;
        }
        try { FixMissingFQDNNames = document.GetBoolean("fix-missing-fqdn-names"); } catch { }
        try { Program.DetailedLog = document.GetBoolean("detailed-logging"); } catch { }

        TomlTable printers;
        try { printers = document.GetSubTable("printers"); }
        catch
        {
            Console.WriteLine($"No printers found in {configPath}, not a valid configuration");
            return false;
        }

        if (printers.Any())
        {
            foreach (var item in printers)
            {
                if (item.Value is not TomlTable printer)
                {
                    continue;
                }

                string name;
                bool setDefaultPrinter = false;
                int defaultPrinterWeight = 0;
                string[] adgroup;
                string[] computers;
                string[] ipaddress;

                // if the name is not a string then we just skip the rest 
                try { name = printer.GetString("name"); } catch { continue; }
                try { setDefaultPrinter = printer.GetBoolean("setdefaultprinter"); } catch { }
                try { defaultPrinterWeight = printer.GetInteger("defaultprinterweight"); } catch { }
                try { adgroup = printer.GetArray("adgroup").Select(x => x.StringValue).ToArray(); } catch { adgroup = []; }
                try { computers = printer.GetArray("computers").Select(x => x.StringValue).ToArray(); } catch { computers = []; }
                try { ipaddress = printer.GetArray("ipaddress").Select(x => x.StringValue).ToArray(); } catch { ipaddress = []; }

                if (FixMissingFQDNNames)
                {
                    // We assume that if there is no dot in the name, it's not a FQDN
                    // In that case we add the computer's domain to the name to make sure we have a FQDN
                    if (!name.Contains('.') && !string.IsNullOrWhiteSpace(computerDomain))
                    {
                        // Add a an entry for the old name, this will be filtered away and end up on the remove list
                        PrinterList.printers.Add(new(name, ["noconnect"], ["noconnect"], [], false, 0));

                        string hostname = name.TrimStart('\\').Split('\\')[0];
                        name = name.Replace(hostname, hostname + "." + computerDomain);
                    }
                }
                PrinterList.printers.Add(new(name, adgroup, computers, ipaddress, setDefaultPrinter, defaultPrinterWeight));

            }
            PrepareConnectDisconnectLists();
            DecideDefaultPrinter();
            return true;
        }
        return false;
    }

    static void PrepareConnectDisconnectLists()
    {
        // We could do the the connection list with the Lamdas below
        // But for the sake of consitency we use a bigger foreach loop where we can have optional logging
        //printersToConnect.AddRange(printers.Where(printer => printer.IPAddresses.Count == 0 && printer.Computers.Count == 0 && printer.Adgroup.Count == 0));
        //printersToConnect.AddRange(printers.Where(
        //    printer => (printer.Computers.Contains(computerName) || printer.IPAddresses.Contains(computerIPv4))
        //    && (printer.Adgroup.Count == 0 || userGroups.Overlaps(printer.Adgroup))
        //    ).ToList());
        foreach (PrinterConnectDef printer in printers)
        {
            bool connectPrinter = false;


            if (printer.IPAddresses.Count == 0 && printer.Computers.Count == 0 && printer.Adgroup.Count == 0)
            {
                if (Program.DetailedLog)
                    Program.Logger.TeeLogMessage($"Adding {printer.Printer} to connection list since it has no extra conditions");
                connectPrinter = true;
            }
            else if (printer.Adgroup.Count == 0)
            {
                if (printer.IPAddresses.Contains(computerIPv4))
                {
                    if (Program.DetailedLog)
                        Program.Logger.TeeLogMessage($"Adding {printer.Printer} to connection list, no group filter, match on ip {computerIPv4}");
                    connectPrinter = true;
                }
                else if (printer.Computers.Contains(computerName))
                {
                    if (Program.DetailedLog)
                        Program.Logger.TeeLogMessage($"Adding {printer.Printer} to connection list, no group filter, match on hostname {computerName}");
                    connectPrinter = true;
                }
            }
            else if (userGroups.Overlaps(printer.Adgroup))
            {
                string prtGroups = string.Join(", ", printer.Adgroup);
                if (printer.IPAddresses.Contains(computerIPv4))
                {
                    if (Program.DetailedLog)
                        Program.Logger.TeeLogMessage($"Adding {printer.Printer} to connection list, match on a group ({prtGroups}), match on ip {computerIPv4}");
                    connectPrinter = true;
                }
                else if (printer.Computers.Contains(computerName))
                {
                    if (Program.DetailedLog)
                        Program.Logger.TeeLogMessage($"Adding {printer.Printer} to connection list, match on a group ({prtGroups}), match on hostname {computerName}");
                    connectPrinter = true;
                }
                else
                {
                    if (Program.DetailedLog)
                        Program.Logger.TeeLogMessage($"Adding {printer.Printer} to connection list, match on a group ({prtGroups})");
                    connectPrinter = true;
                }
            }

            if (connectPrinter)
            {
                printersToConnect.Add(printer);
            }
        }

        // Any printers on the list we are not connecting to will be removed
        printersToRemove.AddRange(printers.Where(printer => !printersToConnect.Contains(printer)).ToList());
    }

    static void DecideDefaultPrinter()
    {
        PrinterConnectDef[] printersToSetAsDefault = printersToConnect.Where(printer => printer.SetDefaultPrinter).ToArray();
        printersToSetDefault.AddRange(printersToSetAsDefault.OrderByDescending(printer => printer.DefaultPrinterWeight).Select(printer => printer.Printer).ToList());
    }
    // If we are running in a Citrix VDI we want the remote computer otherwise we simply want the local hostname.
    // Computer\HKEY_LOCAL_MACHINE\SOFTWARE\Citrix\Ica\Session\
    // ClientAddress - IpAddress
    // ClientName - Hostname
    static string GetHostname()
    {

        string? ClientName = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Citrix\Ica\Session\", "ClientName", null)?.ToString();
        switch (ClientName)
        {
            case null:
                return Environment.MachineName.ToLowerInvariant();
            default:
                CitrixSession = true;
                return ClientName.ToLowerInvariant();
        }
    }
    // Same as hostname, we try to get the Citrix client IP address first before we use the local one.
    static IPAddress GetIPv4()
    {
        IPAddress? ClientIP;
        string? ClientAddress = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Citrix\Ica\Session\", "ClientAddress", null)?.ToString();
        if (null != ClientAddress)
        {
            _ = IPAddress.TryParse(ClientAddress, out ClientIP);
        }
        else
        {
            //https://stackoverflow.com/a/27376368
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            //The IP address used here does not need to be reachable, it's just used to evaluate the connection route
            socket.Connect("8.8.8.8", 65530);
            IPEndPoint endPoint = (IPEndPoint)socket.LocalEndPoint!;
            ClientIP = endPoint.Address;
            socket.Dispose();
        }
        if (null == ClientIP)
            throw new Exception("Unable to get a valid IP, are you connected to network?");
        return ClientIP;
    }

    static HashSet<string> GetUserGroups()
    {
        HashSet<string> groups = [];
        foreach (SecurityIdentifier claim in WindowsIdentity.GetCurrent().Groups!.Cast<SecurityIdentifier>())
        {
            try
            {
                groups.Add(claim.Translate(typeof(NTAccount)).Value.ToLower());
            }
            catch (IdentityNotMappedException)
            {
                //Console.WriteLine("Failed to map SID: " + claim.Value);
            }
        }
        return groups;
    }
}