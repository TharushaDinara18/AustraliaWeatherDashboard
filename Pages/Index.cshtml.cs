using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AustraliaWeatherDashboard.Pages;

public sealed record DailyForecast(
    string Location,
    DateOnly Date,
    double MinimumTemperatureC,
    double MaximumTemperatureC,
    double AverageHumidityPercent,
    double RainfallMm);

public class IndexModel : PageModel
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private static readonly Dictionary<string, (string Lat, string Lon)>
        Cities = new()
        {
            ["Perth"] = ("-31.9523", "115.8613"),
            ["Sydney"] = ("-33.8688", "151.2093"),
            ["Melbourne"] = ("-37.8136", "144.9631"),
            ["Brisbane"] = ("-27.4698", "153.0251"),
            ["Adelaide"] = ("-34.9285", "138.6007")
        };

    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ILogger<IndexModel> logger)
    {
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? SelectedLocation { get; set; } = "Perth";

    [BindProperty(SupportsGet = true)]
    public DateOnly? SelectedDate { get; set; }

    public IEnumerable<string> Locations => Cities.Keys;

    public List<SelectListItem> DateOptions { get; } = new();

    public DailyForecast? Forecast { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? ForecastTimeZone { get; private set; }

    public async Task OnGetAsync()
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please select a valid location and date.";
            return;
        }

        string location = SelectedLocation ?? "Perth";
        SelectedLocation = location;

        if (!Cities.TryGetValue(location, out var city))
        {
            ErrorMessage = "Please select a city from the list.";
            return;
        }

        string url =
            "https://api.open-meteo.com/v1/forecast" +
            $"?latitude={city.Lat}&longitude={city.Lon}" +
            "&daily=temperature_2m_min,temperature_2m_max," +
            "relative_humidity_2m_mean,rain_sum,showers_sum" +
            "&temperature_unit=celsius&precipitation_unit=mm" +
            "&timezone=auto&forecast_days=7";

        try
        {
            string json = await Client.GetStringAsync(url);

            using JsonDocument document = JsonDocument.Parse(json);

            JsonElement root = document.RootElement;
            JsonElement daily = root.GetProperty("daily");

            ForecastTimeZone = root
                .GetProperty("timezone")
                .GetString();

            JsonElement dates = daily.GetProperty("time");

            JsonElement minimums = daily
                .GetProperty("temperature_2m_min");

            JsonElement maximums = daily
                .GetProperty("temperature_2m_max");

            JsonElement humidity = daily
                .GetProperty("relative_humidity_2m_mean");

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
                    dates[i].GetString()!,
                    CultureInfo.InvariantCulture);

                forecasts.Add(new DailyForecast(
                    location,
                    date,
                    minimums[i].GetDouble(),
                    maximums[i].GetDouble(),
                    humidity[i].GetDouble(),
                    rain[i].GetDouble() + showers[i].GetDouble()));

                DateOptions.Add(new SelectListItem(
                    date.ToString(
                        "ddd, dd MMM yyyy",
                        CultureInfo.InvariantCulture),
                    date.ToString(
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture)));
            }

            // On the first visit, use the API's first forecast date.
            SelectedDate ??= forecasts[0].Date;

            Forecast = forecasts.FirstOrDefault(
                forecast => forecast.Date == SelectedDate.Value);

            if (Forecast is null)
            {
                ErrorMessage =
                    "That date is outside this city's forecast range. " +
                    "Choose a date from the list.";
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to retrieve weather forecast");

            ErrorMessage =
                "Could not load weather data. Check your internet " +
                "connection and try again.";

            Forecast = null;
        }
    }
}
