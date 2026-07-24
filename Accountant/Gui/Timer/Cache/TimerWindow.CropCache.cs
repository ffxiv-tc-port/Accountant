using System;
using System.Globalization;
using System.Numerics;
using Accountant.Classes;
using Accountant.Gui.Helper;
using Accountant.Gui.Timer.Cache;
using Accountant.Timers;
using Dalamud.Bindings.ImGui;

namespace Accountant.Gui.Timer;

public partial class TimerWindow
{
    internal sealed partial class CropCache : BaseCache
    {
        // Prefix marking a crop that has not been (successfully) fertilized in over an hour.
        private const string FertilizeReminderMark = "X";

        private readonly PlotCropTimers    _plotCrops;
        private readonly PrivateCropTimers _privateCrops;

        public CropCache(TimerWindow window, ConfigFlags requiredFlags, PlotCropTimers plotCrops, PrivateCropTimers privateCrops)
            : base(Loc.T("Crops"), requiredFlags, window)
        {
            _plotCrops            =  plotCrops;
            _privateCrops         =  privateCrops;
            _plotCrops.Changed    += Resetter;
            _privateCrops.Changed += Resetter;
        }

        protected override void DrawTooltip()
        {
            if (Accountant.Config.ShowCropTooltip)
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(Loc.T("Outdoor crops only refresh every 63 minutes on a ward-specific update timer.\n"
                  + "Any timer may be delayed by up to 63 minutes.\n"
                  + "Fertilizing a plant during these delays will automatically trigger updates.\n"
                  + "They will still grow out/wilt/wither in order, and grown-out plants do not wither anymore.\n"
                  + "Indoors, clearing the house and re-entering should automatically trigger updates.\n"
                  + "You can disable this tooltip in the configuration."));
                ImGui.EndTooltip();
            }
        }

        private static string TimeSpanString2(DateTime target, DateTime now)
        {
            if (target == DateTime.MinValue)
                return Loc.T("Already");
            if (target == DateTime.UnixEpoch)
                return Loc.T("Unknown");
            if (target == DateTime.MaxValue)
                return Loc.T("Never");

            return target < now ? Loc.T("Already") : TimeSpanString(target - now, 3);
        }

        private static Action GenerateTooltip(PlantInfo plant, CacheObject ret, string plantName, DateTime fin, DateTime wilt, DateTime wither)
        {
            var plantTimeString = plant.PlantTime.ToLocalTime().ToString(CultureInfo.CurrentCulture);
            return () =>
            {
                ImGui.BeginTooltip();
                using var _ = ImGuiRaii.PushColor(ImGuiCol.Button, ret.Color.TextToHeader().Value());
                if (Dalamud.GetIcon(ret.Icon, out var icon))
                {
                    ImGui.Image(icon.Handle, Vector2.One * icon.Height / 2);
                    ImGui.SameLine();
                    ImGui.Button(plantName, Vector2.UnitY * icon.Height / 2 - Vector2.UnitX);
                }
                else
                {
                    ImGui.Dummy(new Vector2(ImGui.GetFrameHeightWithSpacing()));
                    ImGui.SameLine();
                    ImGui.Button(plantName, Vector2.UnitY * ImGui.GetFrameHeightWithSpacing() / 2 - Vector2.UnitX);
                }

                var showWilt   = wilt != DateTime.MaxValue;
                var showWither = wither != DateTime.MaxValue;

                ImGui.BeginGroup();
                ImGui.Text(Loc.T("Planted:"));
                ImGui.Text(Loc.T("Tended:"));
                ImGui.Text(Loc.T("Finished:"));
                if (showWilt)
                    ImGui.Text(Loc.T("Wilting:"));
                if (showWither)
                    ImGui.Text(Loc.T("Withering:"));
                if (plant.FertilizeCount > 0)
                    ImGui.Text(Loc.T("Fertilized:"));
                if (plant.Position != Vector3.Zero)
                    ImGui.Text(Loc.T("Position:"));
                ImGui.EndGroup();
                ImGui.SameLine();
                if (!plant.AccuratePlantTime)
                {
                    ImGui.BeginGroup();
                    ImGui.Text("<");
                    ImGui.NewLine();
                    ImGui.Text(fin != DateTime.MaxValue && fin > DateTime.Now ? "<" : "  ");
                    ImGui.EndGroup();
                }
                else
                {
                    ImGui.Text("  ");
                }

                ImGui.SameLine();
                ImGui.BeginGroup();
                ImGui.Text(plantTimeString);
                ImGui.Text(plant.LastTending.ToLocalTime().ToString(CultureInfo.CurrentCulture));
                ImGui.Text(TimeSpanString2(fin, DateTime.UtcNow));
                if (showWilt)
                    ImGui.Text(fin < wilt ? Loc.T("Never") : TimeSpanString2(wilt,     DateTime.UtcNow));
                if (showWither)
                    ImGui.Text(fin < wither ? Loc.T("Never") : TimeSpanString2(wither, DateTime.UtcNow));
                if (plant.FertilizeCount > 0)
                    ImGui.Text(string.Format(Loc.T("{0} times, -{1}"), plant.FertilizeCount, TimeSpanString(plant.FertilizeReduction, 3)));
                if (plant.Position != Vector3.Zero)
                    ImGui.Text(FormattableString.Invariant($"({plant.Position.X:F1}, {plant.Position.Y:F1}, {plant.Position.Z:F1})"));
                ImGui.EndGroup();
                ImGui.EndTooltip();
            };
        }

        protected override void UpdateInternal()
        {
            if (Accountant.Config.OrderByCrop)
                UpdateByCrop();
            else
                UpdateByOwner();

            if (Headers.Exists(h => h.Name.StartsWith(FertilizeReminderMark + " ", StringComparison.Ordinal)))
                DisplayNameOverride = $"{FertilizeReminderMark} {Name}";
        }
    }
}
