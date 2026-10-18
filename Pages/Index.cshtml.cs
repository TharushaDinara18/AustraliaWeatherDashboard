using System.Globalization;
using System.Net;
using AustraliaWeatherDashboard.Models;
using AustraliaWeatherDashboard.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AustraliaWeatherDashboard.Pages;

// Handles the dashboard's selections and forecast results
public class IndexModel : PageModel
{
    // Coordinates and time zones for the available cities
    private static readonly Dictionary<
        string, (string Lat, string Lon, string TimeZone)> Cities = new()
    {
        ["Perth"] = ("-31.9523", "115.8613", "Australia/Perth"),
        ["Sydney"] = ("-33.8688", "151.2093", "Australia/Sydney"),
        ["Melbourne"] = ("-37.8136", "144.9631", "Australia/Melbourne"),
        ["Brisbane"] = ("-27.4698", "153.0251", "Australia/Brisbane"),
        ["Adelaide"] = ("-34.9285", "138.6007", "Australia/Adelaide")
    };

    private readonly IWeatherService _weatherService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IWeatherService weatherService,
        ILogger<IndexModel> logger)
    {
        _weatherService = weatherService;
        _logger = logger;
    }

    // Read the selected city and date from the URL
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
        string location = SelectedLocation ?? "Perth";
        SelectedLocation = location;

        // Only allow cities listed in the dictionary
        if (!Cities.TryGetValue(location, out var city))
        {
            ErrorMessage = "Please select a city from the list.";
            return;
        }

        try
        {
            // Use today's date in the selected city's time zone
            TimeZoneInfo zone =
                TimeZoneInfo.FindSystemTimeZoneById(city.TimeZone);

            DateOnly today = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(
                    DateTimeOffset.UtcNow, zone).DateTime);

            ForecastTimeZone = city.TimeZone;

            // Build five date choices before calling the API
            // The dropdown stays available if the API request fails
            for (int i = 0; i < 5; i++)
            {
                DateOnly date = today.AddDays(i);

                DateOptions.Add(new SelectListItem(
                    date.ToString(
                        "ddd, dd MMM yyyy",
                        CultureInfo.InvariantCulture),
                    date.ToString(
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture)));
            }

            if (!ModelState.IsValid)
            {
                ErrorMessage = "Please select a valid location and date.";
                return;
            }

            // Use today when no date has been selected
            SelectedDate ??= today;

            // Reject dates outside the five-day forecast range
            if (SelectedDate.Value < today ||
                SelectedDate.Value > today.AddDays(4))
            {
                ErrorMessage =
                    "Please choose a date from the five-day forecast list.";
                return;
            }

            // Ask the weather service for this city's forecasts
            var result = await _weatherService.GetForecastsAsync(
                location,
                city.Lat,
                city.Lon,
                HttpContext.RequestAborted);

            ForecastTimeZone = result.TimeZone;

            // Find the forecast that matches the selected date
            Forecast = result.Forecasts.FirstOrDefault(
                forecast => forecast.Date == SelectedDate.Value);

            if (Forecast is null)
            {
                ErrorMessage =
                    "The weather provider has no forecast for that date. " +
                    "Please choose another date.";
            }
        }
        catch (OperationCanceledException)
            when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            // Stop processing if the browser cancels the request
            return;
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogWarning(exception, "Weather request timed out");

            ErrorMessage =
                "The weather service took too long to respond. " +
                "Please try again shortly.";
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode ==
                  HttpStatusCode.ServiceUnavailable)
        {
            _logger.LogWarning(
                exception,
                "Weather API temporarily unavailable");

            ErrorMessage =
                "The weather service is temporarily unavailable. " +
                "Your selections are still available. Please try again later.";
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode ==
                  HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning(
                exception,
                "Weather API request limit reached");

            ErrorMessage =
                "The weather service has received too many requests. " +
                "Please wait before trying again.";
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Weather HTTP request failed");

            ErrorMessage =
                "Could not contact the weather service. " +
                "Check your connection and try again later.";
        }
        catch (Exception exception)
        {
            // Log unexpected errors and show a message on the page
            _logger.LogError(
                exception,
                "Unable to process weather forecast");

            ErrorMessage =
                "Could not process the weather forecast. " +
                "Please try again later.";
        }
    }
}