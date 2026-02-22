namespace HopTracer.Maui;

public static class AppStartupState
{
    public static bool IsGhIoAvailable { get; set; } = true;
    public static string? GhIoErrorMessage { get; set; }
}
