using HopTracer.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddSingleton<IGhxParser, GhxParser>();
builder.Services.AddSingleton<IDiffer, Differ>();
builder.Services.AddSingleton<IConverterService, ConverterService>();
builder.Services.AddSingleton<IGitWrapper, GitWrapper>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors();
app.UseStaticFiles();
app.UseRouting();
app.MapControllers();

// Serve index.html at root
app.MapGet("/", () => Results.Redirect("/index.html"));

app.Run();
