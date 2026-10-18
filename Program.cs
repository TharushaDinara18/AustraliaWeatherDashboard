using AustraliaWeatherDashboard.Services;

var builder = WebApplication.CreateBuilder(args);

// Enable Razor Pages
builder.Services.AddRazorPages();

// Set up the weather service with a 20-second HTTP timeout
builder.Services.AddHttpClient<IWeatherService, WeatherService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});

var app = builder.Build();

// Enable the error page and HSTS outside development
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

// Make static files and Razor Pages available
app.MapStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

// Start the application
app.Run();