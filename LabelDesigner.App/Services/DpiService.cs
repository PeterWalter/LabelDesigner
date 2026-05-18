using System.Runtime.InteropServices;

namespace LabelDesigner.App.Services;

/// <summary>
/// Detects and caches the system DPI scaling factor once at app startup.
/// This avoids repeated WinRT calls and threading issues.
/// </summary>
public static class DpiService
{
    private static double? _cachedPixelsPerMm;
    private const double BASE_PpMm = 96.0 / 25.4; // 3.78 at 100% DPI

    /// <summary>
    /// Gets the cached DPI in pixels per millimeter. Call DetectDpi() first.
    /// </summary>
    public static double PixelsPerMm => _cachedPixelsPerMm ?? BASE_PpMm;

    /// <summary>
    /// Detects system DPI once at app startup (must be called from OnLaunched on UI thread).
    /// Safe to call multiple times—only detects on first call.
    /// </summary>
    public static void DetectDpi()
    {
        if (_cachedPixelsPerMm.HasValue)
            return; // Already detected

        try
        {
            // This call is safe here because we're in OnLaunched on the UI thread with CoreWindow ready
            var displayInfo = Windows.Graphics.Display.DisplayInformation.GetForCurrentView();
            double dpiScale = displayInfo.RawPixelsPerViewPixel;
            _cachedPixelsPerMm = BASE_PpMm * dpiScale;
        }
        catch (COMException ex)
        {
            // Not on UI thread or CoreWindow not ready—use default
            System.Diagnostics.Debug.WriteLine($"DPI detection failed: {ex.Message}. Using default 96 DPI.");
            _cachedPixelsPerMm = BASE_PpMm;
        }
        catch (Exception ex)
        {
            // Any other exception—use default
            System.Diagnostics.Debug.WriteLine($"Unexpected error during DPI detection: {ex.Message}. Using default 96 DPI.");
            _cachedPixelsPerMm = BASE_PpMm;
        }
    }
}
