using System;
using AddonWatcher.Internal;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AddonWatcher.Structs;

public unsafe struct JournalResultInfo
{
    public const int CompleteButtonId = 1;
    public const int QuestNameNodeIdx = 11;

    public AddonJournalResult* Pointer;

    public static implicit operator JournalResultInfo(IntPtr ptr)
        => new() { Pointer = (AddonJournalResult*)ptr };

    /// <summary>任務名稱；版面還沒建好（或已經拆掉）就回 <see cref="SeString.Empty"/>。</summary>
    /// <remarks>
    /// <c>NodeList</c> 的三件事（指標欄位本身、<c>NodeListCount</c> 上界、元素判空）整組交給 <see cref="Helpers.GetNodeSafe"/>（那裡有完整說明）；
    /// 三關都過之後才交給 <see cref="Helpers.TextNodeToString"/>（它自己也判空，這裡不倚賴它）。
    /// </remarks>
    public SeString QuestName
    {
        get
        {
            if (Pointer == null)
                return SeString.Empty;

            var node = Helpers.GetNodeSafe(&Pointer->AtkUnitBase.UldManager, QuestNameNodeIdx);
            if (node == null)
                return SeString.Empty;

            return Helpers.TextNodeToString((AtkTextNode*)node);
        }
    }
}
