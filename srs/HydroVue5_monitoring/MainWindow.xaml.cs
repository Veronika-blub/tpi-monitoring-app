///ETML
///Auteur: Veronika Skupovska
///Description: fenetre principale de l'application de monitoring
///gere l'interface, le cycle d'acquisition SDI-12 et l'export csv

using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace HydroVue5_monitoring
{
    public partial class MainWindow : Window
    {
        private SdiPort _sdiPort = new SdiPort();
        private SdiProtocol _protocol = new SdiProtocol();

        private bool _running;
        private int _frequencySeconds = 5;
        private StreamWriter _csvWriter;
        private string _csvPath;

        // valeurs affichées dans le graphique temps réel
        private ObservableCollection<double> _temperatureVals = new ObservableCollection<double>();
        private ObservableCollection<double> _humidityVals = new ObservableCollection<double>();
        private ObservableCollection<string> _timeLabels = new ObservableCollection<string>();

        public MainWindow()
        {
            InitializeComponent();
            RefreshPorts();
            SetupChart();
        }

        // remplit la liste déroulante avec les ports COM disponibles
        private void RefreshPorts()
        {
            cmbPorts.Items.Clear();
            string[] ports = SerialPort.GetPortNames();

            if (ports.Length == 0)
            {
                Log("system", "Aucun port COM détecté");
                return;
            }

            foreach (string portName in ports)
            {
                cmbPorts.Items.Add(portName);
            }
            cmbPorts.SelectedIndex = 0;
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshPorts();
        }

        private void SetupChart()
        {
            LineSeries<double> temperatureSeries = new LineSeries<double>();
            temperatureSeries.Values = _temperatureVals;
            temperatureSeries.Name = "Température (°C)";
            temperatureSeries.Fill = null;            // supprime la zone colorée sous la courbe
            temperatureSeries.GeometryFill = null;    // supprime les cercles sur la ligne
            temperatureSeries.GeometryStroke = null;
            temperatureSeries.ScalesYAt = 0;

            LineSeries<double> humiditySeries = new LineSeries<double>();
            humiditySeries.Values = _humidityVals;
            humiditySeries.Name = "Humidité (%)";
            humiditySeries.Fill = null;
            humiditySeries.GeometryFill = null;
            humiditySeries.GeometryStroke = null;
            humiditySeries.ScalesYAt = 1;

            chart.Series = new ISeries[] { temperatureSeries, humiditySeries };

            Axis timeAxis = new Axis();
            timeAxis.Labels = _timeLabels;
            chart.XAxes = new Axis[] { timeAxis };

            Axis temperatureAxis = new Axis();
            temperatureAxis.Name = "°C";
            temperatureAxis.Labeler = value => value.ToString("0.0") + " °C";

            Axis humidityAxis = new Axis();
            humidityAxis.Name = "% RH";
            humidityAxis.Labeler = value => value.ToString("0.0") + " %";
            humidityAxis.Position = LiveChartsCore.Measure.AxisPosition.End;

            chart.YAxes = new Axis[] { temperatureAxis, humidityAxis };
        }

        // la fréquence peut changer pendant la session, seulement >= 5
        private void txtFrequency_TextChanged(object sender, TextChangedEventArgs e)
        {
            int value;
            bool valid = int.TryParse(txtFrequency.Text, out value);

            if (valid && value >= 5)
            {
                _frequencySeconds = value;
            }
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (cmbPorts.SelectedItem == null)
            {
                Log("system", "Aucun port sélectionné");
                return;
            }

            string portName = cmbPorts.SelectedItem.ToString();

            try
            {
                _sdiPort.Open(portName);
                txtStatus.Text = "Connecté à " + portName;
                Log("system", "Port " + portName + " ouvert");
            }
            catch (UnauthorizedAccessException)
            {
                // le port existe mais un autre programme l'utilise
                Log("system", "Port COM occupé");
            }
            catch (Exception error)
            {
                Log("system", "Ouverture du port impossible : " + error.Message);
            }
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!_sdiPort.IsOpen)
            {
                Log("system", "Connectez d'abord un port COM");
                return;
            }

            if (_running)
            {
                return;
            }

            // nouveau fichier CSV à chaque session
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string csvFolder = Path.Combine(documents, "MonitoringHygroVue5");
            Directory.CreateDirectory(csvFolder);
            string fileName = "monitoring_" + DateTime.Now.ToString("ddMMyyyy_HHmmss") + ".csv";
            _csvPath = Path.Combine(csvFolder, fileName);

            if (_csvWriter != null)
            {
                _csvWriter.Close();
            }

            _csvWriter = new StreamWriter(_csvPath);
            _csvWriter.AutoFlush = true;   //écriture immédiate
            _csvWriter.WriteLine("timestamp;temperature;humidite");

            _running = true;
            txtStatus.Text = "Acquisition en cours";
            Log("system", "Démarrage de l'acquisition toutes les " +_frequencySeconds + " s");

            Task.Run(AcquisitionLoop);
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            txtStatus.Text = "Arrêté";
            Log("system", "Arrêt de l'acquisition");
        }

        /// <summary>
        /// Boucle d'acquisition executee en arriere-plan.
        /// Tourne tant que _running est vrai
        /// </summary>
        private void AcquisitionLoop()
        {
            while (_running)
            {
                RunCycle();

                // attente entre deux mesures, découpée par secondes pour pouvoir s'arrêter vite
                int waited = 0;
                while (waited< _frequencySeconds && _running)
                {
                    Thread.Sleep(1000);
                    waited = waited + 1;
                }
            }
        }

        // un cycle = un tentative jusqu'à 2 renvois avec BREAK, puis abandon
        private void RunCycle()
        {
            int attempt = 1;
            while (attempt <= 3)
            {
                try
                {
                    if (TryMeasure())
                    {
                        return;
                    }
                }
                catch (Exception error)
                {
                    // câble USB débranché ou port perdu en pleine session
                    Log("system", "Port COM perdu : " + error.Message);
                    _running = false;
                    return;
                }

                attempt = attempt + 1;
                if (attempt <= 3)
                {
                    Log("system", "Nouvelle tentative (" + attempt + "/3)");
                }
            }

            Log("system", "Échec du cycle, on réessaiera au prochain");
        }

        /// <summary>
        /// Execute un cycle SDI-12 complet
        /// </summary>
        /// <returns>true si la mesure a reussi, false sinon</returns>
        private bool TryMeasure()
        {
            // demande de mesure, avec BREAK car c'est le début de la séquence
            string measure = _protocol.MeasureCommand();
            _sdiPort.SendCommand(measure, true);
            
            Log("tx", Encoding.ASCII.GetBytes(measure));

            byte[] ack = _sdiPort.ReadResponse(100);
            if (ack.Length == 0)
            {
                Log("system", "Timeout : pas de réponse à 0M!");
                return false;
            }
            Log("rx", ack);

            // le capteur envoie un service request quand la mesure est prête
            byte[] serviceRequest = _sdiPort.ReadResponse(2000);
            if (serviceRequest.Length == 0)
            {
                Log("system", "Timeout : pas de service request");
                return false;
            }
            Log("rx", serviceRequest);

            // demande des données, sans BREAK car elle suit le service request dans les 87 ms
            string dataCommand = _protocol.DataCommand();
            _sdiPort.SendCommand(dataCommand, false);
            Log("tx", Encoding.ASCII.GetBytes(dataCommand));

            byte[] data = _sdiPort.ReadResponse(100);
            if (data.Length == 0)
            {
                Log("system", "Timeout : pas de données");
                return false;
            }
            Log("rx", data);

            // décod de la trame
            string text = Encoding.ASCII.GetString(data);
            double temperature;
            double humidity;
            if (!_protocol.ParseValues(text, out temperature, out humidity))
                {
                Log("system", "Réponse invalide : " + text.Trim());
                return false;
            }

            // contrôle de la plage physique de la sonde
            if (temperature < -40 || temperature > 70 || humidity < 0 || humidity > 100)
            {
                Log("system", "Valeur hors plage : " + text.Trim());
                return false;
            }

            ShowMeasurement(temperature, humidity);
            return true;
        }

        private void ShowMeasurement(double temperature, double humidity)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

            // CSV: point décimal forcé, point-virgule en séparateur
            string temperatureText = temperature.ToString("0.000", CultureInfo.InvariantCulture);
            string humidityText = humidity.ToString("0.000", CultureInfo.InvariantCulture);

            if (_csvWriter != null)
            {
                _csvWriter.WriteLine(timestamp + ";" + temperatureText + ";" + humidityText);
            }

            // mise à jour de l'interface
            Dispatcher.Invoke(() =>
            {
                txtTemperature.Text = temperature.ToString("0.0", CultureInfo.InvariantCulture) + " °C";
                txtHumidity.Text = humidity.ToString("0.0", CultureInfo.InvariantCulture) + " % RH";
                _temperatureVals.Add(temperature);
                _humidityVals.Add(humidity);
                _timeLabels.Add(DateTime.Now.ToString("HH:mm:ss"));
            });
        }

        // ajoute une ligne horodatée et colorée : TX bleu, RX vert, système jaune
        private void Log(string direction, string message)
        {
            Dispatcher.Invoke(() =>
            {
                string time = DateTime.Now.ToString("HH:mm:ss");
                string prefix = "";
                SolidColorBrush color;

                if (direction == "tx")
                {
                    prefix = "TX ";
                    color = Brushes.DeepSkyBlue;
                }
                else if (direction == "rx")
                {
                    prefix = "RX ";
                    color = Brushes.LightGreen;
                }
                else
                {
                    color = Brushes.Khaki;
                }

                Run line = new Run("[" + time + "] " + prefix + message + "\n");
                line.Foreground = color;
                logParagraph.Inlines.Add(line);
                txtLog.ScrollToEnd();
            });
        }

        //surcharge du methode log
        private void Log(string direction, byte[] data)
        {
            string hex = BitConverter.ToString(data);
            string ascii = Encoding.ASCII.GetString(data).Replace("\r", "").Replace("\n", "");
            Log(direction, "hex: "+ hex+ "\nascii: "+ ascii);
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_temperatureVals.Count == 0)
            {
                Log("system", "Aucune donnée à exporter");
                return;
            }

            // le CSV de session existe déjà dans dataCSV, ici on le copie où l'utilisateur veut
            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog();
            dialog.Filter = "Fichier CSV|*.csv";
            dialog.FileName = "export_" + DateTime.Now.ToString("ddMMyyyy_HHmmss") + ".csv";

            if (dialog.ShowDialog() == true)
            {
                File.Copy(_csvPath, dialog.FileName, true);
                Log("system", "Données exportées vers " + dialog.FileName);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _running = false;
            _sdiPort.Close();

            if (_csvWriter != null)
            {
                _csvWriter.Close();
            }
        }
    }
}