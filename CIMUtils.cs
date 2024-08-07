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

using WmiLight;

namespace PrinterConnector
{
    //https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-printer
    internal sealed class CIMUtils
    {
        private static readonly WmiConnection wmiConnection = new();

        internal static WmiObject? GetPrinterInfo(string printerName)
        {
            foreach(WmiObject printer in wmiConnection.CreateQuery($"Select * From Win32_Printer Where name LIKE \"{printerName.Replace(@"\", @"\\")}\""))
            {
                return printer;
            }
            return null;
        }

        internal static ushort GetPrinterExtendedStatus(string printerName)
        {
            return (GetPrinterInfo(printerName)?.GetPropertyValue<ushort>("PrinterStatus")) ?? 0;
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
            foreach (WmiObject printer in wmiConnection.CreateQuery($"Select Name,Shared,Local From Win32_Printer"))
            {
                string name = printer.GetPropertyValue<string>("Name");
                bool shared = printer.GetPropertyValue<bool>("Shared");
                bool local = printer.GetPropertyValue<bool>("Local");
                if (shared && !local && name != null)
                {
                    _ = connectedPrinters.Add(name.ToLowerInvariant());
                }
            }
            return connectedPrinters;
        }

        internal static uint ConnectPrinter(string printerName)
        {
            using WmiMethod AddPrinterConnection = wmiConnection.GetMethod("Win32_Printer", "AddPrinterConnection");
            using WmiMethodParameters methodParameters = AddPrinterConnection.CreateInParameters();
            methodParameters.SetPropertyValue("Name", printerName);
            return wmiConnection.ExecuteMethod<uint>(AddPrinterConnection, methodParameters, out WmiMethodParameters outParameters);
        }

        internal static void RemovePrinter(string printerName)
        {
            foreach (WmiObject printer in wmiConnection.CreateQuery($"Select * From Win32_Printer Where name LIKE \"{printerName.Replace(@"\", @"\\")}\""))
            {
                wmiConnection.DeleteInstance(printer);
            }
        }

        internal static void SetDefaultPrinter(string printerName)
        {
            var printer = GetPrinterInfo(printerName);
            if(null != printer)
            {
                using WmiMethod SetDefaultPrinter = wmiConnection.GetMethod("Win32_Printer", "SetDefaultPrinter");
                wmiConnection.ExecuteMethod(SetDefaultPrinter, printer, out WmiMethodParameters outParameters);
            }
        }

        internal static string GetComputerDomain()
        {
            string? computerDomain = null;
            foreach (WmiObject system in wmiConnection.CreateQuery("Select domain from Win32_ComputerSystem"))
            {
                computerDomain = system.GetPropertyValue<string>("domain");
                break;
            }
            computerDomain ??= string.Empty;
            return computerDomain;
        }
    }
}