using System.Text.Json;

namespace BatchRename.Core;

public sealed class HistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public HistoryStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(AppDataPaths.BaseDirectory, "history.json");
    }

    public string FilePath { get; }

    public List<RenameHistoryEntry> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            return JsonSerializer.Deserialize<List<RenameHistoryEntry>>(File.ReadAllText(FilePath), JsonOptions) ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public void Add(RenameHistoryEntry entry)
    {
        var entries = LoadForUpdate();
        entries.Insert(0, entry);
        if (entries.Count > 50) entries.RemoveRange(50, entries.Count - 50);
        Save(entries);
    }

    public void MarkUndone(Guid id)
    {
        var entries = LoadForUpdate();
        var entry = entries.FirstOrDefault(candidate => candidate.Id == id);
        if (entry is null) throw new InvalidDataException("找不到要标记为已回退的历史记录。");
        entry.IsUndone = true;
        Save(entries);
    }

    private List<RenameHistoryEntry> LoadForUpdate()
    {
        if (!File.Exists(FilePath)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<RenameHistoryEntry>>(File.ReadAllText(FilePath), JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("历史文件已损坏，已停止写入以避免覆盖原记录。", ex);
        }
    }

    private void Save(List<RenameHistoryEntry> entries)
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(entries, JsonOptions));
        File.Move(temporary, FilePath, true);
    }
}
