using LabelDesigner.App.ViewModels;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Infrastructure;
using LabelDesigner.Infrastructure.Barcode;
using LabelDesigner.Infrastructure.Interfaces;
using LabelDesigner.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LabelDesigner.App.Services;
using Microsoft.UI.Xaml;

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
    public static IConfiguration? Configuration
    {
        get; private set;
    }

    private Window? m_window;
    // private Window? _window;
    // public static IHost Host { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(
            "Ngo9BigBOggjHTQxAR8/V1JHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdlWXtfeHRcQmhfWEd1WktWYEo=");

        //Host = Microsoft.Extensions.Hosting.Host
        //    .CreateDefaultBuilder()
        //    .ConfigureServices((context, services) =>
        //    {
        //        ConfigureServices(services);
        //    })
        //    .Build();
        ConfigureServices();
    }

    private void ConfigureServices()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory);

        Configuration = builder.Build();

        // Configure Dependency Injection
        var services = new ServiceCollection();

        // Core services
        services.AddSingleton<IUndoRedoService, LabelDesigner.Application.Commands.UndoRedoService>();
        services.AddSingleton<ILabelPersistenceService, JsonLabelPersistenceService>();
        services.AddSingleton<ISceneGraphService, LabelDesigner.Application.Services.SceneGraphService>();

        // Infrastructure services
        services.AddSingleton<IBarcodeService, BarcodeService>();
        services.AddSingleton<IRenderService, RenderService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DesignerViewModel>();
        services.AddSingleton<RibbonViewModel>();
        services.AddSingleton<PropertiesViewModel>();

        // Window
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        m_window = Services!.GetRequiredService<MainWindow>();
        m_window.ExtendsContentIntoTitleBar = true;
        MainWindow = m_window;
       
        // Apply saved theme to root element
        if (MainWindow.Content is FrameworkElement root)
        {
            root.RequestedTheme = AppSettingsService.AppTheme;
        }

        MainWindow.Activate();
    }
}
