using System;
using System.Linq;
using System.Windows;

namespace EnterpriseWorkReport.Services
{
    public enum AppTheme
    {
        PastelBlue,
        PastelGreen,
        PastelPurple,
        PastelPink,
        PastelOrange,
        DarkNavy,
        AmoledBlack
    }

    public static class ThemeService
    {
        private static AppTheme _currentTheme = AppTheme.PastelBlue;
        public static AppTheme CurrentTheme => _currentTheme;

        public static readonly string[] ThemeNames = {
            "Pastel Blue", "Pastel Green", "Pastel Purple", "Pastel Pink", "Pastel Orange", "Dark Navy", "AMOLED Black"
        };

        public static void SetTheme(AppTheme theme)
        {
            _currentTheme = theme;
            var mergedDicts = Application.Current.Resources.MergedDictionaries;
            
            var existingThemeDict = mergedDicts.FirstOrDefault(d => 
                d.Source != null && (
                    d.Source.OriginalString.Contains("Theme") ||
                    d.Source.OriginalString.Contains("Colors.xaml")
                ));

            if (existingThemeDict != null)
                mergedDicts.Remove(existingThemeDict);

            string themeUri = theme switch
            {
                AppTheme.PastelBlue => "Themes/PastelBlue.xaml",
                AppTheme.PastelGreen => "Themes/PastelGreen.xaml",
                AppTheme.PastelPurple => "Themes/PastelPurple.xaml",
                AppTheme.PastelPink => "Themes/PastelPink.xaml",
                AppTheme.PastelOrange => "Themes/PastelOrange.xaml",
                AppTheme.DarkNavy => "Themes/DarkNavy.xaml",
                AppTheme.AmoledBlack => "Themes/AmoledBlack.xaml",
                _ => "Themes/PastelBlue.xaml"
            };

            var newThemeDict = new ResourceDictionary { Source = new Uri(themeUri, UriKind.Relative) };
            mergedDicts.Insert(0, newThemeDict);
        }
    }
}