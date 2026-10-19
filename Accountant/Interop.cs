using System;
using System.Collections.Generic;
using Accountant.SeFunctions;
using Dalamud.Game;
using Dalamud.Plugin.Services;

namespace Accountant;

public class Interop
{
    public static PositionInfoAddress     PositionInfo         { get; private set; } = null!;
    public static StaticSquadronContainer SquadronContainer    { get; private set; } = null!;
    public static UpdateGoldSaucerData    UpdateGoldSaucerData { get; private set; } = null!;

    public static void Init(IPluginLog log, ISigScanner sigScanner)
    {
        PositionInfo         = new PositionInfoAddress(log);
        SquadronContainer    = new StaticSquadronContainer(log, sigScanner);
        UpdateGoldSaucerData = new UpdateGoldSaucerData(log, sigScanner);

        MissingNotices.Clear();
        NoteMissing(log, PositionInfo.Address,         "住宅區位置（PositionInfo）：作物計時器與房屋拆除追蹤不更新");
        NoteMissing(log, SquadronContainer.Address,    "冒險者分隊資料（SquadronContainer）：冒險者分隊計時器不更新");
        NoteMissing(log, UpdateGoldSaucerData.Address, "金碟遊樂場資料（GoldSaucerData）：仙人微彩、仙人仙彩計時器不更新");
        NoteMissing(log, GameDataFactory.FreeCompanyInfoAvailable, "公會名稱與會長（InfoProxyFreeCompany）：新的公會不會被記錄，飛空艇、潛水艇計時可能對不上公會");
    }

    /// <summary>特徵碼或 CS 位址對不上而停用的項目（說明文字）。只在 <see cref="Init"/> 寫入。</summary>
    public static readonly List<string> MissingNotices = [];

    private static void NoteMissing(IPluginLog log, IntPtr address, string what)
        => NoteMissing(log, address != IntPtr.Zero, what);

    private static void NoteMissing(IPluginLog log, bool available, string what)
    {
        if (available)
            return;

        MissingNotices.Add(what);
        log.Warning($"遊戲改版後對不上：{what}；其餘功能照常。");
    }
}
