using System;
using System.Numerics;
using Accountant.Gui.Helper;
using Accountant.Manager;
using Accountant.Util;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;

namespace Accountant.Gui.Config;

public partial class ConfigWindow : IDisposable
{
    private readonly        TimerManager       _timers;
    private readonly        Timer.TimerWindow  _timerWindow;
    private readonly        DemolitionManager  _demoManager;
    private readonly        FreeCompanyStorage _freeCompanyStorage;
    private readonly        string             _header;
    private                 bool               _enabled;
    private static readonly Vector2            MinSize = new(640, 540);


    public ConfigWindow(TimerManager timers, Timer.TimerWindow timerWindow, DemolitionManager demoManager,
        FreeCompanyStorage freeCompanyStorage)
    {
        _timers             = timers;
        _timerWindow        = timerWindow;
        _demoManager        = demoManager;
        _freeCompanyStorage = freeCompanyStorage;
        _header             = Accountant.Version != string.Empty ? $"Accountant v{Accountant.Version}###Accountant" : "Accountant###Accountant";
        _missing            = [.. Interop.MissingNotices, .. Accountant.Watcher.MissingHooks, .. TimerManager.MissingHooks];

        Dalamud.PluginInterface.UiBuilder.Draw         += Draw;
        Dalamud.PluginInterface.UiBuilder.OpenConfigUi += Toggle;
        Timer.TimerWindow.DtrManager.ToggleConfigWindow = Toggle;
    }

    public void Dispose()
    {
        Timer.TimerWindow.DtrManager.ToggleConfigWindow = null;
        Dalamud.PluginInterface.UiBuilder.Draw         -= Draw;
        Dalamud.PluginInterface.UiBuilder.OpenConfigUi -= Toggle;
    }

    public void Toggle()
        => _enabled = !_enabled;

    // 載入時拍下的快照：遊戲改版後特徵碼對不上而停用的項目。空的＝一切正常。
    private readonly string[] _missing;

    // 計時器「不更新」在列上看不出來，所以有項目停用時在設定視窗最上方常駐寫明。
    private void DrawMissingNotice()
    {
        if (_missing.Length == 0)
            return;

        ImGui.PushStyleColor(ImGuiCol.Text, 0xFF40A0FF);
        ImGui.TextWrapped("遊戲改版後有遊戲位址對不上，下列項目暫時停用（請等插件更新）：");
        foreach (var line in _missing)
            ImGui.TextWrapped($"・{line}");
        ImGui.PopStyleColor();
        ImGui.Separator();
    }

    private void Draw()
    {
        if (!_enabled)
            return;

        var allocated = GC.GetAllocatedBytesForCurrentThread();
        try
        {
            DrawWindow();
        }
        finally
        {
            Timer.TimerWindow.DiagConfigFrames++;
            Timer.TimerWindow.DiagConfigBytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
        }
    }

    private void DrawWindow()
    {
        ImGui.SetNextWindowSizeConstraints(MinSize * ImGuiHelpers.GlobalScale, MinSize * 5);
        if (!ImGui.Begin(_header, ref _enabled))
        {
            ImGui.End();
            return;
        }

        using var raii = ImGuiRaii.DeferredEnd(ImGui.End);

        DrawMissingNotice();

        if (!ImGui.BeginTabBar("##AccountantTabs"))
            return;

        raii.Push(ImGui.EndTabBar);

        DrawConfigTab();
        DrawColorsTab();
        DrawDemolitionTab();
        DrawBlocklistsTab();
        DrawOrderNamesTab();
        DrawDeleteTab();
        DrawDebugTab();
    }
}
