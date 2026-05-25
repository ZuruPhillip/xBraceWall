using CncWallStation.Features.Grooves;
using CncWallStation.Features.MepSlots;
using CncWallStation.Features.Props;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace CncWallStation.Features
{
    /// <summary>
    /// Newtonsoft.Json 自定义转换器：用于反序列化抽象类 <see cref="Feature"/> 的多态列表。
    /// <para>
    /// 背景：MomWall.Features 类型为 List&lt;Feature&gt;，Feature 是抽象类。
    /// Newtonsoft.Json 没有 System.Text.Json 的 [JsonPolymorphic] 支持，
    /// 因此需要通过 JSON 中的 Type 字段（FeatureType 枚举整数值）判断具体派生类。
    /// </para>
    /// <para>
    /// 关键设计：JsonConverter&lt;T&gt;.CanConvert 被 sealed，子类无法重写，
    /// 默认实现用 IsAssignableFrom 判定，导致对 Groove 等子类也会触发本转换器。
    /// 因此 ReadJson 内必须使用全新的 JsonSerializer（不含本转换器）来反序列化具体类型，
    /// 否则会递归调用引发 StackOverflowException 导致进程崩溃。
    /// </para>
    /// </summary>
    public class FeatureJsonConverter : JsonConverter<Feature>
    {
        public override Feature? ReadJson(
            JsonReader reader,
            Type objectType,
            Feature? existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var jObject = JObject.Load(reader);

            // 从 JSON 中读取 FeatureType 枚举的整数值
            var typeValue = jObject["Type"]?.Value<int>();
            if (typeValue == null)
            {
                throw new JsonSerializationException(
                    "无法确定 Feature 的具体类型：JSON 中缺少 'Type' 字段。");
            }

            var featureType = (FeatureType)typeValue.Value;

            // 根据 FeatureType 映射到具体派生类
            Type concreteType = ResolveConcreteType(featureType);

            // 各子类构造函数有 MachineSide side 参数，但 JSON 中字段名为 InitialSide，
            // 需要补充 "Side" 字段让 Newtonsoft 能匹配构造函数参数名（大小写不敏感）
            if (jObject["Side"] == null && jObject["InitialSide"] != null)
            {
                jObject["Side"] = jObject["InitialSide"];
            }

            // ★ 关键：使用全新的 serializer 实例（不含本转换器）反序列化具体类型。
            // JsonConverter<T>.CanConvert 是 sealed 的，子类无法阻止对 Groove 等子类
            // 的匹配，若复用传入的 serializer 会导致无限递归 → StackOverflowException。
            var cleanSettings = new JsonSerializerSettings();
            var cleanSerializer = JsonSerializer.Create(cleanSettings);
            using var jReader = jObject.CreateReader();
            var feature = (Feature?)cleanSerializer.Deserialize(jReader, concreteType);
            return feature;
        }

        public override void WriteJson(
            JsonWriter writer,
            Feature? value,
            JsonSerializer serializer)
        {
            // 序列化使用默认行为：按具体类型写出所有属性
            serializer.Serialize(writer, value);
        }

        public override bool CanWrite => true;

        /// <summary>
        /// 根据 <see cref="FeatureType"/> 枚举值解析对应的具体派生类 Type
        /// </summary>
        private static Type ResolveConcreteType(FeatureType featureType)
        {
            return featureType switch
            {
                FeatureType.Groove => typeof(Groove),
                FeatureType.Hole => typeof(Hole),
                FeatureType.Pocket => typeof(Pocket),
                FeatureType.MepSlot => typeof(MepSlot),
                FeatureType.RebarSlot => typeof(RebarSlot),
                FeatureType.Propping => typeof(Propping),
                FeatureType.Window => typeof(Window),
                _ => throw new JsonSerializationException(
                    $"未知的 FeatureType: {featureType} ({(int)featureType})")
            };
        }
    }
}
