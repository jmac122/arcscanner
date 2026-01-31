using System.Net.Http;
using Newtonsoft.Json;
using ArcRaidersOverlay.Models;

namespace ArcRaidersOverlay;

public class EventApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;
    private bool _disposed;

    // Default to metaforge API (keyless, reliable)
    private const string DefaultApiUrl = "https://metaforge.app/api/arc-raiders/events-schedule";

    public EventApiClient(string? apiUrl = null)
    {
        _apiUrl = apiUrl ?? DefaultApiUrl;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ArcRaidersOverlay/1.0");
    }

    /// <summary>
    /// Fetches current game events from the API.
    /// </summary>
    public async Task<List<GameEvent>> GetEventsAsync()
    {
        try
        {
            var response = await _httpClient.GetStringAsync(_apiUrl);
            var apiResponse = JsonConvert.DeserializeObject<EventApiResponse>(response);

            if (apiResponse?.Data == null)
            {
                return new List<GameEvent>();
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var events = new List<GameEvent>();

            foreach (var apiEvent in apiResponse.Data)
            {
                // Calculate time remaining
                var timeRemaining = CalculateTimeRemaining(apiEvent.StartTime, apiEvent.EndTime, now);

                events.Add(new GameEvent
                {
                    Name = apiEvent.Name ?? "Unknown",
                    Location = apiEvent.Map ?? "Unknown",
                    Timer = timeRemaining
                });
            }

            return events;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Event API error: {ex.Message}");
            return new List<GameEvent>();
        }
    }

    private static string CalculateTimeRemaining(long startTime, long endTime, long now)
    {
        if (now >= startTime && now < endTime)
        {
            // Event is active - show time until it ends
            var remaining = TimeSpan.FromMilliseconds(endTime - now);
            return $"ACTIVE ({FormatTimeSpan(remaining)} left)";
        }
        else if (now < startTime)
        {
            // Event hasn't started - show time until it starts
            var until = TimeSpan.FromMilliseconds(startTime - now);
            return $"in {FormatTimeSpan(until)}";
        }
        else
        {
            // Event has ended
            return "ENDED";
        }
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
        {
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        }
        else if (ts.TotalMinutes >= 1)
        {
            return $"{ts.Minutes}m {ts.Seconds}s";
        }
        else
        {
            return $"{ts.Seconds}s";
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    // API response models
    private class EventApiResponse
    {
        [JsonProperty("data")]
        public List<ApiEvent>? Data { get; set; }

        [JsonProperty("cachedAt")]
        public long CachedAt { get; set; }
    }

    private class ApiEvent
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("map")]
        public string? Map { get; set; }

        [JsonProperty("icon")]
        public string? Icon { get; set; }

        [JsonProperty("startTime")]
        public long StartTime { get; set; }

        [JsonProperty("endTime")]
        public long EndTime { get; set; }
    }
}
