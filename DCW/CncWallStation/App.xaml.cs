using CncWallStation.Data;
using CncWallStation.Repositories;
using CncWallStation.Services;
using CncWallStation.ViewModels;
using CncWallStation.Views;
using Microsoft.EntityFrameworkCore;
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
                    // ==================== 数据库 ====================
                    var connectionString = "Server=10.34.120.31;Port=3306;Database=DcwCncStation;User ID=root;Password=Zuru123!;Charset=utf8mb4;";

                    services.AddDbContext<AppDbContext>(options =>
                    {
                        options.UseMySql(
                            connectionString,
                            ServerVersion.AutoDetect(connectionString),
                            mysqlOptions =>
                            {
                                mysqlOptions.EnableRetryOnFailure(
                                    maxRetryCount: 3,
                                    maxRetryDelay: TimeSpan.FromSeconds(10),
                                    errorNumbersToAdd: null);
                            });
                    });

                    // ==================== 仓储层 ====================
                    services.AddScoped<IWallRepository, WallRepository>();

                    // ==================== 服务层 ====================
                    services.AddScoped<IPipelineService, PipelineService>();

                    // ==================== 注册服务 ====================
                    services.AddTransient<ControllerPageViewModel>();
                    services.AddTransient<ControllerPage>();
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<MainWindow>();
                    services.AddTransient<BimDataRenderViewModel>();
                    services.AddTransient<BimDataRenderPage>();
                    services.AddTransient<WallListPageViewModel>();
                    services.AddTransient<WallListPage>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await HostApp.StartAsync();

            // 自动应用 EF Core 迁移（确保数据库表结构最新）
            using (var scope = HostApp.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.EnsureCreatedAsync();
            }

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
