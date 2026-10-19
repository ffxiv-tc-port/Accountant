using System;
using Dalamud.Game;
using Dalamud.Plugin.Services;

namespace Accountant.SeFunctions;

public class SeAddressBase
{
    public readonly IntPtr Address;

    public SeAddressBase(IPluginLog log, ISigScanner sigScanner, string signature, int offset = 0)
    {
        // 特徵碼對不上時 Address 留 0、不擲例外；衍生類別解參考前一律先判 0。
        if (sigScanner.TryGetStaticAddressFromSig(signature, out var address) && address != IntPtr.Zero)
            Address = address + offset;
        var baseOffset = (ulong)Address.ToInt64() - (ulong)sigScanner.Module.BaseAddress.ToInt64();
        log.Debug($"{GetType().Name} address 0x{Address.ToInt64():X16}, baseOffset 0x{baseOffset:X16}.");
    }
}
