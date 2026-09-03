using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Accountant.Enums;
using Dalamud.Game;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using OtterLoc;
using OtterLoc.Enums;

namespace Accountant.Internal;

internal static class Localization
{
    private static bool _initialized;

    private static readonly Regex PlantingTextEn =
        new(@"Prepare the bed with (?<soil>.*?) and (a |an )?(?<seeds>.*?)\?", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex PlantingTextFr = new(@"Planter (un |une )?(?<seeds>.*?) avec (?<soil>.*?).\?", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex PlantingTextDe =
        new(@"(?<soil>.*?) verteilen und (einer |einem )?(?<seeds>.*?) aussäen\?", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex PlantingTextJp = new(@"(?<soil>.*?)に(?<seeds>.*?)を植えます。よろしいですか？", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex PlantingTextZh = new(@"將(?<seeds>.*?)種植在(?<soil>.*?)中嗎", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex PatchTextZh = new(@"圃(?<patch>\d).*?壟(?<bed>\d)", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static StringParser PatchParser(ClientLanguage lang)
    {
        // TC(台服)客戶端在 Dalamud 13.0.0.16 之後回報 ClientLanguage 7(TraditionalChinese),
        // 舊版回報 4(ChineseSimplified)。用數值比較才能同時相容 CI 釘的 13.0.0.6(列舉沒有 7 這個名字)與執行期新版。
        if ((int)lang is 4 or 5 or 7)
        {
            IList<string> ZhFunc(string s)
            {
                var m = PatchTextZh.Match(s);
                return m.Success ? new[] { m.Groups["patch"].Value, m.Groups["bed"].Value } : Array.Empty<string>();
            }

            return new StringParser(ZhFunc);
        }

        var (bed, patch) = lang switch
        {
            ClientLanguage.German   => (15, 5),
            ClientLanguage.French   => (23, 8),
            ClientLanguage.Japanese => (1, 4),
            _                       => (0, 9),
        };

        IList<string> Func(string s)
        {
            if (s.Length <= Math.Max(bed, patch))
                return Array.Empty<string>();

            return new[]
            {
                s.Substring(patch, 1),
                s.Substring(bed,   1),
            };
        }

        return new StringParser(Func);
    }

    private static void SetCropCommands(IPluginLog log, IDataManager data)
    {
        var sheet = data.Excel.GetSheet<RawRow>(data.Language.ToLumina(), "custom/001/cmndefhousinggardeningplant_00151");
        var addon = data.Excel.GetSheet<Addon>();

        log.Debug($"[Accountant] Crop command sheet row count: {sheet.Count}");
        for (var i = 0; i <= 11; ++i)
            log.Debug($"[Accountant] Crop command row {i}: '{sheet.GetRow((uint)i).ReadStringColumn(1).ExtractText()}'");

        LocalizationDict<StringId>.RegisterComparer(StringId.HarvestCrop,   sheet.GetRow(6).ReadStringColumn(1).ExtractText());
        LocalizationDict<StringId>.RegisterComparer(StringId.TendCrop,      sheet.GetRow(4).ReadStringColumn(1).ExtractText());
        LocalizationDict<StringId>.RegisterComparer(StringId.FertilizeCrop, sheet.GetRow(3).ReadStringColumn(1).ExtractText());
        LocalizationDict<StringId>.RegisterComparer(StringId.RemoveCrop,    sheet.GetRow(5).ReadStringColumn(1).ExtractText());
        LocalizationDict<StringId>.RegisterComparer(StringId.DisposeCrop, (sheet.GetRow(11).ReadStringColumn(1).ToDalamudString().Payloads[0] as TextPayload)!.Text!,
            MatchType.StartsWith);
        LocalizationDict<StringId>.RegisterComparer(StringId.PlantCrop, sheet.GetRow(2).ReadStringColumn(1).ExtractText());

        // 第 7~10 列是作物狀態台詞。台服 7.20 離線實測(直讀 sqpack 原始 EXD)每列都是兩個 payload:
        // [道具名巨集 0x28][句尾固定文字],依序為「已經枯萎了……」「正茁壯成長。」「的狀態不太好……」「已經成熟了。」。
        // 因此 ^1 取到的正是那段固定文字,而句首會變動的作物名被刻意排除在比對之外。
        var matcher = SeStringMatcher.SinglePayloadComparer(data.Language, sheet.GetRow(7).ReadStringColumn(1).ToDalamudString(), ^1, ^1, ^1, 0);
        LocalizationDict<StringId>.Register(StringId.CropBeyondHope, matcher);
        matcher = SeStringMatcher.SinglePayloadComparer(data.Language, sheet.GetRow(8).ReadStringColumn(1).ToDalamudString(), ^1, ^1, ^1, 0);
        LocalizationDict<StringId>.Register(StringId.CropDoingWell, matcher);
        matcher = SeStringMatcher.SinglePayloadComparer(data.Language, sheet.GetRow(9).ReadStringColumn(1).ToDalamudString(), ^1, ^1, ^1, 0);
        LocalizationDict<StringId>.Register(StringId.CropBetterDays, matcher);
        matcher = SeStringMatcher.SinglePayloadComparer(data.Language, sheet.GetRow(10).ReadStringColumn(1).ToDalamudString(), ^1, ^3, ^1, 0);
        LocalizationDict<StringId>.Register(StringId.CropReady, matcher);
        // ⚠️ CropPrepareBed 這一筆的比對條件永遠不成立,原樣保留只是為了不回退既有行為(它從來沒有被查詢過)。
        // 理由:ExtractText() 已經把巨集攤平成單一字串,再隱式轉成 SeString 只會得到「一個」TextPayload,
        // 於是存下來的位元組是整句攤平文字;但執行期比對的目標是多 payload 的原生字串,索引 0 只拿得到第一段文字。
        // 台服 7.20 實測 Addon#6413 原始位元組是「確定要將<Item>種植在<Item>中嗎？」——
        // ExtractText() 得到「確定要將種植在中嗎？」,而目標的 payload[0] 只有「確定要將」,兩者不可能相等。
        // 這個缺陷與語言無關(英文同理),而且目前無害:全 repo 只有 CropDoingWell / CropBetterDays 會被 Match,
        // 同一個判斷已由 StringId.SeedMatcher(PlantingText* 正規式,含繁中)實際承擔。
        matcher = SeStringMatcher.SinglePayloadComparer(data.Language, addon.GetRow(6413).Text.ExtractText(), 0, 0, ^1, ^1);
        LocalizationDict<StringId>.Register(StringId.CropPrepareBed, matcher);

        LocalizationDict<StringId>.Register(StringId.PlantMatcher, SeStringParser.SpecificPayload(data.Language, 2, 3, 2, 3));
        LocalizationDict<StringId>.Register(StringId.PatchMatcher, PatchParser(data.Language));
        LocalizationDict<StringId>.Register(StringId.SeedMatcher,
            StringParser.FromRegex(data.Language, PlantingTextEn, PlantingTextFr, PlantingTextJp, PlantingTextDe, PlantingTextZh, "seeds", "soil"));
    }

    private static readonly Regex WheelTextEn = new(@"Place (the )?(?<wheel>.*?) on the wheel stand\?", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex WheelTextFr = new(@"Installer (la |le )?(?<wheel>.*?).\?", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex WheelTextDe = new(@"(Das )? (?<wheel>.*?) wirklich in den Ätherrad-Ständer einsetzen\?",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex WheelTextJp = new(@"「(<?wheel>.*?)」を.*ホイールスタンドに設置します。.*よろしいですか？", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

    private static readonly Regex WheelTextZh = new(@"要將「(?<wheel>.*?)」設置到轉輪台上嗎", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex JumboTextEn = new(@"number\s+(?<ticket>\d{4})", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
    private static readonly Regex JumboTextFr = new(@"(?<ticket>\d{4})\s+pour", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex JumboTextDe = new(@"Nummer\s+(?<ticket>\d{4})",
        RegexOptions.Compiled);

    private static readonly Regex JumboTextJp = new(@"(?<ticket>\d{4})番を", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex JumboTextZh = new(@"購買(?<ticket>\d{4})號", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    public static void Initialize(IPluginLog log, IDataManager data)
    {
        if (_initialized)
            return;

        _initialized = true;
        var territories    = data.Excel.GetSheet<TerritoryType>();
        var names          = data.Excel.GetSheet<PlaceName>();
        var addon          = data.Excel.GetSheet<Addon>();
        var goldSaucerTalk = data.Excel.GetSheet<RawRow>(data.Language.ToLumina(), "goldsaucertalk");

        var territory = territories.GetRow((uint)HousingZone.Mist);
        var name      = names.GetRow(territory.PlaceName.RowId);
        LocalizationDict<StringId>.Register(StringId.Mist, name.Name.ExtractText());

        territory = territories.GetRow((uint)HousingZone.LavenderBeds);
        name      = names.GetRow(territory.PlaceName.RowId);
        LocalizationDict<StringId>.Register(StringId.LavenderBeds, name.Name.ExtractText());

        territory = territories.GetRow((uint)HousingZone.Goblet);
        name      = names.GetRow(territory.PlaceName.RowId);
        LocalizationDict<StringId>.Register(StringId.Goblet, name.Name.ExtractText());

        territory = territories.GetRow((uint)HousingZone.Shirogane);
        name      = names.GetRow(territory.PlaceName.RowId);
        LocalizationDict<StringId>.Register(StringId.Shirogane, name.Name.ExtractText());

        territory = territories.GetRow((uint)HousingZone.Empyreum);
        name      = names.GetRow(territory.PlaceName.RowId);
        LocalizationDict<StringId>.Register(StringId.Empyreum, name.Name.ExtractText());

        LocalizationDict<StringId>.RegisterName(StringId.Unknown, data.Language, "Unknown", "不明", "Unbekannt", "Inconnu", "不明");

        LocalizationDict<StringId>.RegisterName(StringId.CropPatch, data.Language, "Patch",      "畑",     "Beet",       "Potager", "圃");
        LocalizationDict<StringId>.RegisterName(StringId.CropPot,   data.Language, "Flower Pot", "プランター", "Blumentopf", "Pot de Fleurs", "花盆");
        LocalizationDict<StringId>.RegisterName(StringId.CropBed,   data.Language, "Bed",        "の畝",    "Furche",     "Emplacement", "園圃");

        LocalizationDict<StringId>.RegisterName(StringId.Cottage,   data.Language, "Cottage",   "コテージ",  "Hütte",         "Maisonnette", "小型房屋");
        LocalizationDict<StringId>.RegisterName(StringId.House,     data.Language, "House",     "ハウス",   "Haus",          "Pavillon", "中型房屋");
        LocalizationDict<StringId>.RegisterName(StringId.Mansion,   data.Language, "Mansion",   "レジデンス", "Residenz",      "Villa", "大型房屋");
        LocalizationDict<StringId>.RegisterName(StringId.Apartment, data.Language, "Apartment", "部屋",    "Wohnung",       "Appartement", "公寓");
        LocalizationDict<StringId>.RegisterName(StringId.Chambers,  data.Language, "Chambers",  "ルーム",   "Zimmer",        "Chambre", "房間");
        LocalizationDict<StringId>.RegisterName(StringId.Completed, data.Language, "Completed", "完成",    "Abgeschlossen", "Complété", "完成");
        LocalizationDict<StringId>.RegisterName(StringId.Available, data.Language, "Available", "利用可能",  "Verfügbar",     "Disponible", "可利用");
        LocalizationDict<StringId>.RegisterName(StringId.Machines,  data.Language, "Machines",  "マシン",   "Maschinen",     "Machines", "航行器");
        LocalizationDict<StringId>.RegisterName(StringId.Retainers, data.Language, "Retainers", "リテイナー", "Gehilfen",      "Servants", "僱員");

        LocalizationDict<StringId>.RegisterName(StringId.Airship, data.Language, "Airship", "飛行船", "Luftschiff", "Aéronef", "飛空艇");
        LocalizationDict<StringId>.Register(StringId.Submersible,
            addon.GetRow(data.Language == ClientLanguage.Japanese ? 6881u : 6888u).Text.ExtractText());
        LocalizationDict<StringId>.Register(StringId.Retainer, addon.GetRow(6163).Text.ExtractText());

        LocalizationDict<StringId>.Register(StringId.WheelFilter,
            StringParser.FromRegex(data.Language, WheelTextEn, WheelTextFr, WheelTextJp, WheelTextDe, WheelTextZh, "wheel"));

        LocalizationDict<StringId>.Register(StringId.BuyMiniCactpotTicket,
            new StringMatcherLetters(goldSaucerTalk.GetRow(16).ReadStringColumn(17).ExtractText()));
        LocalizationDict<StringId>.Register(StringId.BuyJumboCactpotTicket,
            new StringMatcherLetters(addon.GetRow(9276).Text.ExtractText()));
        LocalizationDict<StringId>.Register(StringId.FilterJumboCactpotTicket,
            StringParser.FromRegex(data.Language, JumboTextEn, JumboTextFr, JumboTextJp, JumboTextDe, JumboTextZh, "ticket"));
        SetCropCommands(log, data);
    }
}
