using System.Reflection;

namespace CncWallStation.VersionMappers
{
    // ========== Mapper 工厂（自动注册所有版本）==========

    public class BimWallMapperFactory
    {
        private readonly Dictionary<string, IBimWallMapper> _mappers = new();

        public BimWallMapperFactory()
        {
            // 自动扫描注册所有 Mapper（反射方式，新增版本无需修改工厂）
            var mapperTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IBimWallMapper).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in mapperTypes)
            {
                var instance = (IBimWallMapper)Activator.CreateInstance(type);
                _mappers[instance.SupportedVersion] = instance;
            }
        }

        /// <summary>
        /// 根据版本号获取对应 Mapper
        /// </summary>
        public IBimWallMapper GetMapper(string version)
        {
            if (_mappers.TryGetValue(version, out var mapper))
                return mapper;

            throw new NotSupportedException($"不支持的版本：{version}，已注册版本：{string.Join(", ", _mappers.Keys)}");
        }

        /// <summary>
        /// 列出所有已注册版本
        /// </summary>
        public IEnumerable<string> GetRegisteredVersions() => _mappers.Keys;
    }
}
