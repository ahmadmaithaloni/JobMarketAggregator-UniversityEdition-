using System.Linq;

namespace ScraperAPI.Services.LocationMapper_Service
{
    public class LocationMapper :ILocationMapperService
    {
        private static readonly Dictionary<string, string> CityToCountryMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) // dectionary that contains the user input locations mapped to thier countries
        {
            // --- JORDAN ---
            { "amman", "Jordan" },
            { "irbid", "Jordan" },
            { "zarqa", "Jordan" },
            { "aqaba", "Jordan" },
            { "salt", "Jordan" },
            { "balqa", "Jordan" },
            { "madaba", "Jordan" },
            { "jerash", "Jordan" },
            { "mafraq", "Jordan" },
            { "karak", "Jordan" },
            { "tafila", "Jordan" },
            { "maan", "Jordan" },
            { "ajloun", "Jordan" },
                // --- SAUDI ARABIA (KSA) ---
            { "riyadh", "Saudi Arabia" },
            { "jeddah", "Saudi Arabia" },
            { "dammam", "Saudi Arabia" },
            { "khobar", "Saudi Arabia" },
            { "al khobar", "Saudi Arabia" },
            { "jubail", "Saudi Arabia" },
            { "mecca", "Saudi Arabia" },
            { "makkah", "Saudi Arabia" },
            { "medina", "Saudi Arabia" },
            { "madinah", "Saudi Arabia" },
            { "hofuf", "Saudi Arabia" },
            { "taif", "Saudi Arabia" },
            { "tabuk", "Saudi Arabia" },
            { "buraidah", "Saudi Arabia" },
            { "khamis mushait", "Saudi Arabia" },
            { "abha", "Saudi Arabia" },
            { "hail", "Saudi Arabia" },
            { "yanbu", "Saudi Arabia" },
            { "dhahran", "Saudi Arabia" },
                // --- UAE ---
            { "dubai", "UAE" },
            { "abu dhabi", "UAE" },
            { "sharjah", "UAE" },
            { "ajman", "UAE" },
            { "ras al khaimah", "UAE" },
            { "fujairah", "UAE" },
            { "umm al quwain", "UAE" },
            { "al ain", "UAE" },
                // --- EGYPT ---
            { "cairo", "Egypt" },
            { "alexandria", "Egypt" },
            { "giza", "Egypt" },
            { "sharm el sheikh", "Egypt" },
            { "hurghada", "Egypt" },
            { "mansoura", "Egypt" },
            { "luxor", "Egypt" },
            { "aswan", "Egypt" },
            { "tanta", "Egypt" },
            { "port said", "Egypt" },
            { "suez", "Egypt" },
            { "6th of october", "Egypt" },
            { "new cairo", "Egypt" },
                // --- QATAR ---
            { "doha", "Qatar" },
            { "al rayyan", "Qatar" },
            { "umm salal", "Qatar" },
            { "al wakrah", "Qatar" },
            { "al khor", "Qatar" },
            { "lusail", "Qatar" },
                // --- KUWAIT ---
            { "kuwait city", "Kuwait" },
            { "hawally", "Kuwait" },
            { "salmiya", "Kuwait" },
            { "farwaniya", "Kuwait" },
            { "fahaheel", "Kuwait" },
            { "jahra", "Kuwait" },
            { "mishref", "Kuwait" },
                // --- BAHRAIN ---
            { "manama", "Bahrain" },
            { "riffa", "Bahrain" },
            { "muharraq", "Bahrain" },
            { "hamad town", "Bahrain" },
            { "isa town", "Bahrain" },
                // --- OMAN ---
            { "muscat", "Oman" },
            { "salalah", "Oman" },
            { "sohar", "Oman" },
            { "seeb", "Oman" },
            { "ibri", "Oman" },
            { "nizwa", "Oman" },
                // --- LEBANON ---
            { "beirut", "Lebanon" },
            { "tripoli", "Lebanon" },
            { "sidon", "Lebanon" },
            { "jounieh", "Lebanon" },
            { "zahle", "Lebanon" },
            { "byblos", "Lebanon" },
                // --- IRAQ ---
            { "baghdad", "Iraq" },
            { "basra", "Iraq" },
            { "erbil", "Iraq" },
            { "mosul", "Iraq" },
            { "sulaymaniyah", "Iraq" },
            { "kirkuk", "Iraq" },
            { "karbala", "Iraq" },
            { "najaf", "Iraq" },
                // --- MOROCCO ---
            { "casablanca", "Morocco" },
            { "rabat", "Morocco" },
            { "marrakesh", "Morocco" },
            { "tangier", "Morocco" },
            { "agadir", "Morocco" },
            { "fez", "Morocco" },
                // --- TUNISIA ---
            { "tunis", "Tunisia" },
            { "sfax", "Tunisia" },
            { "sousse", "Tunisia" },
            { "kairouan", "Tunisia" },
                // --- ALGERIA ---
            { "algiers", "Algeria" },
            { "oran", "Algeria" },
            { "constantine", "Algeria" },
            { "annaba", "Algeria" },
                // --- UNITED KINGDOM ---
            { "london", "UK" },
            { "manchester", "UK" },
            { "birmingham", "UK" },
            { "leeds", "UK" },
            { "glasgow", "UK" },
            { "liverpool", "UK" },
            { "bristol", "UK" },
            { "edinburgh", "UK" },
            { "cardiff", "UK" }

        };

        private static readonly List<string> MenaCountries = new List<string>
        {
            // Middle East
            "jordan", "saudi arabia", "uae", "egypt", "qatar", "kuwait", "bahrain", "oman", "lebanon", "iraq",
    
            // North Africa
            "morocco", "tunisia", "algeria"
        };

        private static readonly List<string> UkCountries = new List<string>
        {
            "uk", "united kingdom"
        };

        private static readonly List<string> RandstadCountries = new List<string>
        {
            "usa", "canada", "india", "australia", "germany"
        };

        // the mapper function: 
        public string GetTargetScraperKey(string UserInput)
        {
            if (string.IsNullOrWhiteSpace(UserInput)) return "Global"; // if the user let the location field in the JobLocation field in the JobQuery, then the e location will be global

            // step 1: clean the input (make it in lowercase):
            string CleanedLocation = UserInput.Trim().ToLower();

            // now the code will compare the input to the keys -> to map the key with it's region:
            
            // a: map the input to the key:
            if (CityToCountryMap.ContainsKey(CleanedLocation))
            {
                CleanedLocation = CityToCountryMap[CleanedLocation].ToLower(); // store the key value in the cleaned location variable
            }

            // b: match the collections with the key that stored in the CleanedLocation variable:

            // -> with MENA:
            if (MenaCountries.Any(c => CleanedLocation.Contains(c)))
            {
                return "MENA";
            }

            // -> with UK:
            else if (UkCountries.Any(c => CleanedLocation.Contains(c)))
            {
                return "UK";
            }

            // -> with Randstad:
            else if (RandstadCountries.Any(c => CleanedLocation.Contains(c)))
            {
                return "Randstad";
            }

            //return global if no match found:
            return "Global";
        }

        // for map the location to country:
        public string MapLocationToCountry(string UserInput)
        {
            if (string.IsNullOrEmpty(UserInput)) return "international";

            // clean the location (like in the previous function):
            string CleanedLocation = UserInput.Trim().ToLower();

            // map the input to the key:
            if (CityToCountryMap.ContainsKey(CleanedLocation))
            {
                return CityToCountryMap[CleanedLocation]; // any user input will be cleaned and matched to the country name and returned in string var
            }

            // if the location is not found in the dictionary, return "international"
            return "international";

        }
    }
}
