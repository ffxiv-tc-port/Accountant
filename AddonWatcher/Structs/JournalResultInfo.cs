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
    /// 🔴 <c>NodeList</c> 要驗的是<b>三件事</b>，缺一都是靜默失敗；原本只做了後兩件，
    /// 現在整組交給 <see cref="Helpers.GetNodeSafe"/>（那裡有完整說明）：
    /// <list type="number">
    /// <item><c>NodeList</c> <b>這個指標欄位本身</b> —— 版面拆除時它先被釋放設成 <c>null</c>，
    /// 而 <c>NodeListCount</c> 不保證同時歸零，下面兩關都會放行。</item>
    /// <item><c>NodeListCount</c> 上界 —— 版面還在建的時候 count 可能小於
    /// <see cref="QuestNameNodeIdx"/>，越界讀到的是相鄰記憶體<b>而不是 <c>null</c></b>，
    /// 判空完全擋不住（半套邊界檢查）。</item>
    /// <item>元素本身可為 <c>null</c>。</item>
    /// </list>
    /// 三關都過之後才交給 <see cref="Helpers.TextNodeToString"/>（它自己也判空，這裡是不倚賴它）。
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
