using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Ports;

namespace SerialProg;

public class SerialProg
{
    private readonly Stopwatch _stopwatch;
    SerialPort _serialPort = new();
    private List<byte> _buffer = new List<byte>();

    bool _hasConnection = false;
    bool _isDeviceReady = false;

    UInt16 _seqCounter = 0;

    List<ReqMessage> _pendingMessages = new List<ReqMessage>();

    bool _has_reponse = false;

    Task _taskSendPendingMessage;

    public SerialProg()
    {
        _stopwatch = Stopwatch.StartNew();
        _serialPort.DataReceived += OnDataReceived;
        OnMessageReceived += (msg) =>
        {
            // Console.WriteLine($"{LogPrefix()}-> Seq={msg.Seq}, MsgId={msg.MsgId}, Payload={BitConverter.ToString(msg.Payload)}, Crc={msg.Crc}");
            // Console.WriteLine($"{LogPrefix()}    <- Seq={msg.Seq}, MsgId={msg.MsgId}");
            if (msg.MsgId == ((byte)MessageId.PING | (1u << 7)))
            {
                _isDeviceReady = true;
            }
            if (_pending.TryRemove(msg.Seq, out var task))
            {

                task.TrySetResult(msg);
                // Console.WriteLine($"{LogPrefix()}    <<== Seq={msg.Seq}, MsgId={msg.MsgId}");

            }


        };
    }

    public void SetPort(string port)
    {
        if (_serialPort.IsOpen)
        {
            _serialPort.Close();
        }
        _serialPort.PortName = port;
    }

    public async Task StopDeviceBootProgress()
    {
        _isDeviceReady = false;
        var isDeviceReady = await WaitForDeviceReady();

        if (!isDeviceReady)
        {
            throw new Exception("Cannot find device.");
        }



        ReqMessage msg = new ReqMessage()
        {
            ResetSession = 1
        };
        var buff = ToCobsMsg(msg);
        _serialPort.Write(buff, 0, buff.Length);

        await Task.Delay(100);



        _serialPort.DiscardInBuffer();
        _serialPort.DiscardOutBuffer();
        _serialPort.Close();

    }

    public async Task SetupConnection()
    {
        _serialPort.BaudRate = 921600;
        _serialPort.Open();

        await Task.Delay(20);

        await WaitForDeviceStopBoot();

        _hasConnection = true;

        _seqCounter = 2;

    }

    async Task WaitForDeviceStopBoot()
    {
        _isDeviceReady = false;
        for (int j = 0; j < 3; j++)
        {
            if (!_serialPort.IsOpen) throw new Exception("Serial port is not open.");
            if (_isDeviceReady) return;

            ReqMessage msg = new ReqMessage()
            {
                Ping = 1
            };
            var buff = ToCobsMsg(msg);
            _serialPort.Write(buff, 0, buff.Length);
            await Task.Delay(200);
        }

        throw new Exception("Device is not ready.");
    }

