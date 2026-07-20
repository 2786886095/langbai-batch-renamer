using BatchRename.Core;

var tests = new List<(string Name, Action Run)>
{
    ("模板、查找替换和名称排序", TestPlanning),
    ("补零序号", TestPaddedSequence),
    ("日期、大小、类型和倒序排序", TestSortModes),
    ("命名方案本地保存、覆盖和删除", TestPresetPersistence),
    ("文件与文件夹执行后可回退", TestExecuteAndUndo),
    ("重复目标冲突", TestDuplicateCollision),
    ("外部目标占用冲突", TestExistingTargetCollision),
    ("项目被占用时事务回滚", TestLockedItemRollback),
    ("历史保存失败时恢复原名称", TestHistorySaveFailureRollback),
    ("超长名称在预览阶段被阻止", TestLongNameValidation),
    ("历史文件被占用时安全降级", TestLockedHistoryRead),
    ("历史记录跨进程持久化", TestHistoryPersistence)
    ,("回退状态保存失败时恢复到回退前", TestUndoHistorySaveFailureRollback)
    ,("损坏历史不会在新增记录时被覆盖", TestCorruptHistoryIsNotOverwritten)
    ,("高复杂度正则会超时终止", TestRegexTimeout)
    ,("一万项预览保持完整", TestLargeBatchPlanning)
    ,("总路径超过 260 字符仍可执行并回退", TestLongPathExecuteAndUndo)
};
if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BATCH_RENAME_UNC_TEST_ROOT")))
    tests.Add(("UNC 网络共享执行并回退", TestUncExecuteAndUndo));

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"FAIL  {test.Name}: {ex.Message}");
    }
}

Console.WriteLine($"{tests.Count - failures}/{tests.Count} tests passed");
return failures == 0 ? 0 : 1;

static void TestPlanning()
{
    using var fixture = new TempFixture();
    fixture.File("image10-final.png");
    fixture.File("image2-final.png");
    var items = RenamePlanner.Build(Directory.GetFiles(fixture.Root), new RenameOptions
    {
        Template = "作品_{zN}_{P}{S}",
        SearchText = "-final",
        ReplaceText = string.Empty,
        StartNumber = 1,
        PaddingWidth = 3
    });
    Equal("image2-final.png", items[0].OriginalName);
    Equal("作品_001_image2.png", items[0].NewName);
    Equal("作品_002_image10.png", items[1].NewName);
}

static void TestPaddedSequence()
{
    using var fixture = new TempFixture();
    fixture.File("a.txt");
    fixture.File("b.txt");
    fixture.File("c.txt");
    var items = RenamePlanner.Build(Directory.GetFiles(fixture.Root), new RenameOptions { Template = "{z8}{S}" });
    Equal("08.txt", items[0].NewName);
    Equal("09.txt", items[1].NewName);
    Equal("10.txt", items[2].NewName);
}

static void TestSortModes()
{
    using var fixture = new TempFixture();
    var smallText = fixture.File("small.txt");
    var largeLog = fixture.File("large.log");
    var middleText = fixture.File("middle.txt");
    System.IO.File.WriteAllText(smallText, "1");
    System.IO.File.WriteAllText(middleText, "12345");
    System.IO.File.WriteAllText(largeLog, new string('x', 20));
    System.IO.File.SetLastWriteTime(smallText, new DateTime(2026, 1, 1));
    System.IO.File.SetLastWriteTime(middleText, new DateTime(2026, 1, 2));
    System.IO.File.SetLastWriteTime(largeLog, new DateTime(2026, 1, 3));
    var paths = new[] { middleText, largeLog, smallText };

    var byDate = RenamePlanner.Build(paths, new RenameOptions { Template = "{N}{S}", SortBy = RenameSortBy.ModifiedTime });
    Equal("small.txt", byDate[0].OriginalName);
    Equal("large.log", byDate[2].OriginalName);

    var bySizeDescending = RenamePlanner.Build(paths, new RenameOptions { Template = "{N}{S}", SortBy = RenameSortBy.Size, SortDescending = true });
    Equal("large.log", bySizeDescending[0].OriginalName);
    Equal("small.txt", bySizeDescending[2].OriginalName);

    var byType = RenamePlanner.Build(paths, new RenameOptions { Template = "{N}{S}", SortBy = RenameSortBy.Type });
    Equal("large.log", byType[0].OriginalName);
    True(byType.Skip(1).All(item => item.OriginalName.EndsWith(".txt", StringComparison.Ordinal)));
}

