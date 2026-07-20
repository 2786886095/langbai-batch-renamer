namespace BatchRename.Core;

public static class AppDataPaths
{
    public static string BaseDirectory
    {
        get
        {
            var overridePath = Environment.GetEnvironmentVariable("BATCH_RENAME_DATA_DIR");
            return string.IsNullOrWhiteSpace(overridePath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BatchRename")
                : Path.GetFullPath(overridePath);
        }
    }
}
