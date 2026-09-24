namespace HopTracer.Core.Services;

public static class HopTracerPaths
{
    public static string RootPath
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                localAppData = Path.GetTempPath();
            }

            return Path.Combine(localAppData, "HopTracer");
        }
    }

    public static string BaselinesPath => Path.Combine(RootPath, "baselines");

    public static void EnsureBaselinesDirectory()
    {
        Directory.CreateDirectory(BaselinesPath);
    }
}
