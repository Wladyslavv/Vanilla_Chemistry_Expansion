// Mod settings (mod list -> this mod -> settings). Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using HarmonyLib;

public static class IaSettings
{
    // Section names are plain strings and mod localization is not loaded during OnEnable, so it is bilingual.
    public const string SECTION = "蒸发 / Evaporation";
    public const string FRIDGE_SECTION = "冰箱 / Fridge";
    public const string FERMENT_SECTION = "发酵 / Fermentation";

    private static ConfigEntry<int> waterBoilPercent;
    private static ConfigEntry<int> fridgeCoolPercent;
    private static ConfigEntry<int> fermentPercent;

    public const string CARTEL_SECTION = "卡特尔 / Cartel";
    private static ConfigEntry<bool> cartelEnabled;

    public const string WINE_SECTION = "葡萄酒 / Wine";
    private static ConfigEntry<bool> blendedWineDefaultGenuine;

    // Blended wine pressed in the Chem Finisher comes out labelled genuine instead of blended (FakeWine.cs). Off by
    // default.
    public static bool BlendedWineDefaultGenuine
    {
        get { return blendedWineDefaultGenuine != null && blendedWineDefaultGenuine.Value; }
    }

    public const string FILTER_SECTION = "过滤 / Filtering";
    private static ConfigEntry<bool> keepFilterResidue;
    private static ConfigEntry<int> advancedFabricRepair;

    // Filters (Kotton fabric, advanced Kotton fabric, the water filters, the hydroreed filter) keep what they catch,
    // to be poured out again (FilterResidue.cs). On by default; while off they catch nothing new, as in vanilla.
    public static bool KeepFilterResidue
    {
        get { return keepFilterResidue == null || keepFilterResidue.Value; }
    }

    // Durability the advanced Kotton fabric regains each night (AdvancedFabric.cs); 1000 is a full repair. Typed in,
    // so kept to 0..MAX_DURABILITY here.
    public static int AdvancedFabricRepair
    {
        get
        {
            int value = advancedFabricRepair != null ? advancedFabricRepair.Value : AdvancedFabric.DAILY_REPAIR;
            return Math.Max(0, Math.Min(AdvancedFabric.MAX_DURABILITY, value));
        }
    }

    // The cartel expansion (CartelNetwork.cs): the Cartel Network, the watching path, the boycott, Security's route
    // against the cartel and the cartel merchants. Off by default; while off none of it does anything.
    public static bool CartelEnabled
    {
        get { return cartelEnabled != null && cartelEnabled.Value; }
    }

    // Speed at which yeast turns sugar into alcohol overnight, as a percentage of the base rate (24 uL of
    // sugar per uL of yeast per night).
    public static int FermentPercent
    {
        get { return fermentPercent != null ? fermentPercent.Value : 100; }
    }

    // Speed at which a powered fridge moves its containers toward 3 C (chill) or -18 C (freezer), as a
    // percentage of vanilla (0.5 and 1 C per second).
    public static int FridgeCoolPercent
    {
        get { return fridgeCoolPercent != null ? fridgeCoolPercent.Value : 100; }
    }

    // Speed of vanilla boiling (every evaporative liquid above its boiling point), as a percentage of vanilla. The config
    // key is still "water_boil_percent" (up to 1.10 it only scaled water), so a saved setting carries over.
    public static int BoilPercent
    {
        get { return waterBoilPercent != null ? waterBoilPercent.Value : 100; }
    }

    public static void Register()
    {
        try
        {
            ModConfig config = ModConfig.For(DreamToxinMod.MOD_ID);
            waterBoilPercent = config.Define("water_boil_percent", 100, SECTION,
                () => IaText.Get("setting_water_boil"), (int?)10, (int?)1000,
                () => IaText.Get("setting_water_boil_tooltip"), false);
            fridgeCoolPercent = config.Define("fridge_cool_percent", 400, FRIDGE_SECTION,
                () => IaText.Get("setting_fridge_cool"), (int?)100, (int?)2000,
                () => IaText.Get("setting_fridge_cool_tooltip"), false);
            fermentPercent = config.Define("ferment_percent", 100, FERMENT_SECTION,
                () => IaText.Get("setting_ferment"), (int?)25, (int?)1000,
                () => IaText.Get("setting_ferment_tooltip"), false);
            cartelEnabled = config.Define("cartel_expansion", false, CARTEL_SECTION,
                () => IaText.Get("setting_cartel"), () => IaText.Get("setting_cartel_tooltip"), false);
            blendedWineDefaultGenuine = config.Define("blended_wine_default_genuine", false, WINE_SECTION,
                () => IaText.Get("setting_wine_genuine"), () => IaText.Get("setting_wine_genuine_tooltip"), false);
            keepFilterResidue = config.Define("keep_filter_residue", true, FILTER_SECTION,
                () => IaText.Get("setting_filter_residue"), () => IaText.Get("setting_filter_residue_tooltip"), false);
            // No min/max: the mod settings window draws a ranged number as a slider and an unranged one as a text
            // field, and this one is typed in. AdvancedFabricRepair clamps it instead.
            advancedFabricRepair = config.Define("advanced_fabric_repair", AdvancedFabric.DAILY_REPAIR, FILTER_SECTION,
                () => IaText.Get("setting_fabric_repair"), (int?)null, (int?)null,
                () => IaText.Get("setting_fabric_repair_tooltip"), false);
        }
        catch (Exception e)
        {
            IaLog.Error("settings registration failed", e);
        }
    }
}

// Powered fridges cool (or warm) each container by a fixed number of degrees per second; scale it.
[HarmonyPatch(typeof(MachineFridge), "CoolZoneContainers")]
public static class FridgeCoolSpeedPatch
{
    private static void Prefix(ref double degreesPerSecond)
    {
        degreesPerSecond = degreesPerSecond * IaSettings.FridgeCoolPercent / 100.0;
    }
}

// Vanilla boiling removes 2% (at least 0.1 mL) of each liquid above its boiling point per tick. Scale the
// amount it removed of every liquid by the setting.
[HarmonyPatch(typeof(ChemicalHelper), "HandleBoilingReaction")]
public static class BoilSpeedPatch
{
    private static void Prefix(GameItem container, out Dictionary<string, int> __state)
    {
        __state = null;
        if (container == null || IaSettings.BoilPercent == 100)
        {
            return;
        }
        foreach (Liquid liquid in Liquid.Liquids)
        {
            if (!liquid.isEvaporative)
            {
                continue;
            }
            int amount = IaLiquids.Get(container, liquid.identifier);
            if (amount > 0)
            {
                if (__state == null)
                {
                    __state = new Dictionary<string, int>();
                }
                __state[liquid.identifier] = amount;
            }
        }
    }

    private static void Postfix(GameItem container, Dictionary<string, int> __state)
    {
        int percent = IaSettings.BoilPercent;
        if (container == null || percent == 100 || __state == null)
        {
            return;
        }
        foreach (KeyValuePair<string, int> entry in __state)
        {
            int removed = entry.Value - IaLiquids.Get(container, entry.Key);
            if (removed <= 0)
            {
                continue;
            }
            int scaled = (int)Math.Min((long)entry.Value, (long)removed * percent / 100);
            IaLiquids.Set(container, entry.Key, entry.Value - scaled);
        }
    }
}
