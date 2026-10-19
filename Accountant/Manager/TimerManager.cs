using System;
using System.Collections.Generic;
using System.Linq;
using Accountant.Gui.Timer;
using Accountant.Timers;
using Accountant.Util;
using Dalamud.Hooking;

namespace Accountant.Manager;

public partial class TimerManager
{ }

public sealed partial class TimerManager : IDisposable
{
    public interface ITimerManager : IDisposable
    {
        public void                  SetState();
        public TimerWindow.BaseCache CreateCache(TimerWindow window);
    }

    public readonly RetainerTimers      RetainerTimers    = new();
    public readonly PlotCropTimers      PlotCropTimers    = new();
    public readonly PrivateCropTimers   PrivateCropTimers = new();
    public readonly SubmersibleTimers   SubmersibleTimers = new();
    public readonly AirshipTimers       AirshipTimers     = new();
    public readonly WheelTimers         WheelTimers       = new();
    public readonly TaskTimers          TaskTimers        = new();
    public readonly FreeCompanyStorage  CompanyStorage    = FreeCompanyStorage.Load();

    private readonly ITimerManager[] _managers;

    // 建構子不在框架執行緒：各管理器的訂閱與 hook 啟用先排隊，等 Start() 才依序執行，
    // 否則先訂閱的處理器會在後面的 Reload（先 Clear 再寫入）或計時器視窗第一次列舉途中改同一個字典。
    // 鎖內執行是為了保序：Start 前後各執行緒排進來的訂閱／退訂照呼叫順序生效。
    private static readonly object        StartLock = new();
    private static          List<Action>? _pendingStarts;

    private static void WhenStarted(Action action)
    {
        lock (StartLock)
        {
            if (_pendingStarts != null)
                _pendingStarts.Add(action);
            else
                action();
        }
    }

    /// <summary>插件建構子在計時器視窗建好之後呼叫一次；開始追蹤（訂閱框架更新、聊天、視窗事件，啟用 hook）。</summary>
    public void Start()
    {
        lock (StartLock)
        {
            var pending = _pendingStarts;
            _pendingStarts = null;
            if (pending == null)
                return;

            foreach (var action in pending)
                action();
        }
    }

    /// <summary>特徵碼對不上而沒建立的 hook（說明文字）。只在 <see cref="TimerManager"/> 建構時寫入。</summary>
    public static readonly List<string> MissingHooks = [];

    private static void NoteMissing(IDalamudHook? hook, string what)
    {
        if (hook != null)
            return;

        MissingHooks.Add(what);
        Dalamud.Log.Warning($"遊戲改版後對不上：{what}；其餘功能照常。");
    }

    public TimerManager()
    {
        lock (StartLock)
            _pendingStarts = [];

        MissingHooks.Clear();

        _managers =
        [
            new RetainerManager(RetainerTimers, AirshipTimers, SubmersibleTimers, CompanyStorage),
            new CropManager(PlotCropTimers, PrivateCropTimers),
            new SubmersibleManager(SubmersibleTimers, AirshipTimers, CompanyStorage),
            new AirshipManager(AirshipTimers, SubmersibleTimers, CompanyStorage),
            new WheelManager(WheelTimers, CompanyStorage),
            new SquadronManager(TaskTimers),
            new TaskManager(TaskTimers),
        ];
    }

    public TimerWindow.BaseCache[] CreateCaches(TimerWindow window)
        => _managers.Select(m => m.CreateCache(window)).ToArray();

    public void CheckSettings()
    {
        foreach (var manager in _managers)
            manager.SetState();
    }

    public void Dispose()
    {
        foreach (var manager in _managers)
            manager.Dispose();

        // 從未 Start 就停用：排隊的訂閱一律丟掉、清單留著，之後的 Start 不會去啟用已釋放的 hook。
        lock (StartLock)
            _pendingStarts?.Clear();
    }
}
