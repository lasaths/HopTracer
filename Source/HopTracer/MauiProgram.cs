using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using System.Diagnostics;
#if WINDOWS
using Microsoft.UI.Windowing;
using WinRT.Interop;
#endif

namespace HopTracer.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
        // Ensure dependencies are present before anything else.
        // If unavailable, app still starts and shows in-app remediation instructions.
        AppStartupState.IsGhIoAvailable = DependencyHelper.TryEnsureGhIoDll(out var ghIoErrorMessage);
        AppStartupState.GhIoErrorMessage = ghIoErrorMessage;

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if WINDOWS
		builder.ConfigureLifecycleEvents(events =>
		{
			events.AddWindows(windows =>
			{
				windows.OnWindowCreated(window =>
				{
					var hwnd = WindowNative.GetWindowHandle(window);
					var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
					var appWindow = AppWindow.GetFromWindowId(windowId);
					var iconPath = Path.Combine(AppContext.BaseDirectory, "HopTracer.ico");
					if (File.Exists(iconPath))
					{
						appWindow.SetIcon(iconPath);
					}
				});
			});
		});
#endif

#if DEBUG
		builder.Logging.AddDebug();
		
		// Add file logging for debugging
		var logPath = Path.Combine(Path.GetTempPath(), $"HopTracer_Debug_{DateTime.Now:yyyyMMdd_HHmmss}.log");
		builder.Logging.AddProvider(new FileLoggerProvider(logPath));
		Debug.WriteLine($"Debug log file: {logPath}");
#endif

		return builder.Build();
	}
}

// Simple file logger for debugging
public class FileLoggerProvider : ILoggerProvider
{
	private readonly string _logPath;
	private readonly StreamWriter _writer;

	public FileLoggerProvider(string logPath)
	{
		_logPath = logPath;
		_writer = new StreamWriter(logPath, append: true) { AutoFlush = true };
		_writer.WriteLine($"=== HopTracer Debug Log Started at {DateTime.Now} ===");
	}

	public ILogger CreateLogger(string categoryName)
	{
		return new FileLogger(_writer, categoryName);
	}

	public void Dispose()
	{
		_writer?.Dispose();
	}
}

public class FileLogger : ILogger
{
	private readonly StreamWriter _writer;
	private readonly string _categoryName;

	public FileLogger(StreamWriter writer, string categoryName)
	{
		_writer = writer;
		_categoryName = categoryName;
	}

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

	public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		if (!IsEnabled(logLevel)) return;

		var message = $"[{DateTime.Now:HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {formatter(state, exception)}";
		if (exception != null)
		{
			message += $"\n{exception}";
		}

		lock (_writer)
		{
			_writer.WriteLine(message);
		}
	}
}
