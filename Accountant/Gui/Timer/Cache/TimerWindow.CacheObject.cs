using System;
using System.Numerics;
using Accountant.Gui.Helper;
using Dalamud.Interface.Textures;
using Dalamud.Bindings.ImGui;

namespace Accountant.Gui.Timer.Cache;

public struct CacheObject
{
    public string   Name;
    public DateTime DisplayTime;
    public string?  DisplayString;
    public uint     Icon;
    public float    IconOffset;
    public ColorId  Color;
    public Action?  TooltipCallback;

    private void DrawIcon()
    {
        if (!Dalamud.GetIcon(Icon, out var icon))
            return;

        if (IconOffset == 0)
        {
            ImGui.Image(icon.Handle, Vector2.One * ImGui.GetTextLineHeight());
        }
        else
        {
            var offset = Vector2.One * IconOffset;
            var size   = Vector2.One - offset;
            ImGui.Image(icon.Handle, Vector2.One * ImGui.GetTextLineHeight(), offset, size);
        }

        ImGui.SameLine(0, ImGui.GetStyle().ItemSpacing.X / 2);
    }

    public void Draw(DateTime now)
    {
        if (Accountant.Config.HideDisabled && Color == ColorId.DisabledText)
            return;

        using var color = ImGuiRaii.PushColor(ImGuiCol.Text, Color.Value());
        DrawIcon();
        ImGui.Selectable(Name, false, ImGuiSelectableFlags.None, new Vector2(0, ImGui.GetTextLineHeight()));
        var tooltip = ImGui.IsItemHovered();

        if (DisplayString != null)
        {
            var width = ImGui.CalcTextSize(DisplayString).X;
            ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X - width);
            ImGui.Text(DisplayString);
        }
        else if (DisplayTime > now)
        {
            var display = TimerWindow.TimeSpanString(DisplayTime - now);
            var width   = ImGui.CalcTextSize(display).X;
            ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X - width);
            ImGui.Text(display);
        }

        color.Pop();
        if (tooltip)
            TooltipCallback?.Invoke();
    }
}
