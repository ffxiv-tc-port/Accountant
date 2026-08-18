using System;
using AddonWatcher.Internal;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AddonWatcher.Structs;

public unsafe struct SelectStringInfo
{
    public const int DescriptionNodeIdx = 3;

    public AddonSelectString* Pointer;

    public static implicit operator SelectStringInfo(IntPtr ptr)
        => new() { Pointer = (AddonSelectString*)ptr };

    /// <summary>第一個選項的文字在 <c>AtkValues</c> 裡的索引。</summary>
    public const int FirstItemValueIdx = 7;

    public AtkComponentList* List
    {
        get
        {
            if (Pointer == null)
                return null;

            return Pointer->PopupMenu.PopupMenu.List;
        }
    }

    /// <summary>選項筆數；清單還沒建好（或已經拆掉）就回 0。</summary>
    public int Count
    {
        get
        {
            var list = List;
            if (list == null)
                return 0;

            return list->ListLength;
        }
    }

    /// <summary>
    /// 第 <paramref name="idx"/> 個選項的文字；讀不到就回 <see cref="SeString.Empty"/>。
    /// </summary>
    /// <remarks>
    /// 🔴 <c>AtkUnitBase.AtkValuesSpan</c> 的實作是
    /// <c>new Span&lt;AtkValue&gt;(AtkValues, AtkValuesCount)</c>，
    /// <b>它自己完全不判 <c>AtkValues</c> 這個欄位是不是 null</b>，而 <c>Span</c> 的建構子也不驗指標。
    /// 所以「<c>AtkValues</c> 已經被釋放成 null、<c>AtkValuesCount</c> 還留著上一輪殘值」這個組合
    /// 會<b>合法建構出一個長度非零的 Span</b>，連 Span 自己的邊界檢查都會放行 ——
    /// 一直到真的索引下去才對位址 0 解參考 ＝ AccessViolationException。
    /// AVE 在 .NET Core 是 corrupted-state exception，<c>try/catch</c> 攔不到，整個遊戲行程直接死。
    /// ⇒ 判空只能做在<b>消費端</b>（也就是這裡），不要去改 ClientStructs。
    /// </remarks>
    public SeString ItemText(int idx)
    {
        if (Pointer == null || Pointer->AtkValues == null)
            return SeString.Empty;

        var values = Pointer->AtkValuesSpan;
        var index  = FirstItemValueIdx + idx;
        if (index < 0 || index >= values.Length)
            return SeString.Empty;

        return values[index].String.AsDalamudSeString();
    }

    public SeString Description
    {
        get
        {
            var count = Pointer->AtkUnitBase.UldManager.NodeListCount;
            if (DescriptionNodeIdx >= count)
                return SeString.Empty;

            var node = Pointer->AtkUnitBase.UldManager.NodeList[DescriptionNodeIdx];
            if (node == null)
                return SeString.Empty;

            return Helpers.TextNodeToString((AtkTextNode*)node);
        }
    }
}