static void TestPresetPersistence()
{
    using var fixture = new TempFixture();
    var presetPath = Path.Combine(fixture.Root, "presets.json");
    var store = new PresetStore(presetPath);
    store.Save("漫画序号", new RenameOptions
    {
        Template = "分镜_{zN}_{P}{S}",
        StartNumber = 8,
        PaddingWidth = 4,
        TimeFormat = "yyyy-MM-dd",
        SortBy = RenameSortBy.ModifiedTime,
        SortDescending = true
    });

    var loaded = new PresetStore(presetPath).Load().Single();
    Equal("漫画序号", loaded.Name);
    Equal("分镜_{zN}_{P}{S}", loaded.Options.Template);
    Equal(RenameSortBy.ModifiedTime, loaded.Options.SortBy);
    True(loaded.Options.SortDescending);

    store.Save("漫画序号", new RenameOptions { Template = "覆盖_{N}{S}" });
    Equal("覆盖_{N}{S}", store.Load().Single().Options.Template);
    store.Delete("漫画序号");
    Equal(0, store.Load().Count);
}

static void TestExecuteAndUndo()
{
    using var fixture = new TempFixture();
    var file = fixture.File("a.txt");
    var folder = fixture.Directory("folder");
    var items = RenamePlanner.Build([folder, file], new RenameOptions { Template = "renamed_{N}{S}", StartNumber = 1 });
    var history = RenameExecutor.Execute(items);
    True(File.Exists(Path.Combine(fixture.Root, "renamed_1.txt")));
    True(Directory.Exists(Path.Combine(fixture.Root, "renamed_2")));
    RenameExecutor.Undo(history);
    True(File.Exists(file));
    True(Directory.Exists(folder));
}

static void TestDuplicateCollision()
{
    using var fixture = new TempFixture();
    fixture.File("a.txt");
    fixture.File("b.txt");
    var items = RenamePlanner.Build(Directory.GetFiles(fixture.Root), new RenameOptions { Template = "same{S}" });
    True(items.All(item => item.Error.Contains("相同名称")));
}

static void TestExistingTargetCollision()
{
    using var fixture = new TempFixture();
    var source = fixture.File("source.txt");
    fixture.File("occupied.txt");
    var items = RenamePlanner.Build([source], new RenameOptions { Template = "occupied{S}" });
    True(items[0].Error.Contains("占用"));
}

static void TestLongNameValidation()
{
    using var fixture = new TempFixture();
    var source = fixture.File("source.txt");
    var items = RenamePlanner.Build([source], new RenameOptions { Template = new string('x', 256) });
    True(items[0].Error.Contains("255"));
}

static void TestLockedItemRollback()
{
    using var fixture = new TempFixture();
    var fileA = fixture.File("a.txt");
    var fileB = fixture.File("b.txt");
    var items = RenamePlanner.Build([fileA, fileB], new RenameOptions { Template = "renamed_{P}{S}" });
    using var lockStream = new FileStream(fileB, FileMode.Open, FileAccess.Read, FileShare.Read);
    try
    {
        RenameExecutor.Execute(items);
        throw new InvalidOperationException("Expected the locked rename to fail.");
    }
    catch (IOException) { }
    True(System.IO.File.Exists(fileA));
    True(System.IO.File.Exists(fileB));
    True(!System.IO.File.Exists(Path.Combine(fixture.Root, "renamed_a.txt")));
    True(!System.IO.Directory.EnumerateFileSystemEntries(fixture.Root, ".batchrename-*.tmp").Any());
}

