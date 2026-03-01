using HopTracer.Core.Services;
using HopTracer.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddSingleton<IGhxParser, GhxParser>();
builder.Services.AddSingleton<IDiffer, Differ>();
builder.Services.AddSingleton<IConverterService, ConverterService>();
builder.Services.AddSingleton<IGitWrapper, GitWrapper>();
builder.Services.AddSingleton<INativeIntegration, HopTracer.Web.Services.WindowsNativeIntegration>();
builder.Services.AddSingleton<IFileValidationService, FileValidationService>();
builder.Services.AddSingleton<IFileSelectionCache, FileSelectionCache>();
builder.Services.AddSingleton<IAppDataStorageService, AppDataStorageService>();
builder.Services.AddSingleton<ITempFileManager, TempFileManager>();
builder.Services.AddSingleton<ISessionTokenService, SessionTokenService>();

// Health checks for production monitoring
builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // In production, this runs as a local-only desktop app
        policy.WithOrigins("http://localhost:5000", "http://127.0.0.1:5000")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Force eager creation so startup stale temp cleanup runs once.
_ = app.Services.GetRequiredService<ITempFileManager>();
_ = app.Services.GetRequiredService<ISessionTokenService>();

// Configure the HTTP request pipeline
app.UseCors();
app.UseStaticFiles();
app.UseRouting();
app.UseMiddleware<SessionTokenMiddleware>();
app.MapControllers();
app.MapHealthChecks("/health");

// Serve index.html at root
app.MapGet("/", () => Results.Redirect("/index.html"));

app.Run();
