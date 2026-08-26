using System;
using Accountant.Classes;
using Accountant.Gui.Helper;
using Accountant.Gui.Timer;
using Dalamud.Interface.Utility;
using ImGuiNET;

namespace Accountant.Gui.Config;

public partial class ConfigWindow
{
    private void DrawConfigTab()
    {
        if (!ImGui.BeginTabItem($"{Loc.T("Config")}##AccountantTabs"))
            return;

        using var raii = ImGuiRaii.DeferredEnd(ImGui.EndTabItem);

        if (!ImGui.BeginChild("##GeneralTab"))
            return;

        raii.Push(ImGui.EndChild);

        ImGuiRaii.ConfigCheckmark(Loc.T("Enable Plugin"), Accountant.Config.Enabled,       EnableTimers);
        ImGuiRaii.ConfigCheckmark(Loc.T("Show Timers"),   Accountant.Config.WindowVisible, b => Accountant.Config.WindowVisible = b);
        ImGuiRaii.ConfigCheckmark(Loc.T("Show DTR Header"), Accountant.Config.ShowDtr, b =>
        {
            Accountant.Config.ShowDtr = b;
            if (b)
                TimerWindow.DtrManager.Enable();
            else
                TimerWindow.DtrManager.Disable();
        });
        ImGuiRaii.ConfigCheckmark(Loc.T("Show Unassigned Retainers/Machines in Headers"), Accountant.Config.ShowUnassignedInHeader,
            b => Accountant.Config.ShowUnassignedInHeader = b);
        ImGuiRaii.ConfigCheckmark(Loc.T("Show Sent-Out Retainers/Machines in Headers"), Accountant.Config.ShowUnderwayInHeader,
            b => Accountant.Config.ShowUnderwayInHeader = b);
        ImGuiRaii.ConfigCheckmark(Loc.T("No Collapsed Header Styling"), Accountant.Config.NoHeaderStyling, b => Accountant.Config.NoHeaderStyling = b);
        ImGuiRaii.ConfigCheckmark(Loc.T("No Timer Window Resize"),      Accountant.Config.ProhibitResize,  b => Accountant.Config.ProhibitResize  = b);
        ImGuiRaii.ConfigCheckmark(Loc.T("Fix Timer Window Width"), Accountant.Config.FixedWindowWidth != null,
            b => Accountant.Config.FixedWindowWidth = b ? 300f : null);
        if (Accountant.Config.FixedWindowWidth != null)
        {
            var value = Accountant.Config.FixedWindowWidth.Value;
            ImGui.SetNextItemWidth(300 * ImGuiHelpers.GlobalScale);
            if (ImGui.DragFloat(Loc.T("Fixed Timer Window Width"), ref value, 1f, 100f, 1000f))
                Accountant.Config.FixedWindowWidth = value;

            if (ImGui.IsItemDeactivatedAfterEdit())
                Accountant.Config.Save();
        }

        ImGuiRaii.ConfigCheckmark(Loc.T("No Timer Window Movement"), Accountant.Config.ProhibitMoving, b => Accountant.Config.ProhibitMoving = b);
        ImGuiRaii.ConfigCheckmark(Loc.T("Hide Disabled Objects"),    Accountant.Config.HideDisabled,   b => Accountant.Config.HideDisabled   = b);
        ImGuiRaii.HoverTooltip(Loc.T("Hide objects that are disabled or limited from the timers."));
        ImGui.NewLine();

        ImGuiRaii.ConfigCheckmark(Loc.T("Show Free Company Warning"), Accountant.Config.ShowFreeCompanyWarning,
            b => Accountant.Config.ShowFreeCompanyWarning = b);
        ImGuiRaii.ConfigCheckmark(Loc.T("Enable Retainer Timers"), Accountant.Config.EnableRetainers, EnableRetainers);
        ImGui.NewLine();
        ImGuiRaii.ConfigCheckmark(Loc.T("Enable Airship Timers"),         Accountant.Config.EnableAirships,     EnableAirships);
        ImGuiRaii.ConfigCheckmark(Loc.T("Enable Submersible Timers"),     Accountant.Config.EnableSubmersibles, EnableSubmersibles);
        ImGuiRaii.ConfigCheckmark(Loc.T("Enable Aetherial Wheel Timers"), Accountant.Config.EnableWheels,       EnableWheels);
        ImGui.NewLine();
        ImGuiRaii.ConfigCheckmark(Loc.T("Enable Crop Timers"),        Accountant.Config.EnableCrops, EnableCrops);
        ImGuiRaii.ConfigCheckmark(Loc.T("Ignore Indoor Plot Plants"), Accountant.Config.IgnoreIndoorPlants, IgnoreIndoorPlants);
        ImGuiRaii.ConfigCheckmark(Loc.T("Group Crop Beds by Plant"),  Accountant.Config.OrderByCrop, OrderByCrop);
        ImGuiRaii.ConfigCheckmark(Loc.T("Show Ward-Update Tooltip"),  Accountant.Config.ShowCropTooltip, v => Accountant.Config.ShowCropTooltip = v);
        ImGui.NewLine();
        ImGuiRaii.ConfigCheckmark(Loc.T("Enable Leve Allowance Timers"), Accountant.Config.EnableLeveAllowances, EnableLeveAllowances);
        DrawLeveAllowancesWarningInput();
        ImGuiRaii.ConfigCheckmark(Loc.T("Enable Squadron Mission Timers"),          Accountant.Config.EnableSquadron,     EnableSquadron);
        ImGuiRaii.ConfigCheckmark(Loc.T("Enable Map Allowance Timers"),             Accountant.Config.EnableMapAllowance, EnableMapAllowance);
        ImGuiRaii.ConfigCheckmark(Loc.T("Enable Mini Cactpot Timers"),              Accountant.Config.EnableMiniCactpot,  EnableMiniCactpot);
        ImGuiRaii.ConfigCheckmark(Loc.T("Enable Jumbo Cactpot Timers"),             Accountant.Config.EnableJumboCactpot, EnableJumboCactpot);
        ImGuiRaii.ConfigCheckmark(Loc.T("Enable Custom Delivery Allowance Timers"), Accountant.Config.EnableDeliveries,   EnableDeliveries);
        ImGuiRaii.ConfigCheckmark(Loc.T("Enable Tribe Allowance Timers"),           Accountant.Config.EnableTribes,       EnableTribes);
        DrawTribeAllowancesFinishedInput();
        ImGui.NewLine();
    }

