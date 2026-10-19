using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Accountant.Gui.Timer.Cache;
using Dalamud.Bindings.ImGui;

namespace Accountant.Gui.Timer;

public partial class TimerWindow
{
    public partial class BaseCache
    {
        protected readonly TimerWindow       Window;
        protected readonly List<CacheObject> Objects    = [];
        protected readonly List<SmallHeader> Headers    = [];
        private readonly   HashSet<string>   _seenNames = [];

        protected DateTime Now;
        private   DateTime _nextChange = DateTime.MinValue;

        public           ColorId     Color       = ColorId.NeutralHeader;
        protected        DateTime    DisplayTime = DateTime.MaxValue;
        private readonly ConfigFlags _requiredFlags;
        public readonly  string      Name;

        // Optional display-only override for the top-level header label (e.g. a status marker
        // prefix) - Name itself stays stable since it's also used for the ImGui ID and lookups.
        protected string? DisplayNameOverride;

        protected BaseCache(string name, ConfigFlags requiredFlags, TimerWindow window)
        {
            Name           = name;
            _requiredFlags = requiredFlags;
            Window         = window;
        }

        protected DateTime UpdateNextChange(DateTime time)
        {
            if (time < _nextChange && time > Now)
                _nextChange = time;

            return time;
        }

        protected string GetName(string name, uint serverId)
        {
            if (_seenNames.Add(name))
                return name;

            var server = Accountant.GameData.GetWorldName(serverId);
            return $"{name} ({server})";
        }

        // 只算過摘要時，Objects／Headers 停在舊的那份；_summaryTime＝那次摘要的時間，視窗要畫時照那個時間補建整份。
        private bool     _objectsStale;
        private DateTime _summaryTime;

        public void Update(DateTime now)
        {
            if (now <= _nextChange)
            {
                if (!_objectsStale)
                    return;

                // 畫出來的要等於「摘要那一刻就整份重建」的結果：到期點之前資料沒變，只有時間會讓狀態不同。
                now = _summaryTime;
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread();
            BeginUpdate(now);
            _objectsStale = false;
            _seenNames.Clear();
            Objects.Clear();
            Headers.Clear();

            UpdateInternal();
            DiagFull++;
            DiagFullBytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
        }

        // 每小時診斷用的計數（TimerWindow 印完歸零）；Resets 可能來自計時資料的 Changed 事件，用 Interlocked。
        internal int  DiagFull, DiagSummary, DiagResets;
        internal long DiagFullBytes, DiagSummaryBytes;

        /// <summary>資訊列用：只保證 Color、計數器與下一個到期點是最新的（與 <see cref="Update"/> 算出的相同）。</summary>
        /// <remarks>子類別沒有摘要算法時就是整份重建。</remarks>
        public void UpdateSummary(DateTime now)
        {
            if (now <= _nextChange)
                return;

            if (!HasSummary)
            {
                Update(now);
                return;
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread();
            BeginUpdate(now);
            UpdateSummaryInternal();
            _objectsStale = true;
            _summaryTime  = now;
            DiagSummary++;
            DiagSummaryBytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
        }

        private void BeginUpdate(DateTime now)
        {
            Now                 = now;
            Color               = ColorId.NeutralHeader;
            _nextChange         = DateTime.MaxValue;
            DisplayNameOverride = null;
        }

        protected virtual void UpdateInternal()
        { }

        protected virtual bool HasSummary
            => false;

        /// <summary>與 <see cref="UpdateInternal"/> 設定同樣的 Color、DisplayTime、計數器並呼叫同樣的 UpdateNextChange，但不碰 Objects／Headers。</summary>
        protected virtual void UpdateSummaryInternal()
        { }

        protected virtual void DrawTooltip()
        { }

        public void Resetter()
        {
            Interlocked.Increment(ref DiagResets);
            _nextChange = DateTime.UtcNow;
        }

        public void Draw(DateTime now)
        {
            if (!Accountant.Config.Flags.Check(_requiredFlags))
                return;

            Update(now);
            if (Headers.Count == 0 && Objects.Count == 0)
                return;

            // 每幀路徑，直接推／彈（見 TimerWindow.Draw）。
            ImGui.PushID(Name);
            try
            {
                float posY;
                bool  header, hovered;
                ImGui.PushStyleColor(ImGuiCol.Header, Color.Value());
                try
                {
                    posY    = ImGui.GetCursorPosY();
                    header  = ImGui.CollapsingHeader(DisplayNameOverride ?? Name);
                    hovered = ImGui.IsItemHovered();
                }
                finally
                {
                    ImGui.PopStyleColor();
                }

                if (DisplayTime > now && DisplayTime != DateTime.MaxValue)
                {
                    var s     = TimeSpanString(DisplayTime - now);
                    var width = ImGui.CalcTextSize(s).X;
                    var pos   = ImGui.GetCursorPos();
                    ImGui.SetCursorPos(new Vector2(ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X - width, posY));
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text(s);
                    ImGui.SetCursorPos(pos);
                }

                if (hovered)
                    DrawTooltip();

                if (!header)
                    return;

                DrawBody(now);
            }
            finally
            {
                ImGui.PopID();
            }
        }

        protected virtual void DrawBody(DateTime now)
        {
            foreach (var smallHeader in Headers)
                smallHeader.Draw(this, now);
        }
    }
}
