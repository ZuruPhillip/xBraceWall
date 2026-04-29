using CncWallStation.ViewModels;
using CncWallStation.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Windows;

namespace CncWallStation
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IHost HostApp { get; private set; }
        public App()
        {
            HostApp = Host.CreateDefaultBuilder()
                .UseSerilog((context, services, config) =>
                {
                    config
                        .MinimumLevel.Debug()
                        .Enrich.FromLogContext()
                        .WriteTo.Async(a => a.Console(
                            outputTemplate:
                            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                        ))
                        .WriteTo.Async(a => a.File(
                            "logs/log-.txt",
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 30,
                            outputTemplate:
                            "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}"
                        ));
                })
                .ConfigureServices((context, services) =>
                {
                    // 注册服务
                    services.AddTransient<ControllerPageViewModel>();
                    services.AddTransient<ControllerPage>();
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<MainWindow>();
                    services.AddTransient<BimDataRenderViewModel>();
                    services.AddTransient<BimDataRenderPage>();

                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await HostApp.StartAsync();

            var mainWindow = HostApp.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await HostApp.StopAsync();
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
