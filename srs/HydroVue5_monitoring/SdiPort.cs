///ETML
///Auteur: Veronika Skupovska
///Description: gestion du port serie et communication bas niveau

using System.IO.Ports;

namespace HydroVue5_monitoring
{
    
    class SdiPort
    {
        private SerialPort _port;

        public bool IsOpen
        {
            get
            {
                return _port != null && _port.IsOpen;
            }
        }

        //ouvre le port avec la config SDI12
        public void Open(string portName)
        {
            _port = new SerialPort(portName, 1200, Parity.Even, 7, StopBits.One);
            _port.Open();
        }

        public void Close()
        {
            if (_port != null && _port.IsOpen)
            {
                _port.Close();
            }
        }

        //envoie une commande, BREAK + marking 
        public void SendCommand(string command, bool withBreak)
        {
            if (withBreak)
            {
                _port.BreakState = true;
                Thread.Sleep(12);     // BREAK>= 12 ms
                _port.BreakState = false;
                Thread.Sleep(9);      // marking>= 8.33 ms
            }
            _port.Write(command);
        }

        //lit la réponse jusqu'au LF
        //timeoutMs délai max d'attente, tableau vide si le capteur reste muet
        public byte[] ReadResponse(int timeoutMs)
        {
            _port.ReadTimeout = timeoutMs;
            List<byte> received = new List<byte>();

            try
            {
                while (true)
                {
                    int value = _port.ReadByte();
                    byte data = (byte)value;
                    received.Add(data);

                    if (data== 0x0A)   //LF = fin de trame
                    {
                        break;
                    }
                }
            }
            catch (TimeoutException)
            {
                //le capteur n'a rien envoyé, timeout
            }

            return received.ToArray();
        }
    }
}