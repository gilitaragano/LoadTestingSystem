using System.Text.Json.Serialization;

namespace LoadTestingSytem.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CallsRateUpdateMode
    {
        Static,
        LinearRampUp,
        SecondBySecond,
        DelayBetweenCalls
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
        public SecondBySecondConfig? SecondBySecondConfig { get; set; }

        // Required if Mode == DelayBetweenCalls
        public DelayBetweenCallsConfig? DelayBetweenCallsConfig { get; set; }
    }

    public class DelayBetweenCallsConfig
    {

        /// <summary>
        /// Millisecond offsets within this second when calls should occur.
        /// Examples: [0, 100, 200, 300] means calls at 0s, 0.1s, 0.3s, 0.6s, [] means 1 call without any delay
        /// </summary>
        public List<int> CallDelaysInMs { get; set; } = new();
    }

    public class SecondBySecondConfig
    {
        /// <summary>
        /// Millisecond offsets within this second when calls should occur.
        /// Example: [100, 200, 300] means calls at 0.1s, 0.3s, 0.6s.
        /// </summary>
        public List<int> CallsCountPerSecond { get; set; } = new();
    }

    public class LinearRampUpConfig
    {
        // How many calls per second to increase on each step
        public int CallsRateIncreasePerStep { get; set; }

        // How often (in seconds) to increase the rate
        public int SecondsBetweenIncreases { get; set; }
    }
}
