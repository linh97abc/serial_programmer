namespace SerialProg;

public class RepMessage
{
    public UInt16 Seq {get;set;}
    
    byte _len;
    public byte Len => _len;

    byte _msgId;
    public byte MsgId => _msgId;


    byte[] _payload ;
    public byte[] Payload => _payload;

    UInt16 _crc;
    public UInt16 Crc => _crc;

    public RepMessage(byte[] data)
    {
        Seq = BitConverter.ToUInt16(data, 0);
        _len = data[2];
        _msgId = data[3];
        _payload = data.Skip(4).Take(_len).ToArray();
        _crc = BitConverter.ToUInt16(data, 4 + _len);
    }

    public UInt32 Status => BitConverter.ToUInt32(_payload, 0);
}