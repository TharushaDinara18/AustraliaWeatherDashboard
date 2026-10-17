using AustraliaWeatherDashboard.Models;

namespace AustraliaWeatherDashboard.Services;

public interface IWeatherService
{
    Task<(List<DailyForecast> Forecasts, string TimeZone)>
        GetForecastsAsync(
            string location,
            string latitude,
            string longitude,
            CancellationToken cancellationToken = default);
}