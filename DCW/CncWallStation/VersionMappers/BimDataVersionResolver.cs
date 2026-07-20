using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CncWallStation.VersionMappers
{
    // ========== 版本解析器 ==========

    public static class BimDataVersionResolver
    {
        /// <summary>
        /// 从 JSON 中快速读取 schema 版本号，无需完整反序列化
        /// </summary>
        public static string ResolveVersion(string json)
        {
            var bimWallData = JObject.Parse(json);
            var schema = bimWallData["schema"]?.ToString();
            if (string.IsNullOrEmpty(schema)) return "0.0.0";

            var match = Regex.Match(schema, @"v(\d+\.\d+\.\d+)");
            return match.Success ? match.Groups[1].Value : "0.0.0";
        }
    }
}
