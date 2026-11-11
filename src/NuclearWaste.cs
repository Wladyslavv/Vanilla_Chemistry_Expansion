// Nuclear waste reprocessing (merged from the Nuclear Chemistry test mod in 1.3.0). Pry open vanilla nuclear
// waste with a crowbar and reprocess the spent fuel inside: dissolve it in concentrated acid (decay heat keeps
// the barrel warm), condition the plutonium with reducing agent, extract it with ethyl acetate, distil off the
// solvent and press plutonium ampoules. Fission products never leave the barrel; vitrify them with water glass
// once the barrel runs dry. Leaving fission products around overnight causes radiation sickness.
// Written for C# 5 (see DreamToxinMod.cs). Optional arguments are passed explicitly on tag helpers: C# 5
// ignores `where T : Enum` and would bind the generic overloads.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

// Wired up from DreamToxinMod: Enable/Disable from OnEnable/OnDisable, AddItems from the item directory hook,
// RefreshNames whenever the other liquid names are refreshed. Debug functions share the mod's id, so
// DreamToxinMod.OnDisable removes them.
public static class NuclearWaste
{
    public static void Enable()
    {
        ModHook.OnGameLoadedLate += NcSprites.FixLoadedItems;
        ModHook.OnHandlingNightlyServicesEarly += Radiation.OnNight;
        ModHook.OnWakingUpLate += Radiation.OnWakingUp;
        ModHook.OnGenerateCustomerNormal += WasteMerchant.OnGenerateCustomers;
        try
        {
            string id = DreamToxinMod.MOD_ID;
            ModHelper.AddDebugFunction(id, "Modding", "Nuclear: waste barrel + crowbar", NcDebugKit.SpawnBarrel, false);
            ModHelper.AddDebugFunction(id, "Modding", "Nuclear: reagent kit", NcDebugKit.SpawnReagents, false);
            ModHelper.AddDebugFunction(id, "Modding", "Nuclear: plutonium ampoule", NcDebugKit.SpawnAmpoule, false);
            ModHelper.AddDebugFunction(id, "Modding", "Nuclear: radiation +5%", NcDebugKit.AddRadiation, false);
            ModHelper.AddDebugFunction(id, "Modding", "Nuclear: merchant tomorrow", NcDebugKit.MerchantTomorrow, false);
        }
        catch (Exception e)
        {
            NcLog.Error("debug function registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnGameLoadedLate -= NcSprites.FixLoadedItems;
        ModHook.OnHandlingNightlyServicesEarly -= Radiation.OnNight;
        ModHook.OnWakingUpLate -= Radiation.OnWakingUp;
        ModHook.OnGenerateCustomerNormal -= WasteMerchant.OnGenerateCustomers;
    }

    // Liquids and the ampoule recipe; called once from Content.EnsureRegistered (see DreamToxinMod.cs).
    public static void RegisterContent()
    {
        NcLiquids.Register();
        Ampoule.RegisterRecipe();
    }

    public static void UnregisterContent()
    {
        Ampoule.UnregisterRecipe();
    }

    public static void RefreshNames()
    {
        NcLiquids.RefreshNames();
        Ampoule.RefreshRecipeName();
    }

    public static void AddItems(ModItemDirectory directory)
    {
        try
        {
            directory.Add(Barrel.OPENED_ID, Barrel.CreateOpened);
            directory.Add(Barrel.VITRIFIED_ID, Barrel.CreateVitrified);
            directory.Add(Ampoule.ITEM_ID, Ampoule.Create);
        }
        catch (Exception e)
        {
            NcLog.Error("nuclear item registration failed", e);
        }
    }
}

public static class NcLiquids
{
    public const string SPENT_FUEL = "nc_spent_fuel";
    public const string PU_DISSOLVED = "nc_pu_dissolved";
    public const string FISSION = "nc_fission_products";
    public const string PU_EXTRACT = "nc_pu_extract";
    public const string PLUTONIUM = "nc_plutonium";

    // From Vanilla Chemistry Expansion.
    public const string ETHYL_ACETATE = "ia_ethyl_acetate";
    public const string WATER_GLASS = "vce_water_glass";
    public const string SILICA_GEL = "vce_silica_gel";

    // These never leave an opened barrel.
    public static readonly string[] Radioactive = { SPENT_FUEL, PU_DISSOLVED, FISSION };

    private static readonly object[][] DEFINITIONS = new object[][]
    {
        new object[] { SPENT_FUEL, "liquid_spent_fuel", 200, "3d4a2a" },
        new object[] { PU_DISSOLVED, "liquid_pu_dissolved", 5, "6b8e23" },
        new object[] { FISSION, "liquid_fission_products", 5, "8a9a3a" },
        new object[] { PU_EXTRACT, "liquid_pu_extract", 5, "9acd32" },
        new object[] { PLUTONIUM, "liquid_plutonium", 5, "556b2f" }
    };

    public static void Register()
    {
        foreach (object[] d in DEFINITIONS)
        {
            string id = (string)d[0];
            if (Liquid.LiquidDict.ContainsKey(id))
            {
                continue;
            }
            // SetIdentifier is private, so set its fields directly (same as Vanilla Chemistry Expansion).
            Liquid liquid = new Liquid();
            liquid.displayName = NcText.Get((string)d[1]);
            liquid.identifier = id;
            liquid.part = "PART_" + id.ToUpperInvariant();
            liquid.currentPart = "CURRENT_PART_" + id.ToUpperInvariant();
            liquid.baseMaxPurityFromMachine = 12;
            liquid.basePurifierRemoveAmount = 4000;
            liquid.particleSize = (int)d[2];
            liquid.filterMinimumPurity = 0.0016;
            liquid.filterVolumeRemoved = 1500;
            // One ampoule puts 100 uL of plutonium into a drink: 500, below the game's 750 "toxic" mark (1000 is lethal).
            liquid.lethality = id == PLUTONIUM ? 5.0 : 8.0;
            liquid.color = (string)d[3];
            liquid.colorMultiplier = 3;
            Liquid.Liquids.Add(liquid);
            Liquid.LiquidDict[id] = liquid;
        }
    }

    // Liquids and the ampoule recipe; called once from Content.EnsureRegistered (see DreamToxinMod.cs).
    public static void RegisterContent()
    {
        NcLiquids.Register();
        Ampoule.RegisterRecipe();
    }

    public static void UnregisterContent()
    {
        Ampoule.UnregisterRecipe();
    }

    public static void RefreshNames()
    {
        foreach (object[] d in DEFINITIONS)
        {
            Liquid liquid;
            if (Liquid.LiquidDict.TryGetValue((string)d[0], out liquid))
            {
                liquid.displayName = NcText.Get((string)d[1]);
            }
        }
    }

    public static bool IsRadioactive(string id)
    {
        return Array.IndexOf(Radioactive, id) >= 0;
    }

    public static bool Exists(string id)
    {
        return Liquid.LiquidDict.ContainsKey(id);
    }

    public static int Get(GameItem container, string id)
    {
        if (!Exists(id))
        {
            return 0;
        }
        return container.GetTagReadonly(Liquid.GetCurrentPartFromId(id)).GetInt();
    }

    public static void Set(GameItem container, string id, int amount)
    {
        int value = Math.Max(0, amount);
        container.ModifyTag(Liquid.GetCurrentPartFromId(id), delegate(TagState state)
        {
            state.Enable().SetInt(value);
        }, true);
    }

    public static void Add(GameItem container, string id, int amount)
    {
        if (amount != 0)
        {
            Set(container, id, Get(container, id) + amount);
        }
    }

    public static int RadioactiveTotal(GameItem container)
    {
        int total = 0;
        foreach (string id in Radioactive)
        {
            total += Get(container, id);
        }
        return total;
    }
}

// The opened barrel: a liquid container that can only sit in the main inventory, keeps itself warm with
// decay heat, and never lets its radioactive contents out.
public static class Barrel
{
    public const string WASTE_ID = "nuclear_waste";
    public const string OPENED_ID = "nc.opened_waste";
    public const string VITRIFIED_ID = "nc.vitrified_waste";
    public const int CAPACITY_ML = 1500;
    public const int SPENT_FUEL_UL = 500000;

    // Decay heat: liquids in the barrel boil off nothing important at 70 C; dry, it heats past 300 C.
    public const double WET_TEMP = 70.0;
    public const double DRY_TEMP = 320.0;
    // Below this many uL of coolant (evaporative liquids) the barrel counts as dry.
    public const int DRY_THRESHOLD_UL = 100;

    public static bool IsOpened(GameItem item)
    {
        return item != null && item.identifier == OPENED_ID;
    }

    public static GameItem CreateOpened()
    {
        GameItem item = DirectoryMaster.Item(WASTE_ID, true);
        try
        {
            item.identifier = OPENED_ID;
            item.name = NcText.Get("opened_waste_name");
            item.shortDescription = NcText.Get("opened_waste_desc");
            item.flavorText = NcText.Get("opened_waste_flavor");
            WaterHelper.InitLiquidContainerItem(item, CAPACITY_ML, 0, false, false, false, false, 3);
            // Only plain pouring into another liquid container; no drains or faucets.
            item.mayThisTargetItemFunc = (GameItem s, GameItem t) => CanPourInto(s, t);
            item.canThisTargetItemFunc = (GameItem s, GameItem t) => CanPourInto(s, t);
            item.onThisTargetItemFunc = delegate(GameItem s, GameItem t)
            {
                if (CanPourInto(s, t))
                {
                    WaterHelper.TransferLiquid(s, t);
                    AudioHelper.PourWater();
                    SpriteHelper.UpdateWaterContainerSprite(t);
                }
            };
            // Only the main inventory, the showcase and both counters (outer: frontInvinvElement, inner:
            // backInvinvElement): no boxes, machines, trash or afterhours bags.
            item.mayInventoryAddItemFunc = (GameItem it, GameInventory inv) =>
                EmporiumEntry.Instance != null && (inv == EmporiumEntry.Instance.invElement
                    || inv == EmporiumEntry.Instance.showcaseElement || inv == EmporiumEntry.Instance.frontInvinvElement
                    || inv == EmporiumEntry.Instance.backInvinvElement);
            NcLiquids.Set(item, NcLiquids.SPENT_FUEL, SPENT_FUEL_UL);
            item.ModifyTag("CONTAINER_TEMP_TAG", delegate(TagState state)
            {
                state.Enable().SetDouble(WET_TEMP);
            }, true);
            NcSprites.Apply(item, NcSprites.OPENED);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            NcLog.Error("create " + OPENED_ID + " failed", e);
        }
        return item;
    }

    private static bool CanPourInto(GameItem source, GameItem target)
    {
        return target != null && target != source && target.IsTag("LIQUID_CONTAINER_TAG")
            && WaterHelper.CanUseWaterContainer(target) && GeneralHelper.IsItemOwned(source) && GeneralHelper.IsItemOwned(target)
            && !WaterHelper.IsEmpty(source);
    }

    public static GameItem CreateVitrified()
    {
        GameItem item = DirectoryMaster.Item(WASTE_ID, true);
        try
        {
            MakeVitrified(item);
        }
        catch (Exception e)
        {
            NcLog.Error("create " + VITRIFIED_ID + " failed", e);
        }
        return item;
    }

    // The vitrified canister is harmless and can be thrown away (vanilla only tracks nuclear_waste).
    private static void MakeVitrified(GameItem item)
    {
        item.identifier = VITRIFIED_ID;
        item.name = NcText.Get("vitrified_name");
        item.shortDescription = NcText.Get("vitrified_desc");
        item.flavorText = NcText.Get("vitrified_flavor");
        item.DisableTag("HAZARDOUS_WASTE_TAG", true);
        item.DisableTag("LIQUID_CONTAINER_TAG", true);
        item.mayThisTargetItemFunc = null;
        item.canThisTargetItemFunc = null;
        item.onThisTargetItemFunc = null;
        item.mayItemTargetThisFunc = null;
        item.canItemTargetThisFunc = null;
        item.mayInventoryAddItemFunc = null;
        item.onOneSecondItemFunc = null;
        item.unitValue = 0L;
        NcSprites.Apply(item, NcSprites.VITRIFIED);
        ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
    }

    // Called from the temperature tick of an opened barrel.
    public static bool CanLeave(Liquid liquid)
    {
        return liquid.identifier == NcLiquids.PU_EXTRACT || (liquid.isEvaporative && !NcLiquids.IsRadioactive(liquid.identifier));
    }

    public static int Tick(GameItem barrel)
    {
        // Evaporative liquids (water, alcohol, ethyl acetate) carry the decay heat away. Without them the
        // barrel runs hot, except while spent fuel is still waiting to be dissolved in acid.
        int coolant = 0;
        foreach (Liquid liquid in Liquid.Liquids)
        {
            if (liquid.isEvaporative && !NcLiquids.IsRadioactive(liquid.identifier))
            {
                coolant += barrel.GetTagReadonly(liquid.currentPart).GetInt();
            }
        }
        bool hot = NcLiquids.RadioactiveTotal(barrel) > 0;
        bool cooled = coolant > DRY_THRESHOLD_UL || NcLiquids.Get(barrel, NcLiquids.SPENT_FUEL) > 0;
        double temp = !hot ? 26.0 : (cooled ? WET_TEMP : DRY_TEMP);
        barrel.ModifyTag("CONTAINER_TEMP_TAG", delegate(TagState state)
        {
            state.Enable().SetDouble(temp);
        }, true);
        return Dissolve(barrel) | Vitrify(barrel, temp);
    }

    // Spent fuel + concentrated acid (1:1) -> 0.4% plutonium + 10% fission products. Needs the plain
    // (non-radioactive) liquids in the barrel to be at least 98% acid.
    public const double ACID_SHARE = 0.98;
    public const double PU_PER_FUEL = 0.004;
    public const double FISSION_PER_FUEL = 0.1;
    public const double DISSOLVE_RATE = 0.05;
    public const int DISSOLVE_MIN_UL = 1000;

    private static int Dissolve(GameItem barrel)
    {
        int fuel = NcLiquids.Get(barrel, NcLiquids.SPENT_FUEL);
        int acid = NcLiquids.Get(barrel, "acid");
        if (fuel <= 0 || acid <= 0)
        {
            return 0;
        }
        int plain = 0;
        foreach (Liquid liquid in Liquid.Liquids)
        {
            if (!NcLiquids.IsRadioactive(liquid.identifier))
            {
                plain += barrel.GetTagReadonly(liquid.currentPart).GetInt();
            }
        }
        if (acid < plain * ACID_SHARE)
        {
            return 0;
        }
        int amount = Math.Min(Math.Min(fuel, acid), Math.Max(DISSOLVE_MIN_UL, (int)(fuel * DISSOLVE_RATE)));
        NcLiquids.Set(barrel, NcLiquids.SPENT_FUEL, fuel - amount);
        NcLiquids.Set(barrel, "acid", acid - amount);
        // Products follow the total dissolved so far, so a full barrel yields exactly 2 mL / 50 mL.
        long before = Math.Max(0, SPENT_FUEL_UL - fuel);
        long after = before + amount;
        NcLiquids.Add(barrel, NcLiquids.PU_DISSOLVED, (int)(Math.Floor(after * PU_PER_FUEL) - Math.Floor(before * PU_PER_FUEL)));
        NcLiquids.Add(barrel, NcLiquids.FISSION, (int)(Math.Floor(after * FISSION_PER_FUEL) - Math.Floor(before * FISSION_PER_FUEL)));
        return 1;
    }

    // Dry and above 300 C, water glass (1:1) melts the radioactive contents into glass. Once nothing
    // radioactive is left, the barrel becomes a sealed vitrified canister.
    public const double VITRIFY_TEMP = 300.0;
    public const double VITRIFY_RATE = 0.1;
    public const int VITRIFY_MIN_UL = 1000;

    private static int Vitrify(GameItem barrel, double temp)
    {
        if (temp < VITRIFY_TEMP || !NcLiquids.Exists(NcLiquids.WATER_GLASS))
        {
            return 0;
        }
        // The leftover acid in the barrel turns some water glass into silica gel (Vanilla Chemistry Expansion);
        // gel is silica too, so it serves as glass former just as well.
        int waterGlass = NcLiquids.Get(barrel, NcLiquids.WATER_GLASS);
        int gel = NcLiquids.Get(barrel, NcLiquids.SILICA_GEL);
        int glass = waterGlass + gel;
        int radioactive = NcLiquids.RadioactiveTotal(barrel);
        if (glass <= 0 || radioactive <= 0)
        {
            return 0;
        }
        int amount = Math.Min(Math.Min(glass, radioactive), Math.Max(VITRIFY_MIN_UL, (int)(radioactive * VITRIFY_RATE)));
        int fromWaterGlass = Math.Min(waterGlass, amount);
        NcLiquids.Set(barrel, NcLiquids.WATER_GLASS, waterGlass - fromWaterGlass);
        NcLiquids.Set(barrel, NcLiquids.SILICA_GEL, gel - (amount - fromWaterGlass));
        int left = amount;
        foreach (string id in NcLiquids.Radioactive)
        {
            int have = NcLiquids.Get(barrel, id);
            int take = Math.Min(have, left);
            NcLiquids.Set(barrel, id, have - take);
            left -= take;
        }
        if (NcLiquids.RadioactiveTotal(barrel) <= 0)
        {
            foreach (Liquid liquid in Liquid.Liquids)
            {
                NcLiquids.Set(barrel, liquid.identifier, 0);
            }
            MakeVitrified(barrel);
            NcLog.Info("a barrel was vitrified");
        }
        return 1;
    }
}

public static class Extraction
{
    // Reducing agent conditions plutonium to the extractable state: full at 10x the plutonium, otherwise
    // the unconditioned share is lost to the fission products (like vanilla Dream Cap extraction).
    public const double REDUCTANT_PER_PU = 10.0;
    // Ethyl acetate must be at least twice the fission products for a full extraction.
    public const double ESTER_PER_FISSION = 2.0;
    public const double RATE = 0.1;
    public const int MIN_UL = 20;

    public static int Run(GameItem container)
    {
        if (!NcLiquids.Exists(NcLiquids.ETHYL_ACETATE))
        {
            return 0;
        }
        int pu = NcLiquids.Get(container, NcLiquids.PU_DISSOLVED);
        int reductant = NcLiquids.Get(container, "reducing_agent");
        int ester = NcLiquids.Get(container, NcLiquids.ETHYL_ACETATE);
        if (pu <= 0 || reductant <= 0 || ester <= 0)
        {
            return 0;
        }
        int fission = NcLiquids.Get(container, NcLiquids.FISSION);
        int amount = Math.Min(pu, Math.Max(MIN_UL, (int)(pu * RATE)));
        double conditioned = Math.Min(1.0, reductant / (REDUCTANT_PER_PU * pu));
        double extracted = fission > 0 ? Math.Min(1.0, ester / (ESTER_PER_FISSION * fission)) : 1.0;
        int toExtract = NcMath.RoundRandom(amount * conditioned * extracted);
        NcLiquids.Set(container, NcLiquids.PU_DISSOLVED, pu - amount);
        NcLiquids.Add(container, NcLiquids.PU_EXTRACT, toExtract);
        NcLiquids.Add(container, NcLiquids.FISSION, amount - toExtract);
        NcLiquids.Set(container, "reducing_agent", reductant - Math.Min(reductant, (int)(amount * REDUCTANT_PER_PU)));
        return 1;
    }

    // Outside the barrel, once the ethyl acetate is gone (boiled off above 77 C) the extract leaves plutonium.
    public static int Release(GameItem container)
    {
        int extract = NcLiquids.Get(container, NcLiquids.PU_EXTRACT);
        if (extract <= 0 || Barrel.IsOpened(container))
        {
            return 0;
        }
        if (NcLiquids.Exists(NcLiquids.ETHYL_ACETATE) && NcLiquids.Get(container, NcLiquids.ETHYL_ACETATE) > 0)
        {
            return 0;
        }
        int amount = Math.Min(extract, Math.Max(MIN_UL, (int)(extract * RATE)));
        NcLiquids.Set(container, NcLiquids.PU_EXTRACT, extract - amount);
        NcLiquids.Add(container, NcLiquids.PLUTONIUM, amount);
        return 1;
    }
}

[HarmonyPatch(typeof(ChemicalHelper), "HandleTemperatureReaction")]
public static class NcTemperatureReactionPatch
{
    private static void Postfix(GameItem container)
    {
        try
        {
            if (container == null || WaterHelper.IsEmpty(container))
            {
                return;
            }
            int changed = 0;
            if (Barrel.IsOpened(container))
            {
                changed |= Barrel.Tick(container);
                if (!Barrel.IsOpened(container))
                {
                    return;
                }
            }
            changed |= Extraction.Run(container) | Extraction.Release(container);
            if (changed > 0)
            {
                ItemFeature waterFeature = WaterFeatureHelper.GetWaterFeature(container);
                WaterFeatureHelper.OnLiquidChanged(container, waterFeature != null && waterFeature.IsExposed());
            }
        }
        catch (Exception e)
        {
            NcLog.Error("reaction failed", e);
        }
    }
}

// Pouring out of an opened barrel leaves the radioactive contents behind: hide them from the vanilla
// transfer and put them back afterwards.
[HarmonyPatch(typeof(WaterHelper), "TransferLiquid")]
public static class BarrelPourPatch
{
    private static void Prefix(GameItem sourceContainer, out Dictionary<string, int> __state)
    {
        __state = null;
        if (!Barrel.IsOpened(sourceContainer))
        {
            return;
        }
        // Only the organic phase leaves: the plutonium extract and evaporative liquids (ethyl acetate,
        // alcohol, water). Radioactive contents and the non-volatile raffinate (acid, reducing agent,
        // impurities) stay behind and are vitrified with the fission products.
        __state = new Dictionary<string, int>();
        foreach (Liquid liquid in Liquid.Liquids)
        {
            if (Barrel.CanLeave(liquid))
            {
                continue;
            }
            int amount = sourceContainer.GetTagReadonly(liquid.currentPart).GetInt();
            if (amount > 0)
            {
                __state[liquid.identifier] = amount;
                NcLiquids.Set(sourceContainer, liquid.identifier, 0);
            }
        }
    }

    private static void Postfix(GameItem sourceContainer, Dictionary<string, int> __state)
    {
        if (__state == null)
        {
            return;
        }
        foreach (KeyValuePair<string, int> entry in __state)
        {
            NcLiquids.Add(sourceContainer, entry.Key, entry.Value);
        }
    }
}

// A crowbar dragged onto an owned nuclear waste barrel pries it open.
[HarmonyPatch(typeof(MeleeWeaponItemDirectory), "CreateCrowbarWeapon")]
public static class CrowbarPatch
{
    private static void Postfix(GameItem __result)
    {
        if (__result == null)
        {
            return;
        }
        __result.mayThisTargetItemFunc = (Func<GameItem, GameItem, bool>)Delegate.Combine(__result.mayThisTargetItemFunc,
            (Func<GameItem, GameItem, bool>)((GameItem s, GameItem t) => CanPry(t)));
        __result.canThisTargetItemFunc = (Func<GameItem, GameItem, bool>)Delegate.Combine(__result.canThisTargetItemFunc,
            (Func<GameItem, GameItem, bool>)((GameItem s, GameItem t) => CanPry(t)));
        __result.onThisTargetItemFunc = (Action<GameItem, GameItem>)Delegate.Combine(__result.onThisTargetItemFunc,
            (Action<GameItem, GameItem>)delegate(GameItem s, GameItem t)
            {
                if (CanPry(t))
                {
                    Pry(t);
                }
            });
    }

    private static bool CanPry(GameItem target)
    {
        return target != null && target.identifier == Barrel.WASTE_ID && GeneralHelper.IsItemOwned(target);
    }

    private static void Pry(GameItem waste)
    {
        GameItem opened = DirectoryMaster.Item(Barrel.OPENED_ID, true);
        waste.Destroy();
        ModHelper.SetItemOwnerShip(opened, true);
        ModHelper.MoveItemToInventory(opened);
        AudioHelper.UseWaterFilter();
    }
}

public static class Ampoule
{
    public const string ITEM_ID = "nc.pu_ampoule";
    public const int DOSE_UL = 100;
    public const double MIN_PU_SHARE = 0.9;
    public const long VALUE = 40L;
    public const int DRINK_BONUS = 50;
    public const string NUCLEAR_TAG = "NC_NUCLEAR_DRINK_TAG";

    public static GameItem Create()
    {
        GameItem item = ItemDirectory.CreateEmptyItem(ITEM_ID);
        try
        {
            item.name = NcText.Get("ampoule_name");
            item.shortDescription = NcText.Get("ampoule_desc");
            item.flavorText = NcText.Get("ampoule_flavor");
            item.unitValue = VALUE;
            item.SetGameItemType("MEDICAL");
            ContrabandHelper.InitContrabandItem(item, 3);
            // Onto a drink (nuclear drink) or onto a plutonium battery (self-charge, see PuBattery.cs).
            item.mayThisTargetItemFunc = (GameItem s, GameItem t) => CanSpike(s, t) || PuBattery.CanFuel(s, t);
            item.canThisTargetItemFunc = (GameItem s, GameItem t) => CanSpike(s, t) || PuBattery.CanFuel(s, t);
            item.onThisTargetItemFunc = delegate(GameItem s, GameItem t)
            {
                if (PuBattery.CanFuel(s, t))
                {
                    PuBattery.Fuel(s, t);
                }
                else if (CanSpike(s, t))
                {
                    Spike(s, t);
                }
            };
            NcSprites.Apply(item, NcSprites.AMPOULE);
            AudioHelper.InitPlasticVial(item);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            NcLog.Error("create " + ITEM_ID + " failed", e);
        }
        return item;
    }

    public static bool IsDrink(GameItem item)
    {
        if (item == null)
        {
            return false;
        }
        if (item.IsGameItemType("BREVAGE"))
        {
            return true;
        }
        // Finished wine, vanilla or blended.
        return item.IsTag("WINE_CONTAINER_TAG") && item.GetTagReadonly("WINE_CONTAINER_STATE").GetInt() == 2;
    }

    public static bool IsAmpoule(GameItem item)
    {
        return item != null && item.identifier == ITEM_ID;
    }

    public static bool CanSpike(GameItem ampoule, GameItem target)
    {
        return IsDrink(target) && !target.IsTag(NUCLEAR_TAG) && GeneralHelper.IsItemOwned(ampoule) && GeneralHelper.IsItemOwned(target);
    }

    private static void Spike(GameItem ampoule, GameItem drink)
    {
        drink.unitValue += DRINK_BONUS;
        drink.EnableTag(NUCLEAR_TAG, true);
        drink.name = NcText.Get("nuclear_drink_name", drink.name);
        ampoule.Destroy();
        // Refresh the validated tags now: otherwise the next ampoule of the same multi-select drop still sees an
        // unspiked drink (CanSpike reads IsTag) and spikes it again.
        drink.ValidateShapeState();
    }

    public static void RegisterRecipe()
    {
        if (FinisherRecipes.Find(ITEM_ID) != null)
        {
            return;
        }
        ChemFinisherRecipe recipe = new ChemFinisherRecipe();
        recipe.identifier = ITEM_ID;
        recipe.displayName = NcText.Get("ampoule_name");
        recipe.requiredVolume = DOSE_UL;
        recipe.isRequireDry = false;
        recipe.activeIngredientId = NcLiquids.PLUTONIUM;
        ChemFinisherRecipe.chemFinisherRecipes.Add(recipe);
    }

    public static void UnregisterRecipe()
    {
        ChemFinisherRecipe.chemFinisherRecipes.RemoveAll(r => r.identifier == ITEM_ID);
    }

    public static void RefreshRecipeName()
    {
        ChemFinisherRecipe recipe = FinisherRecipes.Find(ITEM_ID);
        if (recipe != null)
        {
            recipe.displayName = NcText.Get("ampoule_name");
        }
    }
}

// The finisher only presses an ampoule from liquid that is at least 90% plutonium.
[HarmonyPatch(typeof(ChemFinisherRecipe), "CanOutput")]
public static class AmpouleRecipePatch
{
    private static void Postfix(ChemFinisherRecipe __instance, GameItem container, ref bool __result)
    {
        if (!__result || __instance.identifier != Ampoule.ITEM_ID || container == null)
        {
            return;
        }
        int total = WaterHelper.GetTotalVolume(container);
        __result = total > 0 && NcLiquids.Get(container, NcLiquids.PLUTONIUM) >= total * Ampoule.MIN_PU_SHARE;
    }
}

// Nuclear drinks are not contraband, but only Upper Level clients and addicts buy them.
[HarmonyPatch(typeof(StoreClient), "IsClientRefusingItem")]
public static class NuclearDrinkBuyerPatch
{
    private static void Postfix(StoreClient __instance, GameItem gameItem, ref bool __result)
    {
        if (!__result && Refuses(__instance, gameItem))
        {
            __result = true;
        }
    }

    public static bool Refuses(StoreClient client, GameItem gameItem)
    {
        if (client == null || gameItem == null || !gameItem.IsTag(Ampoule.NUCLEAR_TAG))
        {
            return false;
        }
        bool upper = client.IsFaction("FACTION_UPPER_LEVEL");
        bool addict = client.identifier != null && client.identifier.IndexOf("ddict", StringComparison.Ordinal) >= 0;
        return !upper && !addict;
    }
}

public static class Radiation
{
    public const string LEVEL_KEY = "nc_radiation_sickness";
    public const int STEP = 5;
    public const int MAX = 25;
    public const string FEATURE_ID = "ncRadiationSellPenalty";
    public const string NIGHT_LOG_RED = "9c3738";

    public static int Level
    {
        get { return ModHelper.GetModDataInt(DreamToxinMod.MOD_ID, LEVEL_KEY, 0); }
        set { ModHelper.SetModDataInt(DreamToxinMod.MOD_ID, LEVEL_KEY, Math.Max(0, Math.Min(MAX, value))); }
    }

    // Any opened barrel still holding fission products (or spent fuel) overnight makes the player sicker.
    public static void OnNight()
    {
        try
        {
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (Barrel.IsOpened(item) && NcLiquids.RadioactiveTotal(item) > 0)
                {
                    int before = Level;
                    Level = before + STEP;
                    string key = before == 0 ? "nightlog_radiation_new" : (Level > before ? "nightlog_radiation_worse" : "nightlog_radiation_max");
                    // Red, like vanilla's bad news in the night report (shown at the start of the next day).
                    PlayerStore.Instance.AddNightLog(NcText.Get(key, Level), NIGHT_LOG_RED);
                    NcLog.Info("radiation sickness now " + Level + "%");
                    return;
                }
            }
        }
        catch (Exception e)
        {
            NcLog.Error("nightly radiation check failed", e);
        }
    }

    public static void OnWakingUp()
    {
        try
        {
            if (Level > 0)
            {
                StoreUIManager.Instance.Notify(NcText.Get("radiation_notice", Level));
            }
        }
        catch (Exception e)
        {
            NcLog.Error("radiation notice failed", e);
        }
    }

    public static ItemFeature CreateSellPenalty()
    {
        ItemFeature feature = new ItemFeature(ItemFeature.FeatureType.TemporarySelling);
        feature.identifier = FEATURE_ID;
        feature.category = FEATURE_ID;
        feature.valueStage = ItemFeature.ValueStage.Market;
        string display = NcText.Get("radiation_penalty");
        feature.SetPublicDisplay(display);
        feature.SetActualDisplay(display);
        feature.isFeatureMatch = true;
        feature.SetPreExposedValueMod(-Level);
        return feature;
    }
}

// Like vanilla addiction penalties: items the player sells get the radiation penalty.
[HarmonyPatch(typeof(GameItem), "OnAddedToWeightedArea")]
public static class RadiationSellPatch
{
    private static void Postfix(GameItem __instance)
    {
        try
        {
            if (Radiation.Level <= 0 || PlayerStore.Instance.currentClientInstance == null || !GeneralHelper.IsItemOwned(__instance))
            {
                return;
            }
            StoreClient.ClientIntent intent = PlayerStore.Instance.currentClientInstance.GetClientBlueprint().clientIntent;
            if (intent != StoreClient.ClientIntent.BUY && intent != StoreClient.ClientIntent.SELLNBUY)
            {
                return;
            }
            __instance.RemoveItemFeatureByID(Radiation.FEATURE_ID);
            __instance.AddItemFeature(Radiation.CreateSellPenalty());
        }
        catch (Exception e)
        {
            NcLog.Error("radiation sell penalty failed", e);
        }
    }
}

// Each genuine Immunivax shot also cures 5% radiation sickness.
public static class ImmunivaxCure
{
    public static bool IsGenuine(GameItem gameItem)
    {
        ItemFeature feature = gameItem != null ? gameItem.FindItemFeatureByID("CATEGORY_GENUINE_INJECTOR") : null;
        return feature != null && feature.realCondition != null && feature.realCondition.identifier == "genuineInjector";
    }

