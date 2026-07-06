using System;
using System.Collections.Generic;
using Dalamud.Plugin.Ipc;

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
}
