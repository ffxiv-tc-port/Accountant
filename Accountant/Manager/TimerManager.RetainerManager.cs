using System;
using System.Linq;
using Accountant.Classes;
using Accountant.Gui.Timer;
using Accountant.ExternalIpc;
using Accountant.Timers;
using Accountant.Util;
using Dalamud.Plugin.Services;

namespace Accountant.Manager;

public partial class TimerManager
{
    private sealed class RetainerManager : ITimerManager
    {
        public ConfigFlags RequiredFlags
            => ConfigFlags.Enabled | ConfigFlags.Retainers;

        private DateTime _nextRetainerCheck     = DateTime.MinValue;
        private DateTime _nextAutoRetainerSync  = DateTime.MinValue;
        private bool     _state;

        private readonly RetainerTimers     _retainers;
        private readonly AirshipTimers      _airships;
        private readonly SubmersibleTimers  _submersibles;
        private readonly FreeCompanyStorage _companies;

        public RetainerManager(RetainerTimers retainers, AirshipTimers airships, SubmersibleTimers submersibles, FreeCompanyStorage companies)
        {
            _retainers    = retainers;
            _airships     = airships;
            _submersibles = submersibles;
            _companies    = companies;
            SetState();
        }

        public TimerWindow.BaseCache CreateCache(TimerWindow window)
            => new TimerWindow.RetainerCache(window, RequiredFlags, _retainers);

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

            _retainers.Reload();
            Dalamud.Framework.Update += OnFrameworkRetainer;
            _state                   =  true;
        }

        private void Disable()
        {
            if (!_state)
                return;

            Dalamud.Framework.Update -= OnFrameworkRetainer;
            _state                   =  false;
        }

        public void Dispose()
            => Disable();

        private unsafe void UpdateRetainers()
        {
            if (Dalamud.Objects.LocalPlayer is not { } p)
                return;

            var manager = FFXIVClientStructs.FFXIV.Client.Game.RetainerManager.Instance();
            if (manager == null || !manager->IsReady)
                return;

            var retainerList = manager->Retainers;
            var info         = new PlayerInfo(p);
            var count        = manager->GetRetainerCount();
            var changes      = false;

            AutoRetainerOfflineCharacterData? autoRetainerData = null;
            if (AutoRetainerIpc.IsReady())
                autoRetainerData = AutoRetainerIpc.GetOfflineCharacterData(Dalamud.PlayerState.ContentId);

            for (byte i = 0; i < count; ++i)
            {
                var data = new RetainerInfo(retainerList[i]);
                if (data.VentureId == 0 && autoRetainerData != null)
                {
                    var supplement = autoRetainerData.RetainerData.FirstOrDefault(r => r.Name == data.Name);
                    if (supplement is { HasVenture: true, VentureEndsAt: > 0 })
                    {
                        data.VentureId = supplement.VentureID;
                        data.Venture   = global::Accountant.Internal.Helpers.DateFromTimeStamp((uint)supplement.VentureEndsAt);
                    }
                }

                changes |= _retainers.AddOrUpdateRetainer(info, data, i);
            }

            for (var i = count; i < RetainerInfo.MaxSlots; ++i)
                changes |= _retainers.ClearRetainer(info, i);

            if (changes)
                _retainers.Save(info);
        }

        private void SyncAllCharactersFromAutoRetainer()
        {
            if (!AutoRetainerIpc.IsReady())
                return;

            foreach (var cid in AutoRetainerIpc.GetRegisteredCharacters())
            {
                var data = AutoRetainerIpc.GetOfflineCharacterData(cid);
                if (data == null)
                    continue;

                var worldId = Accountant.GameData.GetWorldId(data.CurrentWorld);
                if (worldId == 0)
                    continue;

                var player  = new PlayerInfo(data.Name, (ushort)worldId);
                var changed = false;
                foreach (var retainer in data.RetainerData)
                {
                    if (!retainer.HasVenture || retainer.VentureEndsAt <= 0)
                        continue;

                    changed |= _retainers.UpdateVentureFromExternal(player, retainer.Name, retainer.VentureID,
                        global::Accountant.Internal.Helpers.DateFromTimeStamp((uint)retainer.VentureEndsAt));
                }

                if (changed)
                    _retainers.Save(player);

                // Vessel names are frequently duplicated across different companies (e.g. default
                // "Submersible-1..4" names), so we only touch vessel data once we know for certain
                // which company this character belongs to - otherwise we could silently update the
                // wrong company's timers. That mapping is only learned by actually logging into a
                // character while Accountant is running.
                var scopedCompany = _companies.GetCompanyForCharacter(player);
                if (!scopedCompany.HasValue)
                    continue;

                foreach (var airship in data.OfflineAirshipData)
                {
                    if (airship.ReturnTime == 0)
                        continue;

                    var company = _airships.UpdateArrivalFromExternal(scopedCompany, airship.Name, global::Accountant.Internal.Helpers.DateFromTimeStamp(airship.ReturnTime));
                    if (company.HasValue)
                        _airships.Save(company.Value);
                }

                foreach (var submersible in data.OfflineSubmarineData)
                {
                    if (submersible.ReturnTime == 0)
                        continue;

                    var company = _submersibles.UpdateArrivalFromExternal(scopedCompany, submersible.Name, global::Accountant.Internal.Helpers.DateFromTimeStamp(submersible.ReturnTime));
                    if (company.HasValue)
                        _submersibles.Save(company.Value);
                }
            }
        }

        private void OnFrameworkRetainer(IFramework _)
        {
            var now = DateTime.UtcNow;
            if (_nextRetainerCheck <= now)
            {
                UpdateRetainers();
                _nextRetainerCheck = now.AddMilliseconds(5471);
            }

            if (_nextAutoRetainerSync <= now)
            {
                SyncAllCharactersFromAutoRetainer();
                _nextAutoRetainerSync = now.AddSeconds(60);
            }
        }
    }
}