    // Returns whether it cured anything.
    public static bool TryCure(GameItem gameItem)
    {
        if (!IsGenuine(gameItem) || Radiation.Level <= 0)
        {
            return false;
        }
        Radiation.Level = Radiation.Level - Radiation.STEP;
        StoreUIManager.Instance.Notify(NcText.Get("radiation_cured", Radiation.Level));
        return true;
    }
}

// Sentinels inject through vanilla's ConsumeImmunivax; the cure rides along.
[HarmonyPatch(typeof(HealthData), "ConsumeImmunivax")]
public static class ImmunivaxPatch
{
    private static void Postfix(GameItem gameItem)
    {
        try
        {
            ImmunivaxCure.TryCure(gameItem);
        }
        catch (Exception e)
        {
            NcLog.Error("immunivax radiation cure failed", e);
        }
    }
}

// Vanilla only lets Sentinel-start players inject Immunivax. Also allow it while the player has radiation sickness;
// for non-Sentinels the shot skips vanilla's Sentinel messages and only treats the radiation (fakes do nothing).
[HarmonyPatch(typeof(ConsumableHelper), "InitImmunivax")]
public static class ImmunivaxUsePatch
{
    private static void Postfix(GameItem gameItem)
    {
        try
        {
            if (gameItem == null)
            {
                return;
            }
            Func<GameItem, GameInventory, SlotMarker, bool> can = gameItem.canActivateSlotItemFunc;
            Func<GameItem, GameInventory, SlotMarker, bool> may = gameItem.mayActivateSlotItemFunc;
            Action<GameItem, GameInventory, SlotMarker> use = gameItem.onActivateSlotItemFunc;
            gameItem.canActivateSlotItemFunc = (GameItem item, GameInventory inventory, SlotMarker slot) =>
                (can != null && can(item, inventory, slot)) || ForRadiation(item);
            gameItem.mayActivateSlotItemFunc = (GameItem item, GameInventory inventory, SlotMarker slot) =>
                (may != null && may(item, inventory, slot)) || ForRadiation(item);
            gameItem.onActivateSlotItemFunc = delegate(GameItem item, GameInventory inventory, SlotMarker slot)
            {
                if (HealthData.IsSentinel() || use == null)
                {
                    if (use != null)
                    {
                        use(item, inventory, slot);
                    }
                    return;
                }
                try
                {
                    if (!ImmunivaxCure.TryCure(item))
                    {
                        StoreUIManager.Instance.Notify(NcText.Get("radiation_immunivax_no_effect"));
                    }
                }
                catch (Exception e)
                {
                    NcLog.Error("immunivax radiation shot failed", e);
                }
                item.Destroy();
            };
        }
        catch (Exception e)
        {
            NcLog.Error("immunivax use patch failed", e);
        }
    }

