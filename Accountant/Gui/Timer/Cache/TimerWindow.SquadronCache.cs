using System;
using System.Linq;
using Accountant.Classes;
using Accountant.Enums;
using Accountant.Gui.Timer.Cache;
using Accountant.Gui.Helper;
using Accountant.Timers;
using OtterLoc.Structs;

namespace Accountant.Gui.Timer;

public partial class TimerWindow
{
    internal sealed class SquadronCache : BaseCache
    {
        private readonly TaskTimers _tasks;

        public SquadronCache(TimerWindow window, ConfigFlags requiredFlags, TaskTimers tasks)
            : base(Loc.T("Squadron Timer"), requiredFlags, window)
        {
            _tasks         =  tasks;
            _tasks.Changed += Resetter;
        }

        private CacheObject SquadronObject(string player, Squadron info)
        {
            var ret = new CacheObject
            {
                Name        = player,
                IconOffset  = 0,
                Icon        = Icons.SquadronIcon,
                DisplayTime = UpdateNextChange(info.MissionEnd),
            };

            if (info.MissionId == 0)
            {
                ret.DisplayString = StringId.Available.Value();
                ret.Color         = ColorId.NeutralText;
            }
            else if (info.MissionEnd < Now)
            {
                ret.DisplayString = StringId.Completed.Value();
                ret.Color         = ColorId.TextObjectsHome;
            }
            else
            {
                ret.DisplayString = null;
                ret.Color         = ColorId.TextObjectsAway;
            }

            return ret;
        }

        protected override void UpdateInternal()
        {
            DisplayTime = DateTime.MaxValue;
            var rawColor = ColorId.NeutralText;

            var data = _tasks.Data
                .Where(p => !Accountant.Config.BlockedPlayersTasks.Contains(p.Key.CastedName))
                .Select(p => (GetName(p.Key.Name, p.Key.ServerId), p.Value))
                .OrderByDescending(p => Accountant.Config.GetPriority(p.Item1))
                .ToArray();

            foreach (var (name, task) in data)
            {
                Objects.Add(SquadronObject(name, task.Squadron));
                var obj = Objects.Last();
                if (obj.DisplayTime > Now && obj.DisplayTime < DisplayTime)
                    DisplayTime = obj.DisplayTime;
                rawColor = obj.Color switch
                {
                    ColorId.TextObjectsAway => rawColor == ColorId.TextObjectsHome ? ColorId.TextObjectsMixed : ColorId.TextObjectsAway,
                    ColorId.TextObjectsHome => rawColor == ColorId.TextObjectsAway ? ColorId.TextObjectsMixed : ColorId.TextObjectsHome,
                    _                       => rawColor,
                };
            }

            Color = rawColor.TextToHeader();
        }

        protected override void DrawBody(DateTime now)
        {
            for (var i = 0; i < Objects.Count; ++i)
            {
                using var id = ImGuiRaii.PushId(i);
                Objects[i].Draw(now);
            }
        }
    }
}
