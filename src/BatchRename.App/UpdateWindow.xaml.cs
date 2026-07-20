using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace BatchRename.App;

public partial class UpdateWindow : Window
{
    private readonly UpdateRelease _release;
    private readonly UpdateService _service;
    private readonly CancellationTokenSource _cancellation = new();

    public UpdateWindow(UpdateRelease release, UpdateService service)
    {
        InitializeComponent();
        _release = release;
        _service = service;
        VersionText.Text = $"当前 {UpdateService.CurrentVersion.ToString(3)} → 最新 {_release.Version.ToString(3)}";
        NotesText.Text = string.IsNullOrWhiteSpace(release.Notes) ? "请打开版本页面查看更新说明。" : release.Notes;
        Closed += (_, _) => _cancellation.Cancel();
    }

    private void OpenRelease_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo(_release.PageUri.AbsoluteUri) { UseShellExecute = true });
    private void Later_Click(object sender, RoutedEventArgs e) => Close();

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        InstallButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;
        var progress = new Progress<double>(value => { DownloadProgress.Value = value; ProgressPercent.Text = $"{value:0}%"; });
        try
        {
            var installer = await _service.DownloadAsync(_release, progress, _cancellation.Token);
            ProgressText.Text = "下载完成，准备启动安装程序...";
            if (MessageBox.Show(this, "安装包已下载并校验完成。现在关闭软件并开始安装吗？", "准备安装更新", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            {
                InstallButton.IsEnabled = true; LaterButton.IsEnabled = true; return;
            }
            UpdateService.StartInstaller(installer);
            Application.Current.Shutdown();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"更新下载失败，未改动当前安装。\n\n{ex.Message}", "无法更新", MessageBoxButton.OK, MessageBoxImage.Warning);
            InstallButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
            ProgressText.Text = "下载失败，可重试或打开版本页面手动下载。";
        }
    }
}
