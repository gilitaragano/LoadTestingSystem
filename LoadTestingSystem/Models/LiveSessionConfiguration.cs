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
        public int[]? SecondBySecondConfig { get; set; }
    }

    public class LinearRampUpConfig
    {
        // How many calls per second to increase on each step
        public int CallsRateIncreasePerStep { get; set; }

        // How often (in seconds) to increase the rate
        public int SecondsBetweenIncreases { get; set; }
    }
}
