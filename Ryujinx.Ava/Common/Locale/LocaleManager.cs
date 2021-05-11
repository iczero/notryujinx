using Ryujinx.Common;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace Ryujinx.Ava.Common.Locale
{
    class LocaleManager
    {
        private const string DefaultLanguageCode = "en_US";

        private Dictionary<string, string> _localeStrings;

        public static LocaleManager Instance { get; } = new LocaleManager();

        public LocaleManager()
        {
            _localeStrings = new Dictionary<string, string>();

            Load();
        }

        public void Load()
        {
            string localeLanguageCode = CultureInfo.CurrentCulture.Name.Replace('-', '_');

            // Load english first, if the target language translation is incomplete, we default to english.
            LoadLanguage(DefaultLanguageCode);

            if (localeLanguageCode != DefaultLanguageCode)
            {
                LoadLanguage(localeLanguageCode);
            }
        }

        public string this[string key]
        {
            get
            {
                if (_localeStrings.TryGetValue(key, out string value))
                {
                    return value;
                }

                return key;
            }
        }

        private void LoadLanguage(string languageCode)
        {
            string languageJson = EmbeddedResources.ReadAllText($"Ryujinx.Ava/Assets/Locales/{languageCode}.json");

            if (languageJson == null)
            {
                return;
            }

            _localeStrings = JsonSerializer.Deserialize<Dictionary<string, string>>(languageJson);
        }
    }
}