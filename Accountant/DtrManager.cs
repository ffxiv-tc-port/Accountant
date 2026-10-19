using System;
using Accountant.Gui;
using Accountant.Gui.Timer;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;

namespace Accountant;

internal class DtrManager(
    IDtrBar dtr,
    TimerWindow.CropCache cropCache,
    TimerWindow.RetainerCache retainerCache,
    TimerWindow.MachineCache airshipCache,
    TimerWindow.MachineCache subCache)
{
    private const string        DtrName = "Accountant.DTR";
    private       bool          _enabled;
    private       IDtrBarEntry? _entry;

    // 資訊列文字只由這組值決定：每幀重建會配置 SeStringBuilder 與數段字串，Dalamud 端也得每幀重新編碼比對，
    // 所以沒變就不再設定（節點重建或重新顯示時 Dalamud 會用保存的 Text 自己重寫）。換一個項目時一律重建。
    private readonly record struct DtrState(bool Retainers, bool Machines, bool Crops, bool ShowUnassigned, bool ShowUnderway,
        int RetainerCompleted, int RetainerAvailable, int RetainerSent, ColorId RetainerColor,
        int MachineCompleted, int MachineAvailable, int MachineSent, ColorId MachineColor, ColorId CropColor);

    private DtrState _state;
    private bool     _hasState;

    // 右鍵：開關設定視窗。設定視窗比本物件晚建立，由它在建構時注入、Dispose 時清掉。
    internal Action? ToggleConfigWindow { get; set; }

    private void OnClick(DtrInteractionEvent ev)
    {
        if (ev.ClickType == MouseClickType.Left)
            TimerWindow.Toggle();
        else
            ToggleConfigWindow?.Invoke();
    }

    private const ushort AvailableColor = 32;
    //private const ushort FinishedColor  = 45;
    private const ushort FinishedColor = 46;
    private const ushort WiltingColor  = 541;
    private const ushort BrokenColor   = 17;

    public void Enable()
    {
        if (_enabled)
            return;

        _enabled       = true;
        _entry         = dtr.Get(DtrName);
        _entry.OnClick = OnClick;
        _entry.Tooltip = $"{Loc.T("Left click: open/close the timer window")}\n{Loc.T("Right click: open/close the config window")}";
        _hasState      = false;
        Update(true);
    }

    public void Disable()
    {
        if (!_enabled)
            return;

        _enabled  = false;
        _entry    = null;
        _hasState = false;
        dtr.Remove(DtrName);
    }

    internal void Update(bool updateCaches)
        => Update(DateTime.UtcNow, updateCaches);

    // updateCaches＝視窗隱藏、這一幀沒有別人更新 cache：只要摘要（計數器與顏色），整份等視窗打開再建。
    internal void Update(DateTime now, bool updateCaches)
    {
        if (!Accountant.Config.ShowDtr || _entry == null)
            return;

        var retainers = Accountant.Config.EnableRetainers;
        var subs      = Accountant.Config.EnableSubmersibles;
        var airships  = Accountant.Config.EnableAirships;
        var crops     = Accountant.Config.EnableCrops;

        TimerWindow.ObjectCounter retainerCounter = default, machineCounter = default;
        ColorId                   retainerColor   = default, machineColor   = default, cropColor = default;
        if (retainers)
        {
            if (updateCaches)
                retainerCache.UpdateSummary(now);
            retainerCounter = retainerCache.Counter;
            retainerColor   = retainerCache.Color;
        }

        if (subs)
        {
            if (updateCaches)
                subCache.UpdateSummary(now);

            if (airships)
            {
                if (updateCaches)
                    airshipCache.UpdateSummary(now);
                machineCounter = airshipCache.GlobalCounter + subCache.GlobalCounter;
                machineColor   = airshipCache.Color.Combine(subCache.Color);
            }
            else
            {
                machineCounter = subCache.GlobalCounter;
                machineColor   = subCache.Color;
            }
        }
        else if (airships)
        {
            if (updateCaches)
                airshipCache.UpdateSummary(now);
            machineCounter = airshipCache.GlobalCounter;
            machineColor   = airshipCache.Color;
        }

        if (crops)
        {
            if (updateCaches)
                cropCache.UpdateSummary(now);
            cropColor = cropCache.Color;
        }

        var state = new DtrState(retainers, subs || airships, crops, Accountant.Config.ShowUnassignedInHeader,
            Accountant.Config.ShowUnderwayInHeader, retainerCounter.Completed, retainerCounter.Available, retainerCounter.Sent,
            retainerColor, machineCounter.Completed, machineCounter.Available, machineCounter.Sent, machineColor, cropColor);
        if (_hasState && state == _state)
            return;

        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var builder   = new SeStringBuilder();
        if (retainers)
            PushColor(builder, retainerCounter.GetHeader(SeIconChar.BoxedLetterR), retainerColor);

        if (subs || airships)
        {
            if (retainers)
                builder.AddText(" ");
            PushColor(builder, machineCounter.GetHeader(SeIconChar.BoxedLetterM), machineColor);
        }

        if (crops)
        {
            if (retainers || subs || airships)
                builder.AddText(" ");
            PushColor(builder, SeIconChar.BoxedLetterC.ToIconString(), cropColor);
        }

        _entry.Text = builder.BuiltString;
        _state      = state;
        _hasState   = true;
        DiagTextSets++;
        DiagTextBytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
    }

    // 每小時診斷用（框架執行緒；TimerWindow 印完歸零）。
    internal int  DiagTextSets;
    internal long DiagTextBytes;

    private static void PushColor(SeStringBuilder builder, string text, ColorId color)
    {
        ushort uiColor = color switch
        {
            ColorId.HeaderObjectsAway    => 0,
            ColorId.HeaderObjectsMixed   => AvailableColor,
            ColorId.HeaderObjectsHome    => FinishedColor,
            ColorId.TextCropWithered     => BrokenColor,
            ColorId.TextCropWilted       => WiltingColor,
            ColorId.TextCropGrown        => FinishedColor,
            ColorId.TextCropGrowing      => 0,
            ColorId.TextCropGuaranteed   => 0,
            ColorId.HeaderCropWithered   => BrokenColor,
            ColorId.HeaderCropWilted     => WiltingColor,
            ColorId.HeaderCropGrown      => FinishedColor,
            ColorId.HeaderCropGrowing    => 0,
            ColorId.HeaderCropGuaranteed => 0,
            ColorId.TextObjectsAway      => 0,
            ColorId.TextObjectsMixed     => AvailableColor,
            ColorId.TextObjectsHome      => FinishedColor,
            _                            => 0,
        };
        if (uiColor != 0)
            builder.AddUiGlow(uiColor);
        builder.AddText(text);
        if (uiColor != 0)
            builder.AddUiGlowOff();
    }
}
