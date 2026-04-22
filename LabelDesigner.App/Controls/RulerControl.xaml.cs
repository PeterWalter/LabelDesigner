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
        double pixelsPerMm = 3.78;
        var ds = args.DrawingSession;

        int max = 2000;

        for (int i = 0; i < max; i += 10)
        {
            float tick = i % 100 == 0 ? 15 : 8;

            if (IsVertical)
            {
                ds.DrawLine(0, i, tick, i, Colors.Black);

                if (i % 100 == 0)
                    ds.DrawText((i / pixelsPerMm).ToString("0"), 20, i, Colors.Black);
            }
            else
            {
                ds.DrawLine(i, 0, i, tick, Colors.Black);

                if (i % 100 == 0)
                    ds.DrawText((i / pixelsPerMm).ToString("0"), i, 15, Colors.Black);
            }
        }

        ds.DrawText("mm", 2, 2, Colors.Gray);
    }
}
