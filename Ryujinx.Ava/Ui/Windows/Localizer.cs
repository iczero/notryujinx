using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace Ryujinx.Ava.Ui.Windows
{
    public class Localizer : INotifyPropertyChanged
    {
        private const string EnglishLanguageCode = "eng";

        private readonly Dictionary<string, string> _strings;

        public Localizer()
        {
            _strings = new Dictionary<string, string>();

            LoadSystemLanguage();
        }

        public static Localizer Instance { get; } = new();

        public string this[string key]
        {
            get
            {
                if (_strings.TryGetValue(key, out string value))
                {
                    return value;
                }

                return key;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void LoadSystemLanguage()
        {
            LoadLanguage(CultureInfo.CurrentUICulture.ThreeLetterISOLanguageName);
        }

        public void LoadLanguage(string languageCode)
        {
            _strings.Clear();

            // Load english first, if the target language translation is incomplete, we default to english.
            LoadLanguageImpl(EnglishLanguageCode);

            if (languageCode != EnglishLanguageCode)
            {
                LoadLanguageImpl(languageCode);
            }
        }

        private void LoadLanguageImpl(string languageCode)
        {
            LocalizationLoader.LoadFromEmbeddedResource(_strings, GetEmbeddedPath(languageCode));
        }

        private static string GetEmbeddedPath(string languageCode)
        {
            return $"Ryujinx.Ava.Ui.Resources.UiStrings_{languageCode}.txt";
        }
    }
}