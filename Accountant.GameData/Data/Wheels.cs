using System.Collections.Generic;
using System.Text.RegularExpressions;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Lumina.Excel.Sheets;

namespace Accountant.Data;

public class Wheels
{
    private const int NumWheels = 45;

    private readonly Dictionary<uint, (Item Item, string Name, byte Grade)>   _idToItem   = new(NumWheels);
    private readonly Dictionary<string, (Item Item, string Name, byte Grade)> _nameToItem = new(NumWheels);

    internal (Item, string Name, byte Grade) Find(uint itemId)
        => _idToItem.TryGetValue(itemId, out var wheel) ? wheel : (new Item(), string.Empty, (byte)0);

    internal (Item, string Name, byte Grade) Find(string name)
        => _nameToItem.TryGetValue(name.ToLowerInvariant(), out var wheel) ? wheel : (new Item(), string.Empty, (byte)0);

    private static readonly Regex WheelRegex = new(@"^grade (?<grade>\d) wheel of", RegexOptions.Compiled);
    private static readonly Regex PrimedWheelRegex = new(@"^primed grade (?<grade>\d) wheel of", RegexOptions.Compiled);

    // 台服(TC)注意:我們的 Dalamud fork 的 Lumina 會把指定語言的 Excel 請求靜默改成 client 語言,
    // 下面以 ClientLanguage.English 取得的 Item 表實際上是繁中資料,英文 regex 永遠不中。
    // 台服命名(7.20 dump 實查,基礎/充能各 49 顆、一一對應):
    //   未充能=「N級○○乙太轉輪」、充能完畢=「N級○○充電轉輪」(N=1~3)。
    private static readonly Regex WheelRegexTC       = new(@"^(?<grade>\d)級.+乙太轉輪$", RegexOptions.Compiled);
    private static readonly Regex PrimedWheelRegexTC = new(@"^(?<grade>\d)級.+充電轉輪$", RegexOptions.Compiled);

    internal Wheels(IDataManager gameData)
    {
        var items        = gameData.GetExcelSheet<Item>(ClientLanguage.English);
        var itemsLang    = gameData.GetExcelSheet<Item>();
        var primedWheels = new List<(string, uint)>(50);
        var englishDict  = new Dictionary<string, (Item Item, string Name, byte Grade)>(50);
        foreach (var item in items)
        {
            var englishName  = item.Name.ExtractText().ToLowerInvariant(); // 台服上實際是繁中名(小寫化對 CJK 無作用)
            var match = WheelRegex.Match(englishName);
            if (!match.Success)
                match = WheelRegexTC.Match(englishName);
            if (!match.Success)
            {
                match = PrimedWheelRegex.Match(englishName);
                if (match.Success)
                {
                    primedWheels.Add((englishName.Replace("primed ", ""), item.RowId));
                    continue;
                }

                match = PrimedWheelRegexTC.Match(englishName);
                if (match.Success)
                    primedWheels.Add((englishName.Replace("充電轉輪", "乙太轉輪"), item.RowId));
                continue;
            }

            var grade    = (byte)(match.Groups["grade"].Value[0] - '0');
            var itemLang = itemsLang.GetRow(item.RowId);
            var name     = itemLang.Name.ToDalamudString().TextValue;
            var singular = itemLang.Name.ToDalamudString().TextValue.ToLowerInvariant();
            _idToItem.TryAdd(itemLang.RowId, (itemLang, name, grade));
            _nameToItem.TryAdd(name.ToLowerInvariant(), (itemLang, name, grade));
            _nameToItem.TryAdd(singular,                (itemLang, name, grade));
            englishDict.TryAdd(englishName,             (itemLang, name, grade));
        }

        foreach (var (englishName, primedWheel) in primedWheels)
        {
            var itemLang = itemsLang.GetRow(primedWheel);
            var fullName = itemLang.Name.ToDalamudString().TextValue.ToLowerInvariant();
            var singular = itemLang.Name.ToDalamudString().TextValue.ToLowerInvariant();
            if (!englishDict.TryGetValue(englishName, out var data))
                continue;
            _nameToItem.TryAdd(fullName, data);
            _nameToItem.TryAdd(singular, data);
        }
    }
}
