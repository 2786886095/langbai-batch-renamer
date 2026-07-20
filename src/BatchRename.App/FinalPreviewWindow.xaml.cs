using System.Runtime.InteropServices;
using System.Windows;
using BatchRename.Core;

namespace BatchRename.App;

public partial class FinalPreviewWindow : Window
{
    private readonly IReadOnlyList<FinalPreviewRow> _rows;

    public FinalPreviewWindow(IReadOnlyList<RenamePlanItem> items)
    {
        InitializeComponent();
        _rows = items.Where(item => item.HasChange)
            .Select(item => new FinalPreviewRow(item))
            .ToList();
        DataContext = _rows;
        SummaryText.Text = $"共 {_rows.Count} 项将发生变化；灰色为原名称，蓝色为新名称。";
    }

    private async void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, _rows.Select(row => $"{row.OriginalName} → {row.NewName}")));
            CopyButton.Content = "已复制";
            CopyButton.IsEnabled = false;
            await Task.Delay(1400);
            CopyButton.Content = "复制变更清单";
            CopyButton.IsEnabled = true;
        }
        catch (ExternalException ex)
        {
            MessageBox.Show(this, $"剪贴板暂时被其他程序占用，请稍后重试。\n\n{ex.Message}", "复制失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}

public sealed class FinalPreviewRow
{
    public FinalPreviewRow(RenamePlanItem item)
    {
        OriginalName = item.OriginalName;
        NewName = item.NewName;
        TypeGlyph = item.IsDirectory ? "\uE8B7" : "\uE7C3";
    }

    public string OriginalName { get; }
    public string NewName { get; }
    public string TypeGlyph { get; }
}
