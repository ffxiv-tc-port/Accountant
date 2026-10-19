using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Accountant.Enums;
using Accountant.Gui.Helper;
using Accountant.Manager;
using Accountant.Util;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using Dalamud.Bindings.ImGui;
using OtterLoc.Structs;
using DateTime = System.DateTime;

namespace Accountant.Gui.Timer;

public partial class TimerWindow : IDisposable
{
    private          float   _widthTotal;
    private readonly string  _completedString;
    private readonly string  _availableString;
    private          string  _headerString = "Timers###Accountant.Timers";
    private          ColorId _headerColor  = ColorId.NeutralHeader;
    private          bool    _drawData     = true;

    private readonly BaseCache[] _cache;

    private DateTime _now = DateTime.UtcNow;

    private readonly CropCache          _cropCache;
    private readonly RetainerCache      _retainerCache;
    private readonly MachineCache       _subCache;
    private readonly MachineCache       _airshipCache;
    private readonly DemolitionWarning  _demolitionWarning;
    private readonly FreeCompanyStorage _freeCompany;

    internal static DtrManager DtrManager = null!;

    public TimerWindow(TimerManager manager, DemolitionManager demolitionManager, FreeCompanyStorage freeCompany)
    {
        _freeCompany       = freeCompany;
        _demolitionWarning = new DemolitionWarning(demolitionManager, Dalamud.Framework);
        _cache             = manager.CreateCaches(this);
        SortCache();
        _cropCache       = (CropCache)_cache.First(c => c is CropCache);
        _retainerCache   = (RetainerCache)_cache.First(c => c is RetainerCache);
        _subCache        = (MachineCache)_cache.First(c => c is MachineCache);
        _airshipCache    = (MachineCache)_cache.Last(c => c is MachineCache);
        _completedString = StringId.Completed.Value();
        _availableString = StringId.Available.Value();

        DtrManager = new DtrManager(Dalamud.Dtr, _cropCache, _retainerCache, _airshipCache, _subCache);
        if (Accountant.Config.ShowDtr)
            DtrManager.Enable();

        Dalamud.PluginInterface.UiBuilder.Draw       += Draw;
        Dalamud.PluginInterface.UiBuilder.OpenMainUi += Toggle;
    }

    public static void Toggle()
    {
        Accountant.Config.WindowVisible = !Accountant.Config.WindowVisible;
        Accountant.Config.Save();
    }

    public static void Toggle(DtrInteractionEvent obj)
        => Toggle();

    private void SetWidthTotal()
    {
        if (_widthTotal > 0)
            return;

        var maxWidth = Math.Max(ImGui.CalcTextSize(_completedString).X, ImGui.CalcTextSize(_availableString).X);
        maxWidth = Math.Max(maxWidth, ImGui.CalcTextSize("00:00:00").X);

        var widthTime = maxWidth / ImGuiHelpers.GlobalScale;
        _widthTotal = ImGui.CalcTextSize("mmmmmmmmmmmmmmmmmmmm").X / ImGuiHelpers.GlobalScale
          + widthTime
          + ImGui.GetStyle().ScrollbarSize / ImGuiHelpers.GlobalScale;
    }

    public void SortCache()
        => Array.Sort(_cache, (l, r) => Accountant.Config.GetPriority(r.Name).CompareTo(Accountant.Config.GetPriority(l.Name)));

    public void Dispose()
    {
        _demolitionWarning.Dispose();
        Dalamud.PluginInterface.UiBuilder.Draw       -= Draw;
        Dalamud.PluginInterface.UiBuilder.OpenMainUi -= Toggle;
        DiagReport(true);
    }

    public void ResetCache()
    {
        foreach (var cache in _cache)
            cache.Resetter();
    }

    public void ResetCache(Type type)
    {
        foreach (var cache in _cache.Where(c => c.GetType() == type))
            cache.Resetter();
    }

