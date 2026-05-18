using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace CncWallStation.Extensions
{
    /// <summary>
    /// DI 容器自动注册扩展
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 自动注册所有 AppService（约定：接口 IXxxAppService ↔ 实现 XxxAppService）
        /// 命名空间：CncWallStation.Services
        /// </summary>
        public static IServiceCollection AddAppServices(
            this IServiceCollection services,
            Assembly assembly,
            string namespacePrefix = "CncWallStation.Services.Application")
        {
            var implTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract
                            && t.Namespace != null
                            && t.Namespace.StartsWith(namespacePrefix)
                            && t.Name.EndsWith("AppService"))
                .ToList();

            foreach (var implType in implTypes)
            {
                // 匹配同名接口 IXxxAppService
                var interfaceType = implType.GetInterfaces()
                    .FirstOrDefault(i => i.Name == $"I{implType.Name}");

                if (interfaceType != null)
                {
                    services.AddTransient(interfaceType, implType);
                }
                else
                {
                    // 没有接口的，直接以自身类型注册
                    services.AddTransient(implType);
                }
            }

            return services;
        }

        /// <summary>
        /// 自动注册所有领域服务（约定：以 "Service" 结尾，但排除 AppService）
        /// 命名空间：CncWallStation.Services（不含 Application 子命名空间）
        /// </summary>
        public static IServiceCollection AddDomainServices(
            this IServiceCollection services,
            Assembly assembly,
            string namespacePrefix = "CncWallStation.Services")
        {
            var implTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract
                            && t.Namespace != null
                            && t.Namespace.StartsWith(namespacePrefix)
                            && !t.Namespace.StartsWith($"{namespacePrefix}.Application")  // 排除 AppService
                            && !t.Namespace.StartsWith($"{namespacePrefix}.Mappings")     // 排除 AutoMapper Profile
                            && t.Name.EndsWith("Service"))
                .ToList();

            foreach (var implType in implTypes)
            {
                var interfaceType = implType.GetInterfaces()
                    .FirstOrDefault(i => i.Name == $"I{implType.Name}");

                if (interfaceType != null)
                {
                    services.AddTransient(interfaceType, implType);
                }
                else
                {
                    services.AddTransient(implType);
                }
            }

            return services;
        }

        /// <summary>
        /// 自动注册所有 ViewModel（约定：类名以 "ViewModel" 结尾）
        /// </summary>
        public static IServiceCollection AddViewModels(
            this IServiceCollection services,
            Assembly assembly,
            string namespacePrefix = "CncWallStation.ViewModels")
        {
            var viewModelTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract
                            && t.Namespace != null
                            && t.Namespace.StartsWith(namespacePrefix)
                            && t.Name.EndsWith("ViewModel"))
                .ToList();

            foreach (var type in viewModelTypes)
            {
                services.AddTransient(type);
            }

            return services;
        }

        /// <summary>
        /// 自动注册所有 View（约定：以 "Page" / "Window" / "View" 结尾，命名空间在 Views）
        /// </summary>
        public static IServiceCollection AddViews(
            this IServiceCollection services,
            Assembly assembly,
            string namespacePrefix = "CncWallStation.Views")
        {
            var viewTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract
                            && t.Namespace != null
                            && t.Namespace.StartsWith(namespacePrefix)
                            && (t.Name.EndsWith("Page")
                                || t.Name.EndsWith("Window")
                                || t.Name.EndsWith("View")))
                .ToList();

            foreach (var type in viewTypes)
            {
                services.AddTransient(type);
            }

            return services;
        }

        /// <summary>
        /// 一键注册所有约定服务（AppService / 领域服务 / ViewModel / View）
        /// </summary>
        public static IServiceCollection AddConventionalServices(
            this IServiceCollection services,
            Assembly assembly)
        {
            return services
                .AddAppServices(assembly)
                .AddDomainServices(assembly)
                .AddViewModels(assembly)
                .AddViews(assembly);
        }
    }
}