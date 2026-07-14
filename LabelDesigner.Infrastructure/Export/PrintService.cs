using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using LabelDesigner.Infrastructure.Interfaces;
using LabelDesigner.Infrastructure.Rendering;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Printing;
using System.Numerics;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Graphics.Printing;

namespace LabelDesigner.Infrastructure.Export;

public class PrintService : IPrintService, IDocumentRasterizer
{
    private readonly IBarcodeService _barcode;
    private readonly ISvgService _svg;
    private readonly SemaphoreSlim _printSemaphore = new(1, 1);
    private readonly Dictionary<nint, PrintManager> _printManagers = new();
    
    private IReadOnlyList<ImageSource>? _currentPageSources;
    private string _currentJobTitle = "Label";
    private TaskCompletionSource<PrintTaskCompletion>? _printCompletion;
    private Microsoft.UI.Dispatching.DispatcherQueue? _dispatcherQueue;
    private PrintDocument? _currentPrintDocument;

    /// <inheritdoc/>
    public double PixelsPerMm { get; set; } = 96.0 / 25.4;

    public PrintService(IBarcodeService barcode, ISvgService svg)
    {
        _barcode = barcode;
        _svg = svg;
    }

    public Task PrintAsync(SceneDocument document, nint windowHandle)
    {
        return PrintAsync(new[] { document }, windowHandle, "Label");
    }

    public Task ShowPrintPreviewAsync(SceneDocument document, nint windowHandle)
    {
        return ShowPrintPreviewAsync(new[] { document }, windowHandle, "Label Preview");
    }

    public Task PrintAsync(IReadOnlyList<SceneDocument> documents, nint windowHandle, string? jobTitle = null)
    {
        return ShowPrintDialogAsync(documents, windowHandle, jobTitle ?? BuildJobTitle(documents.Count));
    }

    public Task ShowPrintPreviewAsync(IReadOnlyList<SceneDocument> documents, nint windowHandle, string? jobTitle = null)
    {
        return ShowPrintDialogAsync(documents, windowHandle, jobTitle ?? BuildJobTitle(documents.Count));
    }

    private async Task ShowPrintDialogAsync(IReadOnlyList<SceneDocument> documents, nint windowHandle, string jobTitle)
    {
        if (documents.Count == 0)
            throw new InvalidOperationException("There is nothing to print.");

        if (!PrintManager.IsSupported())
            throw new InvalidOperationException("Printing is not supported on this device.");

        await _printSemaphore.WaitAsync();
        try
        {
            if (!_printManagers.TryGetValue(windowHandle, out var printManager))
            {
                printManager = PrintManagerInterop.GetForWindow(windowHandle);
                printManager.PrintTaskRequested += OnPrintTaskRequested;
                _printManagers[windowHandle] = printManager;
            }

            _currentPageSources = await BuildPageSourcesAsync(documents);
            _currentJobTitle = jobTitle;
            _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            _printCompletion = new TaskCompletionSource<PrintTaskCompletion>(TaskCreationOptions.RunContinuationsAsynchronously);

            await PrintManagerInterop.ShowPrintUIForWindowAsync(windowHandle);

            var completion = await _printCompletion.Task;
            if (completion == PrintTaskCompletion.Failed)
                throw new InvalidOperationException("Windows could not complete the print job.");
        }
        finally
        {
            _currentPageSources = null;
            _printCompletion = null;
            _currentPrintDocument = null;
            _printSemaphore.Release();
        }
    }

    private async Task<IReadOnlyList<ImageSource>> BuildPageSourcesAsync(IReadOnlyList<SceneDocument> documents)
    {
        var sources = new List<ImageSource>(documents.Count);

        foreach (var document in documents)
        {
            var stream = await RenderDocumentToStreamAsync(document, 300f);
            var bitmapImage = new BitmapImage();
            await bitmapImage.SetSourceAsync(stream);
            sources.Add(bitmapImage);
        }

        return sources;
    }

