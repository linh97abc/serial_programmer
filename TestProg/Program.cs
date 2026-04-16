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
Console.WriteLine($" -- FW: {FwFilePath}");
Console.WriteLine($" -- SW: {SwFilePath}");

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

Console.WriteLine("Programming completed successfully.");
Console.Read();
