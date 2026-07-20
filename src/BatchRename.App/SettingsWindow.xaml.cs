using System.Windows;

namespace BatchRename.App;

public partial class SettingsWindow : Window
{
    private readonly MainWindow _mainWindow;
    private readonly AppSettingsStore _store = new();

    public SettingsWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        StartupUpdateCheck.IsChecked = _store.Load().CheckUpdatesOnStartup;
        VersionText.Text = $"当前版本：{UpdateService.CurrentVersion.ToString(3)}";
    }

    private async void CheckNow_Click(object sender, RoutedEventArgs e) => await _mainWindow.CheckForUpdatesAsync(true);
    private void Save_Click(object sender, RoutedEventArgs e) { _store.Save(new AppSettings { CheckUpdatesOnStartup = StartupUpdateCheck.IsChecked == true }); DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
