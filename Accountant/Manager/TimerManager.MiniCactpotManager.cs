using System;
using Accountant.Classes;
using Accountant.Enums;
using Accountant.Gui.Timer;
using Accountant.SeFunctions;
using Accountant.Timers;
using Accountant.Util;
using AddonWatcher;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using OtterLoc.Structs;

namespace Accountant.Manager;

public partial class TimerManager
{
    private sealed class MiniCactpotManager : ITimerManager
    {
        public ConfigFlags RequiredFlags
            => ConfigFlags.Enabled | ConfigFlags.MiniCactpot;

        private readonly TaskTimers _tasks;

        private          bool                            _state;

        private const string Feature = "仙人微彩紀錄";
        private int _detourFailed;
        private readonly IAddonWatcher                   _watcher;
        private readonly Hook<UpdateGoldSaucerDelegate>? _goldSaucerUpdateHook;

        public MiniCactpotManager(TaskTimers tasks)
        {
            _tasks                = tasks;
            _watcher              = Accountant.Watcher;
            _goldSaucerUpdateHook = Interop.UpdateGoldSaucerData.CreateHook(Dalamud.Interop, UpdateGoldSaucerDetour, false);
            SetState();
        }

        public TimerWindow.BaseCache CreateCache(TimerWindow window)
            => throw new NotImplementedException();

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

            _tasks.Reload();
            WhenStarted(() =>
            {
                _goldSaucerUpdateHook?.Enable();
                _watcher.SubscribeYesnoSelected(TicketBought);
            });
            _state = true;
        }

        private void Disable()
        {
            if (!_state)
                return;

            WhenStarted(() =>
            {
                _goldSaucerUpdateHook?.Disable();
                _watcher.UnsubscribeYesnoSelected(TicketBought);
            });
            _state = false;
        }

        public void Dispose()
        {
            Disable();
            _goldSaucerUpdateHook?.Dispose();
        }

        // 遊戲原函式只在 finally 呼叫，恰好一次；插件邏輯的例外不可穿回原生框架（＝遊戲崩潰）。
        private unsafe void UpdateGoldSaucerDetour(IntPtr _, IntPtr packet)
        {
            try
            {
                if (DetourGuard.IsPaused(ref _detourFailed))
                    return;

                if (Dalamud.Objects.LocalPlayer is not { } localPlayer)
                    return;

                var mini = new MiniCactpot()
                {
                    Tickets    = *(byte*)(packet + 0x1B),
                    LastUpdate = DateTime.UtcNow,
                };

                var player = new PlayerInfo(localPlayer);
                if (_tasks.AddOrUpdateMiniCactpot(player, mini))
                    _tasks.Save(player);
            }
            catch (Exception ex)
            {
                DetourGuard.ReportOnce(ref _detourFailed, Feature, ex);
            }
            finally
            {
                _goldSaucerUpdateHook!.OriginalDisposeSafe(_, packet);
            }
        }

        private void TicketBought(IntPtr _, bool which, SeString button, SeString description)
        {
            if (!which)
                return;

            if (!StringId.BuyMiniCactpotTicket.Match(description))
                return;

            var player = new PlayerInfo(Dalamud.Objects.LocalPlayer!);
            if (_tasks.AddOrUpdateMini(player))
                _tasks.Save(player);
        }
    }
}
