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

using System.Net;

namespace PrinterConnector
{
    public class PrinterConnectDef(string printer, string[] adgroup, string[] computers, string[] ipaddress, bool SetDefaultPrinter, int DefaultPrinterWeight)
    {
        public readonly string Printer = printer.ToLowerInvariant();
        public readonly string PrinterServer = printer.ToLowerInvariant().TrimStart('\\').Split('\\')[0];
        public readonly bool SetDefaultPrinter = SetDefaultPrinter;
        public readonly int DefaultPrinterWeight = DefaultPrinterWeight;
        public readonly HashSet<string> Adgroup = adgroup.Select(s => s.ToLowerInvariant().Trim()).ToHashSet();
        public readonly HashSet<string> Computers = computers.Select(s => s.ToLowerInvariant().Trim()).ToHashSet();
        public readonly HashSet<IPAddress> IPAddresses = ProcessIPList(ipaddress);

        private static readonly Dictionary<string, HashSet<IPAddress>> IPRangeCache = [];

        static HashSet<IPAddress> ProcessIPList(string[] ipaddress)
        {
            HashSet<IPAddress> iPAddresses = [];
            foreach (string ip in ipaddress.Select(s=>s.Trim()))
            {
                if (ip.Contains('/'))
                {
                    if (!IPRangeCache.TryGetValue(ip, out HashSet<IPAddress>? value))
                    {
                        value = new IPHelper(ip).GetAllIP().ToHashSet();
                        IPRangeCache.Add(ip, value);
                    }
                    iPAddresses.UnionWith(value);
                }
                else if (ip.Contains('-'))
                {
                    if (!IPRangeCache.TryGetValue(ip, out HashSet<IPAddress>? value))
                    {
                        IPAddress[] splitaddress = ip.Split("-").Select(IPAddress.Parse).ToArray();
                        value = IPHelper.GetAllIP(splitaddress[0].GetAddressBytes(), splitaddress[1].GetAddressBytes()).ToHashSet();
                        IPRangeCache.Add(ip, value);
                    }
                    iPAddresses.UnionWith(value);
                }
                else
                {
                    if (IPAddress.TryParse(ip, out IPAddress? ipAddr))
                    {
                        iPAddresses.Add(ipAddr);
                    }
                }
            }
            return iPAddresses;
        }
    }
}
