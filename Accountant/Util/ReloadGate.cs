using System;
using System.Threading;
using System.Threading.Tasks;

namespace Accountant.Util;

// 背景重載的共用外殼：load 在執行緒池上跑，publish 只在框架執行緒上跑，同一時間只跑一輪。
// 不論 load 或 publish 擲例外、或這一輪被放棄，閘門都會復位，下一次外部變更照常重載。
// 插件已卸載或 Dalamud 卸載中時放棄這一輪、不發布：那時 RunOnFrameworkThread 會就地在執行緒池上執行。
internal sealed class ReloadGate(string what)
{
    // 插件存活期間為 false。排進框架執行緒的發布在插件卸載後仍會執行，靠它擋下。
    public static volatile bool PluginDisposed;

    private          int     _busy;
    private volatile string? _lastFailure;

    public void Run<T>(Func<T> load, Action<T> publish)
    {
        if (Interlocked.Exchange(ref _busy, 1) != 0)
            return;

        Task.Run(() =>
        {
            try
            {
                var loaded = load();
                if (PluginDisposed || Dalamud.Framework.IsFrameworkUnloading)
                {
                    Volatile.Write(ref _busy, 0);
                    return;
                }

                Dalamud.Framework.RunOnFrameworkThread(() => Publish(loaded, publish));
            }
            catch (Exception e)
            {
                Volatile.Write(ref _busy, 0);
                ReportFailure(e);
            }
        });
    }

    private void Publish<T>(T loaded, Action<T> publish)
    {
        try
        {
            // 上面判過之後才開始卸載的話，這裡是在執行緒池上被就地呼叫的。
            if (PluginDisposed || !Dalamud.Framework.IsInFrameworkUpdateThread)
                return;

            publish(loaded);
            _lastFailure = null;
        }
        catch (Exception e)
        {
            ReportFailure(e);
        }
        finally
        {
            Volatile.Write(ref _busy, 0);
        }
    }

    // 來源檔一直是壞的時，每一輪檢查都會再試一次；同一種失敗只記第一次。
    private void ReportFailure(Exception e)
    {
        var root    = e.GetBaseException();
        var failure = $"{root.GetType().Name}: {root.Message}";
        if (failure == _lastFailure)
            return;

        _lastFailure = failure;
        Dalamud.Log.Warning(e, "Could not reload {What:l} after external changes; will retry on the next check.", what);
    }
}
