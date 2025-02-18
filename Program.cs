using System.Data.Common;
using Dotnet.ReActPattern;
using OpenAI.Chat;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ChatClient>(sp => new ChatClient(model: "gpt-4o-mini", apiKey: apiKey));
builder.Services.AddScoped<ChatBot>(sp => 
    new ChatBot(sp.GetRequiredService<ChatClient>(),
    sp.GetRequiredService<ILogger<ChatBot>>(),
    Prompts.WikipediaOnly));
builder.Services.AddScoped<Logic>();
builder.Services.AddLogging();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/demo", async (ChatBot cb) => {
    await cb.ExecuteAsync("How long is a Boeing 757-200?");
});

app.MapPost("/question", async (Question q, Logic l, ILogger<Program> log) => {
    var response = await l.Query(q.query);
    foreach(var r in response){
        log.LogInformation("response line: {r}", r);
    }
    return new Response(response);
});
app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record Question(string query);
record Response(List<string?> response);