    private static void DrawColorsTab()
    {
        if (!ImGui.BeginTabItem($"{Loc.T("Colors")}##AccountantTabs"))
            return;

        using var raii = ImGuiRaii.DeferredEnd(ImGui.EndTabItem);

        if (!ImGui.BeginChild("##ColorsTab"))
            return;

        raii.Push(ImGui.EndChild);

        foreach (var color in Enum.GetValues<ColorId>())
        {
            ImGuiRaii.ConfigColorPicker(Loc.T(color.Name()), Loc.T(color.Description()), color.Value(), c => Accountant.Config.Colors[color] = c,
                color.Default());
        }
    }

    private void DrawLeveAllowancesWarningInput()
    {
        var leveAllowances = Accountant.Config.LeveWarning;
        ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
        if (!ImGui.DragInt(Loc.T("Leve Allowances Warning"), ref leveAllowances, 1, 0, Leve.AllowanceError))
            return;

        if (leveAllowances < 0)
            leveAllowances = 0;
        if (leveAllowances > Leve.AllowanceError)
            leveAllowances = Leve.AllowanceError;
        if (leveAllowances == Accountant.Config.LeveWarning)
            return;

        Accountant.Config.LeveWarning = leveAllowances;
        Accountant.Config.Save();
        _timerWindow.ResetCache(typeof(TimerWindow.TaskCache));
    }

    private void DrawTribeAllowancesFinishedInput()
    {
        var tribeAllowances = Accountant.Config.TribesFinished;
        ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
        if (!ImGui.DragInt(Loc.T("Tribe Quests Finished"), ref tribeAllowances, 1, 0, Tribe.AllowanceCap))
            return;

        if (tribeAllowances < 0)
            tribeAllowances = 0;
        if (tribeAllowances > Tribe.AllowanceCap)
            tribeAllowances = Tribe.AllowanceCap;
        if (tribeAllowances == Accountant.Config.TribesFinished)
            return;

        Accountant.Config.TribesFinished = tribeAllowances;
        Accountant.Config.Save();
        _timerWindow.ResetCache(typeof(TimerWindow.TaskCache));
    }

    private void OrderByCrop(bool toggle)
    {
        Accountant.Config.OrderByCrop = toggle;
        _timerWindow.ResetCache(typeof(TimerWindow.CropCache));
    }

    private void IgnoreIndoorPlants(bool toggle)
    {
        Accountant.Config.IgnoreIndoorPlants = toggle;
        _timerWindow.ResetCache(typeof(TimerWindow.CropCache));
    }

    private void EnableCache(bool toggle, ConfigFlags flag, Type type)
    {
        Accountant.Config.Flags.Set(flag, toggle);
        _timers.CheckSettings();
        _timerWindow.ResetCache(type);
    }

    private void EnableRetainers(bool toggle)
        => EnableCache(toggle, ConfigFlags.Retainers, typeof(TimerWindow.RetainerCache));

    private void EnableAirships(bool toggle)
        => EnableCache(toggle, ConfigFlags.Airships, typeof(TimerWindow.MachineCache));

    private void EnableSubmersibles(bool toggle)
        => EnableCache(toggle, ConfigFlags.Submersibles, typeof(TimerWindow.MachineCache));

    private void EnableWheels(bool toggle)
        => EnableCache(toggle, ConfigFlags.AetherialWheels, typeof(TimerWindow.WheelCache));

    private void EnableCrops(bool toggle)
        => EnableCache(toggle, ConfigFlags.Crops, typeof(TimerWindow.CropCache));

    private void EnableLeveAllowances(bool toggle)
        => EnableCache(toggle, ConfigFlags.LeveAllowances, typeof(TimerWindow.TaskCache));

    private void EnableSquadron(bool toggle)
        => EnableCache(toggle, ConfigFlags.Squadron, typeof(TimerWindow.TaskCache));

    private void EnableMapAllowance(bool toggle)
        => EnableCache(toggle, ConfigFlags.MapAllowance, typeof(TimerWindow.TaskCache));

    private void EnableMiniCactpot(bool toggle)
        => EnableCache(toggle, ConfigFlags.MiniCactpot, typeof(TimerWindow.TaskCache));

    private void EnableJumboCactpot(bool toggle)
        => EnableCache(toggle, ConfigFlags.JumboCactpot, typeof(TimerWindow.TaskCache));

    private void EnableDeliveries(bool toggle)
        => EnableCache(toggle, ConfigFlags.CustomDelivery, typeof(TimerWindow.TaskCache));

    private void EnableTribes(bool toggle)
        => EnableCache(toggle, ConfigFlags.Tribes, typeof(TimerWindow.TaskCache));

    private void EnableTimers(bool toggle)
        => EnableCache(toggle, ConfigFlags.Enabled, typeof(TimerWindow.BaseCache));
}