    private void Draw()
    {
        DiagReportIfDue();
        if (!Accountant.Config.Enabled)
        {
            _diagDisabledFrames++;
            return;
        }

        var shown = Accountant.Config.WindowVisible;
        DiagCountFrame(shown);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        try
        {
            DrawEnabled();
        }
        finally
        {
            var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            if (shown)
                _diagShownBytes += bytes;
            else
                _diagHiddenBytes += bytes;
        }
    }

    private void DrawEnabled()
    {
        _now = DateTime.UtcNow;

        if (!Accountant.Config.WindowVisible)
        {
            DtrManager.Update(true);
            return;
        }

        var flags = ImGuiWindowFlags.None;
        if (Accountant.Config.ProhibitMoving)
            flags |= ImGuiWindowFlags.NoMove;
        if (Accountant.Config.ProhibitResize)
            flags |= ImGuiWindowFlags.NoResize;

        SetWidthTotal();
        var minSize = new Vector2((Accountant.Config.FixedWindowWidth ?? 100) * ImGuiHelpers.GlobalScale,
            ImGui.GetFrameHeightWithSpacing() * 2 + ImGui.GetStyle().ItemSpacing.Y);
        var maxSize = new Vector2((Accountant.Config.FixedWindowWidth ?? 1000) * ImGuiHelpers.GlobalScale, 100000);
        ImGui.SetNextWindowSizeConstraints(minSize, maxSize);

        var enabled = Accountant.Config.WindowVisible;
        // 計時視窗每幀的推／彈直接呼叫 ImGui（ImGuiRaii 每次 new 一個物件）；每個 finally 彈的就是它前面那幾個推入。
        ImGui.PushStyleColor(ImGuiCol.WindowBg, ColorId.Background.Value());
        ImGui.PushStyleColor(ImGuiCol.Text, ColorId.NeutralText.Value());
        try
        {
            if (!_drawData && !Accountant.Config.NoHeaderStyling)
            {
                ImGui.PushStyleColor(ImGuiCol.Border, ColorId.CollapsedBorder.Value());
                ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, _headerColor.Value());
                ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
                try
                {
                    _drawData = ImGui.Begin(_headerString, ref enabled, flags);
                }
                finally
                {
                    ImGui.PopStyleColor(2);
                    ImGui.PopStyleVar();
                }
            }
            else
            {
                _drawData = ImGui.Begin(_headerString, ref enabled, flags);
            }

            if (!enabled)
            {
                Accountant.Config.WindowVisible = false;
                Accountant.Config.Save();
            }

            try
            {
                DrawCompanyWarning();
                DrawDemolition();

                foreach (var cache in _cache)
                    cache.Draw(_now);

                UpdateHeaderString();

                if (Accountant.Config.Flags.Check(ConfigFlags.Crops))
                    _headerColor = _cropCache.Color;
                DtrManager.Update(false);
            }
            finally
            {
                ImGui.End();
            }
        }
        finally
        {
            ImGui.PopStyleColor(2);
        }
    }

    // 視窗標題只由這組值決定；沒變就沿用上一次的字串，不每幀重新內插。
    private readonly record struct HeaderKey(bool Retainers, bool Machines, int RetainerCompleted, int RetainerAvailable,
        int RetainerSent, int MachineCompleted, int MachineAvailable, int MachineSent, bool ShowUnassigned, bool ShowUnderway,
        string? RetainerLabel, string? MachineLabel);

    private HeaderKey _headerKey;

    private void UpdateHeaderString()
    {
        var retainers = Accountant.Config.Flags.Check(ConfigFlags.Retainers);
        var machines  = Accountant.Config.Flags.Any(ConfigFlags.Airships | ConfigFlags.Submersibles);
        if (!retainers && !machines)
            return;

        var r = _retainerCache.Counter;
        var m = _subCache.GlobalCounter + _airshipCache.GlobalCounter;
        var key = new HeaderKey(retainers, machines, r.Completed, r.Available, r.Sent, m.Completed, m.Available, m.Sent,
            Accountant.Config.ShowUnassignedInHeader, Accountant.Config.ShowUnderwayInHeader,
            retainers ? (string)StringId.Retainers.Value() : null, machines ? (string)StringId.Machines.Value() : null);
        if (key == _headerKey)
            return;

        _headerKey = key;
        if (retainers)
            _headerString = machines
                ? $"{r.GetHeader(StringId.Retainers)}    {m.GetHeader(StringId.Machines)}###Accountant.Timers"
                : $"{r.GetHeader(StringId.Retainers)}###Accountant.Timers";
        else
            _headerString = $"{m.GetHeader(StringId.Machines)}###Accountant.Timers";
    }

    private void DrawCompanyWarning()
    {
        if (!Accountant.Config.ShowFreeCompanyWarning)
            return;

        var info           = _freeCompany.GetCurrentCompanyInfo();
        var infoIncomplete = !info.HasValue || info.Value.Tag.Length > 0 && info.Value.Name.IsNullOrEmpty();
        var tagUnavailable = Accountant.GameData.FreeCompanyInfo().Tag.IsNullOrEmpty();
        if (!infoIncomplete || tagUnavailable)
            return;

        using var c = ImGuiRaii.PushColor(ImGuiCol.Header, _demolitionWarning.HeaderColor.Value());
        ImGui.CollapsingHeader(Loc.T("No Full Free Company Data"), ImGuiTreeNodeFlags.DefaultOpen);
        if (ImGui.IsItemHovered())
        {
            using var tt = ImRaii.Tooltip();
            ImGui.TextWrapped(Loc.T("No full data about your current free company available.\n\n"
              + "Please leave your current instance or relog onto your character if this does not help to obtain Free Company Data after a Accountant Update.\n\n"
              + "You can disable this warning permanently in the settings."));
        }
    }

    private void DrawDemolition()
    {
        if (_demolitionWarning.Warnings.Count <= 0)
            return;

        using var c = ImGuiRaii.PushColor(ImGuiCol.Header, _demolitionWarning.HeaderColor.Value());
        if (!ImGui.CollapsingHeader(Loc.T("Demolishing Houses"), ImGuiTreeNodeFlags.DefaultOpen))
            return;

        foreach (var warning in _demolitionWarning.Warnings)
        {
            c.Push(ImGuiCol.Text, warning.Color.Value());
            if (ImGui.TreeNodeEx(warning.Name, ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.Leaf))
                ImGui.TreePop();
            ImGui.SameLine();
            var size = ImGui.CalcTextSize(warning.Status);
            ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - size.X);
            ImGui.TextUnformatted(warning.Status);
            c.Pop();
        }
    }

    // 每個計時器每一幀都格式化一次，結果只由時、分、秒與位數決定，所以記住；各執行緒各一份。
    // 有負值時（負號跟著文化走）不進快取、照原式算。
    [ThreadStatic]
    private static Dictionary<long, string>? _timeSpanStrings;

    internal static string TimeSpanString(TimeSpan span, int align = 2)
    {
        var hours   = (int)span.TotalHours;
        var minutes = span.Minutes;
        var seconds = span.Seconds;
        if (hours < 0 || minutes < 0 || seconds < 0)
            return FormatTimeSpan(hours, minutes, seconds, align);

        var key   = ((long)hours << 13) | ((long)minutes << 7) | ((long)seconds << 1) | (align == 2 ? 1L : 0L);
        var cache = _timeSpanStrings ??= new Dictionary<long, string>();
        if (cache.TryGetValue(key, out var s))
            return s;

        if (cache.Count >= 4096)
            cache.Clear();
        s          = FormatTimeSpan(hours, minutes, seconds, align);
        cache[key] = s;
        return s;
    }

    private static string FormatTimeSpan(int hours, int minutes, int seconds, int align)
        => $"{hours.ToString(align == 2 ? "D2" : "D3")}:{minutes:D2}:{seconds:D2}";

    private string? StatusString(ObjectStatus status)
        => status switch
        {
            ObjectStatus.Available => _availableString,
            ObjectStatus.Completed => _completedString,
            ObjectStatus.Sent      => null,
            ObjectStatus.Limited   => Loc.T("Limited"),
            _                      => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
}
