using System.Text.Json.Serialization;

namespace DurableEntityStateLoss
{
    public class DurableGame
    {
        public string Name { get; set; }
        public int Index { get; set; }
        public Dictionary<int, PlayerStatus> PlayerStatuses { get; set; } = [];
        public DateTime? CreatedDateTime { get; set; }
        public DateTime? LastUpdatedDateTime { get; set; }

        [JsonIgnore] public string Identifier => $"{Name}-{Index}";
    }

    public class PlayerStatus
    {
        public int Score { get; set; }
        public Status Status { get; set; }

        public override string ToString() => $"{Status}: {Score}";
    }

    public enum Status
    {
        Playing,
        Winning
    }
}
