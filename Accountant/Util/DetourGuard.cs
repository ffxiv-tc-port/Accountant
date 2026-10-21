using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Accountant.Util;

// detour 是遊戲原生碼呼叫進來的：受管理例外穿出去＝整個遊戲崩潰。detour 裡的插件邏輯一律攔下，遊戲原函式照常呼叫。
// 每個功能只在第一次出事時記一行 Error、在聊天說一次；可從任何執行緒呼叫（聊天轉到框架執行緒）。
// 出事之後該功能的 detour 以 IsPaused 跳過插件邏輯、只呼叫原函式（聊天說的「暫停記錄」）。
internal static class DetourGuard
{
    public static bool IsPaused(ref int reported) => Volatile.Read(ref reported) != 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ReportOnce(ref int reported, string feature, Exception ex)
    {
        if (Interlocked.Exchange(ref reported, 1) != 0)
            return;

        try
        {
            Dalamud.Log.Error(ex, $"[改版防護] {feature}的處理擲出例外，已攔下；遊戲原本的流程照常，之後同一項不再記錄。");
            _ = Dalamud.Framework.Run(() => Dalamud.Chat.PrintError(
                $"[Accountant] {feature}出錯，暫停記錄（遊戲本身照常）。若遊戲剛改版，請等插件更新。"));
        }
        catch
        {
            // 回報本身也不可以把例外送回原生框架。
        }
    }
}
