using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using LabelDesigner.App.ViewModels;
using LabelDesigner.Infrastructure;
using LabelDesigner.Infrastructure.Interfaces;
using LabelDesigner.Infrastructure.Barcode;
using LabelDesigner.Infrastructure.Persistence;
using LabelDesigner.Infrastructure.Export;
using LabelDesigner.Infrastructure.Data;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.App.Services;

namespace LabelDesigner.App;

public partial class App : Microsoft.UI.Xaml.Application
{
    public static Window? MainWindow
    {
        get; private set;
    }

    public static IServiceProvider? Services
    {
        get; private set;
    }

    private Window? m_window;

    public App()
    {
        InitializeComponent();
        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(
            "Ngo9BigBOggjHTQxAR8/V1JHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdlWXtfeHRcQmhfWEd1WktWYEo=");

        ConfigureServices();
    }

    private void ConfigureServices()
    {
        var services = new ServiceCollection();

        // Core services
        services.AddSingleton<IUndoRedoService, LabelDesigner.Application.Commands.UndoRedoService>();
        services.AddSingleton<ILabelPersistenceService, JsonLabelPersistenceService>();
        services.AddSingleton<ISceneGraphService, LabelDesigner.Application.Services.SceneGraphService>();
        services.AddSingleton<ISnapService, LabelDesigner.Application.Services.SnapService>();
        services.AddSingleton<IDataBindingService, LabelDesigner.Application.Services.DataBindingService>();
        services.AddSingleton<IElementInteractionService, LabelDesigner.Application.Services.ElementInteractionService>();
        services.AddSingleton<ILabelStockPresetService, LabelDesigner.Application.Services.LabelStockPresetService>();

        // Infrastructure services
        services.AddSingleton<IBarcodeService, BarcodeService>();
        services.AddSingleton<IRenderService, RenderService>();
        services.AddSingleton<IPrintService, PrintService>();
        services.AddSingleton<IDocumentRasterizer>(sp => (IDocumentRasterizer)sp.GetRequiredService<IPrintService>());
        services.AddSingleton<IPdfExportService, PdfExportService>();
        services.AddSingleton<ISvgService, SvgService>();
        services.AddSingleton<Core.Interfaces.IDataSourceService, CsvDataSourceService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DesignerViewModel>();
        services.AddSingleton<RibbonViewModel>();
        services.AddSingleton<PropertiesViewModel>();

        // Window
        services.AddSingleton<MainWindow>();

        // Build the provider
        Services = services.BuildServiceProvider();

        // Flush GPU/CPU barcode bitmap cache whenever a new document is loaded or the scene is cleared.
        var scene = Services.GetRequiredService<ISceneGraphService>();
        var render = Services.GetRequiredService<IRenderService>();
        scene.DocumentReset += render.ClearBitmapCache;
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        m_window = Services!.GetRequiredService<MainWindow>();
        m_window.ExtendsContentIntoTitleBar = true;
        MainWindow = m_window;

        if (MainWindow.Content is FrameworkElement root)
        {
            root.RequestedTheme = LabelDesigner.App.Services.AppSettingsService.AppTheme;
        }

        MainWindow.Activate();
    }
}
