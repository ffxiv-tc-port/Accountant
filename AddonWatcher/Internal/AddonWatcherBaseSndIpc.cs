using System;
using AddonWatcher.Structs;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace AddonWatcher.Internal;

// Replacement for the old FireCallback hook: SomethingNeedDoing broadcasts this IPC event whenever
// its /callback command fires a programmatic FireCallback, so we can detect the same
// SelectString/SelectYesno selections without installing any hook on the shared native
// AtkUnitBase::FireCallback function ourselves. If SND isn't installed, Subscribe() below is a
// harmless no-op - there's simply nothing broadcasting to this IPC name.
internal partial class AddonWatcherBase
{
    private const string SndCallbackFiredIpcName = "SomethingNeedDoing.CallbackFired";

    private ICallGateSubscriber<string, int, bool, object?>? _sndCallbackFiredSubscriber;

    // (addon, index, timestamp) of the last selection reported through either the
    // ReceiveEvent-based hooks or this IPC path, so the second of the pair to fire for the same
    // real selection is skipped instead of double-invoking subscribers.
    private (nint Addon, int Index, DateTime Time)? _lastReportedSelection;

    private bool ShouldReport(nint addon, int index)
    {
        var now = DateTime.UtcNow;
        if (_lastReportedSelection is { } last
            && last.Addon == addon
            && last.Index == index
            && (now - last.Time).TotalMilliseconds < 300)
            return false;

        _lastReportedSelection = (addon, index, now);
        return true;
    }

    private void InitSndCallbackIpc(IDalamudPluginInterface pluginInterface)
    {
        try
        {
            _sndCallbackFiredSubscriber =
                pluginInterface.GetIpcSubscriber<string, int, bool, object?>(SndCallbackFiredIpcName);
            _sndCallbackFiredSubscriber.Subscribe(OnSndCallbackFired);
        }
        catch
        {
            _sndCallbackFiredSubscriber = null;
        }
    }

    private void DisposeSndCallbackIpc()
    {
        try
        {
            _sndCallbackFiredSubscriber?.Unsubscribe(OnSndCallbackFired);
        }
        catch
        {
            // ignored - subscriber's owning plugin may already be gone
        }
    }

    private unsafe void OnSndCallbackFired(string addonName, int index, bool updateState)
    {
        var addon = _gui.GetAddonByName(addonName, 1);
        if (addon == nint.Zero || !ShouldReport(addon, index))
            return;

        if (addonName == "SelectString")
        {
            var info = (SelectStringInfo)addon;
            if (index >= 0 && index < info.Count)
            {
                var itemText        = info.ItemText(index);
                var descriptionText = info.Description;
                _log.Verbose("[SND IPC] String {ButtonText} ({Which}) selected on 0x{SelectStringPtr:X} with description {Description}.",
                    itemText, index, (ulong)addon, descriptionText);
                StringSelected!.Invoke(addon, index, itemText, descriptionText);
            }
        }
        else if (addonName == "SelectYesno" && (index == SelectYesNoInfo.YesButtonId || index == SelectYesNoInfo.NoButtonId))
        {
            var info            = (SelectYesNoInfo)addon;
            var yesOrNo         = index == SelectYesNoInfo.YesButtonId;
            var buttonText      = yesOrNo ? info.YesText : info.NoText;
            var descriptionText = info.Description;
            _log.Verbose("[SND IPC] {YesOrNo}-Button {ButtonText} selected on 0x{SelectYesnoPtr:X} with description {Description}.",
                yesOrNo ? "Yes" : "No", buttonText, (ulong)addon, descriptionText);
            YesnoSelected!.Invoke(addon, yesOrNo, buttonText, descriptionText);
        }
    }
}
