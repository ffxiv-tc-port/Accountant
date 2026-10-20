using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Accountant.Classes;
using Dalamud.Logging;
using Newtonsoft.Json;

namespace Accountant.Util;

public class FreeCompanyStorage
{
    public const string FileName = "free_company_data.json";

    public readonly List<FreeCompanyInfo> Infos = [];

    // Keyed and valued by PlayerInfo.CastedName / FreeCompanyInfo.CastedName (plain strings)
    // rather than the structs themselves, since Newtonsoft cannot round-trip non-string dictionary keys.
    public readonly Dictionary<string, string> CharacterCompanies = [];

    [JsonIgnore]
    public DateTime LastChangeTime { get; private set; } = DateTime.UtcNow.AddMilliseconds(500);

    public FreeCompanyInfo? GetCurrentCompanyInfo()
    {
        if (!Accountant.GameData.Valid)
            return null;

        if (!Dalamud.PlayerState.IsLoaded)
            return null;

        var (tag, name, leader) = Accountant.GameData.FreeCompanyInfo();
        var id      = (ushort)Dalamud.PlayerState.HomeWorld.RowId;
        var company = FindByAndUpdateInfo(tag, name, leader, id);
        if (company.HasValue)
        {
            var player = new PlayerInfo(Dalamud.PlayerState.CharacterName, id);
            if (!IsKnownCompany(player, company.Value))
            {
                CharacterCompanies[player.CastedName] = company.Value.CastedName;
                Save();
            }
        }

        return company;
    }

    // 計時視窗每幀都走到這裡。等同 CharacterCompanies.TryGetValue(player.CastedName, out var known)
    // && known == company.CastedName（CastedName＝名字接上 (char)ServerId），但不組這兩個字串。
    private bool IsKnownCompany(PlayerInfo player, FreeCompanyInfo company)
    {
        if (!CharacterCompanies.TryGetAlternateLookup<ReadOnlySpan<char>>(out var lookup))
            return CharacterCompanies.TryGetValue(player.CastedName, out var k) && k == company.CastedName;

        var name = player.Name.AsSpan();
        Span<char> key = name.Length < 256 ? stackalloc char[name.Length + 1] : new char[name.Length + 1];
        name.CopyTo(key);
        key[^1] = (char)player.ServerId;
        if (!lookup.TryGetValue(key, out var known) || known == null)
            return false;

        var companyName = company.Name.AsSpan();
        return known.Length == companyName.Length + 1
         && known.AsSpan(0, companyName.Length).SequenceEqual(companyName)
         && known[^1] == (char)company.ServerId;
    }

    public FreeCompanyInfo? GetCompanyForCharacter(PlayerInfo player)
        => CharacterCompanies.TryGetValue(player.CastedName, out var companyKey)
            ? Infos.FirstOrDefault(i => i.CastedName == companyKey)
            : null;

    private static FileInfo FileInfo
        => new(Path.Combine(Dalamud.PluginInterface.GetPluginConfigDirectory(), FileName));

    public static DateTime GetWriteTime()
        => FileInfo.LastWriteTimeUtc;

    private static string? _directoryForCheck;
    private static string? _fileForCheck;

    // 只給框架執行緒上的 ConfigSync 定期檢查用，結果與 GetWriteTime() 相同。設定目錄還在時 GetPluginConfigDirectory()
    // 回的一定是同一個字串，就沿用組好的路徑；目錄不在才照舊走它（會補建目錄），它失敗回空字串時不記住。
    // 刻意不沿用 FileInfo＋Refresh()：檔案被刪之後 Refresh 不清掉舊屬性，會回上一次的時間。
    public static DateTime GetWriteTimeForCheck()
    {
        var file = _fileForCheck;
        if (file == null || !Directory.Exists(_directoryForCheck))
        {
            var directory = Dalamud.PluginInterface.GetPluginConfigDirectory();
            file = Path.Combine(directory, FileName);
            if (directory.Length > 0)
            {
                _directoryForCheck = directory;
                _fileForCheck      = file;
            }
        }

        return File.GetLastWriteTimeUtc(file);
    }

    public FreeCompanyInfo? FindByLeader(string leader, uint serverId)
        => Infos.FirstOrDefault(f => f.Leader == leader && f.ServerId == serverId);

    public FreeCompanyInfo? FindByTag(string tag, uint serverId)
        => Infos.FirstOrDefault(f => f.Tag == tag && f.ServerId == serverId);

    public FreeCompanyInfo? FindByName(string name, uint serverId)
        => Infos.FirstOrDefault(f => f.Name == name && f.ServerId == serverId);

