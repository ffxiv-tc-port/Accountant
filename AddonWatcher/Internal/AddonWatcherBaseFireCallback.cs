using System;
using AddonWatcher.Structs;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AddonWatcher.Internal;

public unsafe delegate void OnAddonFireCallbackDelegate(AtkUnitBase* thisPtr, uint valueCount, AtkValue* values, bool close);

// Programmatic selections (e.g. plugins calling AtkUnitBase::FireCallback directly, such as
// SomethingNeedDoing's /callback command) never go through the ReceiveEvent/ListIndexChange
// path that SelectStringEventDetour/SelectYesNoEventDetour hook, so they were previously
// invisible to Accountant entirely. FireCallback is the single authoritative point where a
// selection is reported to the addon's owner regardless of how it was made, so we hook it too
// and feed the same StringSelected/YesnoSelected events - guarded by a short dedup window
// since a real manual click still ends up calling FireCallback as well, and we don't want to
// double-count a single manual selection through both hooks.
internal partial class AddonWatcherBase
{
    // (addon, index, timestamp) of the last selection reported through either the
    // ReceiveEvent-based hooks or this FireCallback hook, so the second of the pair to fire
    // for the same real selection is skipped instead of double-invoking subscribers.
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

    private unsafe void FireCallbackDetour(AtkUnitBase* thisPtr, uint valueCount, AtkValue* values, bool close)
    {
        if (valueCount > 0 && values[0].Type == FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int)
        {
            var index = values[0].Int;
            var name  = thisPtr->NameString;

            if (name == "SelectString" && ShouldReport((nint)thisPtr, index))
            {
                var info         = (SelectStringInfo)(IntPtr)thisPtr;
                if (index >= 0 && index < info.Count)
                {
                    var itemText        = info.ItemText(index);
                    var descriptionText = info.Description;
                    _log.Verbose("[FireCallback] String {ButtonText} ({Which}) selected on 0x{SelectStringPtr:X} with description {Description}.",
                        itemText, index, (ulong)thisPtr, descriptionText);
                    StringSelected!.Invoke((IntPtr)thisPtr, index, itemText, descriptionText);
                }
            }
            else if (name == "SelectYesno" && (index == SelectYesNoInfo.YesButtonId || index == SelectYesNoInfo.NoButtonId)
                     && ShouldReport((nint)thisPtr, index))
            {
                var info             = (SelectYesNoInfo)(IntPtr)thisPtr;
                var yesOrNo          = index == SelectYesNoInfo.YesButtonId;
                var buttonText       = yesOrNo ? info.YesText : info.NoText;
                var descriptionText  = info.Description;
                _log.Verbose("[FireCallback] {YesOrNo}-Button {ButtonText} selected on 0x{SelectYesnoPtr:X} with description {Description}.",
                    yesOrNo ? "Yes" : "No", buttonText, (ulong)thisPtr, descriptionText);
                YesnoSelected!.Invoke((IntPtr)thisPtr, yesOrNo, buttonText, descriptionText);
            }
        }

        FireCallbackHook!.Original(thisPtr, valueCount, values, close);
    }
}
