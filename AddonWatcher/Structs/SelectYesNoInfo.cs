using System;
using AddonWatcher.Internal;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace AddonWatcher.Structs;

public unsafe struct SelectYesNoInfo
{
    public const int YesButtonId       = 0;
    public const int NoButtonId        = 1;

    public AddonSelectYesno* Pointer;

    public static implicit operator SelectYesNoInfo(IntPtr ptr)
        => new() { Pointer = (AddonSelectYesno*)ptr };

    /// <summary>「是」按鈕上的文字；按鈕還沒建好就回 <see cref="SeString.Empty"/>。</summary>
    /// <remarks>
    /// 🔴 <c>YesButton</c> 是偏移 0x240 的<b>指標欄位</b>，開窗途中／版面未建好時為 <c>null</c>，
    /// 而 <c>ButtonTextNode</c> 在 <c>AtkComponentButton</c> 偏移 0xC8 ——
    /// <c>null->ButtonTextNode</c> 就是去讀位址 0xC8＝AccessViolation。
    /// AVE 在 .NET Core 是 corrupted-state exception，<c>try/catch</c> 攔不到。
    /// 下游的 <see cref="Helpers.TextNodeToString"/> 判空<b>來不及救</b>，因為崩在算參數的那一步。
    /// </remarks>
    public SeString YesText
    {
        get
        {
            var button = Pointer->YesButton;
            return button == null ? SeString.Empty : Helpers.TextNodeToString(button->ButtonTextNode);
        }
    }

    /// <summary>「否」按鈕上的文字；按鈕還沒建好就回 <see cref="SeString.Empty"/>。</summary>
    /// <remarks>成因與防護同 <see cref="YesText"/>（<c>NoButton</c> 在偏移 0x248）。</remarks>
    public SeString NoText
    {
        get
        {
            var button = Pointer->NoButton;
            return button == null ? SeString.Empty : Helpers.TextNodeToString(button->ButtonTextNode);
        }
    }

    public SeString Description
    {
        get
        {
            var node = Pointer->PromptText;
            return node == null ? SeString.Empty : Helpers.TextNodeToString(node);
        }
    }
}
