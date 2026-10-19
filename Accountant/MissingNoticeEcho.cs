using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;

namespace Accountant;

// 載入時有項目因遊戲位址對不上而停用時，登入後在聊天視窗說一次（每次載入只說一次）；只看 log 容易沒注意到。
public sealed class MissingNoticeEcho : IDisposable
{
    private readonly string[] _missing;
    private          bool     _subscribed;

    public MissingNoticeEcho(IEnumerable<string> missing)
    {
        _missing = [.. missing];
        if (_missing.Length == 0)
            return;

        Dalamud.Framework.Update += OnUpdate;
        _subscribed              =  true;
    }

    private void OnUpdate(IFramework _)
    {
        if (!Dalamud.ClientState.IsLoggedIn || !Dalamud.PlayerState.IsLoaded)
            return;

        Dispose();
        Dalamud.Chat.PrintError($"[Accountant] 遊戲改版後有遊戲位址對不上，下列項目暫時停用（請等插件更新）：{string.Join("；", _missing)}");
    }

    public void Dispose()
    {
        if (!_subscribed)
            return;

        Dalamud.Framework.Update -= OnUpdate;
        _subscribed              =  false;
    }
}
