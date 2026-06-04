using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;

namespace HydroVue5_monitoring
{
    
    class SdiPort
    {
        private SerialPort port;

        public bool IsOpen
        {
            get
            {
                return port != null && port.IsOpen;
            }
        }

        //ouvre le port avec la config SDI12
        public void Open(string portName)
        {
            port = new SerialPort(portName, 1200, Parity.Even, 7, StopBits.One);
            port.Open();
        }

        public void Close()
        {
            if (port != null && port.IsOpen)
            {
                port.Close();
            }
        }

        //envoie une commande, BREAK + marking 
        public void SendCommand(string command, bool withBreak)
        {
            if (withBreak)
            {
                port.BreakState = true;
                Thread.Sleep(12);     // BREAK>= 12 ms
                port.BreakState = false;
                Thread.Sleep(9);      // marking >= 8.33 ms
            }
            port.Write(command);
        }

        //lit la réponse jusqu'au LF
        //timeoutMs délai max d'attente, tableau vide si le capteur reste muet
        public byte[] ReadResponse(int timeoutMs)
        {
            port.ReadTimeout = timeoutMs;
            List<byte> received = new List<byte>();

            try
            {
                while (true)
                {
                    int value = port.ReadByte();
                    byte data = (byte)value;
                    received.Add(data);

                    if (data == 0x0A)   //LF = fin de trame
                    {
                        break;
                    }
                }
            }
            catch (TimeoutException)
            {
                // le capteur n'a rien envoyé, timeout
            }

            return received.ToArray();
        }
    }
}