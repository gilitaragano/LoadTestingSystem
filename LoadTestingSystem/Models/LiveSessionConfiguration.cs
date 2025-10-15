using System.Text.Json.Serialization;

namespace LoadTestingSytem.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CallsRateUpdateMode
    {
        Static,
        LinearRampUp,
        SecondBySecond
    }

    public class LiveSessionConfiguration
    {
        public int CallsLimit { get; set; }

        public CallsRateInfo CallsRateInfo { get; set; }
    }

    public class CallsRateInfo
    {
        public CallsRateUpdateMode CallsRateUpdateMode { get; set; }

        public int InitialCallsRate { get; set; }

        // Required if Mode == LinearRampUp
        public LinearRampUpConfig? LinearRampUpConfig { get; set; }

        // Required if Mode == SecondBySecond
        public SecondConfig[]? SecondBySecondConfig { get; set; }
    }

    public class SecondConfig
    {
        /// <summary>
        /// The number of calls scheduled in this second.
        /// </summary>
        public int CallsCount { get; set; }

        /// <summary>
        /// Millisecond offsets within this second when calls should occur.
        /// Example: [100, 200, 300] means calls at 0.1s, 0.2s, 0.3s.
        /// </summary>
        public List<int>? CallOffsetsMs { get; set; } = new();

        public bool HasValidOffsets =>
            CallOffsetsMs != null &&
            CallOffsetsMs.Count > 0 &&
            CallOffsetsMs.All(offset => offset >= 0 && offset < 1000);
    }

    public class LinearRampUpConfig
    {
        // How many calls per second to increase on each step
        public int CallsRateIncreasePerStep { get; set; }

        // How often (in seconds) to increase the rate
        public int SecondsBetweenIncreases { get; set; }
    }
}
