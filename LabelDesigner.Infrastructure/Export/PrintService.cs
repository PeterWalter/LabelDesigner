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
        var pageSources = await BuildPageSourcesAsync(documents);

        using var session = new PrintSession(windowHandle, jobTitle, pageSources);
        await PrintManagerInterop.ShowPrintUIForWindowAsync(windowHandle);

        var completion = await session.WaitForCompletionAsync();
        if (completion == PrintTaskCompletion.Failed)
            throw new InvalidOperationException("Windows could not complete the print job.");
    }

    private async Task<IReadOnlyList<ImageSource>> BuildPageSourcesAsync(IReadOnlyList<SceneDocument> documents)
    {
        var sources = new List<ImageSource>(documents.Count);

        foreach (var document in documents)
        {
            // Use the same 96-DPI coordinate space as the designer canvas for
            // predictable page composition in print preview.
            var bitmap = await RenderDocumentToBitmapAsync(document, 96f);
            var imageSource = new SoftwareBitmapSource();
            await imageSource.SetBitmapAsync(bitmap);
            sources.Add(imageSource);
        }

        return sources;
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
        float scale = dpi / 96.0f;

        using (var ds = renderTarget.CreateDrawingSession())
        {
            ds.Clear(Colors.White);
            ds.Transform = Matrix3x2.CreateScale(scale);

            var lookup = document.AllElements.ToDictionary(e => e.Id);
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

    private sealed class PrintSession : IDisposable
    {
        private readonly PrintManager _printManager;
        private readonly PrintDocument _printDocument;
        private readonly IPrintDocumentSource _documentSource;
        private readonly IReadOnlyList<ImageSource> _pageSources;
        private readonly List<UIElement> _previewPages = new();
        private readonly TaskCompletionSource<PrintTaskCompletion> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly string _jobTitle;

        public PrintSession(nint hwnd, string jobTitle, IReadOnlyList<ImageSource> pageSources)
        {
            _jobTitle = jobTitle;
            _pageSources = pageSources;

            _printManager = PrintManagerInterop.GetForWindow(hwnd);
            _printManager.PrintTaskRequested += OnPrintTaskRequested;

            _printDocument = new PrintDocument();
            _documentSource = _printDocument.DocumentSource;
            _printDocument.Paginate += OnPaginate;
            _printDocument.GetPreviewPage += OnGetPreviewPage;
            _printDocument.AddPages += OnAddPages;
        }

        public Task<PrintTaskCompletion> WaitForCompletionAsync()
        {
            return _completion.Task;
        }

        public void Dispose()
        {
            _printManager.PrintTaskRequested -= OnPrintTaskRequested;
            _printDocument.Paginate -= OnPaginate;
            _printDocument.GetPreviewPage -= OnGetPreviewPage;
            _printDocument.AddPages -= OnAddPages;
        }

        private void OnPrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
        {
            var printTask = args.Request.CreatePrintTask(_jobTitle, OnPrintTaskSourceRequested);
            printTask.Completed += OnPrintTaskCompleted;
        }

        private void OnPrintTaskSourceRequested(PrintTaskSourceRequestedArgs args)
        {
            args.SetSource(_documentSource);
        }

        private void OnPrintTaskCompleted(PrintTask sender, PrintTaskCompletedEventArgs args)
        {
            _completion.TrySetResult(args.Completion);
        }

        private void OnPaginate(object sender, PaginateEventArgs e)
        {
            _previewPages.Clear();

            var pageDescription = e.PrintTaskOptions.GetPageDescription(1);
            for (int i = 0; i < _pageSources.Count; i++)
                _previewPages.Add(BuildPreviewPage(_pageSources[i], pageDescription, i + 1, _pageSources.Count));

            _printDocument.SetPreviewPageCount(_previewPages.Count, PreviewPageCountType.Final);
        }

        private void OnGetPreviewPage(object sender, GetPreviewPageEventArgs e)
        {
            if (e.PageNumber >= 1 && e.PageNumber <= _previewPages.Count)
                _printDocument.SetPreviewPage(e.PageNumber, _previewPages[e.PageNumber - 1]);
        }

        private void OnAddPages(object sender, AddPagesEventArgs e)
        {
            foreach (var page in _previewPages)
                _printDocument.AddPage(page);

            _printDocument.AddPagesComplete();
        }

        private static UIElement BuildPreviewPage(
            ImageSource bitmapSource,
            PrintPageDescription pageDescription,
            int pageNumber,
            int totalPages)
        {
            const double framePadding = 24;
            const double pageNumberSpacing = 8;

            var page = new Grid
            {
                Width = pageDescription.PageSize.Width,
                Height = pageDescription.PageSize.Height,
                Background = new SolidColorBrush(Colors.White)
            };

            var imageableRect = pageDescription.ImageableRect;
            var contentWidth = Math.Max(1, imageableRect.Width - (framePadding * 2));
            var contentHeight = Math.Max(1, imageableRect.Height - (framePadding * 2) - 24 - pageNumberSpacing);

            var contentBorder = new Border
            {
                Width = contentWidth,
                Height = contentHeight,
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 220, 220, 220)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12),
                Margin = new Thickness(
                    imageableRect.X + framePadding,
                    imageableRect.Y + framePadding,
                    0,
                    0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Child = new Image
                {
                    Source = bitmapSource,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                }
            };

            var caption = new TextBlock
            {
                Text = $"Page {pageNumber} of {totalPages}",
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 96, 96, 96)),
                FontSize = 11,
                Margin = new Thickness(
                    imageableRect.X + framePadding,
                    imageableRect.Y + framePadding + contentHeight + pageNumberSpacing,
                    0,
                    0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            page.Children.Add(contentBorder);
            page.Children.Add(caption);
            page.Measure(pageDescription.PageSize);
            page.Arrange(new Rect(0, 0, pageDescription.PageSize.Width, pageDescription.PageSize.Height));
            page.UpdateLayout();
            return page;
        }
    }
}
