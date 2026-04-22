using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Services.Maps;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LabelDesigner.App;
/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    public static IHost Host
    {
        get; private set;
    }

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();

        // Build DI container
        Host = Microsoft.Extensions.Hosting.Host
            .CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                ConfigureServices(services);
            })
            .Build();
    }
    private void ConfigureServices(IServiceCollection services)
    {
        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DesignerViewModel>();
        services.AddSingleton<RibbonViewModel>();

        // Services
        services.AddSingleton<IBarcodeService, BarcodeService>();
        services.AddSingleton<IRenderService, RenderService>();
        services.AddSingleton<ISnapService, SnapService>();
        services.AddSingleton<IExportService, PdfExportService>();
        services.AddSingleton<IDataService, DataService>();
        services.AddSingleton<IRibbonContextService, RibbonContextService>();

        // Window
        services.AddSingleton<MainWindow>();
    }


    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    //protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    //{
    //    _window = new MainWindow();
    //    _window.Activate();
    //}
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // 🔥 THIS is where the window is created
        var window = Host.Services.GetRequiredService<MainWindow>();

        window.Activate();
    }
}
