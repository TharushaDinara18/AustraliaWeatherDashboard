using AustraliaWeatherDashboard.Models;

namespace AustraliaWeatherDashboard.Services;


// Defines the method used to get forecasts and the location's time zone
public interface IWeatherService
{
    Task<(List<DailyForecast> Forecasts, string TimeZone)>
        GetForecastsAsync(
            string location,
            string latitude,
            string longitude,
            CancellationToken cancellationToken = default);
}