    private async Task<Windows.Storage.Streams.IRandomAccessStream> RenderDocumentToStreamAsync(SceneDocument document, float dpi)
    {
        double pageWidthPx = document.Page.WidthMm * dpi / 25.4;
        double pageHeightPx = document.Page.HeightMm * dpi / 25.4;
        int width = Math.Max(1, (int)Math.Ceiling(pageWidthPx));
        int height = Math.Max(1, (int)Math.Ceiling(pageHeightPx));

        using var device = CanvasDevice.GetSharedDevice();
        using var renderTarget = new CanvasRenderTarget(device, width, height, dpi);
        // scale: convert from canvas screen-pixels (mm × PixelsPerMm) to render pixels (mm × dpi/25.4)
        float scale = dpi / (float)(PixelsPerMm * 25.4);

        using (var ds = renderTarget.CreateDrawingSession())
        {
            ds.Clear(Colors.White);
            ds.Transform = Matrix3x2.CreateScale(scale);

            var lookup = BuildElementLookup(document);
            foreach (var layer in document.Layers)
            {
                if (!layer.Visible)
                    continue;

                foreach (var id in layer.ElementIds)
                {
                    if (!lookup.TryGetValue(id, out var el) || !el.Visible)
                        continue;

                    ElementRenderer.DrawElement(ds, el, lookup, _barcode, _svg, scale);
                }
            }
        }

        var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);
        stream.Seek(0);
        return stream;
    }

    private static string BuildJobTitle(int pageCount)
    {
        return pageCount == 1
            ? "Label"
            : $"Label ({pageCount} pages)";
    }

    public async Task<SoftwareBitmap> RenderDocumentToBitmapAsync(SceneDocument document, float dpi)
    {
        double pageWidthPx = document.Page.WidthMm * dpi / 25.4;
        double pageHeightPx = document.Page.HeightMm * dpi / 25.4;
        int width = Math.Max(1, (int)Math.Ceiling(pageWidthPx));
        int height = Math.Max(1, (int)Math.Ceiling(pageHeightPx));

        using var device = CanvasDevice.GetSharedDevice();
        using var renderTarget = new CanvasRenderTarget(device, width, height, dpi);
        // scale: convert from canvas screen-pixels (mm × PixelsPerMm) to render pixels (mm × dpi/25.4)
        float scale = dpi / (float)(PixelsPerMm * 25.4);

        using (var ds = renderTarget.CreateDrawingSession())
        {
            ds.Clear(Colors.White);
            ds.Transform = Matrix3x2.CreateScale(scale);

            var lookup = BuildElementLookup(document);
            foreach (var layer in document.Layers)
            {
                if (!layer.Visible)
                    continue;

                foreach (var id in layer.ElementIds)
                {
                    if (!lookup.TryGetValue(id, out var el) || !el.Visible)
                        continue;

                    ElementRenderer.DrawElement(ds, el, lookup, _barcode, _svg, scale);
                }
            }
        } // session committed before saving

        var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);
        stream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);
    }

    private static Dictionary<Guid, DesignElement> BuildElementLookup(SceneDocument document)
    {
        var lookup = new Dictionary<Guid, DesignElement>();
        foreach (var element in document.AllElements)
        {
            if (!lookup.ContainsKey(element.Id))
                lookup[element.Id] = element;
        }

        return lookup;
    }

    private void OnPrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
    {
        var printTask = args.Request.CreatePrintTask(_currentJobTitle, OnPrintTaskSourceRequested);
        printTask.Completed += OnPrintTaskCompleted;
    }

    private void OnPrintTaskSourceRequested(PrintTaskSourceRequestedArgs args)
    {
        if (_dispatcherQueue == null)
        {
            throw new InvalidOperationException("DispatcherQueue is not initialized.");
        }

        if (_dispatcherQueue.HasThreadAccess)
        {
            CreateAndSetPrintDocumentSource(args);
        }
        else
        {
            using var cts = new System.Threading.ManualResetEventSlim(false);
            Exception? exception = null;
            _dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    CreateAndSetPrintDocumentSource(args);
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
                finally
                {
                    cts.Set();
                }
            });
            cts.Wait();
            if (exception != null)
            {
                throw new InvalidOperationException("Error creating print document source on UI thread.", exception);
            }
        }
    }

    private void CreateAndSetPrintDocumentSource(PrintTaskSourceRequestedArgs args)
    {
        _currentPrintDocument = new PrintDocument();
        var documentSource = _currentPrintDocument.DocumentSource;
        var previewPages = new List<UIElement>();
        var pageSources = _currentPageSources ?? Array.Empty<ImageSource>();

        _currentPrintDocument.Paginate += (s, e) =>
        {
            previewPages.Clear();
            var pageDescription = e.PrintTaskOptions.GetPageDescription(1);
            for (int i = 0; i < pageSources.Count; i++)
                previewPages.Add(BuildPreviewPage(pageSources[i], pageDescription, i + 1, pageSources.Count));
            _currentPrintDocument.SetPreviewPageCount(previewPages.Count, PreviewPageCountType.Final);
        };

        _currentPrintDocument.GetPreviewPage += (s, e) =>
        {
            if (e.PageNumber >= 1 && e.PageNumber <= previewPages.Count)
                _currentPrintDocument.SetPreviewPage(e.PageNumber, previewPages[e.PageNumber - 1]);
        };

        _currentPrintDocument.AddPages += (s, e) =>
        {
            foreach (var page in previewPages)
                _currentPrintDocument.AddPage(page);
            _currentPrintDocument.AddPagesComplete();
        };

        args.SetSource(documentSource);
    }

    private void OnPrintTaskCompleted(PrintTask sender, PrintTaskCompletedEventArgs args)
    {
        _printCompletion?.TrySetResult(args.Completion);
    }

    private static UIElement BuildPreviewPage(
        ImageSource bitmapSource,
        PrintPageDescription pageDescription,
        int pageNumber,
        int totalPages)
    {
        var page = new Grid
        {
            Width = pageDescription.PageSize.Width,
            Height = pageDescription.PageSize.Height,
            Background = new SolidColorBrush(Colors.White)
        };

        // Place the label image inside the printer's imageable area.
        // No additional padding, borders, or page number chrome.
        var imageableRect = pageDescription.ImageableRect;

        var image = new Image
        {
            Source = bitmapSource,
            Stretch = Stretch.Uniform,
            Width = imageableRect.Width,
            Height = imageableRect.Height,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(imageableRect.X, imageableRect.Y, 0, 0)
        };

        page.Children.Add(image);
        page.Measure(pageDescription.PageSize);
        page.Arrange(new Rect(0, 0, pageDescription.PageSize.Width, pageDescription.PageSize.Height));
        page.UpdateLayout();
        return page;
    }
}
