using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Accountant.Util;
using Dalamud.Logging;
using Newtonsoft.Json;

namespace Accountant.Timers;

public delegate void TimerChange();

public class TimersBase<TIdent, TInfo> : ITimers<TIdent, TInfo>
    where TIdent : struct, ITimerIdentifier
{
    protected readonly Dictionary<TIdent, TInfo> InternalData = new();

    public IReadOnlyDictionary<TIdent, TInfo> Data
        => InternalData;

    public event TimerChange? Changed;
    public DateTime           FileChangeTime { get; private set; } = DateTime.UtcNow.AddMilliseconds(500);

    internal void Invoke()
    {
        Dalamud.Log.Verbose("Change triggered in {TInfo:l}.", typeof(TInfo).Name);
        Changed?.Invoke();
    }

    public DirectoryInfo CreateFolder()
        => CreateFolder(FolderName);

    private static DirectoryInfo CreateFolder(string folderName)
    {
        var path = Path.Combine(Dalamud.PluginInterface.ConfigDirectory.FullName, folderName);
        return Directory.CreateDirectory(path);
    }

    private DirectoryInfo? _folderForCheck;

    // 只給框架執行緒上的 ConfigSync 定期檢查用。路徑＝插件設定目錄＋固定子資料夾名，插件存活期間不變；
    // 資料夾還在就沿用（Refresh 重讀屬性，判斷與 CreateDirectory 對既有資料夾的判斷相同），不在才照舊走 CreateFolder。
    // CreateFolder 擲例外時不快取，下次照樣重試。
    internal DirectoryInfo FolderForCheck()
    {
        var dir = _folderForCheck;
        if (dir != null)
        {
            dir.Refresh();
            if (dir.Exists)
                return dir;
        }

        return _folderForCheck = CreateFolder();
    }

    protected virtual string FolderName
        => throw new NotImplementedException();

    protected virtual string SaveError
        => throw new NotImplementedException();

    protected virtual string ParseError
        => throw new NotImplementedException();

    protected virtual string LoadError
        => throw new NotImplementedException();

    public void Save(TIdent ident, TInfo info)
    {
        try
        {
            var dir      = CreateFolder();
            var fileName = Path.Combine(dir.FullName, $"{ident.IdentifierHash():X8}.json");
            var data     = JsonConvert.SerializeObject((ident, info), Formatting.Indented);
            File.WriteAllText(fileName, data);
            FileChangeTime = DateTime.UtcNow.AddMilliseconds(500);
        }
        catch (Exception e)
        {
            Dalamud.Log.Error($"{SaveError}:\n{e}");
        }
    }

    public void Save(TIdent ident)
    {
        if (InternalData.TryGetValue(ident, out var info))
            Save(ident, info);
    }

    public void DeleteFile(TIdent ident)
    {
        try
        {
            var dir      = CreateFolder(FolderName);
            var fileName = Path.Combine(dir.FullName, $"{ident.IdentifierHash():X8}.json");
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
                FileChangeTime = DateTime.UtcNow.AddMilliseconds(500);
            }
        }
        catch (Exception e)
        {
            Dalamud.Log.Error($"Could not delete file:\n{e}");
        }
    }

    public bool Remove(TIdent ident)
    {
        if (!InternalData.Remove(ident))
            return false;

        DeleteFile(ident);
        return true;
    }

    public void Set(TIdent ident, TInfo info)
        => InternalData[ident] = info;

    public void Reload(bool condition = true)
    {
        if (!condition)
        {
            return;
        }

        InternalData.Clear();
        try
        {
            var folder = CreateFolder(FolderName);
            foreach (var file in folder.EnumerateFiles("*.json"))
            {
                try
                {
                    var data = File.ReadAllText(file.FullName);
                    var (ident, info)   = JsonConvert.DeserializeObject<(TIdent, TInfo)>(data);
                    if (ident.Valid())
                        InternalData[ident] = info;
                    else
                    {
                        Dalamud.Log.Error($"{ParseError}:\nIdentifier was not valid.");
                        file.Delete();
                    }
                }
                catch (Exception e)
                {
                    Dalamud.Log.Error($"{ParseError}:\n{e}");
                }
            }
        }
        catch (Exception e)
        {
            Dalamud.Log.Error($"{LoadError}:\n{e}");
        }
        Invoke();
        FileChangeTime = DateTime.UtcNow.AddMilliseconds(500);
    }

    private readonly ReloadGate _reloadGate = new($"the {typeof(TInfo).Name} timers");

    // Same as Reload(), but for callers on the framework thread that only need to pick up externally-made
    // changes (ConfigSync's periodic mtime check, e.g. a sibling multiboxed instance writing to the same
    // shared config folder). Cost scales with how many timers of this kind are saved, so the file I/O +
    // JSON parsing runs on a background thread; InternalData is mutated only on the framework thread
    // afterwards to avoid racing any UI code enumerating Data mid-reload.
    public void ReloadAsync()
    {
        _reloadGate.Run(() =>
        {
            var loaded = new Dictionary<TIdent, TInfo>();
            try
            {
                var folder = CreateFolder(FolderName);
                foreach (var file in folder.EnumerateFiles("*.json"))
                {
                    try
                    {
                        var data          = File.ReadAllText(file.FullName);
                        var (ident, info) = JsonConvert.DeserializeObject<(TIdent, TInfo)>(data);
                        if (ident.Valid())
                            loaded[ident] = info;
                        else
                        {
                            Dalamud.Log.Error($"{ParseError}:\nIdentifier was not valid.");
                            file.Delete();
                        }
                    }
                    catch (Exception e)
                    {
                        Dalamud.Log.Error($"{ParseError}:\n{e}");
                    }
                }
            }
            catch (Exception e)
            {
                Dalamud.Log.Error($"{LoadError}:\n{e}");
            }

            return loaded;
        }, loaded =>
        {
            InternalData.Clear();
            foreach (var (ident, info) in loaded)
                InternalData[ident] = info;

            Invoke();
            FileChangeTime = DateTime.UtcNow.AddMilliseconds(500);
        });
    }
}
