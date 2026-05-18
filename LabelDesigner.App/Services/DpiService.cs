using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace LabelDesigner.App.Services;

public static class DpiService
{
    private const double MillimetersPerInch = 25.4;
    private static double? _cachedPixelsPerMm;

    public static double PixelsPerMm => _cachedPixelsPerMm ?? (GetDpiForSystem() / MillimetersPerInch);

    public static bool InitializeForWindow(Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        _cachedPixelsPerMm = GetDpiForWindow(hwnd) / MillimetersPerInch;
        return true;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
}
