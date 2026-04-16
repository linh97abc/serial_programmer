using System;
using System.Collections.Generic;

namespace SerialProg;

public static class ComCrc
{
    public static UInt16 CaculateCrc(IEnumerable<byte> data)
    {
        UInt16 crc = 0xFFFF;

        foreach (var b in data)
        {
            crc ^= (UInt16)(b << 8);
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x8000) != 0)
                    crc = (UInt16)((crc << 1) ^ 0x8005);
                else
                    crc <<= 1;
            }
        }

        return crc;
    }
}