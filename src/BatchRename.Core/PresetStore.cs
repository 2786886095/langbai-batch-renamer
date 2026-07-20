using System.Text.Json;

namespace BatchRename.Core;

public sealed class PresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public PresetStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(AppDataPaths.BaseDirectory, "presets.json");
    }

    public string FilePath { get; }

    public List<RenamePreset> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            return (JsonSerializer.Deserialize<List<RenamePreset>>(File.ReadAllText(FilePath), JsonOptions) ?? [])
                .Where(item => !string.IsNullOrWhiteSpace(item.Name) && item.Options is not null)
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public void Save(string name, RenameOptions options)
    {
        var normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
            throw new ArgumentException("方案名称不能为空。", nameof(name));

        var presets = Load();
        var existing = presets.FirstOrDefault(item => string.Equals(item.Name, normalizedName, StringComparison.CurrentCultureIgnoreCase));
        if (existing is null)
            presets.Add(new RenamePreset { Name = normalizedName, Options = options.Clone() });
        else
            existing.Options = options.Clone();
        SaveAll(presets);
    }

    public void Delete(string name)
    {
        var presets = Load();
        presets.RemoveAll(item => string.Equals(item.Name, name, StringComparison.CurrentCultureIgnoreCase));
        SaveAll(presets);
    }

    private void SaveAll(List<RenamePreset> presets)
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        var temporary = FilePath + ".tmp";
        var ordered = presets.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        File.WriteAllText(temporary, JsonSerializer.Serialize(ordered, JsonOptions));
        File.Move(temporary, FilePath, true);
    }
}
