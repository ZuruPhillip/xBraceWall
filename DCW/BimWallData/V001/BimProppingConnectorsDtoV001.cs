using Newtonsoft.Json;

namespace BimWallData.V001
{
    public class BimProppingConnectorsDtoV001
    {
        [JsonProperty("columnBracket")]
        public List<BimProppingConnectorItemDtoV001>? ColumnBracket { get; set; }

        [JsonProperty("standard")]
        public List<BimProppingConnectorItemDtoV001>? Standard { get; set; }

        [JsonProperty("topBracket")]
        public List<BimProppingConnectorItemDtoV001>? TopBracket { get; set; }

        [JsonProperty("typeA")]
        public List<BimProppingConnectorItemDtoV001>? TypeA { get; set; }

        [JsonProperty("typeB")]
        public List<BimProppingConnectorItemDtoV001>? TypeB { get; set; }

        [JsonProperty("typeC")]
        public List<BimProppingConnectorItemDtoV001>? TypeC { get; set; }

        [JsonProperty("typeD")]
        public List<BimProppingConnectorItemDtoV001>? TypeD { get; set; }
    }
}
