using System.Text.Json;

namespace LoadTestingSytem.Common
{
    public static class Utils
    {
        public static List<int> c_validStatusCodes = new List<int> { 200, 201, 202 };

        public static async Task<T> LoadConfig<T>(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Missing config file: {path}");

            var json = await File.ReadAllTextAsync(path);
            var config = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config == null)
                throw new InvalidOperationException($"Failed to deserialize config file: {path}");

            return config;
        }

        public static string NormalizeGuid(string dashedGuid)
        {
            if (Guid.TryParse(dashedGuid, out var parsedGuid))
            {
                return parsedGuid.ToString("N").ToLowerInvariant();
            }
            else
            {
                throw new ArgumentException("Invalid GUID format", nameof(dashedGuid));
            }
        }

       public static double Percentile(List<double> sequence, double percentile)
        {
            if (sequence == null || sequence.Count == 0)
                return 0;

            sequence.Sort();
            var rank = (percentile / 100.0) * (sequence.Count - 1);
            int lowerIndex = (int)Math.Floor(rank);
            int upperIndex = (int)Math.Ceiling(rank);

            if (lowerIndex == upperIndex)
                return sequence[lowerIndex];

            double weight = rank - lowerIndex;
            return sequence[lowerIndex] * (1 - weight) + sequence[upperIndex] * weight;
        }
    }
}
