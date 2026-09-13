using AiTicketTriage.Api.Services;
using Google.GenAI;

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

builder.Services.AddScoped(
    _ => new Client(apiKey: geminiApiKey));

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

app.MapControllers();

app.Run();

 