    private static bool ForRadiation(GameItem item)
    {
        return GeneralHelper.IsItemOwned(item) && Radiation.Level > 0;
    }
}

// A waste broker who sells barrels of nuclear waste once a week.
public static class WasteMerchant
{
    public const string LAST_VISIT_KEY = "nc_merchant_last_visit";
    public const int INTERVAL_DAYS = 7;
    public const long BARREL_PRICE = 50L;

    public static void OnGenerateCustomers(StoreClientManager manager)
    {
        try
        {
            int day = StoreStation.GetDayCounter();
            int last = ModHelper.GetModDataInt(DreamToxinMod.MOD_ID, LAST_VISIT_KEY, 0);
            if (day < INTERVAL_DAYS || day - last < INTERVAL_DAYS)
            {
                return;
            }
            ModHelper.SetModDataInt(DreamToxinMod.MOD_ID, LAST_VISIT_KEY, day);
            manager.AddClient(Create());
        }
        catch (Exception e)
        {
            NcLog.Error("waste merchant failed", e);
        }
    }

    private static StoreClient Create()
    {
        StoreClient client = new StoreClient();
        client.identifier = "nc_waste_broker";
        client.nonShootable = true;
        client.displayName = NcText.Get("merchant_name");
        client.spriteName = SpriteDict.GetRandomLowerLevelSpriteName();
        client.clientFaction = "FACTION_LOWER_LEVEL";
        StoreClientFactionSetup.InitLowerLevelCitizen(client);
        client.clientIntent = StoreClient.ClientIntent.SELL;
        client.mainDialogue.SetText(client.displayName, NcText.Get("merchant_line")).SetEndAction(delegate
        {
            int barrels = UnityEngine.Random.Range(1, 4);
            for (int i = 0; i < barrels; i++)
            {
                GameItem barrel = DirectoryMaster.Item(Barrel.WASTE_ID, true);
                barrel.unitValue = BARREL_PRICE;
                PlayerStore.Instance.AddDirectSellingItemToTable(barrel, false, false, false, 0);
            }
        });
        client.AddBasicDialog();
        client.CompleteClientCreation(false);
        ModHelper.SetClientSource(client, DreamToxinMod.MOD_ID);
        return client;
    }
}

public static class NcSprites
{
    public const string OPENED = "nc_opened_waste";
    public const string VITRIFIED = "nc_vitrified_waste";
    public const string AMPOULE = "nc_pu_ampoule";

