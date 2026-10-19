using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace Accountant.Internal;

// 公會名稱／會長讀 CS 的 InfoProxyFreeCompany，每次讀都從 InfoModule 重查、不留指標；只在框架執行緒（含 hook detour）呼叫。
internal class FreeCompanyTracker
{
    // 名稱空白時判「還沒讀到」或「沒有公會」用的 8 位元組；CS 沒有這一格。
    private const int StateOffset = 0x48;

    // 取 InfoProxyFreeCompany 這條路上 CS 要解的位址都在：缺一個就呼叫會擲例外（在 detour 裡＝遊戲結束），所以先判。
    internal static unsafe bool Available
        => Framework.StaticAddressPointers.ppInstance != null
         && Framework.MemberFunctionPointers.GetUIModule != null
         && InfoModule.MemberFunctionPointers.GetInfoProxyById != null;

    private readonly IClientState _state;
    private readonly IObjectTable _objects;
    private readonly IFramework   _framework;
    private          string?      _freeCompanyName;
    private          string?      _freeCompanyLeader;
    private          string       _characterName  = string.Empty;
    private          string       _freeCompanyTag = string.Empty;
    private          uint         _serverId;

    private readonly ParsedText _nameText     = new();
    private readonly ParsedText _tagText      = new();
    private readonly ParsedText _fcNameText   = new();
    private readonly ParsedText _fcLeaderText = new();

    private static unsafe InfoProxyFreeCompany* Proxy()
        => Available ? InfoProxyFreeCompany.Instance() : null;

    private static unsafe string? GetFcInfo(InfoProxyFreeCompany* fc, ReadOnlySpan<byte> text, ParsedText cache)
    {
        if (fc == null)
            return null;

        if (text[0] != 0)
            return cache.Get(text);
        if (*(ulong*)((byte*)fc + StateOffset) != 0)
            return null;

        return string.Empty;
    }

    private unsafe string? GetFcName()
    {
        var fc = Proxy();
        return GetFcInfo(fc, fc == null ? default : fc->Name, _fcNameText);
    }

    private unsafe string? GetFcLeader()
    {
        var fc = Proxy();
        return GetFcInfo(fc, fc == null ? default : fc->Master, _fcLeaderText);
    }

    private unsafe void Update()
    {
        if (_state.IsLoggedIn && _objects.LocalPlayer is { Address: not 0 } localPlayer)
        {
            // 與 localPlayer.Name／CompanyTag／HomeWorld 讀的是同一個結構的同三個欄位；那三個屬性每讀一次就重新解析一次。
            var chara     = (Character*)localPlayer.Address;
            var newName   = _nameText.Get(((GameObject*)chara)->Name);
            var newTag    = _tagText.Get(chara->FreeCompanyTag);
            var newServer = (uint)chara->HomeWorld;
            if (_characterName != newName || _freeCompanyTag != newTag || newServer != _serverId)
            {
                _freeCompanyName   = null;
                _freeCompanyLeader = null;
            }

            _characterName  = newName;
            _freeCompanyTag = newTag;
            _serverId       = newServer;

            var newCompany = GetFcName();
            var newLeader  = GetFcLeader();
            if (newCompany != null && (_freeCompanyName == null || newCompany.Length > 0))
                _freeCompanyName = newCompany;

            if (newLeader != null && (_freeCompanyLeader == null || newLeader.Length > 0))
                _freeCompanyLeader = newLeader;
        }
        else
        {
            _characterName     = string.Empty;
            _freeCompanyTag    = string.Empty;
            _serverId          = 0;
            _freeCompanyLeader = null;
            _freeCompanyName   = null;
        }
    }

    public FreeCompanyTracker(IPluginLog log, IClientState state, IObjectTable objects, IFramework framework)
    {
        _state     = state;
        _objects   = objects;
        _framework = framework;
        if (!Available)
            log.Error("Could not obtain free company data: InfoProxyFreeCompany is not resolvable.");

        _state.Login  += LoginHandler;
        _state.Logout += LogoutHandler;
    }

    private void LoginHandler()
        => _framework.Update += UpdateAndRemove;

    private void LogoutHandler(int _1, int _2)
    {
        Update();
    }

    private void UpdateAndRemove(IFramework _)
    {
        if (_objects.LocalPlayer == null)
            return;

        Update();
        _framework.Update -= UpdateAndRemove;
    }

    public void Dispose()
    {
        _state.Login      -= LoginHandler;
        _state.Logout     -= LogoutHandler;
        _framework.Update -= UpdateAndRemove;
    }

    public (string Tag, string? Name, string? Leader) FreeCompanyInfo
    {
        get
        {
            Update();
            return (_freeCompanyTag, _freeCompanyName, _freeCompanyLeader);
        }
    }
}
