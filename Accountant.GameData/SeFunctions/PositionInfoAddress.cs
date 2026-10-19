using System;
using Accountant.Enums;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Accountant.SeFunctions;

// 讀 CS 的 HousingManager；只在框架執行緒（含 hook detour）讀，每次讀都從單例重查、不留指標。
public sealed class PositionInfoAddress
{
    // 0＝CS 沒解到 HousingManager 單例或 GetCurrentDivision：各欄位回 0／Unknown。
    public readonly IntPtr Address;

    public unsafe PositionInfoAddress(IPluginLog log)
    {
        if (HousingManager.MemberFunctionPointers.GetCurrentDivision != null)
            Address = (IntPtr)HousingManager.StaticAddressPointers.ppInstance;
        log.Debug($"{nameof(PositionInfoAddress)} HousingManager instance pointer 0x{Address.ToInt64():X16}.");
    }

    public ushort Ward
        => Info.Ward;

    public InternalHousingZone Zone
        => Info.Zone;

    public ushort House
        => Info.House;

    public bool Subdivision
        => Info.Subdivision;

    public byte Plot
        => Info.Plot;

    public HousingFloor HousingFloor
        => Info.HousingFloor;

    // 拆法與改用 CS 前逐位元相同：House＝HouseId 低 16 位元、Ward＝Data2、Zone＝TerritoryTypeId 低位元組；
    // 分區位元組（0＝在屋內、2＝分區）室外取遊戲自己的 GetCurrentDivision，室內取 Unk96A8 的第二個位元組。
    private readonly unsafe struct PositionInfo
    {
        // 樓層 CS 只有 IndoorTerritory.CurrentFloor（0x970C），列舉值意義沒對過；本屬性目前沒有讀取點，保留原位置。
        private const int HousingFloorOffset = 0x9704;

        private readonly HousingTerritory* _territory;
        private readonly HouseId           _houseId;
        private readonly byte              _division;
        private readonly byte              _plot;

        public PositionInfo(OutdoorTerritory* outdoor, HousingManager* manager)
        {
            _territory = (HousingTerritory*)outdoor;
            _houseId   = outdoor->HouseId;
            _division  = manager->GetCurrentDivision();
            _plot      = outdoor->HouseUnit.Value;
        }

        public PositionInfo(IndoorTerritory* indoor)
        {
            _territory = (HousingTerritory*)indoor;
            _houseId   = indoor->HouseId;
            _division  = (byte)(indoor->Unk96A8 >> 8);
            _plot      = (byte)indoor->Unk96A8;
        }

        public ushort House
            => (ushort)(_territory == null || !InHouse ? 0 : (ushort)_houseId.Id + 1);

        public ushort Ward
            => (ushort)(_territory == null ? 0 : _houseId.Data2 + 1);

        public bool Subdivision
            => _territory != null && _division == 2;

        public InternalHousingZone Zone
            => _territory == null ? InternalHousingZone.Unknown : (InternalHousingZone)(byte)_houseId.TerritoryTypeId;

        public byte Plot
            => (byte)(_territory == null || InHouse ? 0 : _plot + 1);

        public HousingFloor HousingFloor
            => _territory == null ? HousingFloor.Unknown : *(HousingFloor*)((byte*)_territory + HousingFloorOffset);

        private bool InHouse
            => _division == 0;
    }

    private unsafe PositionInfo Info
    {
        get
        {
            if (Address == IntPtr.Zero)
                return default;

            var manager = HousingManager.Instance();
            if (manager == null || manager->CurrentTerritory == null)
                return default;

            var current = manager->CurrentTerritory;
            if (current == (HousingTerritory*)manager->OutdoorTerritory)
                return new PositionInfo(manager->OutdoorTerritory, manager);
            if (current == (HousingTerritory*)manager->IndoorTerritory)
                return new PositionInfo(manager->IndoorTerritory);

            // 工房等其他領地：CS 的 HouseId 不在 0x96A0，回不知道。
            return default;
        }
    }
}
