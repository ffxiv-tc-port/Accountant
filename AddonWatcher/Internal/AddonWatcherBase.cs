using System;
using System.Collections.Generic;
using AddonWatcher.SeFunctions;
using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace AddonWatcher.Internal;

internal partial class AddonWatcherBase : IDisposable
{
    private readonly IGameGui   _gui;
    private readonly IPluginLog _log;

    internal SelectStringReceiveEvent       SelectStringReceiveEvent;
    internal SelectYesnoReceiveEvent        SelectYesnoReceiveEvent;
    internal SelectStringOnSetup            SelectStringOnSetup;
    internal SelectYesnoOnSetup             SelectYesnoOnSetup;
    internal JournalResultOnSetup           JournalResultOnSetup;
    internal LotteryWeeklyRewardListOnSetup LotteryWeeklyRewardListOnSetup;
    internal TalkOnUpdate                   TalkOnUpdate;

    internal Hook<OnAddonReceiveEventDelegate>?  SelectYesNoHook;
    internal Hook<OnAddonReceiveEventDelegate>?  SelectStringHook;
    internal Hook<OnAddonSetupDelegate>?        SelectYesnoSetupHook;
    internal Hook<OnAddonSetupDelegate>?        SelectStringSetupHook;
    internal Hook<OnAddonSetupDelegate>?        JournalResultSetupHook;
    internal Hook<OnAddonSetupDelegate>?        LotteryWeeklyRewardListSetupHook;
    internal Hook<OnAddonUpdateDelegate>?       TalkUpdateHook;

    private event ReceiveSelectYesnoDelegate?           YesnoSelected;
    private event ReceiveSelectStringDelegate?          StringSelected;
    private event SelectStringSetupDelegate?            SelectStringSetup;
    private event SelectYesnoSetupDelegate?             SelectYesnoSetup;
    private event JournalResultSetupDelegate?           JournalResultSetup;
    private event LotteryWeeklyRewardListSetupDelegate? LotteryWeeklyRewardListSetup;
    private event TalkUpdateDelegate?                   TalkUpdated;

    public AddonWatcherBase(IPluginLog log, IGameGui gui, ISigScanner sigScanner, IGameInteropProvider provider,
        IDalamudPluginInterface pluginInterface)
    {
        _log = log;
        _gui = gui;

        SelectStringReceiveEvent       = new SelectStringReceiveEvent(_log, sigScanner);
        SelectYesnoReceiveEvent        = new SelectYesnoReceiveEvent(_log, sigScanner);
        SelectStringOnSetup            = new SelectStringOnSetup(_log, sigScanner);
        SelectYesnoOnSetup             = new SelectYesnoOnSetup(_log, sigScanner);
        JournalResultOnSetup           = new JournalResultOnSetup(_log, sigScanner);
        LotteryWeeklyRewardListOnSetup = new LotteryWeeklyRewardListOnSetup(_log, sigScanner);
        TalkOnUpdate                   = new TalkOnUpdate(_log, sigScanner);

        SelectYesNoHook                  = SelectYesnoReceiveEvent.CreateHook(provider, SelectYesNoEventDetour, false);
        SelectStringHook                 = SelectStringReceiveEvent.CreateHook(provider, SelectStringEventDetour, false);
        SelectYesnoSetupHook             = SelectYesnoOnSetup.CreateHook(provider, SelectYesnoOnSetupDetour, false);
        SelectStringSetupHook            = SelectStringOnSetup.CreateHook(provider, SelectStringOnSetupDetour, false);
        JournalResultSetupHook           = JournalResultOnSetup.CreateHook(provider, JournalResultOnSetupDetour, false);
        LotteryWeeklyRewardListSetupHook = LotteryWeeklyRewardListOnSetup.CreateHook(provider, LotteryWeeklyRewardListOnSetupDetour, false);
        TalkUpdateHook                   = TalkOnUpdate.CreateHook(provider, TalkUpdateDetour, false);

        // 另外三個（SelectYesnoSetup／SelectStringSetup／JournalResultSetup）目前沒有訂閱者，缺了不影響功能，不列。
        NoteMissing(SelectYesNoHook,                  "確認框的選擇（SelectYesno）：作物、仙人微彩、仙人仙彩、乙太轉輪的計時器不更新");
        NoteMissing(SelectStringHook,                 "選單的選擇（SelectString）：作物計時器不更新");
        NoteMissing(TalkUpdateHook,                   "對話內容（Talk）：作物計時器不更新");
        NoteMissing(LotteryWeeklyRewardListSetupHook, "仙人仙彩開獎視窗（LotteryWeeklyRewardList）：仙人仙彩計時器不更新");

        // Programmatic selections (e.g. SomethingNeedDoing's /callback command) are detected through a Dalamud
        // IPC event SND broadcasts whenever it fires a programmatic callback - deliberately not by hooking the
        // shared native AtkUnitBase::FireCallback: any hook on that function disrupted the cascading FireCallback
        // calls the client makes internally (e.g. closing a ContextMenu after a selection), causing
        // SelectYesno/InputNumeric-style dialogs to close on the player's next click.
        InitSndCallbackIpc(pluginInterface);
    }

    /// <summary>特徵碼對不上而沒建立的 hook（說明文字）。只在建構時寫入。</summary>
    internal readonly List<string> MissingHooks = [];

    private void NoteMissing(IDalamudHook? hook, string what)
    {
        if (hook != null)
            return;

        MissingHooks.Add(what);
        _log.Warning($"遊戲改版後對不上：{what}；其餘功能照常。");
    }

    public void Dispose()
    {
        SelectYesNoHook?.Dispose();
        SelectStringHook?.Dispose();
        SelectYesnoSetupHook?.Dispose();
        SelectStringSetupHook?.Dispose();
        JournalResultSetupHook?.Dispose();
        LotteryWeeklyRewardListSetupHook?.Dispose();
        TalkUpdateHook?.Dispose();
        DisposeSndCallbackIpc();
    }
}
