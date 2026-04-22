using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices.WindowsRuntime;
using LabelDesigner.Infrastructure.Interfaces;
using Windows.Graphics.Imaging;
using ZXing;

namespace LabelDesigner.Infrastructure.Barcode;



public class BarcodeService : IBarcodeService
{
    public SoftwareBitmap Generate(string value, BarcodeFormat format, int w, int h)
    {
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

        // 🔥 CRITICAL: BGRA8 + Premultiplied
        var bitmap = new SoftwareBitmap(
            BitmapPixelFormat.Bgra8,
            pixelData.Width,
            pixelData.Height,
            BitmapAlphaMode.Premultiplied);

        bitmap.CopyFromBuffer(pixelData.Pixels.AsBuffer());

        return bitmap;
    }
}