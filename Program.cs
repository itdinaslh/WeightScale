using System;
using System.Data.Common;
using System.IO.Ports;
using System.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using System.Runtime.InteropServices;

#nullable disable

public class PortScale
{
    static bool _continue;
    static HubConnection connection;
    static SerialPort _serialPort;

    static string message;

    public static async Task Main()
    {
        connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:7108/hub/scale")
            .WithAutomaticReconnect(new ScaleRetryPolicy())           
            .Build();

        await OpenSignalR();

        string name;
        
        Thread readThread = null;
        StringComparer stringComparer = StringComparer.OrdinalIgnoreCase;
        try
        {
            readThread = new Thread(Read);
        } catch (Exception)
        {
            Console.WriteLine("Please wait...");
        }
        

        // Create a new SerialPort object with default settings.
        _serialPort = new SerialPort();

        // Allow the user to set the appropriate properties.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            _serialPort.PortName = "COM4";
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            _serialPort.PortName = "/dev/ttyUSB0";
        }
        
        _serialPort.BaudRate = 9600;
        _serialPort.Parity = Parity.Even;
        _serialPort.DataBits = 7;
        _serialPort.StopBits = StopBits.One;
        _serialPort.Handshake = Handshake.None;

        //_serialPort.PortName = SetPortName(_serialPort.PortName);
        //_serialPort.BaudRate = SetPortBaudRate(_serialPort.BaudRate);
        //_serialPort.Parity = SetPortParity(_serialPort.Parity);
        //_serialPort.DataBits = SetPortDataBits(_serialPort.DataBits);
        //_serialPort.StopBits = SetPortStopBits(_serialPort.StopBits);
        //_serialPort.Handshake = SetPortHandshake(_serialPort.Handshake);

        // Set the read/write timeouts
        _serialPort.ReadTimeout = 500;
        _serialPort.WriteTimeout = 500;
        
        _continue = true;
        

        try {
            _serialPort.Open();
            readThread.Start();
        } catch (Exception ex) {
            Console.WriteLine(ex.Message.ToString());
        }

        Console.Write("Push data to server...");
        name = Console.ReadLine();

        Console.WriteLine("Type QUIT to exit");

        while (_continue)
        {
            message = Console.ReadLine();

            if (stringComparer.Equals("quit", message))
            {
                _continue = false;
            }
            else
            {
                _serialPort.WriteLine(
                    String.Format("<{0}>: {1}", name, message));
            }
        }

        //Console.WriteLine("Push data to server...");
        //Console.WriteLine("Press CTRL + C to exit");

