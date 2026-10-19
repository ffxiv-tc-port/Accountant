using System;
using System.Collections.Generic;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using CharacterTimerList = System.Collections.Generic.List<(ulong CID, string Name, string World, string WorldOverride,
    (string Name, ulong RetainerID, bool HasVenture, uint VentureID, long VentureEndsAt)[] Retainers,
    (string Name, uint ReturnTime)[] Airships,
    (string Name, uint ReturnTime)[] Submarines)>;

namespace Accountant.ExternalIpc;

// Minimal mirror of AutoRetainerAPI's data contracts so we can read its
// offline retainer data via Dalamud IPC without taking a hard dependency
// (and the ECommons version conflicts that come with it).
public sealed class AutoRetainerOfflineRetainerData
{
    public string Name          = string.Empty;
    public long   VentureEndsAt;
    public bool   HasVenture;
    public uint   VentureID;
    public ulong  RetainerID;
}

public sealed class AutoRetainerOfflineVesselData
{
    public string Name = string.Empty;
    public uint   ReturnTime;
}

public sealed class AutoRetainerOfflineCharacterData
{
    public ulong                                 CID;
    public string                                Name          = string.Empty;
    public string                                World         = string.Empty;
    public string?                               WorldOverride;
    public List<AutoRetainerOfflineRetainerData> RetainerData     = [];
    public List<AutoRetainerOfflineVesselData>   OfflineAirshipData  = [];
    public List<AutoRetainerOfflineVesselData>   OfflineSubmarineData = [];

    public string CurrentWorld
        => WorldOverride ?? World;
}

public static class AutoRetainerIpc
{
    private static ICallGateSubscriber<object>?                                 _init;
    private static ICallGateSubscriber<ulong, AutoRetainerOfflineCharacterData>? _getOfflineCharacterData;
    private static ICallGateSubscriber<List<ulong>>?                            _getRegisteredCharacters;
    private static ICallGateSubscriber<CharacterTimerList>?                     _getCharacterTimers;
    private static string?                                                      _characterTimersFallback;
    private static bool                                                         _characterTimersReported;

