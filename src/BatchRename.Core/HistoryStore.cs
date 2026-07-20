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
        catch (JsonException)
        {
            return [];
        }
    }

    public void Add(RenameHistoryEntry entry)
    {
        var entries = Load();
        entries.Insert(0, entry);
        if (entries.Count > 50) entries.RemoveRange(50, entries.Count - 50);
        Save(entries);
    }

    public void MarkUndone(Guid id)
    {
        var entries = Load();
        var entry = entries.FirstOrDefault(candidate => candidate.Id == id);
        if (entry is null) return;
        entry.IsUndone = true;
        Save(entries);
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
