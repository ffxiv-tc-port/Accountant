using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Accountant.Gui.Timer;

public partial class TimerWindow
{
    public partial class BaseCache
    {
        public struct SmallHeader
        {
            public ColorId  Color;
            public DateTime DisplayTime;
            public string   Name;
            public int      ObjectsBegin;
            public int      ObjectsCount;

            public void Draw(BaseCache cache, DateTime now)
            {
                if (ObjectsCount == 0 || Accountant.Config.HideDisabled && Color == ColorId.DisabledText)
                    return;

                // 每幀路徑，直接推／彈（見 TimerWindow.Draw）。文字顏色在展開時提早彈出，
                // textColorPushed 只在推入與那次彈出時改變，finally 據它決定要不要再彈。
                ImGui.PushStyleColor(ImGuiCol.Text, Color.Value());
                var textColorPushed = true;
                try
                {
                    ImGui.PushStyleVar(ImGuiStyleVar.IndentSpacing, ImGui.GetStyle().IndentSpacing / 2);
                    try
                    {
                        ImGui.PushID(Name);
                        try
                        {
                            var posY   = ImGui.GetCursorPosY();
                            var header = ImGui.TreeNodeEx(Name);
                            if (DisplayTime > now && DisplayTime != DateTime.MaxValue)
                            {
                                var s     = TimeSpanString(DisplayTime - now);
                                var width = ImGui.CalcTextSize(s).X;
                                var pos   = ImGui.GetCursorPos();
                                ImGui.SetCursorPos(new Vector2(ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X - width, posY));
                                ImGui.Text(s);
                                ImGui.SetCursorPos(pos);
                            }

                            if (!header)
                                return;

                            ImGui.PopStyleColor();
                            textColorPushed = false;
                            try
                            {
                                var end = ObjectsBegin + ObjectsCount;
                                for (var i = ObjectsBegin; i < end; ++i)
                                {
                                    ImGui.PushID(i);
                                    try
                                    {
                                        cache.Objects[i].Draw(now);
                                    }
                                    finally
                                    {
                                        ImGui.PopID();
                                    }
                                }
                            }
                            finally
                            {
                                ImGui.TreePop();
                            }
                        }
                        finally
                        {
                            ImGui.PopID();
                        }
                    }
                    finally
                    {
                        ImGui.PopStyleVar();
                    }
                }
                finally
                {
                    if (textColorPushed)
                        ImGui.PopStyleColor();
                }
            }
        }
    }
}
