using System;
using Accountant.Timers;
using Accountant.Util;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Newtonsoft.Json;
using CsGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using ParsedText = Accountant.Internal.ParsedText;

namespace Accountant.Classes;

[method: JsonConstructor]
public readonly struct PlayerInfo(string name, ushort serverId) : IEquatable<PlayerInfo>, ITimerIdentifier
{
    public string Name     { get; } = name;
    public ushort ServerId { get; } = serverId;

    public PlayerInfo(IPlayerCharacter character)
        : this(character.Name.TextValue, (ushort)character.HomeWorld.RowId)
    { }

    private static readonly ParsedText LocalPlayerName = new();

    // 只在框架執行緒上、對本幀取得的自己角色呼叫。結果與 new PlayerInfo(character) 相同：
    // 讀的是 character.Name 解析的同一段位元組，位元組沒變才沿用上次的字串，省掉每次重新解析。
    public static unsafe PlayerInfo FromLocalPlayer(IPlayerCharacter character)
        => new(LocalPlayerName.Get(((CsGameObject*)character.Address)->Name), (ushort)character.HomeWorld.RowId);

    public bool Equals(PlayerInfo other)
        => ServerId == other.ServerId
         && Name == other.Name;

    public override bool Equals(object? obj)
        => obj is PlayerInfo other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Name, ServerId);

    public uint IdentifierHash()
        => (uint)Helpers.CombineHashCodes(Helpers.GetStableHashCode(Name), ServerId);

    public bool Valid()
        => Name.Length > 0 && Accountant.GameData.IsValidWorldId(ServerId);

    [JsonIgnore]
    public string CastedName
        => $"{Name}{(char)ServerId}";

    public static PlayerInfo FromCastedName(string castedName)
        => new(castedName[..^1], castedName[^1]);

    public static bool operator ==(PlayerInfo left, PlayerInfo right)
        => left.Equals(right);

    public static bool operator !=(PlayerInfo left, PlayerInfo right)
        => !(left == right);
}