    public FreeCompanyInfo? FindByAndUpdateInfo(string tag, string? name, string? leader, ushort serverId)
    {
        if (tag.Length == 0)
            return null;

        var l = leader ?? string.Empty;
        var n = name ?? string.Empty;


        if (n.Length == 0)
            return FindByTagAndLeader(tag, l, serverId);

        // 等同 Infos.FindIndex(i => i.Name == n && i.ServerId == serverId)；lambda 會讓本方法一進來就配閉包。
        var idx = -1;
        for (var i = 0; i < Infos.Count; ++i)
        {
            if (Infos[i].Name == n && Infos[i].ServerId == serverId)
            {
                idx = i;
                break;
            }
        }
        if (idx == -1 && serverId != 0)
        {
            if (l.Length == 0)
                return null;

            Infos.Add(new FreeCompanyInfo(n, serverId)
            {
                Leader = l,
                Tag    = tag,
            });
            Save();
            return Infos.Last();
        }

        if (Infos[idx].Tag == tag && Infos[idx].Leader == l)
            return Infos[idx];

        Infos[idx] = new FreeCompanyInfo(n, serverId)
        {
            Leader = l,
            Tag    = tag,
        };
        Save();
        return Infos[idx];
    }

    // 公會名稱還沒讀到時，計時視窗每幀都走到這裡：同伺服器、標籤相同（有會長時會長也相同）的第一筆，沒有回 null。
    private FreeCompanyInfo? FindByTagAndLeader(string tag, string l, ushort serverId)
    {
        foreach (var info in Infos)
        {
            if (info.ServerId == serverId && info.Tag == tag && (l.Length == 0 || info.Leader == l))
                return info;
        }

        return null;
    }

    public void Reload()
    {
        try
        {
            var (info, writeTime) = LoadWithWriteTime();
            Apply(info, writeTime);
        }
        catch (Exception e)
        {
            Dalamud.Log.Warning(e, "Could not reload the free company data; keeping what is already loaded.");
        }
    }

    // 寫入時間在讀檔之前取：之後才寫進去的版本時間一定比它新，下一輪檢查會再重載一次，不會漏掉。
    // 檔案在卻讀不進來時擲例外：呼叫端保留記憶體裡原有的資料，時間戳不動，下一輪檢查再讀。
    private static (FreeCompanyStorage Info, DateTime WriteTime) LoadWithWriteTime()
    {
        var writeTime = GetWriteTime();
        return (Read() ?? CreateNew(), writeTime);
    }

    // 只在框架執行緒上呼叫。時間戳跟上讀到的那一版，同一版不會每一輪檢查都再重載。
    private void Apply(FreeCompanyStorage info, DateTime writeTime)
    {
        Infos.Clear();
        Infos.AddRange(info.Infos);
        CharacterCompanies.Clear();
        foreach (var (player, company) in info.CharacterCompanies)
            CharacterCompanies[player] = company;
        LastChangeTime = writeTime;
    }

    private readonly ReloadGate _reloadGate = new("the free company data");

    // Same as Reload(), but for the caller on the framework thread that only needs to pick up externally-made
    // changes (ConfigSync's periodic mtime check). Load() already builds a brand-new, self-contained instance
    // without touching this instance's state, so it can run on a background thread as-is; only the swap into
    // Infos happens back on the framework thread so nothing enumerating Infos concurrently sees a torn state.
    public void ReloadAsync(Action? onReloaded = null)
        => _reloadGate.Run(LoadWithWriteTime, loaded =>
        {
            Apply(loaded.Info, loaded.WriteTime);
            onReloaded?.Invoke();
        });

    // 啟動時用。檔案在卻讀不進來時回一份空的、刻意不存檔；時間戳設成最小值，定期檢查會再讀一次。
    public static FreeCompanyStorage Load()
    {
        try
        {
            return Read() ?? CreateNew();
        }
        catch (Exception e)
        {
            Dalamud.Log.Error($"Could not read free company storage data:\n{e}");
            return new FreeCompanyStorage { LastChangeTime = DateTime.MinValue };
        }
    }

    // 回 null＝檔案不存在。讀檔或解析失敗（含空檔）時擲例外、刻意不存檔：存了就是拿空資料蓋掉原檔。
    private static FreeCompanyStorage? Read()
    {
        var file = FileInfo;
        if (!file.Exists)
            return null;

        var data    = File.ReadAllText(file.FullName);
        var storage = JsonConvert.DeserializeObject<FreeCompanyStorage>(data)
         ?? throw new InvalidDataException($"{FileName} has no content.");
        if (storage.Infos.RemoveAll(f => f.ServerId == 0) > 0)
            storage.Save();
        return storage;
    }

    private static FreeCompanyStorage CreateNew()
    {
        var newStorage = new FreeCompanyStorage();
        newStorage.Save();
        return newStorage;
    }

    public void Save()
    {
        try
        {
            var file = FileInfo;
            var data = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(file.FullName, data);
            LastChangeTime = DateTime.UtcNow.AddMilliseconds(500);
        }
        catch (Exception e)
        {
            Dalamud.Log.Error($"Could not save free company storage data:\n{e}");
        }
    }
}
