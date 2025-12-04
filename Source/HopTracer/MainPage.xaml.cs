using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using HopTracer.Core.Services;
using HopTracer.Maui.Services;
using Microsoft.Extensions.Logging;

namespace HopTracer.Maui;

public partial class MainPage : ContentPage
{
    private IHost? _webHost;

	public MainPage()
	{
		InitializeComponent();
        Loaded += OnLoaded;
	}

    private async void OnLoaded(object? sender, EventArgs e)
    {
        await StartWebServer();
    }

    private async Task StartWebServer()
    {
        try
        {
            _webHost = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls("http://localhost:5000");
                    
                    // Use Embedded File Provider for single-file portability
                    // Look for embedded files in the HopTracer.Web assembly where wwwroot is located
                    var webAssembly = typeof(HopTracer.Web.Controllers.CompareController).Assembly;
                    var embeddedProvider = new EmbeddedFileProvider(webAssembly, "wwwroot");

                    webBuilder.Configure(app =>
                    {
                        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
                        env.WebRootFileProvider = embeddedProvider;

                        app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
                        
                        app.UseStaticFiles();

                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapControllers();
                            endpoints.MapGet("/", () => Results.Redirect("/index.html"));
                        });
                    });
                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddControllers().AddApplicationPart(typeof(HopTracer.Web.Controllers.CompareController).Assembly);
                        services.AddLogging(logging => 
                        {
                            logging.AddDebug();
                            logging.AddConsole();
                        });
                        
                        services.AddSingleton<IGhxParser, GhxParser>();
                        services.AddSingleton<IDiffer, Differ>();
                        services.AddSingleton<IConverterService, ConverterService>();
                        services.AddSingleton<IGitWrapper, GitWrapper>();
                        services.AddSingleton<INativeIntegration, MauiNativeIntegration>();
                    });
                })
                .Build();

            await _webHost.StartAsync();
            
            // Navigate to localhost
            DiffWebView.Source = "http://localhost:5000";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Failed to start web server: " + ex.Message, "OK");
        }
    }
}