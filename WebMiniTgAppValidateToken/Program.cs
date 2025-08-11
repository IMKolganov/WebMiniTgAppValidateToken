using WebMiniTgAppValidateToken;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(opt =>
{
    opt.AddPolicy("dev", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();
app.UseCors("dev");

string botToken = "your_bot_token";
var expIn = TimeSpan.FromSeconds(86400);

app.MapPost("/validate", async (HttpRequest req) =>
{
    var initData = await ReadInitData(req);
    if (string.IsNullOrWhiteSpace(initData))
        return Results.BadRequest(new { ok = false, error = "init data is empty" });

    var ok = TelegramInitDataValidator.Validate(initData, botToken, expIn, out var err);
    return Results.Json(new { ok, error = err });
});

app.Run("http://localhost:5175");

static async Task<string?> ReadInitData(HttpRequest req)
{
    using var reader = new StreamReader(req.Body);
    var body = await reader.ReadToEndAsync();
    return body;
}