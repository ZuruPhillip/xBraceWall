using BimWallData.V000;
using Newtonsoft.Json;

namespace BimWallData
{
    public abstract class BimWallDtoBase
    {
        [JsonProperty("schema")]
        public string Schema { get; set; }

        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("coreThickness")]
        public float CoreThickness { get; set; }

        [JsonProperty("aacWallElevation")]
        public BimAacWallElevationDtoV000? AacWallElevation { get; set; }

        /// <summary>
        /// 从 schema 中解析版本号，如 "v0.0.0" => "0.0.0"
        /// </summary>
        [JsonIgnore]
        public string Version => ParseVersion(Schema);

        private static string ParseVersion(string schema)
        {
            if (string.IsNullOrEmpty(schema)) return "0.0.0";
            // schema = ".../schemas/v0.0.0/wall_aac.json"
            var match = System.Text.RegularExpressions.Regex.Match(schema, @"v(\d+\.\d+\.\d+)");
            return match.Success ? match.Groups[1].Value : "0.0.0";
        }

        /// <summary>
        /// 校验当前实例数据是否合法，校验通过返回自身，否则抛出异常
        /// </summary>
        /// <returns>校验通过的 BimWallDtoBase 实例</returns>
        /// <exception cref="ArgumentNullException">必填字段为空时抛出</exception>
        /// <exception cref="ArgumentOutOfRangeException">字段值不在合法范围时抛出</exception>
        public BimWallDtoBase Validate()
        {
            // ── Id ──────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(Id))
                throw new ArgumentNullException(
                    nameof(Id),
                    "BimWallDto.Id 不能为空");

            // ── Schema ──────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(Schema))
                throw new ArgumentNullException(
                    nameof(Schema),
                    "BimWallDto.Schema 不能为空");

            // ── AacWallElevation ────────────────────────────────
            if (AacWallElevation == null)
                throw new ArgumentNullException(
                    nameof(AacWallElevation),
                    "BimWallDto.AacWallElevation 不能为空");

            // ── AacWallElevation.Contour ────────────────────────
            if (AacWallElevation.Contour == null || AacWallElevation.Contour.Count <= 2)
                throw new ArgumentNullException(
                    nameof(AacWallElevation.Contour),
                    $"BimWallDto.AacWallElevation.Contour 不能为空且至少需要 3 个点，" +
                    $"当前点数：{AacWallElevation.Contour?.Count ?? 0}");

            // ── CoreThickness ───────────────────────────────────
            if (CoreThickness < 100)
                throw new ArgumentOutOfRangeException(
                    nameof(CoreThickness),
                    CoreThickness,
                    $"BimWallDto.CoreThickness 必须大于等于 100，当前值：{CoreThickness}");

            return this;
        }
    }
}
