using Newtonsoft.Json;

namespace DurableEntityStateLoss
{
    [JsonObject(MemberSerialization.OptIn)]
    public class DurableGame
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("index")]
        public int Index { get; set; }
        [JsonProperty("statuses")]
        public Dictionary<int, PlayerStatus> PlayerStatuses { get; set; } = [];
        [JsonProperty("created")]
        public DateTime? CreatedDateTime { get; set; }
        [JsonProperty("lastUpdated")]
        public DateTime? LastUpdatedDateTime { get; set; }

        public string Identifier => $"{Name}-{Index}";
    }

    public class PlayerStatus
    {
        [JsonProperty("score")]
        public int Score { get; set; }
        [JsonProperty("status")]
        public Status Status { get; set; }

        public override string ToString() => $"{Status}: {Score}";
    }

    public enum Status
    {
        Playing,
        Winning
    }
}
