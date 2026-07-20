namespace BatchRename.Core;

public static class RenameExecutor
{
    public static RenameHistoryEntry ExecuteAndRecord(IReadOnlyList<RenamePlanItem> items, HistoryStore historyStore)
    {
        ArgumentNullException.ThrowIfNull(historyStore);
        var history = Execute(items);
        try
        {
            historyStore.Add(history);
            return history;
        }
        catch (Exception saveError) when (saveError is IOException or UnauthorizedAccessException)
        {
            try
            {
                Undo(history);
            }
            catch (Exception rollbackError) when (rollbackError is IOException or UnauthorizedAccessException or RenameValidationException)
            {
                throw new IOException("重命名已完成，但回退记录保存失败，且无法自动恢复原名称。请立即检查文件状态。",
                    new AggregateException(saveError, rollbackError));
            }
            throw new IOException("无法保存回退记录，已自动恢复原名称。", saveError);
        }
    }

    public static RenameHistoryEntry Execute(IReadOnlyList<RenamePlanItem> items)
    {
        if (!RenameValidator.Validate(items))
            throw new RenameValidationException("预览中存在冲突或无效名称，请修正后再执行。");

        var changing = items.Where(item => item.HasChange).ToList();
        if (changing.Count == 0)
            throw new RenameValidationException("没有需要重命名的项目。");

        var moves = changing.Select(item => new PendingMove(
            item.SourcePath,
            item.TargetPath,
            item.IsDirectory,
            CreateTemporaryPath(item.ParentPath))).ToList();

        ExecuteTwoPhase(moves);
        return new RenameHistoryEntry
        {
            Operations = moves.Select(move => new RenameOperation
            {
                OriginalPath = move.Source,
                RenamedPath = move.Target,
                IsDirectory = move.IsDirectory
            }).ToList()
        };
    }

    public static void Undo(RenameHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.IsUndone) throw new RenameValidationException("这次操作已经回退。 ");

        var currentPaths = entry.Operations.Select(op => op.RenamedPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var operation in entry.Operations)
        {
            if (!Exists(operation.RenamedPath, operation.IsDirectory))
                throw new RenameValidationException($"无法回退：当前项目不存在：{operation.RenamedPath}");
            if (ExistsAny(operation.OriginalPath) && !currentPaths.Contains(operation.OriginalPath))
                throw new RenameValidationException($"无法回退：原名称已被占用：{operation.OriginalPath}");
        }

        var moves = entry.Operations.Select(operation => new PendingMove(
            operation.RenamedPath,
            operation.OriginalPath,
            operation.IsDirectory,
            CreateTemporaryPath(Path.GetDirectoryName(operation.RenamedPath) ?? string.Empty))).ToList();
        ExecuteTwoPhase(moves);
        entry.IsUndone = true;
    }

    private static void ExecuteTwoPhase(IReadOnlyList<PendingMove> moves)
    {
        var staged = new List<PendingMove>();
        var completed = new List<PendingMove>();
        try
        {
            foreach (var move in moves)
            {
                Move(move.Source, move.Temporary, move.IsDirectory);
                staged.Add(move);
            }

            foreach (var move in moves)
            {
                Move(move.Temporary, move.Target, move.IsDirectory);
                completed.Add(move);
            }
        }
        catch
        {
            foreach (var move in completed.AsEnumerable().Reverse())
            {
                TryMove(move.Target, move.Temporary, move.IsDirectory);
            }

            foreach (var move in staged.AsEnumerable().Reverse())
            {
                TryMove(move.Temporary, move.Source, move.IsDirectory);
            }
            throw;
        }
    }

    private static string CreateTemporaryPath(string parent)
    {
        string path;
        do path = Path.Combine(parent, $".batchrename-{Guid.NewGuid():N}.tmp");
        while (ExistsAny(path));
        return path;
    }

    private static void Move(string source, string target, bool isDirectory)
    {
        if (isDirectory) Directory.Move(source, target);
        else File.Move(source, target);
    }

    private static void TryMove(string source, string target, bool isDirectory)
    {
        try
        {
            if (Exists(source, isDirectory) && !ExistsAny(target)) Move(source, target, isDirectory);
        }
        catch
        {
            // The original exception is more useful. Any remaining temporary item keeps user data recoverable.
        }
    }

    private static bool Exists(string path, bool isDirectory) => isDirectory ? Directory.Exists(path) : File.Exists(path);
    private static bool ExistsAny(string path) => File.Exists(path) || Directory.Exists(path);

    private sealed record PendingMove(string Source, string Target, bool IsDirectory, string Temporary);
}
