using System;
using System.Linq;
using Dalamud.Game;
using Dalamud.Game.Text.SeStringHandling;

namespace OtterLoc;

public sealed class SeStringMatcher : ILocMatcher
{
    private readonly Func<SeString, bool> _func;

    public SeStringMatcher(Func<SeString, bool> func)
        => _func = func;

    public string Name
        => "Func";

    public bool Matches(string s)
        => throw new NotImplementedException();

    public bool Matches(SeString s)
        => _func(s);

    /// <summary>
    /// 產生一個「只比對單一 payload」的比對器:取語言別指定的那個索引,其餘 payload 一律忽略。
    /// </summary>
    /// <remarks>
    /// 只看一個 payload 是刻意的,不是偷懶:目標字串的其他 payload 帶的是會變動的內容
    /// (作物/道具名稱的連結 payload 與其結尾標記),拿來比對必然對不上。
    /// ⚠️ 代價:兩個「在該索引上的 payload 相同、其他位置不同」的字串會被判為相等。
    /// 目前唯一真正被消費的兩個項目是 CropDoingWell 與 CropBetterDays
    /// (Accountant/Manager/TimerManager.CropManager.cs 的 CheckPlant),兩者都取 ^1 也就是句尾固定文字。
    /// 台服 7.20 離線實測 custom/001/CmnDefHousingGardeningPlant_00151:
    /// 第 8、9 列各只有兩個 payload([道具名巨集][句尾文字]),句尾文字分別是
    /// 「正茁壯成長。」與「的狀態不太好……」,彼此不同,所以在目前的用法下不會互相誤判。
    /// </remarks>
    public static SeStringMatcher SinglePayloadComparer(ClientLanguage lang, SeString s, Index idxEn, Index idxFr, Index idxJp, Index idxDe)
    {
        var idx = lang switch
        {
            ClientLanguage.Japanese => idxJp,
            ClientLanguage.German   => idxDe,
            ClientLanguage.French   => idxFr,
            ClientLanguage.English  => idxEn,
            // 台服沒有自己的索引參數,落在這裡沿用 idxEn。
            // TC(台服)客戶端在 Dalamud 13.0.0.16 之後回報 ClientLanguage 7(TraditionalChinese),舊版回報 4(ChineseSimplified)。
            // 台服 7.20 實測:上面 remarks 提到的兩列與英文同構(句尾才是固定文字),idxEn 的 ^1 正好取到它,
            // 所以這個 fallback 對現有呼叫點是對的 —— 但那是逐列驗過的結論,不是通則,新增呼叫點要重新驗。
            _                       => idxEn,
        };

        // 原本直接寫 s.Payloads[idx]。索引越界時 List 的索引子只會擲出不帶上下文的 ArgumentOutOfRangeException,
        // 而這整段跑在 Localization.Initialize(外掛載入路徑)上,台服 sheet 的 payload 結構一旦與國際服不同就會變成
        // 一則查不出來源的載入失敗。改成先自己驗界,把語言、索引、實際 payload 數與字串內容一起帶進訊息。
        var count    = s.Payloads.Count;
        var srcIndex = idx.IsFromEnd ? count - idx.Value : idx.Value;
        if (srcIndex < 0 || srcIndex >= count)
            throw new ArgumentOutOfRangeException(nameof(idx), idx,
                $"SinglePayloadComparer: 語言 {lang} 取索引 {idx},但來源 SeString 只有 {count} 個 payload:\"{s.TextValue}\"");

        var payload = s.Payloads[srcIndex];
        var bytes   = payload.Encode().Where(c => c != '\r' && c != '\n').ToArray();
        var type    = payload.Type;

        bool Func(SeString x)
        {
            var index = (uint)(idx.IsFromEnd ? x.Payloads.Count - idx.Value : idx.Value);
            if (index >= x.Payloads.Count)
                return false;

            var p = x.Payloads[(int)index];
            return p.Type == type && bytes.SequenceEqual(p.Encode().Where(c => c != '\r' && c != '\n'));
        }

        return new SeStringMatcher(Func);
    }
}
