using System.IO;
using System.Windows;

namespace BatchRename.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow(ReadPaths(e.Args));
        MainWindow = window;
        window.Show();
    }

    private static IReadOnlyList<string> ReadPaths(string[] args)
    {
        var paths = new List<string>();
        for (var index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--selection-file", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                var listFile = args[++index];
                try
                {
                    if (File.Exists(listFile)) paths.AddRange(File.ReadAllLines(listFile));
                    File.Delete(listFile);
                }
                catch (IOException) { }
                continue;
            }
            if (File.Exists(args[index]) || Directory.Exists(args[index])) paths.Add(args[index]);
        }
        return paths;
    }
}
