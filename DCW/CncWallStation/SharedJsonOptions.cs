using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CncWallStation
{
    /// <summary>
    /// MomWall 序列化/反序列化共享配置。
    /// 统一使用 System.Text.Json，避免 Newtonsoft.Json 与 STJ 格式不兼容。
    /// 覆盖：PipelineService（写入）、PlcDataAppService（读取）、
    ///       DataCheckService（写入）、DataCheckValidator（读取），共 6 处。
    /// </summary>
    public static class SharedJsonOptions
    {
        public static readonly JsonSerializerOptions Instance = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }
}
