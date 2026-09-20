using Newtonsoft.Json;

namespace BimWallData.V002
{
    public class BimProppingConnectorsDtoV002
    {
        [JsonProperty("columnBracket")]
        public List<BimProppingConnectorItemDtoV002>? ColumnBracket { get; set; }

        [JsonProperty("standard")]
        public List<BimProppingConnectorItemDtoV002>? Standard { get; set; }

        [JsonProperty("topBracket")]
        public List<BimProppingConnectorItemDtoV002>? TopBracket { get; set; }

        [JsonProperty("typeA")]
        public List<BimProppingConnectorItemDtoV002>? TypeA { get; set; }

        [JsonProperty("typeB")]
        public List<BimProppingConnectorItemDtoV002>? TypeB { get; set; }

        [JsonProperty("typeC")]
        public List<BimProppingConnectorItemDtoV002>? TypeC { get; set; }

        [JsonProperty("typeD")]
        public List<BimProppingConnectorItemDtoV002>? TypeD { get; set; }
    }
}
