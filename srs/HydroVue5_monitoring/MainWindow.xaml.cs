using System.Text;
using System.Windows;
using System.IO.Ports;


namespace HydroVue5_monitoring
{
    /// <summary>
    ///
    /// </summary>
    public partial class MainWindow : Window
    {
        private SerialPort? _port;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            _port = new SerialPort("COM3", 1200, Parity.Even, 7, StopBits.One);
            _port.DataReceived += Port_DataReceived;
            _port.Open();
        }

        private void Port_DataReceived(object sender, EventArgs e)
        {
            byte[] buffer = new byte[_port!.BytesToRead];
            _port.Read(buffer, 0, buffer.Length);
            string hex = BitConverter.ToString(buffer);
            string ascii = Encoding.ASCII.GetString(buffer);
            Dispatcher.Invoke(() => Log($"hex:  {hex}\nascii:   {ascii}\n----------------\n"));
        }


        private void Log(string message)
        {
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            txtLog.ScrollToEnd();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                _port!.BreakState = true;
                Thread.Sleep(12);
                _port.BreakState = false;
                Thread.Sleep(9);
                _port.WriteLine("0M!");
                Dispatcher.Invoke(() => Log(">Send: 0M!"));
            });
        }

        private void btnSend0D0_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                _port!.BreakState = true;
                Thread.Sleep(15);
                _port.BreakState = false;
                Thread.Sleep(9);
                _port.WriteLine("0D0!");
                Dispatcher.Invoke(() => Log(">Send: 0D0!"));
            });
        }
        private void btnDisconnect_Click(object sender, EventArgs e)
        { _port.Close();        }
    }
}