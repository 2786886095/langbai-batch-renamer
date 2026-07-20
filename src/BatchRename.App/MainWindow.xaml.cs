using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BatchRename.Core;
using Microsoft.Win32;

namespace BatchRename.App;

public static class AppCommands
{
    public static readonly RoutedUICommand AddFiles = new("添加文件", nameof(AddFiles), typeof(AppCommands));
    public static readonly RoutedUICommand AddFolders = new("添加文件夹", nameof(AddFolders), typeof(AppCommands));
    public static readonly RoutedUICommand RefreshPreview = new("重新生成预览", nameof(RefreshPreview), typeof(AppCommands));
    public static readonly RoutedUICommand OpenHistory = new("回退历史", nameof(OpenHistory), typeof(AppCommands));
    public static readonly RoutedUICommand ExecuteRename = new("打开最终预览", nameof(ExecuteRename), typeof(AppCommands));
}

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly List<string> _paths = [];
    private readonly HistoryStore _historyStore = new();
    private readonly PresetStore _presetStore = new();
    private readonly AppSettingsStore _settingsStore = new();
    private readonly UpdateService _updateService = new();
    private readonly DispatcherTimer _previewTimer;
    private bool _isLoaded;
    private bool _isApplyingPreset;
    private string _lastRuleError = string.Empty;
    private int _previewGeneration;
    private bool _isCheckingForUpdates;

    public MainWindow(IReadOnlyList<string> initialPaths)
    {
        InitializeComponent();
        TemplateBox.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(RuleChanged));
        _previewTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(180) };
        _previewTimer.Tick += async (_, _) => { _previewTimer.Stop(); await RegeneratePreviewNowAsync(); };
        DataContext = this;
        AddPaths(initialPaths);
        LoadPresets();
        Loaded += async (_, _) =>
        {
            _isLoaded = true;
            await RegeneratePreviewNowAsync();
            if (_settingsStore.Load().CheckUpdatesOnStartup)
            {
                await Task.Delay(600);
                await CheckForUpdatesAsync(false);
            }
        };
    }

    public BulkObservableCollection<RenameRowViewModel> Rows { get; } = [];
    public ObservableCollection<RenamePreset> Presets { get; } = [];
    public event PropertyChangedEventHandler? PropertyChanged;

    private RenameOptions? ReadOptions()
    {
        if (!int.TryParse(StartNumberBox.Text, out var start))
        {
            _lastRuleError = "起始序号必须是整数。";
            return null;
        }
        if (!int.TryParse(PaddingWidthBox.Text, out var width) || width is < 1 or > 12)
        {
            _lastRuleError = "补零位数必须是 1 到 12 之间的整数。";
            return null;
        }

        var timeFormat = TimeFormatBox.Text;
        if (string.IsNullOrWhiteSpace(timeFormat) && TimeFormatBox.SelectedItem is ComboBoxItem item)
            timeFormat = item.Content?.ToString() ?? "yyyyMMdd_HHmmss";
        var sortBy = Enum.TryParse<RenameSortBy>(SortByBox.SelectedValue?.ToString(), out var parsedSort)
            ? parsedSort
            : RenameSortBy.Name;
        _lastRuleError = string.Empty;
        return new RenameOptions
        {
            Template = TemplateBox.Text,
            SearchText = SearchBox.Text,
            ReplaceText = ReplaceBox.Text,
            UseRegex = RegexCheck.IsChecked == true,
            StartNumber = start,
            PaddingWidth = width,
            TimeFormat = timeFormat,
            SortBy = sortBy,
            SortDescending = SortDescendingCheck.IsChecked == true
        };
    }

    private void RegeneratePreview()
    {
        if (!_isLoaded) return;
        _previewGeneration++;
        ExecuteButton.IsEnabled = false;
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private async Task RegeneratePreviewNowAsync()
    {
        if (!_isLoaded) return;
        _previewTimer.Stop();
        var generation = ++_previewGeneration;
        UpdateHelperText();
        var options = ReadOptions();
        if (options is null) { SetRuleError(_lastRuleError); return; }
        var paths = _paths.ToArray();
        try
        {
            var plan = await Task.Run(() => RenamePlanner.Build(paths, options));
            if (generation != _previewGeneration) return;
            var rows = new List<RenameRowViewModel>(plan.Count);
            foreach (var item in plan)
            {
                var row = new RenameRowViewModel(item);
                row.PropertyChanged += Row_PropertyChanged;
                rows.Add(row);
            }
            Rows.ReplaceAll(rows);
            ValidateRows();
        }
        catch (RenameValidationException ex) { if (generation == _previewGeneration) SetRuleError(ex.Message); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (generation == _previewGeneration) SetRuleError($"暂时无法读取所选项目：{ex.Message}");
        }
        RefreshSummary();
    }

    private void ValidateRows()
    {
        var items = Rows.Select(row => row.Model).ToList();
        var valid = RenameValidator.Validate(items);
        foreach (var row in Rows) row.RefreshStatus();
        var changed = items.Count(item => item.HasChange);
        var errors = items.Count(item => !string.IsNullOrEmpty(item.Error));
        ExecuteButton.IsEnabled = items.Count > 0 && valid && changed > 0;
        CommandManager.InvalidateRequerySuggested();
        ValidationText.Foreground = errors > 0 ? new SolidColorBrush(Color.FromRgb(196, 54, 54)) : (Brush)FindResource("MutedBrush");
        ValidationText.Text = errors > 0
            ? $"发现 {errors} 项问题：将鼠标停在红色状态上查看原因。"
            : items.Count == 0 ? "添加项目后即可预览。"
            : changed == 0 ? "当前规则不会改变任何名称。"
            : $"预览有效：{changed} 项将被重命名，{items.Count - changed} 项保持不变。";
        EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PreviewGrid.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SetRuleError(string message)
    {
        ExecuteButton.IsEnabled = false;
        ValidationText.Foreground = new SolidColorBrush(Color.FromRgb(196, 54, 54));
        ValidationText.Text = message;
    }

    private void RefreshSummary()
    {
        var fileCount = _paths.Count(File.Exists);
        var folderCount = _paths.Count(Directory.Exists);
        var sortDescription = GetSortDescription();
        SelectionSummary.Text = _paths.Count == 0
            ? $"尚未添加项目 · {sortDescription}"
            : $"已选择 {_paths.Count} 项 · {fileCount} 个文件 · {folderCount} 个文件夹 · {sortDescription}";
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(path => File.Exists(path) || Directory.Exists(path)))
        {
            var fullPath = Path.GetFullPath(path);
            if (!_paths.Contains(fullPath, StringComparer.OrdinalIgnoreCase)) _paths.Add(fullPath);
        }
        if (_isLoaded) RegeneratePreview();
    }

    private void RuleChanged(object sender, EventArgs e)
    {
        if (!_isLoaded || _isApplyingPreset) return;
        PresetStatusText.Text = TemplateBox.SelectedItem is RenamePreset preset
            ? $"当前设置已修改；点击“保存”可覆盖“{preset.Name}”。"
            : "当前格式尚未保存；点击“保存”可加入下拉列表。";
        RegeneratePreview();
    }
    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(RenameRowViewModel.NewName)) ValidateRows(); }

    private void InsertToken_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string token }) return;
        TemplateBox.ApplyTemplate();
        if (TemplateBox.Template.FindName("PART_EditableTextBox", TemplateBox) is TextBox editor)
        {
            var insertionPoint = editor.SelectionStart;
            editor.SelectedText = token;
            editor.CaretIndex = insertionPoint + token.Length;
            editor.Focus();
        }
        else
        {
            TemplateBox.Text += token;
            TemplateBox.Focus();
        }
    }

    private void TemplateBox_Loaded(object sender, RoutedEventArgs e)
    {
        TemplateBox.ApplyTemplate();
        if (TemplateBox.Template.FindName("PART_EditableTextBox", TemplateBox) is not TextBox editor) return;
        editor.ContextMenu = new ContextMenu
        {
            Items =
            {
                CreateEditMenuItem("撤销", ApplicationCommands.Undo, editor, "Ctrl+Z"),
                new Separator(),
                CreateEditMenuItem("剪切", ApplicationCommands.Cut, editor, "Ctrl+X"),
                CreateEditMenuItem("复制", ApplicationCommands.Copy, editor, "Ctrl+C"),
                CreateEditMenuItem("粘贴", ApplicationCommands.Paste, editor, "Ctrl+V"),
                CreateEditMenuItem("删除", ApplicationCommands.Delete, editor),
                new Separator(),
                CreateEditMenuItem("全选", ApplicationCommands.SelectAll, editor, "Ctrl+A")
            }
        };
    }

    private static MenuItem CreateEditMenuItem(string header, ICommand command, TextBox target, string gesture = "") => new()
    {
        Header = header,
        Command = command,
        CommandTarget = target,
        InputGestureText = gesture
    };

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = "选择要批量重命名的文件", Filter = "所有文件|*.*" };
        if (dialog.ShowDialog(this) == true) AddPaths(dialog.FileNames);
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Multiselect = true, Title = "选择要批量重命名的文件夹" };
        if (dialog.ShowDialog(this) == true) AddPaths(dialog.FolderNames);
    }

    private void Clear_Click(object sender, RoutedEventArgs e) { _paths.Clear(); RegeneratePreview(); }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RegeneratePreviewNowAsync();

    private void AddFilesCommand_Executed(object sender, ExecutedRoutedEventArgs e) => AddFiles_Click(sender, e);
    private void AddFoldersCommand_Executed(object sender, ExecutedRoutedEventArgs e) => AddFolder_Click(sender, e);
    private void RefreshPreviewCommand_Executed(object sender, ExecutedRoutedEventArgs e) => Refresh_Click(sender, e);
    private void OpenHistoryCommand_Executed(object sender, ExecutedRoutedEventArgs e) => History_Click(sender, e);
    private void ExecuteRenameCommand_Executed(object sender, ExecutedRoutedEventArgs e) => Execute_Click(sender, e);
    private void ExecuteRenameCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = ExecuteButton?.IsEnabled == true;
        e.Handled = true;
    }

    private void Execute_Click(object sender, RoutedEventArgs e)
    {
        var items = Rows.Select(row => row.Model).ToList();
        if (!RenameValidator.Validate(items)) { ValidateRows(); return; }
        var finalPreview = new FinalPreviewWindow(items) { Owner = this };
        if (finalPreview.ShowDialog() != true) return;

        try
        {
            var targetPaths = items.Select(item => item.TargetPath).ToList();
            var history = RenameExecutor.ExecuteAndRecord(items, _historyStore);
            _paths.Clear();
            _paths.AddRange(targetPaths);
            RegeneratePreview();
            MessageBox.Show(this, $"已安全重命名 {history.Operations.Count} 个项目，并保存回退记录。", "批量重命名完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or RenameValidationException)
        {
            MessageBox.Show(this, $"执行过程中发生错误，请根据下方说明检查文件状态。\n\n{ex.Message}", "批量重命名失败", MessageBoxButton.OK, MessageBoxImage.Error);
            RegeneratePreview();
        }
    }

    private void History_Click(object sender, RoutedEventArgs e)
    {
        new HistoryWindow(_historyStore) { Owner = this }.ShowDialog();
        RegeneratePreview();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => new SettingsWindow(this) { Owner = this }.ShowDialog();
    private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();

    public async Task CheckForUpdatesAsync(bool userInitiated)
    {
        if (_isCheckingForUpdates) return;
        _isCheckingForUpdates = true;
        try
        {
            var release = await _updateService.CheckAsync();
            var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive) ?? this;
            if (release is null)
            {
                if (userInitiated) MessageBox.Show(owner, $"当前已是最新版本 {UpdateService.CurrentVersion.ToString(3)}。", "检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            new UpdateWindow(release, _updateService) { Owner = owner }.ShowDialog();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            if (userInitiated) MessageBox.Show(this, $"暂时无法连接 GitHub 检查更新。\n\n{ex.Message}", "检查更新失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { _isCheckingForUpdates = false; }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
    private void Window_Drop(object sender, DragEventArgs e) { if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) AddPaths(paths); }
    private void PreviewGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e) => Dispatcher.BeginInvoke(ValidateRows);

    private void LoadPresets(string? selectName = null)
    {
        var currentTemplate = string.IsNullOrWhiteSpace(TemplateBox.Text) ? "{P}{S}" : TemplateBox.Text;
        _isApplyingPreset = true;
        Presets.Clear();
        foreach (var preset in _presetStore.Load()) Presets.Add(preset);
        TemplateBox.SelectedItem = selectName is null
            ? null
            : Presets.FirstOrDefault(item => string.Equals(item.Name, selectName, StringComparison.CurrentCultureIgnoreCase));
        TemplateBox.Text = TemplateBox.SelectedItem is RenamePreset selected ? selected.Options.Template : currentTemplate;
        DeletePresetButton.IsEnabled = TemplateBox.SelectedItem is RenamePreset;
        _isApplyingPreset = false;
    }

    private void TemplateBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DeletePresetButton.IsEnabled = TemplateBox.SelectedItem is RenamePreset;
        if (!_isLoaded || _isApplyingPreset || TemplateBox.SelectedItem is not RenamePreset preset) return;

        _isApplyingPreset = true;
        var options = preset.Options;
        TemplateBox.Text = options.Template;
        SearchBox.Text = options.SearchText;
        ReplaceBox.Text = options.ReplaceText;
        RegexCheck.IsChecked = options.UseRegex;
        StartNumberBox.Text = options.StartNumber.ToString();
        PaddingWidthBox.Text = options.PaddingWidth.ToString();
        TimeFormatBox.SelectedIndex = -1;
        TimeFormatBox.Text = options.TimeFormat;
        SortByBox.SelectedValue = options.SortBy.ToString();
        SortDescendingCheck.IsChecked = options.SortDescending;
        _isApplyingPreset = false;
        PresetStatusText.Text = $"已载入本机方案“{preset.Name}”。";
        RegeneratePreview();
    }

    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        var options = ReadOptions();
        if (options is null) { SetRuleError(_lastRuleError); return; }
        var suggestedName = TemplateBox.SelectedItem is RenamePreset selected ? selected.Name : "我的命名方案";
        var dialog = new SavePresetWindow(suggestedName) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        var exists = Presets.Any(item => string.Equals(item.Name, dialog.PresetName, StringComparison.CurrentCultureIgnoreCase));
        if (exists && MessageBox.Show(this, $"“{dialog.PresetName}”已经存在，是否用当前设置覆盖？", "覆盖命名方案", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;

        try
        {
            _presetStore.Save(dialog.PresetName, options);
            LoadPresets(dialog.PresetName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"无法将方案写入本机配置目录。\n\n{ex.Message}", "保存方案失败", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        PresetStatusText.Text = $"已保存本机方案“{dialog.PresetName}”。";
        ValidationText.Foreground = (Brush)FindResource("MutedBrush");
        ValidationText.Text = $"已将当前规则保存为“{dialog.PresetName}”。";
    }

    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateBox.SelectedItem is not RenamePreset preset) return;
        if (MessageBox.Show(this, $"确定删除本机方案“{preset.Name}”吗？此操作不会删除文件。", "删除命名方案", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            return;
        try
        {
            _presetStore.Delete(preset.Name);
            LoadPresets();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"无法更新本机方案文件。\n\n{ex.Message}", "删除方案失败", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        PresetStatusText.Text = "当前设置尚未保存为本地方案。";
        ValidationText.Foreground = (Brush)FindResource("MutedBrush");
        ValidationText.Text = $"已删除本机方案“{preset.Name}”。";
    }

    private void ComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ComboBox { IsDropDownOpen: true }) return;
        e.Handled = true;
        ScrollRulePanel(e.Delta);
    }

    private void RuleScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled) return;
        e.Handled = true;
        ScrollRulePanel(e.Delta);
    }

    private void ScrollRulePanel(int wheelDelta)
    {
        var notches = Math.Max(1, Math.Abs(wheelDelta) / 120.0);
        var distance = 96 * notches;
        RuleScrollViewer.ScrollToVerticalOffset(RuleScrollViewer.VerticalOffset + (wheelDelta > 0 ? -distance : distance));
    }

    private void UpdateHelperText()
    {
        StartHintText.Text = int.TryParse(StartNumberBox.Text, out var start)
            ? $"仅 {{N}}/{{zN}} 使用：首项 {start}，随后 {start + 1}、{start + 2}……"
            : "请输入整数；它决定 {N}/{zN} 的第一个序号。";
        PaddingHintText.Text = int.TryParse(PaddingWidthBox.Text, out var width) && width is >= 1 and <= 12
            ? $"仅 {{zN}} 使用：{Math.Max(0, start).ToString().PadLeft(width, '0')}、{Math.Max(0, start + 1).ToString().PadLeft(width, '0')}……"
            : "请输入 1–12；用于在序号左侧补 0。";

        var timeFormat = TimeFormatBox.Text;
        if (string.IsNullOrWhiteSpace(timeFormat) && TimeFormatBox.SelectedItem is ComboBoxItem item)
            timeFormat = item.Content?.ToString() ?? string.Empty;
        try
        {
            var example = new DateTime(2026, 7, 20, 14, 30, 25).ToString(timeFormat);
            TimeHintText.Text = $"仅 {{T}} 使用：yyyy=年，MM=月，dd=日，HH=时，mm=分，ss=秒；示例 {example}";
        }
        catch (FormatException)
        {
            TimeHintText.Text = "格式无效；可使用 yyyyMMdd_HHmmss，例如 20260720_143025。";
        }

        SortHintText.Text = SortByBox.SelectedValue?.ToString() switch
        {
            nameof(RenameSortBy.ModifiedTime) => "按最后修改时间排列；勾选倒序可让最新项目排在前面。",
            nameof(RenameSortBy.Size) => "按文件字节大小排列；文件夹大小按 0 处理。",
            nameof(RenameSortBy.Type) => "文件夹优先，再按文件扩展名排列；同类型按名称自然排序。",
            _ => "名称中的数字按实际大小排序，例如 2 会排在 10 前面。"
        };
    }

    private string GetSortDescription()
    {
        var label = SortByBox.SelectedValue?.ToString() switch
        {
            nameof(RenameSortBy.ModifiedTime) => "按修改日期",
            nameof(RenameSortBy.Size) => "按大小",
            nameof(RenameSortBy.Type) => "按类型",
            _ => "按名称自然排序"
        };
        return SortDescendingCheck.IsChecked == true ? $"{label}（倒序）" : label;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class RenameRowViewModel : INotifyPropertyChanged
{
    public RenameRowViewModel(RenamePlanItem model) => Model = model;
    public RenamePlanItem Model { get; }
    public string OriginalName => Model.OriginalName;
    public string TypeGlyph => Model.IsDirectory ? "\uE8B7" : "\uE7C3";
    public string TypeLabel => Model.IsDirectory ? "文件夹" : "文件";
    public Brush TypeBrush => new SolidColorBrush(Color.FromRgb(37, 99, 235));
    public string NewName
    {
        get => Model.NewName;
        set
        {
            if (Model.NewName == value) return;
            Model.NewName = value;
            Model.IsManual = value != Model.SuggestedName;
            OnPropertyChanged();
        }
    }
    public string Error => Model.Error;
    public string StatusText => !string.IsNullOrEmpty(Model.Error) ? "需修正" : Model.HasChange ? (Model.IsManual ? "已手改" : "可执行") : "不变";
    public string StatusGlyph => !string.IsNullOrEmpty(Model.Error) ? "\uEA39" : "\uE73E";
    public Brush StatusBrush => !string.IsNullOrEmpty(Model.Error)
        ? new SolidColorBrush(Color.FromRgb(180, 35, 24))
        : Model.HasChange ? new SolidColorBrush(Color.FromRgb(6, 118, 71)) : new SolidColorBrush(Color.FromRgb(100, 116, 139));
    public Brush StatusBackground => !string.IsNullOrEmpty(Model.Error)
        ? new SolidColorBrush(Color.FromRgb(254, 243, 242))
        : Model.HasChange ? new SolidColorBrush(Color.FromRgb(236, 253, 243)) : new SolidColorBrush(Color.FromRgb(241, 245, 249));
    public event PropertyChangedEventHandler? PropertyChanged;
    public void RefreshStatus()
    {
        OnPropertyChanged(nameof(Error)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusGlyph)); OnPropertyChanged(nameof(StatusBrush)); OnPropertyChanged(nameof(StatusBackground));
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> items)
    {
        CheckReentrancy();
        Items.Clear();
        foreach (var item in items) Items.Add(item);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
