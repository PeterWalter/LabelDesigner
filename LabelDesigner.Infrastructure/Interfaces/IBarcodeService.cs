using System;
using System.Collections.Generic;
using System.Text;

using Windows.Graphics.Imaging;

namespace LabelDesigner.Infrastructure.Interfaces;


public interface IBarcodeService
{
    SoftwareBitmap Generate(string value, ZXing.BarcodeFormat format, int w, int h);
}
