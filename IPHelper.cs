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

namespace PrinterConnector;

// Original ideas from: https://stackoverflow.com/a/4172982
public sealed class IPHelper
{
    private byte[]? beginIP;
    private byte[]? endIP;

    public IPHelper(string ipRange)
    {
        ArgumentNullException.ThrowIfNull(ipRange);

        if (!TryParseCIDRNotation(ipRange) && !TryParseSimpleRange(ipRange))
            throw new ArgumentException("Unable to parse ipRange", nameof(ipRange));
    }

    public IEnumerable<IPAddress> GetAllIP()
    {
        if (beginIP != null && endIP != null)
            return GetAllIP(beginIP, endIP);
        else
            throw new Exception("Unable to get IP range");
    }

    public static IEnumerable<IPAddress> GetAllIP(byte[] beginIP, byte[] endIP)
    {
        int capacity = 1;
        for (int i = 0; i < 4; i++)
            capacity *= endIP[i] - beginIP[i] + 1;

        List<IPAddress> ips = new(capacity);
        for (int i0 = beginIP[0]; i0 <= endIP[0]; i0++)
        {
            for (int i1 = beginIP[1]; i1 <= endIP[1]; i1++)
            {
                for (int i2 = beginIP[2]; i2 <= endIP[2]; i2++)
                {
                    for (int i3 = beginIP[3]; i3 <= endIP[3]; i3++)
                    {
                        ips.Add(new IPAddress(new byte[] { (byte)i0, (byte)i1, (byte)i2, (byte)i3 }));
                    }
                }
            }
        }
        return ips;
    }

    // Parse IP-range string in CIDR notation.
    // For example "12.15.0.0/16".
    private bool TryParseCIDRNotation(string ipRange)
    {
        string[] x = ipRange.Split('/');

        if (x.Length != 2)
            return false;

        byte bits = byte.Parse(x[1]);
        uint ip = 0;
        String[] ipParts0 = x[0].Split('.');
        for (int i = 0; i < 4; i++)
        {
            ip <<= 8;
            ip += uint.Parse(ipParts0[i]);
        }

        byte shiftBits = (byte)(32 - bits);
        uint ip1 = (ip >> shiftBits) << shiftBits;

        uint ip2 = ip1 >> shiftBits;
        for (int k = 0; k < shiftBits; k++)
        {
            ip2 = (ip2 << 1) + 1;
        }

        beginIP = new byte[4];
        endIP = new byte[4];

        for (int i = 0; i < 4; i++)
        {
            beginIP[i] = (byte)((ip1 >> (3 - i) * 8) & 255);
            endIP[i] = (byte)((ip2 >> (3 - i) * 8) & 255);
        }

        return true;
    }

    // Parse IP-range string "12.15-16.1-30.10-255"
    private bool TryParseSimpleRange(string ipRange)
    {
        String[] ipParts = ipRange.Split('.');

        beginIP = new byte[4];
        endIP = new byte[4];
        for (int i = 0; i < 4; i++)
        {
            string[] rangeParts = ipParts[i].Split('-');

            if (rangeParts.Length < 1 || rangeParts.Length > 2)
                return false;

            beginIP[i] = byte.Parse(rangeParts[0]);
            endIP[i] = (rangeParts.Length == 1) ? beginIP[i] : byte.Parse(rangeParts[1]);
        }

        return true;
    }

}