static void TestLockedHistoryRead()
{
    using var fixture = new TempFixture();
    var historyPath = Path.Combine(fixture.Root, "history.json");
    System.IO.File.WriteAllText(historyPath, "[]");
    using var lockStream = new FileStream(historyPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    Equal(0, new HistoryStore(historyPath).Load().Count);
}

static void TestHistorySaveFailureRollback()
{
    using var fixture = new TempFixture();
    var fileA = fixture.File("a.txt");
    var fileB = fixture.File("b.txt");
    var historyPath = Path.Combine(fixture.Root, "history.json");
    System.IO.File.WriteAllText(historyPath, "[]");
    var items = RenamePlanner.Build([fileA, fileB], new RenameOptions { Template = "renamed_{P}{S}" });
    using var lockStream = new FileStream(historyPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    try
    {
        RenameExecutor.ExecuteAndRecord(items, new HistoryStore(historyPath));
        throw new InvalidOperationException("Expected the history save to fail.");
    }
    catch (IOException) { }
    True(System.IO.File.Exists(fileA));
    True(System.IO.File.Exists(fileB));
    True(!System.IO.File.Exists(Path.Combine(fixture.Root, "renamed_a.txt")));
}

static void TestHistoryPersistence()
{
    using var fixture = new TempFixture();
    var historyPath = Path.Combine(fixture.Root, "history.json");
    var store = new HistoryStore(historyPath);
    var entry = new RenameHistoryEntry
    {
        Operations = [new RenameOperation { OriginalPath = "a", RenamedPath = "b", IsDirectory = false }]
    };
    store.Add(entry);
    var loaded = new HistoryStore(historyPath).Load();
    Equal(entry.Id, loaded.Single().Id);
    store.MarkUndone(entry.Id);
    True(new HistoryStore(historyPath).Load().Single().IsUndone);
}

static void TestUndoHistorySaveFailureRollback()
{
    using var fixture = new TempFixture();
    var original = fixture.File("a.txt");
    var historyPath = Path.Combine(fixture.Root, "history.json");
    var store = new HistoryStore(historyPath);
    var entry = RenameExecutor.ExecuteAndRecord(
        RenamePlanner.Build([original], new RenameOptions { Template = "renamed_{P}{S}" }), store);
    using var lockStream = new FileStream(historyPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    try
    {
        RenameExecutor.UndoAndRecord(entry, store);
        throw new InvalidOperationException("Expected the history update to fail.");
    }
    catch (IOException) { }
    True(!System.IO.File.Exists(original));
    True(System.IO.File.Exists(Path.Combine(fixture.Root, "renamed_a.txt")));
    True(!entry.IsUndone);
}

static void TestCorruptHistoryIsNotOverwritten()
{
    using var fixture = new TempFixture();
    var historyPath = Path.Combine(fixture.Root, "history.json");
    const string corrupt = "{not-json";
    System.IO.File.WriteAllText(historyPath, corrupt);
    try
    {
        new HistoryStore(historyPath).Add(new RenameHistoryEntry());
        throw new InvalidOperationException("Expected corrupt history to block the write.");
    }
    catch (InvalidDataException) { }
    Equal(corrupt, System.IO.File.ReadAllText(historyPath));
}

static void TestRegexTimeout()
{
    using var fixture = new TempFixture();
    var path = fixture.File(new string('a', 240) + "b.txt");
    try
    {
        RenamePlanner.Build([path], new RenameOptions
        {
            Template = "{P}{S}",
            SearchText = "^(a+)+$",
            ReplaceText = "x",
            UseRegex = true
        });
    }
    catch (RenameValidationException ex) when (ex.Message.Contains("超时"))
    {
        return;
    }
    throw new InvalidOperationException("Expected pathological regex to time out.");
}

static void TestLargeBatchPlanning()
{
    using var fixture = new TempFixture();
    var paths = Enumerable.Range(1, 10_000)
        .Select(index => Path.Combine(fixture.Root, $"item{index}.txt"))
        .ToArray();
    foreach (var path in paths) System.IO.File.WriteAllText(path, string.Empty);
    var items = RenamePlanner.Build(paths.Reverse(), new RenameOptions { Template = "batch_{zN}{S}", PaddingWidth = 5 });
    Equal(10_000, items.Count);
    Equal("item1.txt", items[0].OriginalName);
    Equal("batch_00001.txt", items[0].NewName);
    Equal("item10000.txt", items[^1].OriginalName);
}

static void TestLongPathExecuteAndUndo()
{
    using var fixture = new TempFixture();
    var directory = fixture.Root;
    while (directory.Length < 280)
    {
        directory = Path.Combine(directory, "segment_1234567890");
        System.IO.Directory.CreateDirectory(directory);
    }
    var original = Path.Combine(directory, "a.txt");
    System.IO.File.WriteAllText(original, "long-path");
    var entry = RenameExecutor.Execute(RenamePlanner.Build([original], new RenameOptions { Template = "renamed_{P}{S}" }));
    var renamed = Path.Combine(directory, "renamed_a.txt");
    True(System.IO.File.Exists(renamed));
    RenameExecutor.Undo(entry);
    True(System.IO.File.Exists(original));
}

static void TestUncExecuteAndUndo()
{
    var basePath = Environment.GetEnvironmentVariable("BATCH_RENAME_UNC_TEST_ROOT")!;
    var root = Path.Combine(basePath, "BatchRename.Unc.Tests", Guid.NewGuid().ToString("N"));
    System.IO.Directory.CreateDirectory(root);
    try
    {
        var original = Path.Combine(root, "network.txt");
        System.IO.File.WriteAllText(original, "unc");
        var entry = RenameExecutor.Execute(RenamePlanner.Build([original], new RenameOptions { Template = "renamed_{P}{S}" }));
        True(System.IO.File.Exists(Path.Combine(root, "renamed_network.txt")));
        RenameExecutor.Undo(entry);
        True(System.IO.File.Exists(original));
    }
    finally
    {
        System.IO.Directory.Delete(root, true);
    }
}

static void True(bool condition)
{
    if (!condition) throw new InvalidOperationException("Expected true.");
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
}

sealed class TempFixture : IDisposable
{
    public TempFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "BatchRename.Tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Root);
    }

    public string Root { get; }
    public string File(string name)
    {
        var path = Path.Combine(Root, name);
        System.IO.File.WriteAllText(path, name);
        return path;
    }

    public string Directory(string name)
    {
        var path = Path.Combine(Root, name);
        System.IO.Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Root)) System.IO.Directory.Delete(Root, true);
    }
}
