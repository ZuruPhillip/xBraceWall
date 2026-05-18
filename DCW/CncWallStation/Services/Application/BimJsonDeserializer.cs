using BimWallData;
using BimWallData.V000;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace CncWallStation.Services.Application
{
    /// <summary>
    /// 版本感知 BimJSON 反序列化器
    /// 根据 BimJSON 中的 schema 字段提取版本号，路由到对应版本的 DTO 进行反序列化
    /// </summary>
    public class BimJsonDeserializer
    {
        private static readonly Regex VersionRegex = new(@"v(\d+)\.(\d+)\.(\d+)", RegexOptions.Compiled);

        /// <summary>
        /// 从 BimJSON 文本中提取版本号字符串（如 "0.0.0"）
        /// </summary>
        public static string ExtractVersion(string bimJson)
        {
            if (string.IsNullOrWhiteSpace(bimJson))
                return "0.0.0";

            try
            {
                var jObj = JObject.Parse(bimJson);
                var schema = jObj["schema"]?.Value<string>() ?? "";
                var match = VersionRegex.Match(schema);
                return match.Success ? $"{match.Groups[1]}.{match.Groups[2]}.{match.Groups[3]}" : "0.0.0";
            }
            catch
            {
                return "0.0.0";
            }
        }

        /// <summary>
        /// 按版本号反序列化 BimJSON 为对应的 DTO 类型
        /// </summary>
        /// <param name="bimJson">原始 BimJSON 字符串</param>
        /// <returns>反序列化后的 BimWallDtoBase 实例</returns>
        /// <exception cref="NotSupportedException">当版本号不支持时抛出</exception>
        /// <exception cref="JsonException">当 JSON 格式错误时抛出</exception>
        public BimWallDtoBase Deserialize(string bimJson)
        {
            if (string.IsNullOrWhiteSpace(bimJson))
                throw new ArgumentException("BimJSON 数据不能为空", nameof(bimJson));

            var version = ExtractVersion(bimJson);
            return DeserializeByVersion(bimJson, version);
        }

        /// <summary>
        /// 尝试反序列化，失败时抛出带有版本信息的异常
        /// </summary>
        public BimWallDtoBase DeserializeOrThrow(string bimJson)
        {
            var version = ExtractVersion(bimJson);

            try
            {
                return DeserializeByVersion(bimJson, version);
            }
            catch (NotSupportedException)
            {
                throw; // 版本不支持，直接抛出
            }
            catch (Exception ex) when (ex is not NotSupportedException)
            {
                throw new JsonException(
                    $"反序列化 BimJSON 数据失败（版本: v{version}）。错误: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 根据版本号路由到对应的 DTO 类
        /// </summary>
        private static BimWallDtoBase DeserializeByVersion(string bimJson, string version)
        {
            // 提取次版本号（minor version），即中间的数字
            // 格式: "major.minor.patch" → 取 minor
            var parts = version.Split('.');
            var minor = parts.Length >= 2 && int.TryParse(parts[1], out var m) ? m : 0;

            return minor switch
            {
                0 => JsonConvert.DeserializeObject<BimWallDtoV000>(bimJson)
                     ?? throw new JsonException("反序列化结果为 null（V000）"),

                // 预留 V001 扩展点（后续版本）
                // 1 => JsonConvert.DeserializeObject<BimWallDtoV001>(bimJson)
                //      ?? throw new JsonException("反序列化结果为 null（V001）"),

                _ => throw new NotSupportedException(
                    $"不支持的数据版本: v{version}。" +
                    $"当前支持的次版本号: 0（V000）")
            };
        }
    }
}