    public static void Apply(GameItem item, string key)
    {
        LoadedMod mod = ModLoader.Mods.Find(m => m.Manifest != null && m.Manifest.ID == DreamToxinMod.MOD_ID);
        if (mod != null && mod.Sprites.ContainsKey(key))
        {
            item.SetSpriteAndShapeFromMod(DreamToxinMod.MOD_ID, key);
        }
    }

    private static string KeyFor(GameItem item)
    {
        if (item == null)
        {
            return null;
        }
        if (item.identifier == Barrel.OPENED_ID)
        {
            return OPENED;
        }
        if (item.identifier == Barrel.VITRIFIED_ID)
        {
            return VITRIFIED;
        }
        return item.identifier == Ampoule.ITEM_ID ? AMPOULE : null;
    }

    // Mod sprites may not be loaded yet when saved items are recreated.
    public static void FixLoadedItems()
    {
        try
        {
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                string key = KeyFor(item);
                if (key != null && (!item.useSpriteFromMod || item.spritePath != key))
                {
                    Apply(item, key);
                }
            }
        }
        catch (Exception e)
        {
            NcLog.Error("updating saved sprites failed", e);
        }
    }
}

public static class NcMath
{
    private static readonly System.Random random = new System.Random();

    public static int RoundRandom(double value)
    {
        if (value <= 0.0)
        {
            return 0;
        }
        int whole = (int)value;
        return whole + (random.NextDouble() < value - whole ? 1 : 0);
    }
}

