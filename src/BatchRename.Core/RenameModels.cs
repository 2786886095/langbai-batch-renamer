using System.Text.Json.Serialization;

namespace BatchRename.Core;

public sealed class RenameOptions
{
    public string Template { get; set; } = "{P}{S}";
    public string SearchText { get; set; } = string.Empty;
    public string ReplaceText { get; set; } = string.Empty;
    public bool UseRegex { get; set; }
    public int StartNumber { get; set; } = 1;
    public int PaddingWidth { get; set; } = 3;
    public string TimeFormat { get; set; } = "yyyyMMdd_HHmmss";
}

public sealed class RenamePlanItem
{
    public required string SourcePath { get; init; }
    public required string OriginalName { get; init; }
    public required bool IsDirectory { get; init; }
    public required DateTime LastWriteTime { get; init; }
    public required string SuggestedName { get; init; }
    public string NewName { get; set; } = string.Empty;
    public bool IsManual { get; set; }
    public string Error { get; set; } = string.Empty;

    [JsonIgnore]
    public string ParentPath => Path.GetDirectoryName(SourcePath) ?? string.Empty;

    [JsonIgnore]
    public string TargetPath => Path.Combine(ParentPath, NewName);

    [JsonIgnore]
    public bool HasChange => !string.Equals(SourcePath, TargetPath, StringComparison.Ordinal);
}

public sealed class RenameOperation
{
    public required string OriginalPath { get; init; }
    public required string RenamedPath { get; init; }
    public required bool IsDirectory { get; init; }
}

public sealed class RenameHistoryEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public List<RenameOperation> Operations { get; init; } = [];
    public bool IsUndone { get; set; }
}

public sealed class RenameValidationException(string message) : Exception(message);
