using AutoMapper;
using CncWallStation.EntityFrameworkCore;
using CncWallStation.Services;
using CncWallStation.Services.Application;
using CncWallStation.Services.Mappings;
using CncWallStation.ViewModels;
using CncWallStation.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        public static IHost HostApp { get; private set; } = null!;

        public App()
        {
            HostApp = Host.CreateDefaultBuilder()
                //显式配置 ContentRoot 和加载配置文件
                .UseContentRoot(AppContext.BaseDirectory)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(AppContext.BaseDirectory)
                          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                          .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                                       optional: true, reloadOnChange: true)
                          .AddEnvironmentVariables();
                })
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
                    // 从 appsettings.json 读取连接字符串
                    var connectionString = context.Configuration.GetConnectionString("Default")
                        ?? throw new InvalidOperationException(
                            "未找到连接字符串 'ConnectionStrings:Default'，请检查 appsettings.json");

                    services.AddDbContextFactory<AppDbContext>(options =>
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

                    // ==================== AutoMapper 配置 ====================
                    services.AddSingleton(sp =>
                    {
                        var config = new MapperConfiguration(cfg =>
                        {
                            cfg.AddProfile<CncWallStationAutoMapperProfile>();
                        });
                        config.AssertConfigurationIsValid();
                        return config.CreateMapper();
                    });

                    // ==================== 应用服务层（查询层） ====================
                    services.AddTransient<IWallAppService, WallAppService>();
                    services.AddTransient<IProjectAppService, ProjectAppService>();

                    // ==================== 领域服务层 ====================
                    services.AddTransient<IPipelineService, PipelineService>();

                    // ==================== ViewModel & View ====================
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

        protected override void OnStartup(StartupEventArgs e)
        {
            HostApp.StartAsync().GetAwaiter().GetResult();

            using (var scope = HostApp.Services.CreateScope())
            {
                var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
                using var db = factory.CreateDbContext();
                db.Database.EnsureCreated();
            }

            var mainWindow = HostApp.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            HostApp.StopAsync().GetAwaiter().GetResult();
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}