public static class NcDebugKit
{
    public static void SpawnBarrel()
    {
        Give(DirectoryMaster.Item(Barrel.WASTE_ID, true));
        Give(DirectoryMaster.Item("crowbar", true));
    }

    // Enough for one barrel: 600 mL pure acid, 25 mL reducing agent, 120 mL ethyl acetate, 60 mL water glass.
    public static void SpawnReagents()
    {
        Give(Filled("water_jug", "acid", 600000));
        Give(Filled("large_bottled_water", "reducing_agent", 25000));
        if (NcLiquids.Exists(NcLiquids.ETHYL_ACETATE))
        {
            Give(Filled("large_bottled_water", NcLiquids.ETHYL_ACETATE, 120000));
        }
        if (NcLiquids.Exists(NcLiquids.WATER_GLASS))
        {
            Give(Filled("large_bottled_water", NcLiquids.WATER_GLASS, 60000));
        }
        Give(PreBuildChemHelper.CreateBaseBottle());
        Give(PreBuiltItemHelper.CreateRealGenuineInjector());
    }

    public static void SpawnAmpoule()
    {
        Give(DirectoryMaster.Item(Ampoule.ITEM_ID, true));
    }

    public static void AddRadiation()
    {
        Radiation.Level = Radiation.Level + Radiation.STEP;
        NcLog.Info("radiation sickness now " + Radiation.Level + "%");
    }

