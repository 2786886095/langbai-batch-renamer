using System.Windows;
using System.Windows.Input;

namespace BatchRename.App;

public partial class SavePresetWindow : Window
{
    public SavePresetWindow(string suggestedName)
    {
        InitializeComponent();
        NameBox.Text = suggestedName;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public string PresetName => NameBox.Text.Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PresetName))
        {
            ErrorText.Text = "请输入方案名称。";
            NameBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
