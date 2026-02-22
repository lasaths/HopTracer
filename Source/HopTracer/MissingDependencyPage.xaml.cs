namespace HopTracer.Maui;

public partial class MissingDependencyPage : ContentPage
{
    private const string SetupCommand = @".\scripts\setup_dependencies.ps1";
    private static readonly Uri RhinoDownloadUri = new("https://www.rhino3d.com/download/");
    private static readonly Uri SetupDocsUri = new("https://github.com/lasaths/HopTracer/blob/main/docs/BUILD_AND_RELEASE.md");

    public MissingDependencyPage(string? startupErrorMessage = null)
    {
        InitializeComponent();
        SummaryLabel.Text = string.IsNullOrWhiteSpace(startupErrorMessage)
            ? "GH_IO.dll was not detected."
            : startupErrorMessage;
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        var isAvailable = DependencyHelper.TryEnsureGhIoDll(out var errorMessage);
        AppStartupState.IsGhIoAvailable = isAvailable;
        AppStartupState.GhIoErrorMessage = errorMessage;

        if (!isAvailable)
        {
            SummaryLabel.Text = errorMessage ?? "GH_IO.dll was not detected.";
            await DisplayAlertAsync("GH_IO Not Found", "Rhino is still not detected. Install Rhino and retry.", "OK");
            return;
        }

        if (Window is not null)
        {
            Window.Page = new AppShell();
        }
    }

    private async void OnCopyCommandClicked(object? sender, EventArgs e)
    {
        await Clipboard.SetTextAsync(SetupCommand);
        await DisplayAlertAsync("Copied", $"Command copied: {SetupCommand}", "OK");
    }

    private async void OnOpenRhinoDownloadClicked(object? sender, EventArgs e)
    {
        await OpenUriAsync(RhinoDownloadUri);
    }

    private async void OnOpenSetupDocsClicked(object? sender, EventArgs e)
    {
        await OpenUriAsync(SetupDocsUri);
    }

    private async Task OpenUriAsync(Uri uri)
    {
        try
        {
            await Launcher.Default.OpenAsync(uri);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Open Link Failed", $"Could not open link: {ex.Message}", "OK");
        }
    }
}
