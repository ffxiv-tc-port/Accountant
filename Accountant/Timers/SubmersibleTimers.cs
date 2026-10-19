using System.Linq;
using Accountant.Classes;
using Accountant.Enums;
using Accountant.Util;
using Dalamud.Logging;

namespace Accountant.Timers;

public sealed class SubmersibleTimers : TimersBase<FreeCompanyInfo, MachineInfo[]>
{
    protected override string FolderName
        => "submersibles";

    protected override string SaveError
        => "Could not write submersible data";

    protected override string ParseError
        => "Invalid submersible data file could not be parsed";

    protected override string LoadError
        => "Error loading submersible timers";

    public bool AddOrUpdateSubmersible(FreeCompanyInfo company, MachineInfo submersible, byte slot)
    {
        if (slot >= MachineInfo.MaxSlots)
        {
            Dalamud.Log.Error($"Only {MachineInfo.MaxSlots} submersibles supported.");
            return false;
        }

        if (submersible.Type != MachineType.Submersible || !submersible.Name.Any())
            return false;

        if (!InternalData.TryGetValue(company, out var submersibles))
        {
            submersibles       = MachineInfo.GenerateDefaultArray();
            submersibles[slot] = submersible;
            InternalData[company]     = submersibles;
            Invoke();
            return true;
        }

        var oldMachine = submersibles[slot];
        if (Helpers.DateTimeClose(oldMachine.Arrival, submersible.Arrival) && oldMachine.Name == submersible.Name)
            return false;

        submersibles[slot] = submersible;
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
                if (machines[i].Type != MachineType.Submersible || machines[i].Name != vesselName)
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

    public bool ClearSubmersible(FreeCompanyInfo company, byte slot)
    {
        if (slot >= MachineInfo.MaxSlots)
        {
            Dalamud.Log.Error($"Only {MachineInfo.MaxSlots} submersibles supported.");
            return false;
        }
        if (!InternalData.TryGetValue(company, out var submersibles))
            return false;
        if (submersibles[slot].Type != MachineType.Submersible)
            return false;
        submersibles[slot] = MachineInfo.None;
        Invoke();
        return true;
    }
}
