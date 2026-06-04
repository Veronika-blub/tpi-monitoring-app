using System.Collections.Generic;
using System.Globalization;

namespace HydroVue5_monitoring
{
    // Couche protocole : commandes SDI-12 et décodage des réponses (adresse 0 par défaut)
    class SdiProtocol
    {
        // commande de mesure
        public string MeasureCommand()
        {
            return "0M!";
        }

        // commande de lecture des données
        public string DataCommand()
        {
            return "0D0!";
        }

        // décode une trame du type 0+24.025+50.421 en température et humidité
        public bool ParseValues(string response, out double temperature, out double humidity)
        {
            temperature = 0;
            humidity = 0;

            if (response == null || response.Length < 3)
            {
                return false;
            }

            // le premier caractère est l'adresse du capteur, on le retire
            string payload = response.Trim().Substring(1);

            // chaque valeur commence par son signe (+ ou -), on reconstruit les nombres un par un
            // on ne peut pas juste Split('+') sinon une température négative perdrait son signe
            List<string> numbers = new List<string>();
            string current = "";

            for (int i = 0; i < payload.Length; i++)
            {
                char character = payload[i];
                bool isSign = character == '+' || character == '-';

                if (isSign && current.Length > 0)
                {
                    numbers.Add(current);
                    current = "";
                }
                current = current + character;
            }

            if (current.Length > 0)
            {
                numbers.Add(current);
            }

            if (numbers.Count < 2)
            {
                return false;
            }

            bool tempOk = double.TryParse(numbers[0], NumberStyles.Float, CultureInfo.InvariantCulture, out temperature);
            bool humOk = double.TryParse(numbers[1], NumberStyles.Float, CultureInfo.InvariantCulture, out humidity);

            if (!tempOk || !humOk)
            {
                return false;
            }

            return true;
        }
    }
}