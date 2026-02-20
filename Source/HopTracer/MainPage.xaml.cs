using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using HopTracer.Core.Services;
using HopTracer.Maui.Services;
using HopTracer.Web.Services;
using Microsoft.Extensions.Logging;
using System.Reflection;

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
            var port = GetAvailablePort(5000);
            var url = $"http://localhost:{port}";

            _webHost = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls(url);
                    
                    // Use Embedded File Provider for single-file portability
                    // In portable builds, wwwroot files are embedded in the main assembly (HopTracer.Maui)
                    // Embedded resources use RootNamespace + Link path: "HopTracer.Maui.wwwroot.index.html"
                    var mainAssembly = Assembly.GetExecutingAssembly();
                    var webAssembly = typeof(HopTracer.Web.Controllers.CompareController).Assembly;
                    
                    // Create providers with different base namespaces to handle both scenarios
                    var mainProviderWithNs = new EmbeddedFileProvider(mainAssembly, "HopTracer.Maui.wwwroot");
                    var mainProviderStandard = new EmbeddedFileProvider(mainAssembly, "wwwroot");
                    var webProvider = new EmbeddedFileProvider(webAssembly, "wwwroot");
                    
                    // Composite provider checks all possibilities
                    var embeddedProvider = new CompositeFileProvider(
                        mainProviderWithNs,
                        mainProviderStandard,
                        webProvider
                    );

                    webBuilder.Configure(app =>
                    {
                        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
                        env.WebRootFileProvider = embeddedProvider;

                        // Restrict CORS to localhost only for security
                        app.UseCors(x => x.WithOrigins(url, $"http://127.0.0.1:{port}")
                                          .AllowAnyMethod()
                                          .AllowAnyHeader());
                        
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
                        services.AddSingleton<IFileValidationService, FileValidationService>();
                        services.AddSingleton<IFileSelectionCache, FileSelectionCache>();
                    });
                })
                .Build();

            await _webHost.StartAsync();
            
            // Navigate to localhost
            DiffWebView.Source = url;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Failed to start web server: " + ex.Message, "OK");
        }
    }

    private int GetAvailablePort(int startingPort)
    {
        var properties = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
        var listeners = properties.GetActiveTcpListeners();
        var port = startingPort;

        while (listeners.Any(x => x.Port == port))
        {
            port++;
        }
        return port;
    }
}