        readThread.Join();
        _serialPort.Close();
    }

    public static async void Read()
    {
        while (_continue)
        {
            try
            {
                string readData = _serialPort.ReadLine();
                // _serialPort.DataReceived += 
                int value = 0;
                string data = "";
                if (readData is not null || readData != "" && readData.Length > 9)
                {
                    data = readData.Substring(9, 5);
                    if (data.Substring(0, 1) != "0")
                    {
                        int.TryParse(data, out value);
                    }
                    else if (data.Substring(1, 1) != "0")
                    {
                        string cur = data.Substring(1, 4);
                        int.TryParse(cur, out value);
                    }
                    else if (data.Substring(2, 1) != "0")
                    {
                        string cur = data.Substring(2, 3);
                        value = int.Parse(cur);
                    }
                    else if (data.Substring(3, 1) != "0")
                    {
                        string cur = data.Substring(3, 2);
                        value = int.Parse(cur);
                    }
                    else
                    {
                        value = 0;
                    }
                }

                string val = value.ToString();

                try
                {
                    await connection.InvokeAsync("Timbangan1", val);
                    //Console.WriteLine(val);
                    //Console.WriteLine(val);
                }
                catch (Exception ex)
                {
                    //await OpenSignalR();
                    Console.WriteLine(ex);
                }
                
            }
            catch (Exception ex) {
                //if (conn.State == HubConnectionState.Disconnected)
                //{                    
                //    await OpenSignalR();                    
                //}
                Console.WriteLine(ex);
            }
        }
    }

    private void OnDataReceived() {
        while (_serialPort.BytesToRead > 0) {
            message += _serialPort.ReadByte();
        }
    }

    private static void TryConnectSerial()
    {
        if (!_serialPort.IsOpen)
        {
            _serialPort.Open();
        }
    }

    private static async Task OpenSignalR()
    {
        try
        {
            await connection.StartAsync();
            if (connection.State == HubConnectionState.Connected)
            {
                Console.WriteLine("Connected to server");
            }
        } catch (Exception)
        {            
            Console.WriteLine("Reconnecting, please wait...");
            OnDisconnected();
        }
        
    }

    static void OnDisconnected()
    {
        Console.WriteLine("connection closed");
        var t = connection.StartAsync();

        bool result = false;
        t.ContinueWith(task =>
        {
            if (!task.IsFaulted)
            {
                result = true;
            }
        }).Wait();

        if (!result)
        {
            OnDisconnected();
        }
    }

    // Display Port values and prompt user to enter a port.
    public static string SetPortName(string defaultPortName)
    {
        string portName;

        Console.WriteLine("Available Ports:");
        foreach (string s in SerialPort.GetPortNames())
        {
            Console.WriteLine("   {0}", s);
        }

        Console.Write("Enter COM port value (Default: {0}): ", defaultPortName);
        portName = Console.ReadLine();

        if (portName == "" || !(portName.ToLower()).StartsWith("com"))
        {
            portName = defaultPortName;
        }
        return portName;
    }
    // Display BaudRate values and prompt user to enter a value.
    public static int SetPortBaudRate(int defaultPortBaudRate)
    {
        string baudRate;

        Console.Write("Baud Rate(default:{0}): ", defaultPortBaudRate);
        baudRate = Console.ReadLine();

        if (baudRate == "")
        {
            baudRate = defaultPortBaudRate.ToString();
        }

        return int.Parse(baudRate);
    }

    // Display PortParity values and prompt user to enter a value.
    public static Parity SetPortParity(Parity defaultPortParity)
    {
        string parity;

        Console.WriteLine("Available Parity options:");
        foreach (string s in Enum.GetNames(typeof(Parity)))
        {
            Console.WriteLine("   {0}", s);
        }

        Console.Write("Enter Parity value (Default: {0}):", defaultPortParity.ToString(), true);
        parity = Console.ReadLine();

        if (parity == "")
        {
            parity = defaultPortParity.ToString();
        }

        return (Parity)Enum.Parse(typeof(Parity), parity, true);
    }
    // Display DataBits values and prompt user to enter a value.
    public static int SetPortDataBits(int defaultPortDataBits)
    {
        string dataBits;

        Console.Write("Enter DataBits value (Default: {0}): ", defaultPortDataBits);
        dataBits = Console.ReadLine();

        if (dataBits == "")
        {
            dataBits = defaultPortDataBits.ToString();
        }

        return int.Parse(dataBits.ToUpperInvariant());
    }

    // Display StopBits values and prompt user to enter a value.
    public static StopBits SetPortStopBits(StopBits defaultPortStopBits)
    {
        string stopBits;

        Console.WriteLine("Available StopBits options:");
        foreach (string s in Enum.GetNames(typeof(StopBits)))
        {
            Console.WriteLine("   {0}", s);
        }

        Console.Write("Enter StopBits value (None is not supported and \n" +
         "raises an ArgumentOutOfRangeException. \n (Default: {0}):", defaultPortStopBits.ToString());
        stopBits = Console.ReadLine();

        if (stopBits == "")
        {
            stopBits = defaultPortStopBits.ToString();
        }

        return (StopBits)Enum.Parse(typeof(StopBits), stopBits, true);
    }
    public static Handshake SetPortHandshake(Handshake defaultPortHandshake)
    {
        string handshake;

        Console.WriteLine("Available Handshake options:");
        foreach (string s in Enum.GetNames(typeof(Handshake)))
        {
            Console.WriteLine("   {0}", s);
        }

        Console.Write("Enter Handshake value (Default: {0}):", defaultPortHandshake.ToString());
        handshake = Console.ReadLine();

        if (handshake == "")
        {
            handshake = defaultPortHandshake.ToString();
        }

        return (Handshake)Enum.Parse(typeof(Handshake), handshake, true);
    }
}

public class ScaleRetryPolicy : IRetryPolicy {
    private int value = 0;

    public TimeSpan? NextRetryDelay(RetryContext context) {
        if (context.ElapsedTime < TimeSpan.FromMinutes(3)) {
            value += 1;
            return TimeSpan.FromSeconds(value * 5);            
        } else {
            return null;
        }
    }
}