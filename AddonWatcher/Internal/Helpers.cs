using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AddonWatcher.Internal;

public static class Helpers
{
    /// <summary>
    /// 讀取文字節點的內容；節點取不到（<paramref name="node"/> 為 <c>null</c>）或不是文字節點時回
    /// <see cref="SeString.Empty"/>。
    /// </summary>
    /// <remarks>
    /// 🔴 呼叫端拿節點的三種方式（<c>UldManager.NodeList[idx]</c>、addon 的指標欄位、
    /// <c>GetTextNodeById()</c>）<b>都合法回 <c>null</c></b>，所以判空的責任在這裡收斂。
    /// 這個判空擋的是兩件事：
    /// <list type="number">
    /// <item><c>node->AtkResNode.Type</c> 本身就是對 <paramref name="node"/> 的解參考，
    /// 節點為 <c>null</c> 時當場 AccessViolation；AVE 在 .NET Core 是 corrupted-state exception，
    /// <c>try/catch</c> 完全攔不到，整個遊戲行程直接死。</item>
    /// <item>更陰的是 <c>&amp;node->NodeText</c>：<c>NodeText</c> 位在 <c>AtkTextNode</c> 偏移 0xC0，
    /// 節點為 <c>null</c> 時<b>不會當場崩</b>，而是靜默算出毒指標 0xC0 ——
    /// 那個值連 <c>MemoryHelper.ReadSeString</c> 內部的 <c>!= null</c> 判空都騙得過去，
    /// 一路到真的去讀位址 0xC0 才炸，崩潰現場完全指不到真因。</item>
    /// </list>
    /// 這是讀取型存取子，取不到就安靜回空字串、不寫 log（呼叫端含每幀的 Talk 更新路徑）。
    /// </remarks>
    public static unsafe SeString TextNodeToString(AtkTextNode* node)
        => node != null && node->AtkResNode.Type == NodeType.Text
            ? MemoryHelper.ReadSeString(&node->NodeText)
            : SeString.Empty;

    /// <summary>
    /// 從 <paramref name="uld"/> 的 <c>NodeList</c> 取第 <paramref name="index"/> 個節點；
    /// 取不到回 <c>null</c>。
    /// </summary>
    /// <remarks>
    /// 🔴 <c>NodeList</c> 要驗的是<b>三件事</b>，這個 repo 原本只做到後兩件：
    /// <list type="number">
    /// <item><b><c>NodeList</c> 這個指標欄位本身</b>：版面拆除時它會先被釋放並設成
    /// <c>null</c>，而 <c>NodeListCount</c> 不保證同時歸零。那一瞬間 <c>NodeList[i]</c>
    /// 讀的是位址 <c>i*8</c>，不是 <c>null</c> —— 上界檢查與元素判空<b>兩關都會放行</b>，
    /// 拿到的是垃圾指標。</item>
    /// <item><b>上界</b>：版面還在建的時候 <c>NodeListCount</c> 可能小於索引，
    /// 越界讀到的是<b>相鄰記憶體而不是 <c>null</c></b>，元素判空擋不住。</item>
    /// <item><b>元素本身</b>：即使索引在範圍內，<c>NodeList[i]</c> 仍可能是 <c>null</c>。</item>
    /// </list>
    /// 三件事一起做才是完整的守衛；少做任何一件，失敗形式都一樣是靜默拿到垃圾指標。
    /// </remarks>
    public static unsafe AtkResNode* GetNodeSafe(AtkUldManager* uld, int index)
    {
        if (uld == null || uld->NodeList == null)
            return null;
        if (index < 0 || index >= uld->NodeListCount)
            return null;

        return uld->NodeList[index];
    }
}
