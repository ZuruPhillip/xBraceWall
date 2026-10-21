using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CncWallStation.EntityFrameworkCore
{
    /// <summary>
    /// EF Core 设计时工厂（用于 dotnet ef migrations / dbcontext 命令）。
    /// 设计原则：连接串只有一个来源——项目根目录的 appsettings.json 的 ConnectionStrings:Default，
    /// 与运行期 App.xaml.cs 的读取方式保持一致，避免设计时误连到其他服务器。
    /// 若需临时指向其它库/服务器，可设置环境变量 <see cref="ConnectionStringEnvironmentVariable"/> 覆盖，
    /// 或使用 dotnet ef 的 --connection 参数，无需改动代码。
    /// </summary>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        /// <summary>appsettings.json 中的连接串节点名。</summary>
        private const string ConnectionStringName = "Default";

        /// <summary>设计时覆盖连接串的环境变量名。</summary>
        public const string ConnectionStringEnvironmentVariable = "DCW_DB_CONNECTION";

        /// <summary>appsettings.json 文件名。</summary>
        private const string AppSettingsFileName = "appsettings.json";

        /// <summary>向上回溯查找配置文件的最大层级。</summary>
        private const int MaxSearchDepth = 6;

        public AppDbContext CreateDbContext(string[] args)
        {
            var connectionString = ResolveConnectionString();

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseMySql(
                connectionString,
                // 写死版本号：与 App.xaml.cs 保持一致，同时避免 AutoDetect 在网络不通时长时间阻塞
                new MySqlServerVersion(new Version(8, 0, 32)),
                mysqlOptions =>
                {
                    mysqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                });

            return new AppDbContext(optionsBuilder.Options);
        }

        /// <summary>
        /// 解析连接串：环境变量优先，其次读取 appsettings.json。
        /// </summary>
        private static string ResolveConnectionString()
        {
            var overridden = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(overridden))
            {
                return overridden.Trim();
            }

            var appSettingsPath = LocateAppSettingsFile()
                ?? throw new InvalidOperationException(
                    $"未找到 {AppSettingsFileName}。已尝试：当前工作目录（{Directory.GetCurrentDirectory()}）、" +
                    $"程序集输出目录（{AppContext.BaseDirectory}）及其上溯目录。");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.GetDirectoryName(appSettingsPath)!)
                .AddJsonFile(Path.GetFileName(appSettingsPath), optional: false, reloadOnChange: false)
                .Build();

            var connectionString = configuration.GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"配置文件 {appSettingsPath} 中缺少连接字符串 'ConnectionStrings:{ConnectionStringName}'。");
            }

            return connectionString;
        }

        /// <summary>
        /// 按「当前工作目录 → 程序集输出目录」顺序逐层上溯查找 appsettings.json。
        /// dotnet ef 命令通常以项目目录为工作目录，直接命中第一候选。
        /// </summary>
        private static string? LocateAppSettingsFile()
        {
            var candidate = FindUpwards(Directory.GetCurrentDirectory());
            return candidate ?? FindUpwards(AppContext.BaseDirectory);
        }

        private static string? FindUpwards(string startDirectory)
        {
            var directory = new DirectoryInfo(startDirectory);
            for (var depth = 0; depth < MaxSearchDepth && directory is not null; depth++)
            {
                var candidate = Path.Combine(directory.FullName, AppSettingsFileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return null;
        }
    }
}
