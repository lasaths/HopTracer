using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using HopTracer.Core.Services;
using HopTracer.Maui.Services;
using HopTracer.Web.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using System.Reflection;
using System.Diagnostics;
using System.Net;

namespace HopTracer.Maui;

public partial class MainPage : ContentPage
{
    private IHost? _webHost;
    private readonly IUpdateCheckService _updateCheckService = new GitHubUpdateCheckService();
    private bool _updateCheckStarted;

	public MainPage()
	{
		InitializeComponent();
        Loaded += OnLoaded;
	}

    private async void OnLoaded(object? sender, EventArgs e)
    {
        await StartWebServer();
        _ = CheckForUpdatesAsync();
    }

    private async Task StartWebServer()
    {
        try
        {
            string? resolvedUrl = null;

            _webHost = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel(options =>
                    {
                        // Some Kestrel/Windows combinations reject ListenLocalhost(0).
                        // Bind explicitly to loopback with an ephemeral port.
                        options.Listen(IPAddress.Loopback, 0);
                    });
                    
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
                        app.UseCors(x => x.SetIsOriginAllowed(IsLoopbackOrigin)
                                          .AllowAnyMethod()
                                          .AllowAnyHeader());
                        
                        app.UseStaticFiles();

                        app.UseRouting();
                        app.UseMiddleware<SessionTokenMiddleware>();
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
                        services.AddSingleton<IAppDataStorageService, AppDataStorageService>();
                        services.AddSingleton<ITempFileManager, TempFileManager>();
                        services.AddSingleton<ISessionTokenService, SessionTokenService>();
                    });
                })
                .Build();

            _ = _webHost.Services.GetRequiredService<ITempFileManager>();
            _ = _webHost.Services.GetRequiredService<ISessionTokenService>();

            await _webHost.StartAsync();

            var server = _webHost.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
            resolvedUrl = addresses?.FirstOrDefault(addr =>
                addr.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) ||
                addr.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(resolvedUrl))
            {
                resolvedUrl = "http://localhost:5000";
            }
            
            // Navigate to localhost
            DiffWebView.Source = resolvedUrl;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", "Failed to start web server: " + ex.Message, "OK");
        }
    }

    private static bool IsLoopbackOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host;
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_updateCheckStarted)
        {
            return;
        }
        _updateCheckStarted = true;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var result = await _updateCheckService.CheckForUpdateAsync(cts.Token);
            if (!result.IsUpdateAvailable || string.IsNullOrWhiteSpace(result.LatestVersion) || string.IsNullOrWhiteSpace(result.ReleaseUrl))
            {
                return;
            }

            var dismissedVersion = Preferences.Default.Get("hoptracer.last_notified_update", string.Empty);
            if (string.Equals(dismissedVersion, result.LatestVersion, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var openRelease = await MainThread.InvokeOnMainThreadAsync(() =>
                DisplayAlertAsync(
                    "Update Available",
                    $"HopTracer {result.LatestVersion} is available.\nYou are on {result.CurrentVersion}.",
                    "Open Release",
                    "Later"));

            Preferences.Default.Set("hoptracer.last_notified_update", result.LatestVersion);
            if (openRelease)
            {
                await Launcher.Default.OpenAsync(result.ReleaseUrl);
            }
        }
        catch (OperationCanceledException)
        {
            // Silent timeout keeps startup non-blocking.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check failed: {ex.Message}");
        }
    }
}