    public static bool IsReady()
    {
        try
        {
            _init ??= Dalamud.PluginInterface.GetIpcSubscriber<object>("AutoRetainer.Init");
            _init.InvokeAction();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static AutoRetainerOfflineCharacterData? GetOfflineCharacterData(ulong cid)
    {
        try
        {
            _getOfflineCharacterData ??=
                Dalamud.PluginInterface.GetIpcSubscriber<ulong, AutoRetainerOfflineCharacterData>("AutoRetainer.GetOfflineCharacterData");
            return _getOfflineCharacterData.InvokeFunc(cid);
        }
        catch
        {
            return null;
        }
    }

    public static IReadOnlyList<ulong> GetRegisteredCharacters()
    {
        try
        {
            _getRegisteredCharacters ??= Dalamud.PluginInterface.GetIpcSubscriber<List<ulong>>("AutoRetainer.GetRegisteredCIDs");
            return _getRegisteredCharacters.InvokeFunc();
        }
        catch
        {
            return [];
        }
    }

    /// <summary> Every character AutoRetainer has registered, in its order. </summary>
    /// <remarks>
    /// Uses <c>AutoRetainer.GetCharacterTimersV1</c> when AutoRetainer provides it: its result is made of runtime types only,
    /// so the call gate hands it over without the JSON round trip <c>GetOfflineCharacterData</c> costs per character.
    /// Any failure there falls back to <c>GetRegisteredCIDs</c> plus one <c>GetOfflineCharacterData</c> per character, as before.
    /// </remarks>
    public static List<AutoRetainerOfflineCharacterData> GetRegisteredCharacterData()
        => TryGetCharacterTimers(out var data) ? data : GetRegisteredCharacterDataLegacy();

    internal static List<AutoRetainerOfflineCharacterData> GetRegisteredCharacterDataLegacy()
    {
        var result = new List<AutoRetainerOfflineCharacterData>();
        foreach (var cid in GetRegisteredCharacters())
        {
            var data = GetOfflineCharacterData(cid);
            if (data != null)
                result.Add(data);
        }

        return result;
    }

    /// <remarks> A null answer means AutoRetainer's main loop did not answer in time; that round is skipped like an empty roster. </remarks>
    internal static bool TryGetCharacterTimers(out List<AutoRetainerOfflineCharacterData> data)
    {
        string reason, kind;
        try
        {
            _getCharacterTimers ??= Dalamud.PluginInterface.GetIpcSubscriber<CharacterTimerList>("AutoRetainer.GetCharacterTimersV1");
            var timers = _getCharacterTimers.InvokeFunc();
            data = timers == null ? [] : FromCharacterTimers(timers);
            ReportCharacterTimersPath(null, null);
            return true;
        }
        catch (IpcNotReadyError)
        {
            kind   = "not ready";
            reason = "AutoRetainer does not provide it (an older version, or not loaded yet)";
        }
        catch (IpcTypeMismatchError ex)
        {
            kind   = "type mismatch";
            reason = $"its type does not match ({ex.Message})";
        }
        catch (Exception ex)
        {
            kind   = "failed";
            reason = $"it failed ({(ex.InnerException ?? ex).GetType().Name}: {(ex.InnerException ?? ex).Message})";
        }

        ReportCharacterTimersPath(reason, kind);
        data = [];
        return false;
    }

    /// <summary> The same values the JSON round trip of <c>GetOfflineCharacterData</c> gives, nulls included. </summary>
    internal static List<AutoRetainerOfflineCharacterData> FromCharacterTimers(CharacterTimerList timers)
    {
        var result = new List<AutoRetainerOfflineCharacterData>(timers.Count);
        foreach (var c in timers)
        {
            result.Add(new AutoRetainerOfflineCharacterData
            {
                CID                  = c.CID,
                Name                 = c.Name,
                World                = c.World,
                WorldOverride        = c.WorldOverride,
                RetainerData         = FromRetainers(c.Retainers)!,
                OfflineAirshipData   = FromVessels(c.Airships)!,
                OfflineSubmarineData = FromVessels(c.Submarines)!,
            });
        }

        return result;
    }

    private static List<AutoRetainerOfflineRetainerData>? FromRetainers((string Name, ulong RetainerID, bool HasVenture, uint VentureID, long VentureEndsAt)[]? src)
    {
        if (src == null)
            return null;

        var result = new List<AutoRetainerOfflineRetainerData>(src.Length);
        foreach (var r in src)
        {
            result.Add(new AutoRetainerOfflineRetainerData
            {
                Name          = r.Name,
                VentureEndsAt = r.VentureEndsAt,
                HasVenture    = r.HasVenture,
                VentureID     = r.VentureID,
                RetainerID    = r.RetainerID,
            });
        }

        return result;
    }

    private static List<AutoRetainerOfflineVesselData>? FromVessels((string Name, uint ReturnTime)[]? src)
    {
        if (src == null)
            return null;

        var result = new List<AutoRetainerOfflineVesselData>(src.Length);
        foreach (var v in src)
            result.Add(new AutoRetainerOfflineVesselData { Name = v.Name, ReturnTime = v.ReturnTime });
        return result;
    }

    // Logs only when the path changes, so a missing endpoint costs one line per session instead of one per minute.
    private static void ReportCharacterTimersPath(string? reason, string? kind)
    {
        if (_characterTimersReported && _characterTimersFallback == kind)
            return;

        _characterTimersReported = true;
        _characterTimersFallback = kind;
        if (reason == null)
            Dalamud.Log.Information("[AutoRetainer] Reading character data through AutoRetainer.GetCharacterTimersV1.");
        else
            Dalamud.Log.Information($"[AutoRetainer] Not using AutoRetainer.GetCharacterTimersV1 because {reason}; reading character data the old way (GetRegisteredCIDs and GetOfflineCharacterData).");
    }
}
