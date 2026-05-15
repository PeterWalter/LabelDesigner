using System.Runtime.InteropServices.WindowsRuntime;
using LabelDesigner.Infrastructure.Interfaces;
using Windows.Graphics.Imaging;
using ZXing;

namespace LabelDesigner.Infrastructure.Barcode;

public class BarcodeService : IBarcodeService
{
    public SoftwareBitmap Generate(string value, BarcodeFormat format, int w, int h)
    {
        if (w < 1) w = 1;
        if (h < 1) h = 1;

        var writer = new BarcodeWriterPixelData
        {
            Format = format,
            Options = new ZXing.Common.EncodingOptions
            {
                Width = w,
                Height = h,
                Margin = 0
            }
        };
        var pixelData = writer.Write(value);
        byte[] pixels = pixelData.Pixels;

        // Make white/light background transparent
        for (int i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i] > 200 && pixels[i + 1] > 200 && pixels[i + 2] > 200)
            {
                pixels[i] = 0; pixels[i + 1] = 0; pixels[i + 2] = 0; pixels[i + 3] = 0;
            }
        }

        var bitmap = new SoftwareBitmap(
            BitmapPixelFormat.Bgra8,
            pixelData.Width,
            pixelData.Height,
            BitmapAlphaMode.Premultiplied);

        bitmap.CopyFromBuffer(pixels.AsBuffer());
        return bitmap;
    }
}
