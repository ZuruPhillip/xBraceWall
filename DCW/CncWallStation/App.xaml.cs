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

                    //服务注册
                    services.AddTransient<MainWindow>();
                    services.AddTransient<BimJsonDeserializer>();
                    services.AddSingleton<JsonKeyTranslationConfig>();
                    services.AddSingleton<DataCheckValidatorFactory>();
                    services.AddSingleton<BimWallMapperFactory>();

                    // OPC UA 通讯服务（单例）
                    services.AddSingleton<IOpcUaService, OpcUaService>();

                    services.AddConventionalServices(Assembly.GetExecutingAssembly());

                    // MainPageViewModel 需单例，确保 MainViewModel 和 MainPage 共享同一实例
                    services.AddSingleton<MainPageViewModel>();

                    // 异常报告 PDF 导出服务
                    services.AddTransient<ExceptionReportExportService>();

                })
                .Build();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // 加载上次保存的语言偏好
            LocalizationService.Instance.LoadSavedLanguage();

            HostApp.StartAsync().GetAwaiter().GetResult();

            using (var scope = HostApp.Services.CreateScope())
            {
                var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
                using var db = factory.CreateDbContext();
                db.Database.EnsureCreated();

                // EnsureCreated 在 DB 已存在时不会新增表，手动补建 PlcInstruction 表
                db.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS PlcInstruction (
                        Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                        WallId BIGINT NOT NULL,
                        T INT NOT NULL,
                        F INT NOT NULL,
                        D INT NOT NULL,
                        X0 FLOAT NOT NULL,
                        Y0 FLOAT NOT NULL,
                        Z0 FLOAT NOT NULL,
                        X1 FLOAT NOT NULL,
                        Y1 FLOAT NOT NULL,
                        Z1 FLOAT NOT NULL,
                        SortOrder INT NOT NULL,
                        HandlerName VARCHAR(64) NOT NULL,
                        FeatureName VARCHAR(64) NOT NULL,
                        UpdatedBy VARCHAR(64) NULL,
                        UpdatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        INDEX IX_PlcInstruction_WallId (WallId),
                        INDEX IX_PlcInstruction_WallId_SortOrder (WallId, SortOrder),
                        CONSTRAINT FK_PlcInstruction_Wall FOREIGN KEY (WallId) REFERENCES Wall(Id) ON DELETE CASCADE
                    );
                ");

                // 手动补建 Opc 表
                db.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS Opc (
                        Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                        WallId BIGINT NOT NULL,
                        GroupId VARCHAR(64) NOT NULL,
                        NodeId VARCHAR(256) NOT NULL,
                        Value VARCHAR(128) NOT NULL,
                        CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        INDEX IX_Opc_GroupId (GroupId),
                        INDEX IX_Opc_WallId (WallId)
                    );
                ");

                // 手动补建 MachiningException 表
                db.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS MachiningException (
                        Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                        WallId BIGINT NOT NULL,
                        ExceptionType INT NOT NULL,
                        CustomType VARCHAR(128) NULL,
                        Description MEDIUMTEXT NOT NULL,
                        PhotoPaths TEXT NULL,
                        Registrant VARCHAR(64) NOT NULL,
                        CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        OccurredAt DATETIME NULL,
                        FrequencyCount INT NOT NULL DEFAULT 1,
                        IsResolved TINYINT(1) NOT NULL DEFAULT 0,
                        RepairMethod VARCHAR(512) NULL,
                        Resolver VARCHAR(64) NULL,
                        RepairDuration DECIMAL(10,2) NULL,
                        CompletionTime DATETIME NULL,
                        ImprovementSuggestion TEXT NULL,
                        Remarks TEXT NULL,
                        INDEX IX_MachiningException_WallId (WallId),
                        CONSTRAINT FK_MachiningException_Wall FOREIGN KEY (WallId) REFERENCES Wall(Id) ON DELETE CASCADE
                    );
                ");

                // MachiningException 表结构升级（幂等，逐条 try-catch 忽略已存在/不存在错误）
                var alterStatements = new[]
                {
                    "ALTER TABLE MachiningException CHANGE COLUMN Operator Registrant VARCHAR(64) NOT NULL",
                    "ALTER TABLE MachiningException ADD COLUMN OccurredAt DATETIME NULL",
                    "ALTER TABLE MachiningException ADD COLUMN FrequencyCount INT NOT NULL DEFAULT 1",
                    "ALTER TABLE MachiningException ADD COLUMN RepairMethod VARCHAR(512) NULL",
                    "ALTER TABLE MachiningException ADD COLUMN Resolver VARCHAR(64) NULL",
                    "ALTER TABLE MachiningException ADD COLUMN RepairDuration DECIMAL(10,2) NULL",
                    "ALTER TABLE MachiningException ADD COLUMN CompletionTime DATETIME NULL",
                    "ALTER TABLE MachiningException ADD COLUMN ImprovementSuggestion TEXT NULL",
                    "ALTER TABLE MachiningException ADD COLUMN Remarks TEXT NULL"
                };
                foreach (var sql in alterStatements)
                {
                    try
                    {
                        db.Database.ExecuteSqlRaw(sql);
                    }
                    catch (Exception ex)
                    {
                        // 忽略列已存在(1060)、列不存在(1054) 等错误，保证幂等
                        System.Diagnostics.Debug.WriteLine($"DDL 幂等跳过: {sql} => {ex.Message}");
                    }
                }

                // 手动补建 MachiningRecord 表
                db.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS MachiningRecord (
                        Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                        WallId BIGINT NOT NULL,
                        Operator VARCHAR(64) NOT NULL,
                        StartTime TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        EndTime TIMESTAMP NULL,
                        TotalDurationSeconds BIGINT NULL,
                        Status INT NOT NULL,
                        INDEX IX_MachiningRecord_WallId (WallId),
                        CONSTRAINT FK_MachiningRecord_Wall FOREIGN KEY (WallId) REFERENCES Wall(Id) ON DELETE CASCADE
                    );
                ");
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