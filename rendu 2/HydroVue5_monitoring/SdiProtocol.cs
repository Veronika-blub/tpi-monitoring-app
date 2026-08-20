///ETML
///Auteur: Veronika Skupovska
///Description: commandes SDI-12 et decodage des reponses du capteur

using System.Globalization;

namespace HydroVue5_monitoring
{
    class SdiProtocol
    {
        // commande de mesure
        public string MeasureCommand()
        {
            return "0M!";
        }

        // commande de lecture de données
        public string DataCommand()
        {
            return "0D0!";
        }

        /// <summary>
        /// Decode une reponse du type 0+24.025+50.421 en temperature et humidite
        /// </summary>
        /// <returns>false si la reponse est invalide ou hors plage</returns>
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

            // chaque valeur commence par son signe
            List<string> numbers = new List<string>();
            string current = "";

            for (int i = 0; i < payload.Length; i++)
            {
                char character = payload[i];
                bool isSign = character == '+' || character== '-';

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

            if (!double.TryParse(numbers[0], NumberStyles.Float, CultureInfo.InvariantCulture, out temperature) 
                || !double.TryParse(numbers[1], NumberStyles.Float, CultureInfo.InvariantCulture, out humidity))
            {
                return false;
            }

            return true;
        }
    }
}