// See https://aka.ms/new-console-template for more information

using SerialProg;
using System;


// get argument from command line
if (args.Length == 0)
{
    Console.WriteLine("Please provide the serial port name as an argument.");
    return;
}

string PortName = args[0];
string FwFilePath = args[1];
string SwFilePath = args[2];

// string PortName = "COM5";
// string FwFilePath = "app_hw.bin";
// string SwFilePath = "app_sw.bin";

Console.WriteLine($"Serial Programmer: port {PortName}");

if (FwFilePath == "-")
{
    Console.WriteLine("Skip firmware programming.");
}
else
{
    Console.WriteLine($"Firmware file path: {FwFilePath}");
    if (!System.IO.File.Exists(FwFilePath))
    {
        Console.WriteLine($"Firmware file not found: {FwFilePath}");
        return;
    }
}


if (SwFilePath == "-")
{
    Console.WriteLine("Skip software programming.");
}
else
{
    Console.WriteLine($"Software file path: {SwFilePath}");
    if (!System.IO.File.Exists(SwFilePath))
    {
        Console.WriteLine($"Software file not found: {SwFilePath}");
        return;
    }
}


var serialProg = new SerialProg.SerialProg();

serialProg.SetPort(PortName);


try
{
    Console.WriteLine("Waiting for device to be ready...");
    await serialProg.StopDeviceBootProgress();
    await serialProg.SetupConnection();


}
catch (Exception ex)
{
    Console.WriteLine("Error setting up connection: " + ex.Message);
    return;
}


if (FwFilePath != "-")
{
    try
    {
        Console.WriteLine("Start program firmware...");
        await serialProg.ProgFlash(FwFilePath, 0x80000);
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error Programming Firmware: " + ex.Message);
        return;
    }
}

if (SwFilePath != "-")
{
    try
    {
        Console.WriteLine("Start program software...");
        await serialProg.ProgFlash(SwFilePath, 0x200000);
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error Programming Software: " + ex.Message);
        return;
    }
}

Console.WriteLine("Programming completed successfully.");
// Console.Read();
