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

using Microsoft.Management.Infrastructure;

namespace PrinterConnector
{
    //https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-printer
    internal sealed class CIMUtils
    {
        const string cimNamespace = @"root\cimv2";
        private static readonly CimSession cimSession = CimSession.Create(null);

        internal static uint ConnectPrinter(string printerName)
        {
            CimMethodResult result = cimSession.InvokeMethod(cimNamespace, "Win32_Printer", "AddPrinterConnection",
            [
                CimMethodParameter.Create("Name", printerName, CimFlags.None)
            ]);
            return (uint)result.ReturnValue.Value;
        }
        
        internal static CimInstance GetPrinterInfo(string printerName)
        {
            CimInstance printer = cimSession.QueryInstances(cimNamespace, "WQL", $"Select * From Win32_Printer Where name LIKE \"{printerName.Replace(@"\", @"\\")}\"").First();
            return printer;
        }

        internal static uint GetPrinterExtendedStatus(string printerName)
        {
            return uint.Parse(s: GetPrinterInfo(printerName).CimInstanceProperties["PrinterStatus"].Value.ToString());
        }

        internal static string PrinterConnectReturnCodeTranslate(uint returnCode)
        {
            return returnCode switch
            {
                0 => "Code 0: Successfully connected printer.",
                5 => "Code 5: Access Denied.",
                10 => "Code 10: Successfully connected printer.",
                123 => "Code 123: The filename, directory name, or volume label syntax is incorrect.",
                1722 => "Code 1722: The RPC Server is Unavailable",
                1801 => "Code 1801: Invalid Printer Name.",
                1930 => "Code 1930: Incompatible Printer Driver.",
                3019 => "Code 3019: The specified printer driver was not found on the system and needs to be downloaded.",
                _ => $"Unkown return code: {returnCode}",
            };
        }
        internal static HashSet<string> GetConnectedPrinters()
        {
            HashSet<string> connectedPrinters = [];
            foreach (CimInstance printer in cimSession.EnumerateInstances(cimNamespace, "Win32_Printer"))
            {
                var properties = printer.CimInstanceProperties;

                string? name = properties["Name"].Value.ToString();
                bool shared = (bool)properties["Shared"].Value;
                bool local = (bool)properties["Local"].Value;
                if (shared && !local && name != null)
                {
                    _ = connectedPrinters.Add(name.ToLowerInvariant());
                }
            }
            return connectedPrinters;
        }

        internal static void RemovePrinter(string printerName)
        {
            var printer = cimSession.QueryInstances(cimNamespace, "WQL", $"Select * From Win32_Printer Where name LIKE \"{printerName.Replace(@"\",@"\\")}\"").First();
            cimSession.DeleteInstance(printer);
        }

        internal static void SetDefaultPrinter(string printerName)
        {
            var printer = cimSession.QueryInstances(cimNamespace, "WQL", $"Select * From Win32_Printer Where name LIKE \"{printerName.Replace(@"\", @"\\")}\"").First();
            cimSession.InvokeMethod(printer, "SetDefaultPrinter",[]);
        }

        internal static string GetComputerDomain()
        {
            string? computerDomain = cimSession.QueryInstances(cimNamespace, "WQL", "Select domain from Win32_ComputerSystem")
                .FirstOrDefault()?.CimInstanceProperties["domain"].Value.ToString();

            if (string.IsNullOrEmpty(computerDomain))
                return "";

            return computerDomain;
        }
    }
}