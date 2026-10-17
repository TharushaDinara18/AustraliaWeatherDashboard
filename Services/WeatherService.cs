using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using AustraliaWeatherDashboard.Models;

namespace AustraliaWeatherDashboard.Services;

public class WeatherService : IWeatherService
{
    private readonly HttpClient _httpClient;

    // Shared across service instances within this running application.
    private static readonly ConcurrentDictionary<string, CacheEntry>
        Cache = new();

    private sealed record CacheEntry(
        DateTimeOffset ExpiresAt,
        string TimeZone,
        List<DailyForecast> Forecasts);

    public WeatherService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<(List<DailyForecast> Forecasts, string TimeZone)>
        GetForecastsAsync(
            string location,
            string latitude,
            string longitude,
            CancellationToken cancellationToken = default)
    {
        string cacheKey = $"{location}|{latitude}|{longitude}";

        // Reuse a recent successful response for this location.
        if (Cache.TryGetValue(cacheKey, out var cached) &&
            cached.ExpiresAt > DateTimeOffset.UtcNow &&
            cached.Forecasts[0].Date == GetLocalToday(cached.TimeZone))
        {
            // Return a separate list so callers cannot alter the cache.
            return (new List<DailyForecast>(cached.Forecasts),
                cached.TimeZone);
        }

        string url =
            "https://api.open-meteo.com/v1/forecast" +
            $"?latitude={Uri.EscapeDataString(latitude)}" +
            $"&longitude={Uri.EscapeDataString(longitude)}" +
            "&daily=temperature_2m_min,temperature_2m_max," +
            "relative_humidity_2m_mean,rain_sum,showers_sum" +
            "&temperature_unit=celsius&precipitation_unit=mm" +
            "&timezone=auto&forecast_days=7";

        string json = await DownloadForecastAsync(
            url, cancellationToken);

        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement root = document.RootElement;
        JsonElement daily = root.GetProperty("daily");

        string timeZone = root.GetProperty("timezone").GetString()
            ?? throw new JsonException("Missing forecast time zone.");

        JsonElement dates = daily.GetProperty("time");
        JsonElement minimums = daily.GetProperty("temperature_2m_min");
        JsonElement maximums = daily.GetProperty("temperature_2m_max");
        JsonElement humidity =
            daily.GetProperty("relative_humidity_2m_mean");
        JsonElement rain = daily.GetProperty("rain_sum");
        JsonElement showers = daily.GetProperty("showers_sum");

        int count = dates.GetArrayLength();

        // All arrays must describe the same number of forecast days.
        if (count == 0 ||
            minimums.GetArrayLength() != count ||
            maximums.GetArrayLength() != count ||
            humidity.GetArrayLength() != count ||
            rain.GetArrayLength() != count ||
            showers.GetArrayLength() != count)
        {
            throw new JsonException("Incomplete forecast response.");
        }

        var forecasts = new List<DailyForecast>();

        for (int i = 0; i < count; i++)
        {
            DateOnly date = DateOnly.ParseExact(
                dates[i].GetString()
                    ?? throw new JsonException("Missing forecast date."),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);

            forecasts.Add(new DailyForecast(
                location,
                date,
                minimums[i].GetDouble(),
                maximums[i].GetDouble(),
                humidity[i].GetDouble(),
                rain[i].GetDouble() + showers[i].GetDouble()));
        }

        // Only cache responses after they have been processed successfully.
        Cache[cacheKey] = new CacheEntry(
            DateTimeOffset.UtcNow.AddMinutes(5),
            timeZone,
            new List<DailyForecast>(forecasts));

        return (forecasts, timeZone);
    }

    private async Task<string> DownloadForecastAsync(
        string url,
        CancellationToken cancellationToken)
    {
        // Make at most two attempts: the original request and one retry.
        for (int attempt = 0; attempt < 2; attempt++)
        {
            using HttpResponseMessage response =
                await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode ==
                    HttpStatusCode.ServiceUnavailable &&
                attempt == 0)
            {
                TimeSpan delay = TimeSpan.FromSeconds(2);

                // Respect the server's Retry-After instruction, if supplied.
                var retryAfter = response.Headers.RetryAfter;

                if (retryAfter?.Delta is TimeSpan specifiedDelay)
                {
                    delay = specifiedDelay;
                }
                else if (retryAfter?.Date is DateTimeOffset retryDate)
                {
                    delay = retryDate - DateTimeOffset.UtcNow;
                }

                if (delay < TimeSpan.Zero)
                {
                    delay = TimeSpan.Zero;
                }

                // If a longer wait is requested, return the error to the
                // page instead of retrying earlier than the server allows.
                if (delay <= TimeSpan.FromSeconds(10))
                {
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync(
                cancellationToken);
        }

        throw new HttpRequestException(
            "The weather service remained unavailable.",
            null,
            HttpStatusCode.ServiceUnavailable);
    }

    private static DateOnly GetLocalToday(string timeZone)
    {
        TimeZoneInfo zone =
            TimeZoneInfo.FindSystemTimeZoneById(timeZone);

        return DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow, zone).DateTime);
    }
}