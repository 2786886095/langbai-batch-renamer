using System.Text.RegularExpressions;

namespace BatchRename.Core;

public static partial class RenamePlanner
{
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static List<RenamePlanItem> Build(IEnumerable<string> paths, RenameOptions options)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Template))
            throw new RenameValidationException("命名规则不能为空。");
        if (options.PaddingWidth is < 1 or > 12)
            throw new RenameValidationException("补零位数必须在 1 到 12 之间。");

        Regex? searchRegex = null;
        if (options.UseRegex && !string.IsNullOrEmpty(options.SearchText))
        {
            try
            {
                searchRegex = new Regex(options.SearchText, RegexOptions.CultureInvariant);
            }
            catch (ArgumentException ex)
            {
                throw new RenameValidationException($"正则表达式无效：{ex.Message}");
            }
        }

        var ordered = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .OrderBy(path => Path.GetFileName(path), NaturalNameComparer.Instance)
            .ThenBy(path => path, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var result = new List<RenamePlanItem>(ordered.Count);
        for (var index = 0; index < ordered.Count; index++)
        {
            var path = ordered[index];
            var isDirectory = Directory.Exists(path);
            var originalName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var extension = isDirectory ? string.Empty : Path.GetExtension(originalName);
            var prefix = isDirectory ? originalName : Path.GetFileNameWithoutExtension(originalName);
            var changedPrefix = ApplySearch(prefix, options, searchRegex);
            var lastWrite = isDirectory ? Directory.GetLastWriteTime(path) : File.GetLastWriteTime(path);
            var generated = ExpandTemplate(options.Template, changedPrefix, extension, lastWrite, index, options);

            var item = new RenamePlanItem
            {
                SourcePath = path,
                OriginalName = originalName,
                IsDirectory = isDirectory,
                LastWriteTime = lastWrite,
                SuggestedName = generated,
                NewName = generated
            };
            result.Add(item);
        }

        RenameValidator.Validate(result);
        return result;
    }

    private static string ApplySearch(string prefix, RenameOptions options, Regex? searchRegex)
    {
        if (string.IsNullOrEmpty(options.SearchText)) return prefix;
        return options.UseRegex
            ? searchRegex!.Replace(prefix, options.ReplaceText)
            : prefix.Replace(options.SearchText, options.ReplaceText, StringComparison.Ordinal);
    }

    private static string ExpandTemplate(
        string template,
        string prefix,
        string extension,
        DateTime lastWrite,
        int index,
        RenameOptions options)
    {
        return TokenRegex().Replace(template, match =>
        {
            var token = match.Groups[1].Value;
            return token switch
            {
                "P" => prefix,
                "S" => extension,
                "T" => FormatTime(lastWrite, options.TimeFormat),
                "N" => (options.StartNumber + index).ToString(),
                "zN" => (options.StartNumber + index).ToString().PadLeft(options.PaddingWidth, '0'),
                _ when int.TryParse(token, out var start) => (start + index).ToString(),
                _ when token.StartsWith('z') && int.TryParse(token[1..], out var paddedStart)
                    => (paddedStart + index).ToString().PadLeft(Math.Max(2, token.Length - 1), '0'),
                _ => match.Value
            };
        });
    }

    private static string FormatTime(DateTime value, string format)
    {
        try
        {
            return value.ToString(format);
        }
        catch (FormatException)
        {
            throw new RenameValidationException("时间格式无效，请使用例如 yyyyMMdd_HHmmss 的格式。");
        }
    }

    [GeneratedRegex(@"\{(P|S|T|N|zN|\d+|z\d+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    internal static string? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "新名称不能为空。";
        if (name is "." or "..") return "新名称不能是 . 或 ..。";
        if (name.EndsWith(' ') || name.EndsWith('.')) return "Windows 名称不能以空格或句点结尾。";
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "新名称包含 Windows 不允许的字符。";

        var stem = name.Split('.')[0];
        if (ReservedDeviceNames.Contains(stem)) return $"{stem} 是 Windows 保留名称。";
        return null;
    }
}

public static class RenameValidator
{
    public static bool Validate(IReadOnlyList<RenamePlanItem> items)
    {
        foreach (var item in items) item.Error = string.Empty;

        foreach (var item in items)
        {
            item.Error = RenamePlanner.ValidateName(item.NewName) ?? string.Empty;
        }

        foreach (var group in items.GroupBy(item => item.TargetPath, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() <= 1) continue;
            foreach (var item in group) item.Error = "多个项目将得到相同名称。";
        }

        var selectedSources = items.Select(item => item.SourcePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items.Where(item => string.IsNullOrEmpty(item.Error) && item.HasChange))
        {
            if ((File.Exists(item.TargetPath) || Directory.Exists(item.TargetPath)) && !selectedSources.Contains(item.TargetPath))
                item.Error = "目标名称已被未选中的文件或文件夹占用。";
        }

        return items.All(item => string.IsNullOrEmpty(item.Error));
    }
}