    async Task<bool> WaitForDeviceReady()
    {
        _serialPort.BaudRate = 9600;
        _serialPort.Open();
        _isDeviceReady = false;

        ReqMessage msg = new ReqMessage()
        {
            Ping = 1
        };
        var buff = ToCobsMsg(msg);

        for (int i = 0; i < (10000 / 200); i++)
        {
            for (int j = 0; j < 40; j++)
            {
                if (!_serialPort.IsOpen) return false;
                if (_isDeviceReady) return true;

                _serialPort.Write(buff, 0, buff.Length);

                // var buf = "1234\0".Select(c => (byte)c).ToArray();
                // _serialPort.Write(buf, 0, buf.Length);
            }

            await Task.Delay(200);

        }

        return false;

    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {

        lock (_buffer)
        {
            byte[] temp = new byte[_serialPort.BytesToRead];
            _serialPort.Read(temp, 0, temp.Length);
            _buffer.AddRange(temp);
            ProcessBuffer();
        }
    }

    private void ProcessBuffer()
    {
        while (true)
        {
            int idx = _buffer.IndexOf(0x00);
            if (idx < 0) return;

            byte[] frame = _buffer.GetRange(0, idx).ToArray();
            _buffer.RemoveRange(0, idx + 1);

            if (frame.Length == 0) continue;

            try
            {

                byte[] decoded = COBS.NET.COBS.Decode(frame);

                var crc_expected = ComCrc.CaculateCrc(decoded.Take(decoded.Length - 2));
                var crc_actual = BitConverter.ToUInt16(decoded, decoded.Length - 2);


                if (crc_expected == crc_actual)
                {
                    OnMessageReceived.Invoke(new RepMessage(decoded));

                }
            }
            catch (Exception ex)
            {
                // Console.WriteLine("Decode lỗi: " + ex.Message);
            }
        }
    }




    private string LogPrefix()
    {
        return $"[{_stopwatch.ElapsedMilliseconds}] ";
    }

    private readonly ConcurrentDictionary<UInt16, TaskCompletionSource<RepMessage>> _pending = new();

    static byte[] ToCobsMsg(ReqMessage req)
    {
        byte[] decoded = COBS.NET.COBS.Encode(req.ToBytes());
        return decoded;
    }

    object _sendLock = new object();
    public async Task<RepMessage> SendMessage(ReqMessage msg)
    {
        const int timeout = 100;
        const int maxRetries = 3;
        int retryCount = 0;

        TaskCompletionSource<RepMessage> task = null;

        // Assign sequence number only once
        lock (_sendLock)
        {
            msg.Seq = _seqCounter++;
        }
        task = new TaskCompletionSource<RepMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending.TryAdd(msg.Seq, task);

        var buff = ToCobsMsg(msg);

        while (retryCount < maxRetries)
        {


            lock (_sendLock)
            {
                // System.Diagnostics.Debug.WriteLine($"{LogPrefix()}-> Seq={msg.Seq}, MsgId={msg.MsgId}");
                _serialPort.Write(buff, 0, buff.Length);

            }

            try
            {
                var rep = await task.Task.WaitAsync(TimeSpan.FromMilliseconds(timeout));
                if (rep.MsgId == (msg.MsgId | (1u << 7)))
                {
                    return rep;
                }
            }
            catch (TimeoutException)
            {

                retryCount++;
            }
        }


        _pending.TryRemove(msg.Seq, out _);
        throw new Exception("Failed to send message.");
    }

    // byte[] DebugBytes = new byte[0x100000];


    async Task SendChunkFile(uint start_page_index, byte[] chunk)
    {
        var n_part = (chunk.Length + 127) / 128;

        for (int i = 0; i < n_part; i++)
        {
            int offset = i * 128;
            int length = Math.Min(128, chunk.Length - offset);
            byte[] part = new byte[length];
            Array.Copy(chunk, offset, part, 0, length);

            ReqMessage datafileMsg = new ReqMessage()
            {
                FileData = new((UInt16)(start_page_index + i), part)
            };

            // Array.Copy(part, 0, DebugBytes, offset + (start_page_index * 128), length);

            // System.Diagnostics.Debug.WriteLine($"{LogPrefix()}Sent page {start_page_index} ->  {start_page_index + i}");
            await SendMessage(datafileMsg);
            // System.Diagnostics.Debug.WriteLine($"{LogPrefix()}Sent page {start_page_index} <-  {start_page_index + i}");
        }
    }


    public async Task ProgFlash(string filePath, uint flash_offset)
    {


        byte[] fileData = await File.ReadAllBytesAsync(filePath);
        uint file_size = (uint)fileData.Length;


        ReqMessage beginUpfileMsg = new ReqMessage()
        {
            BeginUpFile = 1
        };

        await SendMessage(beginUpfileMsg);

        var n_page = (file_size + 127) / 128;

        var n_chunk = Math.Min(n_page, 8);


        var min_page_p_chunk = n_page / n_chunk;
        var n_odd_page = n_page % n_chunk;

        List<Task> sendChunkTasks = new List<Task>();
        uint page_offset = 0;
        uint current_start_page = 0;

        for (int i = 0; i < n_chunk; i++)
        {
            var page_p_chunk = min_page_p_chunk + (i < n_odd_page ? 1u : 0u);

            uint chunk_size;
            if (i == n_chunk - 1)
            {
                chunk_size = (uint)fileData.Length - page_offset;
            }
            else
            {
                chunk_size = page_p_chunk * 128;
            }
            sendChunkTasks.Add(SendChunkFile(
                current_start_page,
                 fileData.Skip((int)page_offset).Take((int)chunk_size).ToArray()));

            page_offset += chunk_size;
            current_start_page += page_p_chunk;
        }

        Console.WriteLine($"{LogPrefix()}Send file ...");
        await Task.WhenAll(sendChunkTasks.ToArray());



        // for (int i = 0; i < fileData.Length; i++)
        // {
        //     if (fileData[i] != DebugBytes[i])
        //     {
        //         throw new Exception($"Data mismatch at index {i}");
        //     }
        // }
        // Console.WriteLine($"{LogPrefix()}Data verification passed.");

        // return;


        UInt32 data_crc = Crc32.CaculateCrc(fileData);

        var prog_flash_cmd_result = await SendMessage(new()
        {
            ProgFlash = new(file_size, data_crc, flash_offset)
        });

        if (prog_flash_cmd_result.Status != 0)
        {
            throw new Exception("Programming flash failed.");
        }

        for (int i = 0; i < 3000; i++)
        {
            var read_stt_cmd_result = await SendMessage(new()
            {
                ReadStatus = 1
            });

            UInt16 progress_val = (UInt16)read_stt_cmd_result.Status;

            if ((read_stt_cmd_result.Status & (1u << 18)) != 0)
            {
                throw new Exception("CRC invalid.");
            }
            // System.Diagnostics.Debug.WriteLine($"{LogPrefix()}Programming progress: {progress_val}%");

            Console.WriteLine($"{LogPrefix()}Programming progress: {progress_val}%");

            if (progress_val == 100)
            {
                return;
            }

            await Task.Delay(100);
        }
    }

    public event Action<RepMessage> OnMessageReceived;
}
