using System.IO;
using System.Windows;
using System.Windows.Media;
using BatchRename.Core;

namespace BatchRename.App;

public partial class HistoryWindow : Window
{
    private readonly HistoryStore _store;
    public HistoryWindow(HistoryStore store) { InitializeComponent(); _store = store; Reload(); }
    private void Reload()
    {
        HistoryList.ItemsSource = _store.Load().Select(entry => new HistoryRow(entry)).ToList();
        EmptyHistory.Visibility = HistoryList.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryHint.Text = HistoryList.Items.Count == 0 ? "还没有批量重命名记录。" : "选择一条未回退的记录。";
    }
    private void HistoryList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UndoButton.IsEnabled = HistoryList.SelectedItem is HistoryRow { Entry.IsUndone: false };
    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is not HistoryRow row || row.Entry.IsUndone) return;
        if (MessageBox.Show(this, $"将把 {row.Entry.Operations.Count} 个项目恢复到本次重命名前的名称。", "确认回退", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        try
        {
            RenameExecutor.Undo(row.Entry); _store.MarkUndone(row.Entry.Id); Reload();
            MessageBox.Show(this, "已恢复原名称。", "回退完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or RenameValidationException)
        {
            MessageBox.Show(this, ex.Message, "无法回退", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

public sealed class HistoryRow(RenameHistoryEntry entry)
{
    public RenameHistoryEntry Entry { get; } = entry;
    public string Title => $"{Entry.CreatedAt:yyyy-MM-dd HH:mm:ss} · {Entry.Operations.Count} 项";
    public string Summary => Entry.Operations.Count == 0 ? "空操作" : $"{Path.GetFileName(Entry.Operations[0].OriginalPath)} → {Path.GetFileName(Entry.Operations[0].RenamedPath)}" + (Entry.Operations.Count > 1 ? "，以及其他项目" : string.Empty);
    public string StateText => Entry.IsUndone ? "已回退" : "可回退";
    public Brush BadgeBackground => new SolidColorBrush(Entry.IsUndone ? Color.FromRgb(241, 245, 249) : Color.FromRgb(236, 253, 243));
    public Brush BadgeForeground => new SolidColorBrush(Entry.IsUndone ? Color.FromRgb(100, 116, 139) : Color.FromRgb(6, 118, 71));
}
