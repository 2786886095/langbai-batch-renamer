using System.Diagnostics;
using System.Windows;

namespace BatchRename.App;

public partial class AboutWindow : Window
{
    public AboutWindow() { InitializeComponent(); VersionText.Text = $"版本 {UpdateService.CurrentVersion.ToString(3)}"; }
    private static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    private void OpenGithub_Click(object sender, RoutedEventArgs e) => OpenUrl(UpdateService.RepositoryUrl);
    private void OpenReleases_Click(object sender, RoutedEventArgs e) => OpenUrl(UpdateService.ReleasesUrl);
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
