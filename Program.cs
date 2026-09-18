using AiTicketTriage.Api.Services;
using Google.GenAI;
using Google.GenAI.Types;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var aiTimeoutSeconds =
    builder.Configuration
        .GetValue<int?>("Ai:TriageTimeoutSeconds")
    ?? 30;

builder.Services.AddRequestTimeouts(options =>
{
    options.AddPolicy(
        "AiTriage",
        TimeSpan.FromSeconds(aiTimeoutSeconds));
});

builder.Services.AddProblemDetails();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();



var geminiApiKey =
    builder.Configuration["Gemini:ApiKey"]
    ?? throw new InvalidOperationException(
        "Gemini API key is not configured.");

var httpOptions = new HttpOptions
{
    RetryOptions = new HttpRetryOptions
    {
        Attempts = 3,
        InitialDelay = 1.0,
        MaxDelay = 4.0,
        ExpBase = 2.0,
        Jitter = 0.5,
        HttpStatusCodes =
        [
            408,
            429,
            500,
            502,
            503,
            504
        ]
    }
};

builder.Services.AddScoped(
    _ => new Client(apiKey: geminiApiKey, httpOptions: httpOptions));

builder.Services.AddScoped<
    IAiTicketTriageService,
    GeminiTicketTriageService>();



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();

app.UseStatusCodePages();

app.UseHttpsRedirection();

app.UseRequestTimeouts();

app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();


