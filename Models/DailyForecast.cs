namespace AustraliaWeatherDashboard.Models;

// Stores the weather values for one location and date
public sealed record DailyForecast(
    string Location,
    DateOnly Date,
    double MinimumTemperatureC,
    double MaximumTemperatureC,
    double AverageHumidityPercent,
    double RainfallMm);
    