    public static void MerchantTomorrow()
    {
        ModHelper.SetModDataInt(DreamToxinMod.MOD_ID, WasteMerchant.LAST_VISIT_KEY, -WasteMerchant.INTERVAL_DAYS);
        NcLog.Info("waste broker will come at the next customer generation (day 7 or later)");
    }

    private static GameItem Filled(string containerId, string liquidId, int ul)
    {
        GameItem container = DirectoryMaster.Item(containerId, true);
        foreach (Liquid liquid in Liquid.Liquids)
        {
            NcLiquids.Set(container, liquid.identifier, 0);
        }
        WaterHelper.AddLiquid(container, liquidId, ul);
        return container;
    }

    private static void Give(GameItem item)
    {
        if (item == null)
        {
            return;
        }
        ModHelper.SetItemOwnerShip(item, true);
        ModHelper.MoveItemToInventory(item);
    }
}

public static class NcText
{
    public static string Get(string key, params object[] args)
    {
        return ModHelper.GetLocalized(DreamToxinMod.MOD_ID, key, args);
    }
}

public static class NcLog
{
    public static void Info(string message)
    {
        UnityEngine.Debug.Log("[VanillaChemExt/Nuclear] " + message);
    }

    public static void Error(string context, Exception e)
    {
        UnityEngine.Debug.LogError("[VanillaChemExt/Nuclear] " + context + ": " + e);
    }
}

// Vanilla wine bottles veto anything dragged onto them that cannot fill them (WineHelper.CanFillBottle, used as the
// bottle's mayItemTargetThisFunc / canItemTargetThisFunc), and a finished wine can never be filled, so a plutonium
// ampoule was always refused on wine (since 1.3.0). Let the ampoule through; the bottle's own drop action only
// pours water rations, so the ampoule's Spike still does the work.
[HarmonyPatch(typeof(WineHelper), "CanFillBottle")]
public static class NuclearWineTargetPatch
{
    private static void Postfix(GameItem source, GameItem bottle, ref bool __result)
    {
        try
        {
            if (!__result && Ampoule.IsAmpoule(source) && Ampoule.CanSpike(source, bottle))
            {
                __result = true;
            }
        }
        catch (Exception e)
        {
            NcLog.Error("nuclear wine target check failed", e);
        }
    }
}

// An emptied (poured or drunk) nuclear wine bottle is a plain bottle again.
[HarmonyPatch(typeof(WineHelper), "ResetToEmptyBottle")]
public static class NuclearWineResetPatch
{
    private static void Postfix(GameItem item)
    {
        if (item != null)
        {
            item.DisableTag(Ampoule.NUCLEAR_TAG, true);
        }
    }
}
