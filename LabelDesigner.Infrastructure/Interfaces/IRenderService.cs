using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

using LabelDesigner.Core.Models;
using Microsoft.Graphics.Canvas;

namespace LabelDesigner.Infrastructure.Interfaces;


public interface IRenderService
{
    void Render(CanvasDrawingSession ds,
                    IEnumerable<DesignElement> elements,
                    DesignElement? selected,
                    IEnumerable<GuideLine>? guides);
 //   void Render(CanvasDrawingSession drawingSession, ObservableCollection<DesignElement> elements, DesignElement? selected);
}
