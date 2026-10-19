using System.Linq;
using Accountant.Classes;
using Accountant.Enums;
using Accountant.Util;
using Dalamud.Logging;

namespace Accountant.Timers;

public sealed class AirshipTimers : TimersBase<FreeCompanyInfo, MachineInfo[]>
{
    protected override string FolderName
        => "airships";

    protected override string SaveError
        => "Could not write airship data";

    protected override string ParseError
        => "Invalid airship data file could not be parsed";

    protected override string LoadError
        => "Error loading airship timers";

    public bool AddOrUpdateAirship(FreeCompanyInfo company, MachineInfo airship, byte slot)
    {
        if (slot >= MachineInfo.MaxSlots)
        {
            Dalamud.Log.Error($"Only {MachineInfo.MaxSlots} airships supported.");
            return false;
        }

        if (airship.Type != MachineType.Airship || !airship.Name.Any())
            return false;

        if (!InternalData.TryGetValue(company, out var airships))
        {
            airships              = MachineInfo.GenerateDefaultArray();
            airships[slot]        = airship;
            InternalData[company] = airships;
            Invoke();
            return true;
        }

        var oldMachine = airships[slot];
        if (Helpers.DateTimeClose(oldMachine.Arrival, airship.Arrival) && oldMachine.Name == airship.Name)
            return false;

        airships[slot] = airship;
        Invoke();
        return true;
    }

    public FreeCompanyInfo? UpdateArrivalFromExternal(FreeCompanyInfo? scopedCompany, string vesselName, System.DateTime arrival)
    {
        var candidates = scopedCompany.HasValue
            ? InternalData.Where(kv => kv.Key.Equals(scopedCompany.Value))
            : InternalData;

        foreach (var (company, machines) in candidates)
        {
            for (var i = 0; i < machines.Length; ++i)
            {
                if (machines[i].Type != MachineType.Airship || machines[i].Name != vesselName)
                    continue;

                if (Helpers.DateTimeClose(machines[i].Arrival, arrival))
                    return null;

                machines[i].Arrival = arrival;
                Invoke();
                return company;
            }
        }

        return null;
    }

    public bool ClearAirship(FreeCompanyInfo company, byte slot)
    {
        if (slot >= MachineInfo.MaxSlots)
        {
            Dalamud.Log.Error($"Only {MachineInfo.MaxSlots} airships supported.");
            return false;
        }

        if (!InternalData.TryGetValue(company, out var airships))
            return false;
        if (airships[slot].Type != MachineType.Airship)
            return false;

        airships[slot] = MachineInfo.None;
        Invoke();
        return true;
    }
}
