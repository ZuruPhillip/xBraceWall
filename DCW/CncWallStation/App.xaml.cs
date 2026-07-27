using AutoMapper;
using CncWallStation.EntityFrameworkCore;
using CncWallStation.Extensions;
using CncWallStation.Localization;
using CncWallStation.Services.Application;
using CncWallStation.Services.Configs;
using CncWallStation.Services.DataCheck;
using CncWallStation.Services.Mappings;
using CncWallStation.Services.OpcUa;
using CncWallStation.VersionMappers;
using CncWallStation.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

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
            // ==================== 全局异常处理 ====================
            RegisterGlobalExceptionHandlers();

            HostApp = Host.CreateDefaultBuilder()
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
                    // 日志级别与配置从 appsettings.json 的 Serilog 节点读取；
                    // 缺省时给出合理默认，Async 包裹避免阻塞。
                    config
                        .ReadFrom.Configuration(context.Configuration)
                        .Enrich.FromLogContext()
                        .WriteTo.Async(a => a.Console(
                            outputTemplate:
                            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
                        .WriteTo.Async(a => a.File(
                            "logs/log-.txt",
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 30,
                            fileSizeLimitBytes: 50 * 1024 * 1024,
                            rollOnFileSizeLimit: true,
                            outputTemplate:
                            "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}"));
                })
                .ConfigureServices((context, services) =>
                {
                    // ==================== 数据库 ====================
                    var connectionString = context.Configuration.GetConnectionString("Default")
                        ?? throw new InvalidOperationException(
                            "未找到连接字符串 'ConnectionStrings:Default'，请检查 appsettings.json");

                    services.AddDbContextFactory<AppDbContext>(options =>
                    {
                        options.UseMySql(
                            connectionString,
                            // 写死版本号避免启动时同步连接探测（ServerVersion.AutoDetect 会拖慢冷启动）
                            // 如果 MySQL 版本不是 8.0，请修改此处版本号
                            new MySqlServerVersion(new Version(8, 0, 32)),
                            mysqlOptions =>
                            {
                                mysqlOptions.EnableRetryOnFailure(
                                    maxRetryCount: 3,
                                    maxRetryDelay: TimeSpan.FromSeconds(10),
                                    errorNumbersToAdd: null);
                            });
                    });

                    // ==================== AutoMapper ====================
                    services.AddSingleton(sp =>
                    {
                        var config = new MapperConfiguration(cfg =>
                        {
                            cfg.AddProfile<CncWallStationAutoMapperProfile>();
                        });
                        config.AssertConfigurationIsValid();
                        return config.CreateMapper();
                    });

                    // ==================== 服务注册 ====================
                    services.AddTransient<MainWindow>();
                    services.AddTransient<BimJsonDeserializer>();
                    services.AddSingleton<JsonKeyTranslationConfig>();
                    services.AddSingleton<DataCheckValidatorFactory>();
                    services.AddSingleton<BimWallMapperFactory>();

                    services.AddConventionalServices(Assembly.GetExecutingAssembly());

                    // OPC UA 通讯服务（单例）——必须在 AddConventionalServices 之后注册，
                    // 否则 AddDomainServices 会将 OpcUaService 覆盖为 Transient，
                    // 导致各 ViewModel 拿到不同实例，StatusChanged 事件无法同步到状态栏。
                    services.AddSingleton<IOpcUaService, OpcUaService>();

                    // MainPageViewModel 单例，确保 MainViewModel 和 MainPage 共享实例
                    services.AddSingleton<MainPageViewModel>();

                    // 异常报告 PDF 导出服务
                    services.AddTransient<ExceptionReportExportService>();

                    // 数据库初始化 HostedService（启动时执行建表/升级）
                    services.AddHostedService<DatabaseInitializerHostedService>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                // 加载上次保存的语言偏好
                LocalizationService.Instance.LoadSavedLanguage();

                // 启动 Host（会执行 DatabaseInitializerHostedService.StartAsync）
                await HostApp.StartAsync();

                var mainWindow = HostApp.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "应用启动失败");
                MessageBox.Show(
                    $"应用启动失败，请检查数据库连接与配置。\n\n{ex.Message}",
                    "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            try
            {
                // StopAsync 停止 HostedService；DisposeAsync 释放容器单例（含 OPC 会话）。
                await HostApp.StopAsync(TimeSpan.FromSeconds(5));
                await ((IAsyncDisposable)HostApp).DisposeAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "应用退出时释放 Host 异常");
            }
            finally
            {
                Log.CloseAndFlush();
            }

            base.OnExit(e);
        }

        // ══════════════════════════════════════════
        //  全局异常处理
        // ══════════════════════════════════════════
        private void RegisterGlobalExceptionHandlers()
        {
            // UI 线程未处理异常
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            // 非 UI 线程未处理异常（通常无法恢复）
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                Log.Fatal(args.ExceptionObject as Exception, "非 UI 线程未处理异常");

            // 未观测的 Task 异常（OPC 的 fire-and-forget 重连/KeepAlive 尤其相关）
            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                Log.Error(args.Exception, "未观测的 Task 异常");
                args.SetObserved();
            };
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception, "UI 线程未处理异常");
            MessageBox.Show(
                $"发生错误：\n{e.Exception.Message}",
                "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;  // 阻止应用崩溃（视业务需要可移除）
        }
    }
}