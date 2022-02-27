using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FakeESP
{
    public class SerialPort : IDisposable
    {
        private System.IO.Ports.SerialPort port;

        public SerialPort(string portName, int baudRate)
        {
            port = new System.IO.Ports.SerialPort();
            port.PortName = portName;
            port.BaudRate = baudRate;
            port.Parity = System.IO.Ports.Parity.None;
            port.DataBits = 8;
            port.StopBits = System.IO.Ports.StopBits.One;
            port.Handshake = System.IO.Ports.Handshake.None;
            //port.ReadTimeout = 1;
            port.Open();
            //port.DataReceived += Port_DataReceived;
        }

        public byte ReadByte(out bool Success)
        {
            try
            {
                if (port != null)
                {
                    int v = port.ReadByte();
                    var b = Convert.ToByte(v);
                    Success = true;
                    return b;
                }
            }
            catch
            {
            }
            Success = false;
            return 0xff;
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (port != null)
                port.Write(buffer, offset, count);
        }

        public void Write(string text)
        {
            if (port != null)
            {
                var buff = Encoding.ASCII.GetBytes(text);
                port.Write(buff, 0, buff.Length);
            }
        }

        public void SetBaud(int baudRate)
        {
            port.BaudRate = baudRate;
        }

        private void Port_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            string x = e.EventType.ToString();
        }

        #region IDisposable Support

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    if (port != null)
                    {
                        if (port.IsOpen)
                            port.Close();
                        port.Dispose();
                        port = null;
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~SerialPort()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion IDisposable Support
    }
}
