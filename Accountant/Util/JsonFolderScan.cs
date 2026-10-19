using System;
using System.IO;
using System.IO.Enumeration;

namespace Accountant.Util;

// 結果與 new DirectoryInfo(dir).EnumerateFiles("*.json").Any(f => f.LastWriteTimeUtc > threshold) 相同，
// 但不為每個檔配 FileInfo 與路徑字串。選項照 EnumerationOptions.Compatible：不略過隱藏／系統檔、存取錯誤照樣擲。
// 唯一差別：時間戳落在 DateTime 上限前、本機時區位移以內（9999 年最後幾小時）時舊寫法擲 ArgumentException，這裡照常比較。
internal static class JsonFolderScan
{
    private static readonly EnumerationOptions Options = new()
    {
        AttributesToSkip   = 0,
        IgnoreInaccessible = false,
    };

    public static bool AnyJsonWrittenAfter(string directory, DateTime threshold)
    {
        using var enumerator = new NewerJsonEnumerator(directory, threshold);
        return enumerator.MoveNext();
    }

    private sealed class NewerJsonEnumerator(string directory, DateTime threshold)
        : FileSystemEnumerator<bool>(directory, Options)
    {
        protected override bool ShouldIncludeEntry(ref FileSystemEntry entry)
            => !entry.IsDirectory
             && FileSystemName.MatchesWin32Expression("*.json", entry.FileName, ignoreCase: true)
             && entry.LastWriteTimeUtc.UtcDateTime > threshold;

        protected override bool TransformEntry(ref FileSystemEntry entry)
            => true;
    }
}
