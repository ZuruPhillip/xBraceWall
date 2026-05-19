using System.Reflection;

namespace CncWallStation.Services.DataCheck
{
    /// <summary>
    /// 校验器工厂 — 自动反射扫描所有 IDataCheckValidator 实现
    /// </summary>
    public class DataCheckValidatorFactory
    {
        private readonly Dictionary<string, IDataCheckValidator> _validators = new();

        public DataCheckValidatorFactory()
        {
            var validatorTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IDataCheckValidator).IsAssignableFrom(t)
                            && !t.IsInterface
                            && !t.IsAbstract);

            foreach (var type in validatorTypes)
            {
                var instance = (IDataCheckValidator)Activator.CreateInstance(type)!;
                _validators[instance.SupportedVersion] = instance;
            }
        }

        /// <summary>
        /// 根据版本号获取校验器
        /// </summary>
        public IDataCheckValidator GetValidator(string version)
        {
            if (_validators.TryGetValue(version, out var validator))
                return validator;

            throw new NotSupportedException(
                $"不支持的 BimData 版本：{version}，" +
                $"已注册版本：{string.Join(", ", _validators.Keys)}");
        }

        /// <summary>
        /// 列出所有已注册版本
        /// </summary>
        public IEnumerable<string> GetRegisteredVersions() => _validators.Keys;
    }
}
