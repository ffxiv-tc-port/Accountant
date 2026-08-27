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
            if (!CharacterCompanies.TryGetValue(player.CastedName, out var known) || known != company.Value.CastedName)
            {
                CharacterCompanies[player.CastedName] = company.Value.CastedName;
                Save();
            }
        }

        return company;
    }

    public FreeCompanyInfo? GetCompanyForCharacter(PlayerInfo player)
        => CharacterCompanies.TryGetValue(player.CastedName, out var companyKey)
            ? Infos.FirstOrDefault(i => i.CastedName == companyKey)
            : null;

    private static FileInfo FileInfo
        => new(Path.Combine(Dalamud.PluginInterface.GetPluginConfigDirectory(), FileName));

    public static DateTime GetWriteTime()
        => FileInfo.LastWriteTimeUtc;

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
        {
            var infos = Infos.Where(i => i.ServerId == serverId);
            return l.Length > 0
                ? infos.Cast<FreeCompanyInfo?>().FirstOrDefault(i => i!.Value.Tag == tag && i.Value.Leader == l)
                : infos.Cast<FreeCompanyInfo?>().FirstOrDefault(i => i!.Value.Tag == tag);
        }

        var idx = Infos.FindIndex(i => i.Name == n && i.ServerId == serverId);
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

    public void Reload()
    {
        var info = Load();
        Infos.Clear();
        Infos.AddRange(info.Infos);
    }

    private bool _reloadingAsync;

    // Same as Reload(), but for the caller on the framework thread that only needs to pick up
    // externally-made changes (ConfigSync's periodic mtime check, e.g. a sibling multiboxed
    // instance writing to the same shared config folder) rather than the initial startup load.
    // Load() already builds a brand-new, self-contained instance without touching this instance's
    // state, so it can run on a background thread as-is; only the swap into Infos happens back on
    // the framework thread so nothing enumerating Infos concurrently sees a torn state.
    public void ReloadAsync(Action? onReloaded = null)
    {
        if (_reloadingAsync)
            return;
        _reloadingAsync = true;

        Task.Run(() =>
        {
            var info = Load();
            Dalamud.Framework.RunOnFrameworkThread(() =>
            {
                Infos.Clear();
                Infos.AddRange(info.Infos);
                onReloaded?.Invoke();
                _reloadingAsync = false;
            });
        });
    }

    public static FreeCompanyStorage Load()
    {
        var file = FileInfo;
        if (file.Exists)
            try
            {
                var data    = File.ReadAllText(file.FullName);
                var storage = JsonConvert.DeserializeObject<FreeCompanyStorage>(data)!;
                if (storage.Infos.RemoveAll(f => f.ServerId == 0) > 0)
                    storage.Save();
                return storage;
            }
            catch (Exception e)
            {
                Dalamud.Log.Error($"Could not read free company storage data:\n{e}");
            }

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
