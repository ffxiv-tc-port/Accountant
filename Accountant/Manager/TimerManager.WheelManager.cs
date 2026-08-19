using System;
using System.Linq;
using Accountant.Classes;
using Accountant.Enums;
using Accountant.Gui.Timer;
using Accountant.Timers;
using Accountant.Util;
using AddonWatcher;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using OtterLoc.Structs;

namespace Accountant.Manager;

public partial class TimerManager
{
    private sealed class WheelManager : ITimerManager
    {
        public ConfigFlags RequiredFlags
            => ConfigFlags.Enabled | ConfigFlags.AetherialWheels;

        private readonly IAddonWatcher      _watcher;
        private readonly FreeCompanyStorage _companyStorage;

        private          bool        _state;
        private readonly WheelTimers _wheels;

        public WheelManager(WheelTimers wheels, FreeCompanyStorage companyStorage)
        {
            _wheels         = wheels;
            _watcher        = Accountant.Watcher;
            _companyStorage = companyStorage;

            SetState();
        }

        public TimerWindow.BaseCache CreateCache(TimerWindow window)
            => new TimerWindow.WheelCache(window, RequiredFlags, _wheels);

        public void SetState()
        {
            if (Accountant.Config.Flags.Check(RequiredFlags))
                Enable();
            else
                Disable();
        }

        private void Enable()
        {
            if (_state)
                return;

            _wheels.Reload();
            Dalamud.Framework.Update += OnFrameworkWheel;
            _watcher.SubscribeYesnoSelected(WheelOnYesNo);
            _state = true;
        }

        private void Disable()
        {
            if (!_state)
                return;

            _watcher.UnsubscribeYesnoSelected(WheelOnYesNo);
            Dalamud.Framework.Update -= OnFrameworkWheel;
            _state                   =  false;
        }

        public void Dispose()
            => Disable();

        private static unsafe byte ActiveWheelSlot()
        {
            var wheel = (AtkUnitBase*)Dalamud.GameGui.GetAddonByName("AetherialWheel", 1).Address;
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (wheel == null || wheel->UldManager.NodeListCount < 14)
                return 0;

            for (var i = 8; i < 8 + WheelInfo.MaxSlots; ++i)
            {
                var button = (AtkComponentNode*)wheel->UldManager.NodeList[i];
                if (button == null || button->Component->UldManager.NodeListCount < 3)
                    continue;

                if (button->Component->UldManager.NodeList[2]->IsVisible())
                    return (byte)(14 - i);
            }

            return 0;
        }

        private void WheelOnYesNo(IntPtr _, bool which, SeString button, SeString description)
        {
            if (!which)
                return;

            var newDesc = new SeString(description.Payloads.Where(p => p is not NewLinePayload).ToList());
            var ret     = StringId.WheelFilter.Filter(newDesc);
            if (ret.Count == 0)
                return;

            var slot = ActiveWheelSlot();
            if (slot == 0)
                return;

            var (item, _, grade) = Accountant.GameData.FindWheel(ret[0]);
            if (grade == 0)
                return;

            var info = new WheelInfo
            {
                Accurate = true,
                Grade    = grade,
                ItemId   = item.RowId,
                Placed   = DateTime.UtcNow,
            };

            var fc = _companyStorage.GetCurrentCompanyInfo();
            if (fc == null)
            {
                Dalamud.Log.Error("Could not log wheel, unable to obtain free company name.");
                return;
            }

            if (_wheels!.AddOrUpdateWheel(fc.Value, info, slot))
                _wheels.Save(fc.Value);
        }

        private unsafe void OnFrameworkWheel(IFramework _)
        {
            var wheel = (AtkUnitBase*)Dalamud.GameGui.GetAddonByName("AetherialWheel", 1).Address;
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (wheel == null || wheel->UldManager.NodeListCount < 14)
                return;

            FreeCompanyInfo? fc = null;

            bool SetCompanyInfo()
            {
                if (fc != null)
                    return false;

                fc = _companyStorage.GetCurrentCompanyInfo();
                if (fc != null)
                    return false;

                Dalamud.Log.Error("Could not log machines, unable to obtain free company name.");
                return true;
            }

            var change = false;
            for (var i = 8; i < 8 + WheelInfo.MaxSlots; ++i)
            {
                var button = (AtkComponentNode*)wheel->UldManager.NodeList[i];
                // AtkComponentNode.Component 是偏移 0xB0 的指標欄位，節點還在建的時候可能還沒接上；
                // 少了這一關，下面那個看起來很像守衛的 NodeListCount 比對其實是對位址 0xB0+n 解參考。
                if (button == null || button->Component == null || button->Component->UldManager.NodeListCount < 10)
                    continue;

                if (SetCompanyInfo())
                    return;

                // 上面的 NodeListCount < 10 已經涵蓋索引 5／8／9 的上界，但**元素本身仍可為 null**
                // （上界檢查與元素判空是兩件事，只做一半就是半套邊界檢查）。
                var nodes = button->Component->UldManager.NodeList;
                var text  = nodes[5];
                if (text == null)
                    continue;

                var now  = DateTime.UtcNow;
                var slot = (byte)(14 - i);
                if (!text->IsVisible())
                {
                    change |= _wheels.RemoveWheel(fc!.Value, slot);
                }
                else
                {
                    // 🔴 fillNode／nameNode 任一取不到就跳過這一格（每幀輪詢⇒安靜跳過、不寫 log）。
                    // 跳過是 fail-closed：寧可這一幀不更新，也不要拿殘缺的資料覆寫既有紀錄。
                    var fillNode = nodes[8];
                    var nameNode = (AtkTextNode*)nodes[9];
                    if (fillNode == null || nameNode == null)
                        continue;

                    var fill = fillNode->ScaleX;
                    // 🔴 &nameNode->NodeText 對 null 節點**不會當場崩**：NodeText 在 AtkTextNode 偏移 0xC0，
                    // 算出的毒指標 0xC0 連 MemoryHelper.ReadSeString 內部的 != null 判空都騙得過去，
                    // 一路到真的去讀位址 0xC0 才炸 —— 崩潰現場完全指不到這一行。
                    var seString = MemoryHelper.ReadSeString(&nameNode->NodeText);
                    seString.Payloads.RemoveAll(p => p is NewLinePayload);
                    var name = seString.TextValue;
                    var (item, _, grade) = Accountant.GameData.FindWheel(name);
                    if (grade == 0)
                        continue;

                    var info = new WheelInfo()
                    {
                        Accurate = false,
                        ItemId   = item.RowId,
                        Grade    = grade,
                        Placed   = fill >= 0.9999 ? DateTime.MinValue : now.AddHours(-WheelInfo.HoursType(grade) * fill),
                    };
                    change |= _wheels.AddOrUpdateWheel(fc!.Value, info, slot);
                }
            }

            if (change)
                _wheels.Save(fc!.Value);
        }
    }
}
