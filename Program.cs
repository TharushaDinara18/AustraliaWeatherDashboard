using AustraliaWeatherDashboard.Services;

var builder = WebApplication.CreateBuilder(args);

// Register Razor Pages.
builder.Services.AddRazorPages();

// Register WeatherService as the implementation of IWeatherService.
builder.Services.AddHttpClient<IWeatherService, WeatherService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});

var app = builder.Build();

// Configure error handling and HSTS outside development.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

// Map static files and Razor Pages.
app.MapStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();