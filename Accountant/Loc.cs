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
            ClientLanguage.ChineseTraditional => "zh_TW",
            // TC private-server clients (e.g. FFXIVSimpleLauncher) are built on the
            // Simplified Chinese client, so Dalamud reports ChineseSimplified even
            // though the actual text and community expect Traditional Chinese.
            ClientLanguage.ChineseSimplified => "zh_TW",
            _                                => null,
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
