using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CncWallStation.EntityFrameworkCore
{
    /// <summary>
    /// EF Core 设计时工厂（用于 dotnet ef migrations 命令）
    /// </summary>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            var connectionString = "Server=10.34.120.31;Port=3306;Database=DcwCncStation;User ID=root;Password=Zuru123!;Charset=utf8mb4;";

            optionsBuilder.UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString),
                mysqlOptions =>
                {
                    mysqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                });

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
