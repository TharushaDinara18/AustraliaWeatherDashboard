namespace AustraliaWeatherDashboard.Models;

public sealed record DailyForecast(
    string Location,
    DateOnly Date,
    double MinimumTemperatureC,
    double MaximumTemperatureC,
    double AverageHumidityPercent,
    double RainfallMm);
    