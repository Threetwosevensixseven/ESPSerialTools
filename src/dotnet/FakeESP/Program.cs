using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FakeESP
{
    class Program
    {
        public static SerialPort listener;

        static void Main(string[] args)
        {
            try
            {
                string port = (ConfigurationManager.AppSettings["ListeningSerialPort"] ?? "").Trim();
                string cval = (ConfigurationManager.AppSettings["ListeningSerialBaud"] ?? "").Trim();
                int baud;
                int.TryParse(cval, out baud);

                listener = new SerialPort(port, baud);
                var buffer = new byte[8192];
                int pos = 0;
                var enc = Encoding.ASCII;
                bool connIsOpen = false;
                bool isReceiving = false;
                ushort toReceive = 0;
                byte[] rBuffer = new byte[0];
                ushort rPos = 0;
                bool startup = true;
                //.*?AT\+CIPSTART="(?<Protocol>[A-Z]{3})","(?<Host>[^"]*?)",(?<Port>\d{1,5})\r\n
                var rStart = new Regex(".*?AT\\+CIPSTART=\"(?<Protocol>[A-Z]{3})\",\"(?<Host>[^\"]*?)\",(?<Port>\\d{1,5})\\r\\n");
                //.*?AT\+CIPSEND=(?<Count>\d{1,5})\r\n
                var rSend = new Regex(".*?AT\\+CIPSEND=(?<Count>\\d{1,5})\\r\\n");
                //.*?AT\+UART(?<Flavour>_CUR|)\?\r\n
                var rBaudQ = new Regex(@".*?AT\+UART(?<Flavour>_CUR|)\?\r\n");
                //.*?AT\+UART(?<Flavour>_CUR|_DEF|)=(?<Baud>\d{1,9}),\d,\d,\d,\d\r\n
                var rBaudSet = new Regex(@".*?AT\+UART(?<Flavour>_CUR|_DEF|)=(?<Baud>\d{1,9}),\d,\d,\d,\d\r\n");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Yellow = debug info; ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Green = RX text; ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("White = TX text");
                WriteStatus("Listening on " + port + " at " + baud + " baud...");
                while (true)
                {
                    bool success;
                    do
                    {
                        var b = listener.ReadByte(out success);
                        if (success)
                        {
                            if (!isReceiving && b == 0)
                            {
                            }
                            else if (startup && b == 213)
                            { 
                                // Work around different 3.01.10 joystick behaviour
                                // When UART is not directed to joystick port
                            }
                            else
                            {
                                startup = false;
                                buffer[pos++] = b;
                                Console.Write(Convert.ToChar(b));
                            }
                            if (isReceiving)
                            {
                                rBuffer[rPos++] = b;
                                //WriteStatus("Received " + rPos + " bytes so far");
                                toReceive--;
                                if (toReceive == 0)
                                {
                                    isReceiving = false;
                                    string rval = enc.GetString(rBuffer, 0, rBuffer.Length);
                                    WriteStatus("Received " + rBuffer.Length + " bytes, sending to printer...");
                                    SendWrite("\r\nRecv " + rBuffer.Length + " bytes\r\n\r\nSEND OK\r\n");
                                    WriteStatus("Processing AT commands again");
                                }
                                pos = 0;
                            }
                            string val = enc.GetString(buffer, 0, pos);
                            if (isReceiving)
                            {
                            }
                            else if (val.EndsWith("AT\r\n"))
                            {
                                pos = 0;
                                WriteStatus("Received AT test command");
                                SendWrite("\r\nOK\r\n");
                            }
                            else if (val.EndsWith("AT+GMR\r\n"))
                            {
                                pos = 0;
                                WriteStatus("ESP version was queried, sending sample info for 1.6.2.0/2.2.1");
                                SendWrite("AT+GMR\r\n" 
                                    + "AT version:1.6.2.0(Apr 13 2018 11:10:59)\r\n"
                                    + "SDK version:2.2.1(6ab97e9)\r\n"
                                    + "compile time:Jun  7 2018 19:34:27\r\n"
                                    + "Bin version(Wroom 02):1.6.2\r\n"
                                    + "OK\r\n");
                            }
                            else if (rBaudQ.IsMatch(val))
                            {
                                pos = 0;
                                var m = rBaudQ.Match(val);
                                string flavour = m.Groups["Flavour"].Value ?? "";
                                string flav = "Baud";
                                if (flavour == "_CUR")
                                    flav = "CURrent baud";
                                WriteStatus(flav + " was queried, returning " + baud);
                                SendWrite("+UART" + flavour + ":" + baud + ",8,1,0,0\r\nOK\r\n");
                            }
                            else if (rBaudSet.IsMatch(val))
                            {
                                pos = 0;
                                var m = rBaudSet.Match(val);
                                string flavour = m.Groups["Flavour"].Value ?? "";
                                string baudc = m.Groups["Baud"].Value ?? "";
                                string flav = "baud";
                                if (flavour == "_CUR")
                                    flav = "CURrent baud";
                                else if ((flavour == "_DEF"))
                                    flav = "DEFault baud";
                                int baudn;
                                if (!int.TryParse(m.Groups["Baud"].Value, out baudn))
                                {
                                    SendWrite("\r\nERROR\r\n");
                                }
                                else
                                {
                                    connIsOpen = true;
                                    WriteStatus("Preparing to change " + port + " " + flav + " to " + baudn);
                                    SendWrite("\r\nOK\r\n");
                                    listener.SetBaud(baudn);
                                    WriteStatus("Changed " + port + " " + flav + " to " + baudn);
                                }
                            }
                            else if (val.EndsWith("ATE0\r\n"))
                            {
                                pos = 0;
                                WriteStatus("Turning off remote echo");
                                SendWrite("\r\nOK\r\n");
                            }
                            else if (val.EndsWith("AT+CIPCLOSE\r\n"))
                            {
                                pos = 0;
                                if (connIsOpen)
                                {
                                    connIsOpen = false;
                                    WriteStatus("Disconnected previously open connection");
                                    SendWrite("\r\nOK\r\n");
                                }
                                else
                                {
                                    WriteStatus("Nothing was connected, so couldn't disconnect");
                                    SendWrite("\r\nERROR\r\n");
                                }
                            }
                            else if (val.EndsWith("AT+CIPMUX=0\r\n"))
                            {
                                pos = 0;
                                WriteStatus("Turning off connection multiplexing");
                                SendWrite("\r\nOK\r\n");
                            }
                            else if (rStart.IsMatch(val))
                            {
                                pos = 0;
                                var m = rStart.Match(val);
                                string protocol = m.Groups["Protocol"].Value ?? "";
                                string host = (m.Groups["Host"].Value ?? "").Trim();
                                ushort portn;

                                if (protocol != "TCP")
                                {
                                    SendWrite("\r\nERROR\r\n");
                                }
                                else if (string.IsNullOrEmpty(host))
                                {
                                    SendWrite("\r\nERROR\r\n");
                                }
                                else if (!UInt16.TryParse(m.Groups["Port"].Value, out portn))
                                {
                                    SendWrite("\r\nERROR\r\n");
                                }
                                else
                                {
                                    connIsOpen = true;
                                    WriteStatus("Connected to " + host + ":" + portn);
                                    SendWrite("CONNECT\r\n\r\nOK\r\n");
                                }
                            }
                            else if (rSend.IsMatch(val))
                            {
                                pos = 0;
                                var m = rSend.Match(val);
                                ushort uval;
                                if (!UInt16.TryParse(m.Groups["Count"].Value, out uval))
                                {
                                    SendWrite("\r\nERROR\r\n");
                                }
                                else
                                {
                                    toReceive = uval;
                                    isReceiving = true;
                                    rBuffer = new byte[toReceive];
                                    rPos = 0;
                                    WriteStatus("Waiting to receive " + toReceive + " bytes...");
                                    SendWrite("\r\nOK\r\n>");
                                }
                            }
                        }
                    }
                    while (success);
                    Thread.Sleep(1);
                }
            }
            finally
            {
                if (listener == null)
                    listener.Dispose();
                listener = null;
            }
        }

        static void SendWrite(string Text)
        {
            listener.Write(Text);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(Text);
            Console.ForegroundColor = ConsoleColor.Green;
        }

        static void WriteStatus(string Text)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            var now = DateTime.Now;
            Console.WriteLine(now.ToShortDateString() + " " + now.ToString("HH:mm:ss.fff") + ": " + (Text ?? ""));
            Console.ForegroundColor = ConsoleColor.Green;
        }
    }
}
