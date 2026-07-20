using System.Runtime.InteropServices;

namespace BatchRename.Core;

public sealed class NaturalNameComparer : IComparer<string>
{
    public static NaturalNameComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        if (OperatingSystem.IsWindows())
        {
            return StrCmpLogicalW(x, y);
        }

        return StringComparer.CurrentCultureIgnoreCase.Compare(x, y);
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string psz1, string psz2);
}
