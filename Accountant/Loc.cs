using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Dalamud.Game;
using Newtonsoft.Json;

namespace Accountant;

// Lightweight UI localization: Loc.T(key, fallback) looks up key in the loaded
// language dictionary and falls back to the English text if missing or unloaded.
public static class Loc
{
    private static Dictionary<string, string>? _strings;

    public static bool IsLoaded
        => _strings != null;

    public static void Load(ClientLanguage language)
    {
        var code = language switch
        {
            // TC(台服)客戶端在 Dalamud 13.0.0.16 之後回報 ClientLanguage 7(TraditionalChinese),
            // 舊版回報 4(ChineseSimplified)。用數值比較才能同時相容 CI 釘的 13.0.0.6(列舉沒有 7 這個名字)與執行期新版。
            (ClientLanguage)4 or (ClientLanguage)5 or (ClientLanguage)7 => "zh_TW",
            _                                                           => null,
        };

        if (code == null)
        {
            _strings = null;
            return;
        }

        var assembly    = Assembly.GetExecutingAssembly();
        var resourceName = $"Accountant.loc.{code}.json";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            _strings = null;
            return;
        }

        using var reader = new StreamReader(stream);
        var       json   = reader.ReadToEnd();
        _strings = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
    }

    public static string T(string fallback)
        => _strings != null && _strings.TryGetValue(fallback, out var translated) ? translated : fallback;
}
