using AutoMapper;
using CncWallStation.EntityFrameworkCore;
using CncWallStation.Services;
using CncWallStation.Services.Application;
using CncWallStation.Services.Mappings;
using CncWallStation.ViewModels;
using CncWallStation.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Windows;
using Volo.Abp.Domain.Repositories;

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

            // ✅ 改用 AddDbContextFactory，每次业务调用创建独立 DbContext
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
            services.AddSingleton<IMapper>(sp =>
            {
                var config = new MapperConfiguration(cfg =>
                {
                    cfg.AddProfile<CncWallStationAutoMapperProfile>();
                });
                config.AssertConfigurationIsValid();
                return config.CreateMapper();
            });

            //
             //services.AddScoped(typeof(IRepository<,>), typeof(EfCoreRepository<,>));

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
            // 同步等待，避免 async void 导致基类逻辑乱序
            HostApp.StartAsync().GetAwaiter().GetResult();

            // 初始化数据库（注意：用 Factory 创建独立实例）
            using (var scope = HostApp.Services.CreateScope())
            {
                var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
                using var db = factory.CreateDbContext();
                db.Database.EnsureCreated();
                // 如果用迁移：db.Database.Migrate();
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
