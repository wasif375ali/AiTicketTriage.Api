using AiTicketTriage.Api.Exceptions;
using AiTicketTriage.Api.Services;
using Google.GenAI;
using Google.GenAI.Types;

var builder = WebApplication.CreateBuilder(args);

var aiTimeoutSeconds =
    builder.Configuration
        .GetValue<int?>("Ai:TriageTimeoutSeconds")
    ?? 90;

builder.Services.AddRequestTimeouts(options =>
{
    options.AddPolicy(
        "AiTriage",
        TimeSpan.FromSeconds(aiTimeoutSeconds));
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AiTriageExceptionHandler>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var geminiApiKey =
    builder.Configuration["Gemini:ApiKey"]
    ?? throw new InvalidOperationException(
        "Gemini API key is not configured.");

var httpOptions = new HttpOptions
{
    Timeout = 20_000,
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

builder.Services.AddSingleton<IApplicationLogStore, SqlApplicationLogStore>();
builder.Services.AddSingleton(httpOptions);

builder.Services.AddScoped(sp =>
{
    var logStore = sp.GetRequiredService<IApplicationLogStore>();
    var options = sp.GetRequiredService<HttpOptions>();

    return new Client(
        apiKey: geminiApiKey,
        httpOptions: options,
        clientOptions: new ClientOptions
        {
            HttpClientFactory = () => new HttpClient(
                new GeminiHttpLoggingHandler(new HttpClientHandler(), logStore),
                disposeHandler: true)
            {
                Timeout = TimeSpan.FromMilliseconds(options.Timeout ?? 20_000)
            }
        });
});

builder.Services.AddScoped<
    IAiTicketTriageService,
    GeminiTicketTriageService>();

var app = builder.Build();

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
