using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LabelDesigner.App.Controls;

public sealed partial class RulerControl : UserControl
{
    public bool IsVertical
    {
        get; set;
    }
    public RulerControl()
    {
        InitializeComponent();
    }
    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;

        double pixelsPerMm = 3.78;   // 96 DPI
        int max = 2000;

        for (int i = 0; i < max; i += 5) // small step for smooth ticks
        {
            bool major = (i % 100 == 0);   // every 100px (~26mm)
            bool medium = (i % 50 == 0);

            float tickSize = major ? 15 : medium ? 10 : 5;

            if (IsVertical)
            {
                ds.DrawLine(0, i, tickSize, i, Colors.Black);

                if (major)
                {
                    var mm = (i / pixelsPerMm).ToString("0");
                    ds.DrawText(mm, 20, i - 6, Colors.Black);
                }
            }
            else
            {
                ds.DrawLine(i, 0, i, tickSize, Colors.Black);

                if (major)
                {
                    var mm = (i / pixelsPerMm).ToString("0");
                    ds.DrawText(mm, i - 6, 15, Colors.Black);
                }
            }
        }

        // unit label
        ds.DrawText("mm", 2, 2, Colors.Gray);
    }
}
