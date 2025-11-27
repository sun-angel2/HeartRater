using System;
using System.Globalization;
using System.Threading;

namespace PulseLink.Services;

public class LocalizationService
{
    public event Action? LanguageChanged;

    public void SetLanguage(string culture)
    {
        try
        {
            var cultureInfo = new CultureInfo(culture);
            Thread.CurrentThread.CurrentCulture = cultureInfo;
            Thread.CurrentThread.CurrentUICulture = cultureInfo;
            
            LanguageChanged?.Invoke();
        }
        catch (CultureNotFoundException)
        {
            // Fallback or log
        }
    }
}