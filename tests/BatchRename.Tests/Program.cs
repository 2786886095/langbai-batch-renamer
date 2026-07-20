using BatchRename.Core;

var tests = new (string Name, Action Run)[]
{
    ("模板、查找替换和名称排序", TestPlanning),
    ("补零序号", TestPaddedSequence),
    ("文件与文件夹执行后可回退", TestExecuteAndUndo),
    ("重复目标冲突", TestDuplicateCollision),
    ("外部目标占用冲突", TestExistingTargetCollision),
    ("历史记录跨进程持久化", TestHistoryPersistence)
};

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

Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed");
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
