using System;
using System.Numerics;
using Accountant.Enums;
using Accountant.Gui;
using OtterLoc.Structs;

namespace Accountant.Classes;

public struct PlantInfo
{
    public const int PotsPerApartment = 2;
    public const int PotsPerChamber   = 2;

    public DateTime PlantTime;
    public DateTime LastTending;
    public Vector3  Position;
    public uint     PlantId;
    public bool     AccuratePlantTime;
    public ushort   FertilizeCount;
    public TimeSpan FertilizeReduction;
    public DateTime LastFertilizeTime;

    public bool Active()
        => PlantId != 0;

    public bool CloseEnough(Vector3 rhs)
        => (Position - rhs).LengthSquared() < 0.01;

    public DateTime FinishTime()
        => PlantId != 0 ? PlantTime.AddMinutes(Accountant.GameData.FindCrop(PlantId).Data.GrowTime) : DateTime.MinValue;

    public DateTime WiltingTime()
        => PlantId != 0 ? LastTending.AddMinutes(Accountant.GameData.FindCrop(PlantId).Data.WiltTime) : DateTime.MinValue;

    public DateTime DyingTime()
        => PlantId != 0 ? LastTending.AddMinutes(Accountant.GameData.FindCrop(PlantId).Data.WiltTime).AddDays(1) : DateTime.MinValue;

    // True if the crop is still growing (with more than 24 hours of growth left - not worth a
    // reminder once it's close to done anyway) and it has been more than an hour since it was
    // last (successfully) fertilized - a nudge to go fertilize it again for the growth-time bonus.
    public bool NeedsFertilizeReminder(DateTime now)
    {
        if (PlantId == 0)
            return false;

        var baseline = LastFertilizeTime == DateTime.MinValue ? PlantTime : LastFertilizeTime;
        if (baseline == DateTime.MinValue)
            return false;

        var fin = FinishTime();
        return fin - now > TimeSpan.FromHours(24) && now - baseline > TimeSpan.FromHours(1);
    }

    // "Already fully fertilized" is a failure to actually fertilize, but it confirms the crop
    // is currently up to date, so - if we don't already have a real fertilize timestamp - treat
    // this moment as a stand-in for one rather than leaving it looking overdue.
    public void MarkFertilizedIfUnset(DateTime now)
    {
        if (LastFertilizeTime == DateTime.MinValue)
            LastFertilizeTime = now;
    }

    public bool Update(uint itemId, DateTime? plantTime, DateTime? tendTime,
        DateTime? fertilizeTime, Vector3? position = null)
    {
        var ret = false;
        if (PlantId != itemId)
        {
            PlantId            = itemId;
            PlantTime          = DateTime.MinValue;
            LastTending        = DateTime.MinValue;
            AccuratePlantTime  = false;
            FertilizeCount     = 0;
            FertilizeReduction = TimeSpan.Zero;
            LastFertilizeTime  = DateTime.MinValue;
            ret                = true;
        }

        if (tendTime.HasValue && tendTime.Value != LastTending)
        {
            LastTending = tendTime.Value;
            if (PlantTime == DateTime.MinValue)
                PlantTime = LastTending;
            // if the plant is grown, and yet we tended it, assume it's a new plant
            if (this.FinishTime() < tendTime)
            {
                PlantTime           = LastTending;
                AccuratePlantTime   = false;
                FertilizeCount      = 0;
                FertilizeReduction  = TimeSpan.Zero;
                LastFertilizeTime   = DateTime.MinValue;
            }
            ret = true;
        }

        if (plantTime.HasValue && plantTime.Value != PlantTime)
        {
            AccuratePlantTime  = true;
            PlantTime          = plantTime.Value;
            LastTending        = PlantTime;
            FertilizeCount     = 0;
            FertilizeReduction = TimeSpan.Zero;
            LastFertilizeTime  = DateTime.MinValue;
            ret                = true;
        }

        // A fertilize report can be the very first time we see this crop (e.g. a fresh
        // fertilize success on a previously untracked slot). Without a plant/tend time to
        // go on, use this moment as an approximate planted time instead of leaving the crop
        // identified but dateless (same rationale as the tend-based fallback above).
        if (fertilizeTime.HasValue && PlantTime == DateTime.MinValue)
        {
            PlantTime   = fertilizeTime.Value;
            LastTending = fertilizeTime.Value;
            ret         = true;
        }

        if (fertilizeTime.HasValue && PlantTime != DateTime.MinValue)
        {
            // Fertilizing reduces the remaining growth time by 1%; it does not affect wilting/withering.
            var remaining = FinishTime() - fertilizeTime.Value;
            if (remaining > TimeSpan.Zero)
            {
                var reduction = remaining * 0.01;
                PlantTime          -= reduction;
                FertilizeReduction += reduction;
                LastFertilizeTime  =  fertilizeTime.Value;
                ++FertilizeCount;
                ret = true;
            }
        }

        if (position.HasValue)
        {
            Position = position.Value;
            return true;
        }

        return ret;
    }

    public (DateTime, DateTime, DateTime, ColorId, DateTime, bool) GetCropTimes(DateTime now)
    {
        if (PlantId == 0)
            return (DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, ColorId.NeutralText, DateTime.MinValue, false);

        var fin    = FinishTime();
        var wilt   = WiltingTime();
        var wither = wilt.AddDays(1);

        if (wither < now)
            return fin < wither 
                ? (DateTime.MinValue, DateTime.MaxValue, DateTime.MaxValue, ColorId.TextCropGrown, DateTime.MinValue, true) 
                : (DateTime.MaxValue, DateTime.MinValue, DateTime.MinValue, ColorId.TextCropWithered, DateTime.MinValue, true);

        if (fin < now)
            return (DateTime.MinValue, DateTime.MaxValue, DateTime.MaxValue, ColorId.TextCropGrown, DateTime.MinValue, true);

        if (fin < wither)
            return (fin, wilt < fin ? wilt : DateTime.MaxValue, DateTime.MaxValue, ColorId.TextCropGuaranteed, fin, true);

        if (wilt < now)
            return (fin, DateTime.MinValue, wither, ColorId.TextCropWilted, wither, AccuratePlantTime);

        return (fin, wilt, wither, ColorId.TextCropGrowing, wilt, AccuratePlantTime);
    }

    public static string GetPrivateName(ushort idx)
        => idx < PotsPerApartment
            ? $"{StringId.Apartment.Value()}, {StringId.CropPot.Value()} {idx + 1}"
            : $"{StringId.Chambers.Value()}, {StringId.CropPot.Value()} {idx + 1 - PotsPerApartment}";

    public static string GetPlotName(PlotSize size, ushort idx)
    {
        var s = size.OutdoorBeds();
        return idx < s
            ? $"{StringId.CropBed.Value()} {(idx >> 3) + 1}-{(idx & 0b111) + 1}"
            : $"{StringId.CropPot.Value()} {idx + 1 - s}";
    }
}
