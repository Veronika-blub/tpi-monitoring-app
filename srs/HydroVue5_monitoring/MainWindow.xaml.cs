using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace HydroVue5_monitoring
{
    public partial class MainWindow : Window
    {
        private SdiPort sdiPort = new SdiPort();
        private SdiProtocol protocol = new SdiProtocol();

        private bool running;
        private int frequencySeconds = 5;
        private StreamWriter csvWriter;
        private string csvPath;

        // valeurs affichées dans le graphique temps réel
        private ObservableCollection<double> temperatureValues = new ObservableCollection<double>();
        private ObservableCollection<double> humidityValues = new ObservableCollection<double>();
        private ObservableCollection<string> timeLabels = new ObservableCollection<string>();

        public MainWindow()
        {
            InitializeComponent();
            RefreshPorts();
            SetupChart();
        }

        // remplit la liste déroulante avec les ports COM disponibles (EF-01)
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

        // prépare les deux courbes et les deux axes Y labellisés (EF-05, ENF-U04)
        // prépare les deux courbes et les deux axes Y labellisés (EF-05, ENF-U04)
        private void SetupChart()
        {
            LineSeries<double> temperatureSeries = new LineSeries<double>();
            temperatureSeries.Values = temperatureValues;
            temperatureSeries.Name = "Température (°C)";
            temperatureSeries.Fill = null;            // supprime la zone colorée sous la courbe
            temperatureSeries.GeometryFill = null;    // supprime les cercles sur la ligne
            temperatureSeries.GeometryStroke = null;
            temperatureSeries.ScalesYAt = 0;

            LineSeries<double> humiditySeries = new LineSeries<double>();
            humiditySeries.Values = humidityValues;
            humiditySeries.Name = "Humidité (%)";
            humiditySeries.Fill = null;
            humiditySeries.GeometryFill = null;
            humiditySeries.GeometryStroke = null;
            humiditySeries.ScalesYAt = 1;

            chart.Series = new ISeries[] { temperatureSeries, humiditySeries };

            Axis timeAxis = new Axis();
            timeAxis.Labels = timeLabels;
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

        // la fréquence peut changer pendant la session, on garde seulement >= 5 s (EF-06, CT-04)
        private void txtFrequency_TextChanged(object sender, TextChangedEventArgs e)
        {
            int value;
            bool valid = int.TryParse(txtFrequency.Text, out value);

            if (valid && value >= 5)
            {
                frequencySeconds = value;
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
                sdiPort.Open(portName);
                txtStatus.Text = "Connecté à " + portName;
                Log("system", "Port " + portName + " ouvert");
            }
            catch (UnauthorizedAccessException)
            {
                // le port existe mais un autre programme l'utilise déjà
                Log("system", "Port COM occupé");
            }
            catch (Exception error)
            {
                Log("system", "Ouverture du port impossible : " + error.Message);
            }
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!sdiPort.IsOpen)
            {
                Log("system", "Connectez d'abord un port COM");
                return;
            }

            if (running)
            {
                return;
            }

            // nouveau fichier CSV à chaque session (ENF-D03)
            Directory.CreateDirectory("dataCSV");
            string fileName = "monitoring_" + DateTime.Now.ToString("ddMMyyyy_HHmmss") + ".csv";
            csvPath = Path.Combine("dataCSV", fileName);

            if (csvWriter != null)
            {
                csvWriter.Close();
            }

            csvWriter = new StreamWriter(csvPath);
            csvWriter.AutoFlush = true;   // écriture immédiate, aucune mesure perdue si crash (ENF-P04)
            csvWriter.WriteLine("timestamp;temperature;humidite");

            running = true;
            txtStatus.Text = "Acquisition en cours";
            Log("system", "Démarrage de l'acquisition toutes les " + frequencySeconds + " s");

            Task.Run(AcquisitionLoop);
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            if (!running)
            {
                return;
            }

            running = false;
            txtStatus.Text = "Arrêté";
            Log("system", "Arrêt de l'acquisition");
        }

        // boucle exécutée en arrière-plan tant que la session est active (EF-02, EF-08)
        private void AcquisitionLoop()
        {
            while (running)
            {
                RunOneCycle();

                // attente entre deux mesures, découpée par secondes pour pouvoir s'arrêter vite
                int waited = 0;
                while (waited < frequencySeconds && running)
                {
                    Thread.Sleep(1000);
                    waited = waited + 1;
                }
            }
        }

        // un cycle = 1 tentative + jusqu'à 2 renvois avec BREAK, puis abandon (option B, EF-09)
        private void RunOneCycle()
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
                    // câble USB débranché ou port perdu en pleine session (ENF-P03)
                    Log("system", "Port COM perdu : " + error.Message);
                    running = false;
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

        // une tentative complète : 0M! -> atttn -> service request -> 0D0! -> données (EF-03)
        private bool TryMeasure()
        {
            // demande de mesure, avec BREAK car c'est le début de la séquence
            string measure = protocol.MeasureCommand();
            sdiPort.SendCommand(measure, true);
            Log("tx", measure + "   " + Describe(Encoding.ASCII.GetBytes(measure)));

            byte[] ack = sdiPort.ReadResponse(100);
            if (ack.Length == 0)
            {
                Log("system", "Timeout : pas de réponse à 0M!");
                return false;
            }
            Log("rx", Describe(ack));

            // le capteur envoie un service request quand la mesure est prête (jusqu'à ~1 s)
            byte[] serviceRequest = sdiPort.ReadResponse(2000);
            if (serviceRequest.Length == 0)
            {
                Log("system", "Timeout : pas de service request");
                return false;
            }
            Log("rx", Describe(serviceRequest));

            // demande des données, sans BREAK car elle suit le service request dans les 87 ms
            string dataCommand = protocol.DataCommand();
            sdiPort.SendCommand(dataCommand, false);
            Log("tx", dataCommand + "   " + Describe(Encoding.ASCII.GetBytes(dataCommand)));

            byte[] data = sdiPort.ReadResponse(100);
            if (data.Length == 0)
            {
                Log("system", "Timeout : pas de données");
                return false;
            }
            Log("rx", Describe(data));

            // décod de la trame
            string text = Encoding.ASCII.GetString(data);
            double temperature;
            double humidity;
            bool decoded = protocol.ParseValues(text, out temperature, out humidity);
            if (!decoded)
            {
                Log("system", "Réponse invalide : " + text.Trim());
                return false;
            }

            // contrôle de la plage physique de la sonde (manuel §6.1 et §6.2)
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

            // CSV : point décimal forcé, point-virgule en séparateur (ENF-D01, ENF-D02)
            string temperatureText = temperature.ToString("0.000", CultureInfo.InvariantCulture);
            string humidityText = humidity.ToString("0.000", CultureInfo.InvariantCulture);

            if (csvWriter != null)
            {
                csvWriter.WriteLine(timestamp + ";" + temperatureText + ";" + humidityText);
            }

            // mise à jour de l'interface depuis le thread d'arrière-plan
            Dispatcher.Invoke(() =>
            {
                txtTemperature.Text = temperature.ToString("0.0", CultureInfo.InvariantCulture) + " °C";
                txtHumidity.Text = humidity.ToString("0.0", CultureInfo.InvariantCulture) + " % RH";

                temperatureValues.Add(temperature);
                humidityValues.Add(humidity);
                timeLabels.Add(DateTime.Now.ToString("HH:mm:ss"));
            });
        }

        // met une trame en forme pour le log : hexadécimal + texte, comme l'analyse Wireshark
        private string Describe(byte[] data)
        {
            string hex = BitConverter.ToString(data);
            string ascii = Encoding.ASCII.GetString(data).Replace("\r", "").Replace("\n", "");
            return "hex: " + hex + "   ascii: " + ascii;
        }

        // ajoute une ligne horodatée et colorée : TX bleu, RX vert, système jaune (EF-11)
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

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            if (temperatureValues.Count == 0)
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
                File.Copy(csvPath, dialog.FileName, true);
                Log("system", "Données exportées vers " + dialog.FileName);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            running = false;
            sdiPort.Close();

            if (csvWriter != null)
            {
                csvWriter.Close();
            }
        }
    }
}