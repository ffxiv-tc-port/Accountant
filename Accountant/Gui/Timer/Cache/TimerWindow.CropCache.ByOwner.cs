using System;
using System.Collections.Generic;
using System.Linq;
using Accountant.Classes;
using Accountant.Enums;
using Accountant.Gui.Timer.Cache;
using OtterLoc.Structs;

namespace Accountant.Gui.Timer;

public partial class TimerWindow
{
    internal sealed partial class CropCache
    {
        private CacheObject GeneratePlant(PlantInfo plant, string name)
        {
            var ret = new CacheObject
            {
                Name       = plant.NeedsFertilizeReminder(Now) ? $"{FertilizeReminderMark} {name}" : name,
                IconOffset = 0,
            };

            if (plant.Active())
            {
                var (data, plantName)                   = Accountant.GameData.FindCrop(plant.PlantId);
                var (fin, wilt, wither, color, time, _) = plant.GetCropTimes(Now);
                ret.DisplayTime                         = UpdateNextChange(time);
                ret.DisplayString                       = time < Now ? string.Empty : null;
                ret.Icon                                = data.Item.Icon;
                ret.Color                               = color;
                ret.TooltipCallback                     = GenerateTooltip(plant, ret, plantName, fin, wilt, wither);
            }
            else
            {
                ret.Color         = ColorId.NeutralText;
                ret.DisplayString = StringId.Available.Value();
                ret.Icon          = Icons.PottingSoilIcon;
            }

            return ret;
        }

        private static void FinalizeOwnerName(ref SmallHeader owner, string baseName, List<CacheObject> children)
        {
            var needsReminder = children.Any(c => c.Name.StartsWith(FertilizeReminderMark + " ", StringComparison.Ordinal));
            owner.Name = needsReminder ? $"{FertilizeReminderMark} {baseName}###{baseName}" : baseName;
        }

        private SmallHeader GenerateOwner(PlayerInfo player, IList<PlantInfo> plants)
        {
            var baseName = GetName(player.Name, player.ServerId);
            var owner = new SmallHeader
            {
                Name         = baseName,
                ObjectsBegin = Objects.Count,
                ObjectsCount = plants.Count,
                Color        = ColorId.NeutralText,
            };

            var children = new List<CacheObject>();
            for (ushort i = 0; i < plants.Count; ++i)
            {
                if (!Accountant.Config.BlockedCrops.Contains(plants[i].PlantId))
                {
                    Objects.Add(GeneratePlant(plants[i], PlantInfo.GetPrivateName(i)));
                    children.Add(Objects.Last());
                    UpdateParent(Objects.Last().Color, Objects.Last().DisplayTime, ref owner.Color, ref owner.DisplayTime);
                }
            }

            FinalizeOwnerName(ref owner, baseName, children);
            UpdateParent(owner.Color.TextToHeader(), owner.DisplayTime, ref Color, ref DisplayTime);
            return owner;
        }

        private SmallHeader GenerateOwner(PlotInfo plot, IList<PlantInfo> plants)
        {
            var baseName = GetName(plot.Name, plot.ServerId);
            var owner = new SmallHeader
            {
                Name         = baseName,
                ObjectsBegin = Objects.Count,
                Color        = ColorId.NeutralText,
            };

            var plotSize = Accountant.GameData.GetPlotSize(plot.Zone, plot.Plot);
            var count    = plants.Count;
            if (Accountant.Config.IgnoreIndoorPlants)
                count -= plotSize.IndoorBeds();
            var objects  = 0;
            var children = new List<CacheObject>();
            for (ushort i = 0; i < count; ++i)
            {
                if (!Accountant.Config.BlockedCrops.Contains(plants[i].PlantId))
                {
                    ++objects;
                    Objects.Add(GeneratePlant(plants[i], PlantInfo.GetPlotName(plotSize, i)));
                    children.Add(Objects.Last());
                    UpdateParent(Objects.Last().Color, Objects.Last().DisplayTime, ref owner.Color, ref owner.DisplayTime);
                }
            }

            owner.ObjectsCount = objects;

            FinalizeOwnerName(ref owner, baseName, children);
            UpdateParent(owner.Color.TextToHeader(), owner.DisplayTime, ref Color, ref DisplayTime);

            return owner;
        }

        private static void UpdateParent(ColorId newColor, DateTime displayTime, ref ColorId oldColor, ref DateTime oldDisplayTime)
        {
            var tmp = oldColor.Combine(newColor);
            if (tmp != oldColor)
            {
                oldColor       = newColor;
                oldDisplayTime = displayTime;
            }
            else if (newColor == oldColor && oldDisplayTime > displayTime)
            {
                oldDisplayTime = displayTime;
            }
        }

        private void UpdateByOwner()
        {
            foreach (var (plot, plants) in _plotCrops.Data
                         .Where(p => !Accountant.Config.BlockedPlots.Contains(p.Key.Value)))
                Headers.Add(GenerateOwner(plot, plants));

            foreach (var (player, plants) in _privateCrops.Data
                         .Where(p => !Accountant.Config.BlockedPlayersCrops.Contains(p.Key.CastedName)))
                Headers.Add(GenerateOwner(player, plants));

            if (Accountant.Config.Priorities.Count > 0)
                Headers.Sort((a, b) => Accountant.Config.GetPriority(NameForPriority(b.Name)).CompareTo(Accountant.Config.GetPriority(NameForPriority(a.Name))));
        }

        private static string NameForPriority(string headerName)
        {
            var idPos = headerName.IndexOf("###", StringComparison.Ordinal);
            if (idPos >= 0)
                return headerName[(idPos + 3)..];

            return headerName.StartsWith(FertilizeReminderMark + " ", StringComparison.Ordinal)
                ? headerName[(FertilizeReminderMark.Length + 1)..]
                : headerName;
        }
    }
}
