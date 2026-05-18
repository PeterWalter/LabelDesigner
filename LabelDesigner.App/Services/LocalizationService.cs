using System.Globalization;
using System.Resources;

namespace LabelDesigner.App.Services;

public static class LocalizationService
{
    private static readonly ResourceManager ResourceManager =
        new("LabelDesigner.App.Resources.Strings", typeof(LocalizationService).Assembly);

    public static string Get(string key)
    {
        return ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }
}
