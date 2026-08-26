using System;
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

        // Detecting programmatic selections (e.g. SomethingNeedDoing's /callback command) used to
        // hook the shared native AtkUnitBase::FireCallback directly, but that was found to corrupt
        // unrelated dialogs' closing behavior (any hook on that function, regardless of what it
        // does, disrupted cascading FireCallback calls the client makes internally - e.g. closing a
        // ContextMenu after a selection - causing SelectYesno/InputNumeric-style dialogs to close on
        // the player's next click). SomethingNeedDoing now broadcasts a Dalamud IPC event instead
        // whenever it fires a programmatic callback, so we can detect the same selections without
        // installing any native hook at all.
        InitSndCallbackIpc(pluginInterface);
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
