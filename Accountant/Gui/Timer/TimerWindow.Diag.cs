using System;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Accountant.Gui.Timer;

// 每小時一行 Information 給使用者回報：Draw 配置落在哪條路徑、計時視窗開著的比例、各計時快取整份重建與只算摘要各幾次。
// 計數只在框架執行緒的 Draw 裡用整數欄位累加（不配置）；字串只在印的那一刻組。
public partial class TimerWindow
{
    private const long DiagWindowMs = 60 * 60 * 1000;

    internal static int  DiagConfigFrames;
    internal static long DiagConfigBytes;

    private long _diagStart = Environment.TickCount64;
    private int  _diagFrames, _diagShownFrames, _diagDisabledFrames, _diagOpened, _diagClosed;
    private long _diagHiddenBytes, _diagShownBytes;
    private bool _diagLastShown, _diagHasLast;

    private void DiagCountFrame(bool shown)
    {
        _diagFrames++;
        if (shown)
            _diagShownFrames++;
        if (_diagHasLast && shown != _diagLastShown)
        {
            if (shown)
                _diagOpened++;
            else
                _diagClosed++;
        }

        _diagLastShown = shown;
        _diagHasLast   = true;
    }

    private void DiagReportIfDue()
    {
        if (Environment.TickCount64 - _diagStart >= DiagWindowMs)
            DiagReport(false);
    }

    private static string Kb(long bytes)
        => (bytes / 1024.0).ToString("0.0", CultureInfo.InvariantCulture);

    private void DiagReport(bool unloading)
    {
        var now = Environment.TickCount64;
        if (_diagFrames + _diagDisabledFrames > 0)
        {
            var ic = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(512);
            sb.Append(ic, $"{(now - _diagStart) / 60000.0:0.0} min{(unloading ? " (unfinished, plugin unloading)" : string.Empty)}, ");
            sb.Append(ic, $"{_diagFrames} frames drawn, {_diagDisabledFrames} with Accountant disabled; ");
            sb.Append(ic, $"timer window shown in {_diagShownFrames} ({(_diagFrames > 0 ? 100.0 * _diagShownFrames / _diagFrames : 0):0.0}%), ");
            sb.Append(ic, $"opened {_diagOpened} times, closed {_diagClosed} times. ");
            sb.Append(ic, $"Draw allocated {Kb(_diagHiddenBytes)} KB while the window was hidden, {Kb(_diagShownBytes)} KB while shown, ");
            sb.Append(ic, $"config window {Kb(DiagConfigBytes)} KB in {DiagConfigFrames} frames. Included in those: ");
            sb.Append(ic, $"status bar text set {DtrManager.DiagTextSets} times, {Kb(DtrManager.DiagTextBytes)} KB; ");
            sb.Append("caches as resets / full rebuilds (KB) / summaries (KB):");
            foreach (var cache in _cache)
            {
                var resets = Interlocked.Exchange(ref cache.DiagResets, 0);
                if (resets == 0 && cache.DiagFull == 0 && cache.DiagSummary == 0)
                    continue;

                sb.Append(ic, $" {cache.GetType().Name} {cache.Name} {resets} / {cache.DiagFull} ({Kb(cache.DiagFullBytes)}) / ");
                sb.Append(ic, $"{cache.DiagSummary} ({Kb(cache.DiagSummaryBytes)});");
            }

            Dalamud.Log.Information("[TimerDiag] {Report:l}", sb.ToString());
        }

        foreach (var cache in _cache)
        {
            Interlocked.Exchange(ref cache.DiagResets, 0);
            cache.DiagFull         = 0;
            cache.DiagSummary      = 0;
            cache.DiagFullBytes    = 0;
            cache.DiagSummaryBytes = 0;
        }

        DtrManager.DiagTextSets  = 0;
        DtrManager.DiagTextBytes = 0;
        DiagConfigFrames         = 0;
        DiagConfigBytes          = 0;
        _diagFrames              = _diagShownFrames = _diagDisabledFrames = _diagOpened = _diagClosed = 0;
        _diagHiddenBytes         = _diagShownBytes  = 0;
        _diagStart               = now;
    }
}
