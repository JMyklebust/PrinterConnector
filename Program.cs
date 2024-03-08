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

using System.Reflection;
using System.Net;
using System.Net.Sockets;

namespace PrinterConnector
{
    internal class Program
    {
        internal static bool DetailedLog = false;
        // Flag to ignore printerservers that resolve to a Public IP range
        // This should reduce the chance of mapping to a malicious printerserver
        public const bool AllowPrivateNetworkOnly = true;
        public static readonly Logging Logger = new();
        // Very simple though albeit not elegant way of handling private IP prefixes
        // If you have a specific public IP range you want to allow, you can do so here
        private static readonly string[] privateNetworkPrefixes = [
            // IPv4 A-class
            "10.",
            // IPv4 B-class
            "172.16.",
            "172.17.",
            "172.18.",
            "172.19.",
            "172.20.",
            "172.21.",
            "172.22.",
            "172.23.",
            "172.24.",
            "172.25.",
            "172.26.",
            "172.27.",
            "172.28.",
            "172.29.",
            "172.30.",
            "172.31.",
            // IPv4 C-class
            "192.168.",
            // IPv6 private
            "fd",
        ];


        static void Main(string[] args)
        {
            string? SettingsPath = null;
            if (args.Length > 0)
                SettingsPath = args[0];

            Logger.TeeLogMessage("PrinterConnector, Copyright (C) 2024 Jens-Kristian Myklebust");
            Logger.TeeLogMessage("Build time: " + BuildTimeStamp.GetTimestamp());
            // Ignore warnings for unreacable code here
            // This is intended depending on the state of AllowPrivateNetworkOnly
#pragma warning disable CS0162 // Unreachable code detected
            if (AllowPrivateNetworkOnly)
            {
                Logger.TeeLogMessage("Compiled with 'AllowPrivateNetworkOnly' flag");
                Logger.TeeLogMessage("Will not attempt to connect to servers using non-private IP-address");
            }
            else if (!AllowPrivateNetworkOnly)
            {
                Logger.TeeLogMessage("Not compiled with 'AllowPrivateNetworkOnly' flag", Logging.LogSeverity.Warning);
                Logger.TeeLogMessage("Will attempt to connect to servers using non-private IP-address", Logging.LogSeverity.Warning);
            }
#pragma warning restore CS0162 // Unreachable code detected

            if (PrinterList.CitrixSession)
                Logger.TeeLogMessage($"Running in Citrix session. Client hostname: {PrinterList.computerName}, client IP: {PrinterList.computerIPv4}");
            else
                Logger.TeeLogMessage($"Computer hostname: {PrinterList.computerName}, computer IP: {PrinterList.computerIPv4}");


            try
            {
                if (File.Exists(SettingsPath))
                {
                    if (PrinterList.GetSettings(SettingsPath))
                        Logger.TeeLogMessage($"Loaded config file '{SettingsPath}'", Logging.LogSeverity.Information);
                    else
                        Logger.TeeLogMessage($"Failed to load config file '{SettingsPath}'", Logging.LogSeverity.Warning);
                }
                else
                {
                    if (PrinterList.GetSettings())
                        Logger.TeeLogMessage($"Loaded local configuration.xml file.", Logging.LogSeverity.Information);
                    else
                        Logger.TeeLogMessage($"Failed to load local configuration.xml file.", Logging.LogSeverity.Warning);
                }
            }
            catch (Exception)
            {
                Logger.TeeLogMessage("Was unable to parse config", Logging.LogSeverity.Error);
                throw new Exception("Unable to read config");
            }

            ProcessList();

            Logger.TeeLogMessage("Finished.");
        }

        static void ProcessList()
        {
            HashSet<string> connectedPrinters = CIMUtils.GetConnectedPrinters();
            HashSet<string> skipPrinterServer = [];
            HashSet<string> lookupOKPrinterServer = [];

            if (PrinterList.printersToConnect.Count <= 0)
                Logger.TeeLogMessage("No printers to connect");
            if (PrinterList.printersToRemove.Count <= 0)
                Logger.TeeLogMessage("No printers to remove");

            foreach (PrinterConnectDef printer in PrinterList.printersToRemove)
            {
                if (connectedPrinters.Contains(printer.Printer))
                {
                    Logger.TeeLogMessage($"match to remove {printer.Printer}");
                    CIMUtils.RemovePrinter(printer.Printer);
                }
            }

            foreach (PrinterConnectDef printer in PrinterList.printersToConnect)
            {
                string printerName = printer.Printer;
                string printerServer = printer.PrinterServer;
                if (!lookupOKPrinterServer.Contains(printerServer))
                {
                    if (skipPrinterServer.Contains(printerServer))
                    {
                        Logger.TeeLogMessage($"Skipping '{printerName}' since connection or DNS lookup to printer server '{printerServer}' has failed", Logging.LogSeverity.Warning);
                        continue;
                    }
                    else if (!CheckValidHostname(printerServer))
                    {
                        Logger.TeeLogMessage($"Skipping '{printerName}', failed DNS lookup on printer server '{printerServer}'", Logging.LogSeverity.Warning);
                        skipPrinterServer.Add(printerServer);
                        continue;
                    }
                    else
                    {
                        lookupOKPrinterServer.Add(printerServer);
                    }
                }

                if (!connectedPrinters.Contains(printerName))
                {
                    Logger.TeeLogMessage($"match to add {printerName}");
                    uint returnCode = CIMUtils.ConnectPrinter(printerName);
                    int readyStatus = -1;
                    int retryCount = 10;
                    if (returnCode == 1722)
                    {
                        skipPrinterServer.Add(printerServer);
                    }
                    if (returnCode == 0 || returnCode == 10)
                    {
                        do
                        {
                            Thread.Sleep(1000);
                            readyStatus = (int)CIMUtils.GetPrinterExtendedStatus(printerName);
                            retryCount++;
                        } while ((readyStatus != 3 || readyStatus != 4) && retryCount < 10);

                        Logger.TeeLogMessage(CIMUtils.PrinterConnectReturnCodeTranslate(returnCode));
                    }
                    else
                    {
                        Logger.TeeLogMessage(CIMUtils.PrinterConnectReturnCodeTranslate(returnCode), Logging.LogSeverity.Error);
                    }
                }
                else
                {
                    Logger.TeeLogMessage($"printer already connected {printerName}");
                }
            }
        }

        private static bool CheckValidHostname(string hostname)
        {
            IPAddress[] addressList = [];
            try
            {
                addressList = Dns.GetHostEntry(hostname).AddressList;
            }
            catch (SocketException)
            {
                return false;
            }

            if (AllowPrivateNetworkOnly)
            {
                string addr = "";
                foreach (var address in addressList)
                {
                    addr = address.ToString();
                    foreach (string privateNetworkPrefix in privateNetworkPrefixes)
                    {
                        if (addr.StartsWith(privateNetworkPrefix))
                        {
                            return true;
                        }
                    }
                }
                Logger.TeeLogMessage($"{hostname} resolves to a public IP and this is not allowed ({addr}).", Logging.LogSeverity.Warning);
                return false;
            }
            return true;
        }
    }
    public static class BuildTimeStamp
    {
        public static string GetTimestamp()
        {
            var assembly = Assembly.GetEntryAssembly();
            var stream = (assembly?.GetManifestResourceStream("PrinterConnector.BuildTimeStamp.txt")) ?? throw new Exception("Unable to get build timestamp");
            using var reader = new StreamReader(stream);
            var timestamp = reader.ReadToEnd();
            return timestamp.Trim();
        }
    }
}
