using System.Windows;
using System.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace HopTracer;

public partial class MainWindow : Window
{
    private IHost? _webHost;

    public MainWindow()
    {
        try
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            
            // Log startup
            Debug.WriteLine("=== HopTracer Starting ===");
            Console.WriteLine("=== HopTracer Starting ===");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error initializing window: {ex.Message}\n\n{ex.StackTrace}", 
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Debug.WriteLine("Window loaded, starting web server...");
            Console.WriteLine("Window loaded, starting web server...");

            // Start the web server
            var contentRoot = Path.GetDirectoryName(typeof(MainWindow).Assembly.Location) ?? "";
            var wwwroot = Path.Combine(contentRoot, "wwwroot");
            
            Debug.WriteLine($"Content Root: {contentRoot}");
            Debug.WriteLine($"WWWRoot: {wwwroot}");
            Console.WriteLine($"Content Root: {contentRoot}");
            Console.WriteLine($"WWWRoot: {wwwroot}");
            Console.WriteLine($"WWWRoot exists: {Directory.Exists(wwwroot)}");

            _webHost = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls("http://localhost:5000");
                    webBuilder.Configure(app =>
                    {
                        app.UseCors();
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
                        services.AddControllers();
                        services.AddCors(options =>
                        {
                            options.AddDefaultPolicy(policy =>
                            {
                                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                            });
                        });
                    });
                    webBuilder.UseContentRoot(contentRoot);
                    webBuilder.UseWebRoot(wwwroot);
                })
                .Build();

            await _webHost.StartAsync();
            Debug.WriteLine("Web server started successfully!");
            Console.WriteLine("✓ Web server started at http://localhost:5000");

            // Initialize WebView2
            Debug.WriteLine("Initializing WebView2...");
            Console.WriteLine("Initializing WebView2...");
            await webView.EnsureCoreWebView2Async();
            Debug.WriteLine("WebView2 initialized, navigating...");
            Console.WriteLine("✓ WebView2 initialized");
            
            webView.CoreWebView2.Navigate("http://localhost:5000");
            Console.WriteLine("✓ Navigated to http://localhost:5000");
            Console.WriteLine("\n=== Application Ready ===\n");
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error starting application:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
            Debug.WriteLine($"ERROR: {errorMsg}");
            Console.WriteLine($"ERROR: {errorMsg}");
            MessageBox.Show(errorMsg, "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Cancel the close temporarily to allow async shutdown
        if (_webHost != null && !e.Cancel)
        {
            e.Cancel = true;
            
            try
            {
                Debug.WriteLine("Shutting down web server...");
                Console.WriteLine("Shutting down web server...");
                
                // Use async shutdown with timeout
                var shutdownTask = _webHost.StopAsync();
                if (await Task.WhenAny(shutdownTask, Task.Delay(2000)) == shutdownTask)
                {
                    _webHost?.Dispose();
                    Debug.WriteLine("Web server stopped");
                    Console.WriteLine("✓ Web server stopped");
                }
                else
                {
                    Debug.WriteLine("Web server shutdown timed out, forcing close");
                    Console.WriteLine("⚠ Web server shutdown timed out");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during shutdown: {ex.Message}");
                Console.WriteLine($"Error during shutdown: {ex.Message}");
            }
            finally
            {
                // Ensure the application closes
                Application.Current.Shutdown();
            }
        }
    }
}

