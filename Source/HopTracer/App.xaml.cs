namespace HopTracer.Maui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
        if (AppStartupState.IsGhIoAvailable)
        {
            return new Window(new AppShell());
        }

        return new Window(new MissingDependencyPage(AppStartupState.GhIoErrorMessage));
	}
}
