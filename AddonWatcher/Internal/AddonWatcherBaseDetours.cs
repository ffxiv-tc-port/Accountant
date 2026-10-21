using System;
using System.Linq;
using AddonWatcher.Enums;
using AddonWatcher.Structs;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using AtkValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace AddonWatcher.Internal;

internal partial class AddonWatcherBase
{
    // OnSetup 的第三個參數是 AtkValue 陣列：[0] 說明、[1] 是、[2] 否；不是字串型別或不在陣列裡就當空字串。
    private static unsafe SeString AtkValueText(IntPtr values, int count, int index)
    {
        if (values == IntPtr.Zero || index >= count)
            return SeString.Empty;

        var value = (AtkValue*)values + index;
        if (value->Type is not (AtkValueType.String or AtkValueType.ManagedString or AtkValueType.String8) || !value->String.HasValue)
            return SeString.Empty;

        return Dalamud.Memory.MemoryHelper.ReadSeStringNullTerminated((IntPtr)value->String.Value);
    }

    private void SelectYesnoOnSetupDetour(IntPtr unit, int _, IntPtr data)
    {
        SelectYesnoSetupHook!.OriginalDisposeSafe(unit, _, data);
        var description = AtkValueText(data, _, 0);
        var yes         = AtkValueText(data, _, 1);
        var no          = AtkValueText(data, _, 2);
        _log.Verbose("SelectYesno 0x{SelectYesnoPtr:X} setup with description {Description} and options {YesOption}/{NoOption}.",
            (ulong)unit, description, yes, no);
        SelectYesnoSetup?.Invoke(unit, description, yes, no);
    }

    private void SelectStringOnSetupDetour(IntPtr unit, int _, IntPtr data)
    {
        SelectStringSetupHook!.OriginalDisposeSafe(unit, _, data);
        var ptr         = (SelectStringInfo)unit;
        var description = ptr.Description;
        var options     = Enumerable.Range(0, ptr.Count).Select(i => ptr.ItemText(i)).ToArray();
        _log.Verbose("SelectString 0x{SelectStringPtr:X} setup with description {Description} and {OptionCount} options.", (ulong)unit,
            description, options.Length);
        SelectStringSetup?.Invoke(unit, description, options);
    }

    private void JournalResultOnSetupDetour(IntPtr unit, int _, IntPtr data)
    {
        JournalResultSetupHook!.OriginalDisposeSafe(unit, _, data);
        var ptr       = (JournalResultInfo)unit;
        var questName = ptr.QuestName;
        _log.Verbose("JournalResult 0x{JournalResultPtr:X} setup for quest {QuestName}.", (ulong)unit, questName);
        JournalResultSetup?.Invoke(unit, questName);
    }

    private void LotteryWeeklyRewardListOnSetupDetour(IntPtr unit, int _, IntPtr data)
    {
        LotteryWeeklyRewardListSetupHook!.OriginalDisposeSafe(unit, _, data);
        _log.Verbose("LotteryWeeklyRewardList 0x{LotteryWeeklyRewardListPtr:X} setup.", (ulong)unit);
        LotteryWeeklyRewardListSetup?.Invoke(unit);
    }

    private void SelectYesNoEventDetour(IntPtr atkUnit, EventType eventType, int which, IntPtr source, IntPtr data)
    {
        // Captures text up front but defers logging/event dispatch until after Original() -
        // the addon's own event handling must return promptly.
        Action? deferred = null;

        if (eventType == EventType.Change)
        {
            var ptr             = (SelectYesNoInfo)atkUnit;
            var descriptionText = ptr.Description;
            switch (which)
            {
                case SelectYesNoInfo.YesButtonId when ShouldReport(atkUnit, which):
                    var yesText = ptr.YesText;
                    deferred = () =>
                    {
                        _log.Verbose("Yes-Button {ButtonText} selected on 0x{SelectYesnoPtr:X} with description {Description}.", yesText,
                            (ulong)atkUnit, descriptionText);
                        YesnoSelected!.Invoke(atkUnit, true, yesText, descriptionText);
                    };
                    break;
                case SelectYesNoInfo.NoButtonId when ShouldReport(atkUnit, which):
                    var noText = ptr.NoText;
                    deferred = () =>
                    {
                        _log.Verbose("No-Button {ButtonText} selected on 0x{SelectYesnoPtr:X} with description {Description}.", noText,
                            (ulong)atkUnit, descriptionText);
                        YesnoSelected!.Invoke(atkUnit, false, noText, descriptionText);
                    };
                    break;
            }
        }

        SelectYesNoHook!.OriginalDisposeSafe(atkUnit, eventType, which, source, data);
        InvokeDeferred(deferred, "SelectYesno");
    }

    private bool _deferredFailureLogged;

    // 跑在遊戲主執行緒的 hook 回呼裡：訂閱者的例外穿回原生碼＝遊戲結束，所以在這裡攔下、只記一次。
    private void InvokeDeferred(Action? deferred, string source)
    {
        if (deferred == null)
            return;

        try
        {
            deferred();
        }
        catch (Exception e)
        {
            if (_deferredFailureLogged)
                return;
            _deferredFailureLogged = true;
            _log.Error(e, "{Source} selection callback threw; ignored (logged once per session).", source);
        }
    }

    private static readonly ByteString SelectStringName = new("SelectString");

    private unsafe void SelectStringEventDetour(IntPtr atkUnit, EventType eventType, int which, IntPtr source, IntPtr data)
    {
        Action? deferred = null;

        if (eventType == EventType.ListIndexChange && data != IntPtr.Zero)
        {
            var owner = ((PopupMenu*)atkUnit)->Owner;
            var idx = (byte)((AtkEventData*)data)->ListItemData.SelectedIndex;
            if (SelectStringName.EqualsNullTerminated(owner->Name) && ShouldReport((IntPtr)owner, idx))
            {
                var ptr              = (IntPtr)owner;
                var selectStringInfo = (SelectStringInfo) ptr;
                var descriptionText  = selectStringInfo.Description;
                var itemText         = selectStringInfo.ItemText(idx);
                deferred = () =>
                {
                    _log.Verbose("String {ButtonText} ({Which}) selected on 0x{SelectStringPtr:X} with description {Description}.", itemText,
                        which, (ulong)ptr, descriptionText);
                    StringSelected!.Invoke(ptr, idx, itemText, descriptionText);
                };
            }
        }

        SelectStringHook!.OriginalDisposeSafe(atkUnit, eventType, which, source, data);
        InvokeDeferred(deferred, "SelectString");
    }


    private void TalkUpdateDetour(IntPtr unit, IntPtr data)
    {
        TalkUpdateHook!.OriginalDisposeSafe(unit, data);
        var ptr     = (TalkInfo)unit;
        var speaker = ptr.Speaker;
        var text    = ptr.Text;
        _log.Verbose("Talk at 0x{Unit:X} updated - Speaker: {Speaker}\n{Text}", (ulong)unit, speaker, text);
        TalkUpdated?.Invoke(unit, text, speaker);
    }
}
