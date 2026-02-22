namespace HopTracer.Web.Services;

public interface IAppDataStorageService
{
    string RootPath { get; }
    string UploadsPath { get; }
    string BaselinesPath { get; }
}

public class AppDataStorageService : IAppDataStorageService
{
    public string RootPath { get; }
    public string UploadsPath { get; }
    public string BaselinesPath { get; }

    public AppDataStorageService()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.GetTempPath();
        }

        RootPath = Path.Combine(localAppData, "HopTracer");
        UploadsPath = Path.Combine(RootPath, "uploads");
        BaselinesPath = Path.Combine(RootPath, "baselines");

        Directory.CreateDirectory(RootPath);
        Directory.CreateDirectory(UploadsPath);
        Directory.CreateDirectory(BaselinesPath);
    }
}
