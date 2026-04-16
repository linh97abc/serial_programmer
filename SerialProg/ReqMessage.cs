using System.Dynamic;

namespace SerialProg;

public record ProgFlashPayload(UInt32 size, UInt32 crc, UInt32 offset);
public record FileDataPayload(UInt16 page_index, byte[] data);

public class ReqMessage
{
    public UInt16 Seq { get; set; }

    byte _msgId;
    public byte MsgId => _msgId;

    byte[] _payload = Array.Empty<byte>();
    public byte Len => (byte)_payload.Length;
    public byte[] Payload => _payload;

    public byte ResetSession
    {
        set
        {
            _msgId = (byte)MessageId.RESET_SESSION;
            _payload = Array.Empty<byte>();
        }
    }

    public byte Ping
    {
        set
        {
            _msgId = (byte)MessageId.PING;
            _payload = Array.Empty<byte>();
        }
    }

    public byte ReadStatus
    {
        set
        {
            _msgId = (byte)MessageId.READ_STATUS;
            _payload = Array.Empty<byte>();
        }
    }
    public byte GetImgInfo
    {
        set
        {
            _msgId = (byte)MessageId.GET_IMG_INFO;
            _payload = Array.Empty<byte>();
        }
    }

    public byte BeginUpFile
    {
        set
        {
            _msgId = (byte)MessageId.BEGIN_UP_FILE;
            _payload = Array.Empty<byte>();
        }
    }

    public FileDataPayload FileData
    {
        set
        {
            _msgId = (byte)MessageId.DATA_FILE;
            _payload = new byte[2 + value.data.Length];
            BitConverter.GetBytes(value.page_index).CopyTo(_payload, 0);
            value.data.CopyTo(_payload, 2);
        }
    }

    public ProgFlashPayload ProgFlash
    {
        set
        {
            _msgId = (byte)MessageId.PROG_FLASH;
            _payload = new byte[12];
            BitConverter.GetBytes(value.size).CopyTo(_payload, 0);
            BitConverter.GetBytes(value.crc).CopyTo(_payload, 4);
            BitConverter.GetBytes(value.offset).CopyTo(_payload, 8);
        }
    }

    public byte[] ToBytes()
    {
        byte[] data = new byte[6 + _payload.Length];
        BitConverter.GetBytes(Seq).CopyTo(data, 0);
        data[2] = Len;
        data[3] = MsgId;
        _payload.CopyTo(data, 4);

        UInt16 crc = ComCrc.CaculateCrc(data.Take(4 + _payload.Length));
        BitConverter.GetBytes(crc).CopyTo(data, 4 + _payload.Length);

        return data;
    }

}