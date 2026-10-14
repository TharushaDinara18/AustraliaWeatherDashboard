using System.Globalization;
using System.Text.Json;
using AustraliaWeatherDashboard.Models;

namespace AustraliaWeatherDashboard.Services;

public class WeatherService
{
    private readonly HttpClient _httpClient;

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
        string url =
            "https://api.open-meteo.com/v1/forecast" +
            $"?latitude={latitude}&longitude={longitude}" +
            "&daily=temperature_2m_min,temperature_2m_max," +
            "relative_humidity_2m_mean,rain_sum,showers_sum" +
            "&temperature_unit=celsius&precipitation_unit=mm" +
            "&timezone=auto&forecast_days=7";

        string json = await _httpClient.GetStringAsync(
            url,
            cancellationToken);

        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement root = document.RootElement;
        JsonElement daily = root.GetProperty("daily");

        string timeZone = root.GetProperty("timezone").GetString()
            ?? throw new JsonException("Missing forecast time zone.");

        JsonElement dates = daily.GetProperty("time");
        JsonElement minimums = daily.GetProperty("temperature_2m_min");
        JsonElement maximums = daily.GetProperty("temperature_2m_max");
        JsonElement humidity = daily.GetProperty("relative_humidity_2m_mean");
        JsonElement rain = daily.GetProperty("rain_sum");
        JsonElement showers = daily.GetProperty("showers_sum");

        int count = dates.GetArrayLength();

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
            DateOnly date = DateOnly.Parse(
                dates[i].GetString()
                    ?? throw new JsonException("Missing forecast date."),
                CultureInfo.InvariantCulture);

            forecasts.Add(new DailyForecast(
                location,
                date,
                minimums[i].GetDouble(),
                maximums[i].GetDouble(),
                humidity[i].GetDouble(),
                rain[i].GetDouble() + showers[i].GetDouble()));
        }

        return (forecasts, timeZone);
    }
}