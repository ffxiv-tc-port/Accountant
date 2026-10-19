using System.Diagnostics.CodeAnalysis;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace Accountant;

public class Dalamud
{
    public static void Initialize(IDalamudPluginInterface pluginInterface)
        => pluginInterface.Create<Dalamud>();

        // @formatter:off
        [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] public static ICommandManager         Commands        { get; private set; } = null!;
        [PluginService] public static ISigScanner             SigScanner      { get; private set; } = null!;
        [PluginService] public static IDataManager            GameData        { get; private set; } = null!;
        [PluginService] public static IObjectTable            Objects         { get; private set; } = null!;
        [PluginService] public static IClientState            ClientState     { get; private set; } = null!;
        [PluginService] public static IPlayerState           PlayerState     { get; private set; } = null!;
        [PluginService] public static IChatGui                Chat            { get; private set; } = null!;
        [PluginService] public static IFramework              Framework       { get; private set; } = null!;
        [PluginService] public static ICondition              Conditions      { get; private set; } = null!;
        [PluginService] public static IGameGui                GameGui         { get; private set; } = null!;
        [PluginService] public static ITargetManager          Targets         { get; private set; } = null!;
        [PluginService] public static ITextureProvider        Textures        { get; private set; } = null!;
        [PluginService] public static IGameInteropProvider    Interop         { get; private set; } = null!;
        [PluginService] public static IPluginLog              Log             { get; private set; } = null!;
        [PluginService] public static IDtrBar                 Dtr             { get; private set;} = null!;
        [PluginService] public static INotificationManager    Notifications   { get; private set;} = null!;
        // @formatter:on

    public static bool GetIcon(uint iconId, [NotNullWhen(true)] out IDalamudTextureWrap? icon)
    {
        if (iconId == 0)
        {
            icon = null;
            return false;
        }

        // 找得到圖示時兩者回同一個共用材質，但 GetFromGameIcon 每次呼叫都配置一個取路徑的委派；
        // 找不到時照舊走 GetFromGameIcon（它會擲 IconNotFoundException），行為不變。
        if (!Textures.TryGetFromGameIcon(new GameIconLookup(iconId), out var tex))
            tex = Textures.GetFromGameIcon(new GameIconLookup(iconId));
        return tex.TryGetWrap(out icon, out _);
    }
}
