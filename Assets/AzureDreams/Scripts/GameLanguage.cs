using UnityEngine;

namespace AzureDreamsReimagined
{
    internal static class GameLanguage
    {
        private const string Preference = "ad_language";
        private const string English = "en";
        private const string BrazilianPortuguese = "pt-BR";
        private static string _selectedCode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSelection() => _selectedCode = null;

        private static string Normalize(string code) => code == BrazilianPortuguese ? BrazilianPortuguese : English;

        // Current game strings are supplied in both languages by their callers.
        // Keep drawing independent of Localization table and catalog initialization.
        internal static bool Portuguese =>
            (_selectedCode ??= Normalize(PlayerPrefs.GetString(Preference, English))) == BrazilianPortuguese;

        internal static string Text(string en, string ptBr) => Portuguese && !string.IsNullOrEmpty(ptBr) ? ptBr : en;

        internal static void Select(string code, bool save = true)
        {
            _selectedCode = Normalize(code);
            if (!save) return;
            PlayerPrefs.SetString(Preference, _selectedCode);
            PlayerPrefs.Save();
        }
    }
}
