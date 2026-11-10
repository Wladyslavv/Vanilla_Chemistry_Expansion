// Ore chemistry: metal ore can be blended (only in a blender with all 4 chem modules) into a powder of
// calcite, quartz sand and impurities. Calcite becomes quicklime, which purifies base by causticizing
// its chemical contaminant; quartz becomes water glass and then silica gel, which strips chemical
// contaminant from acid when heated. Heavy metal settles out of strong base on its own and is removed
// from strong acid by freezing (at the cost of an equal amount of acid).
// C# 5 only; optional arguments on tag helpers are passed explicitly (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using HarmonyLib;

public static class OreLiquids
{
    public const string CALCITE = "vce_calcite";
    public const string QUARTZ = "vce_quartz";
    public const string QUICKLIME = "vce_quicklime";
    public const string CALCIUM_CARBONATE = "vce_calcium_carbonate";
    public const string WATER_GLASS = "vce_water_glass";
    public const string SILICA_GEL = "vce_silica_gel";

    // Graded filtering: Kotton fabric (pore 150) only catches the coarse quartz sand (200); the water
    // filters (pore 22) also catch the fine solids (100), including silica gel granules. Dissolved liquids (5)
    // such as water glass pass every filter.
    // Columns: id, name key, particle size, colour, colour weight in mixtures.
    private static readonly object[][] DEFINITIONS = new object[][]
    {
        new object[] { CALCITE, "liquid_calcite", 100, "e3d6b0", 2 },
        new object[] { QUARTZ, "liquid_quartz", 200, "8a857a", 2 },
        new object[] { QUICKLIME, "liquid_quicklime", 100, "f7f5ef", 2 },
        new object[] { CALCIUM_CARBONATE, "liquid_calcium_carbonate", 100, "d6cfbd", 2 },
        new object[] { WATER_GLASS, "liquid_water_glass", 5, "6fb3ad", 2 },
        new object[] { SILICA_GEL, "liquid_silica_gel", 100, "b4dbe8", 2 }
    };

    public static void Register()
    {
        foreach (object[] definition in DEFINITIONS)
        {
            RegisterLiquid((string)definition[0], (string)definition[1], (int)definition[2], (string)definition[3], (int)definition[4]);
        }
    }

    public static void RefreshNames()
    {
        foreach (object[] definition in DEFINITIONS)
        {
            Liquid liquid;
            if (Liquid.LiquidDict.TryGetValue((string)definition[0], out liquid))
            {
                liquid.displayName = Text.Get((string)definition[1]);
            }
        }
    }

    private static void RegisterLiquid(string id, string nameKey, int particleSize, string color, int colorMultiplier)
    {
        if (Liquid.LiquidDict.ContainsKey(id))
        {
            return;
        }
        // SetIdentifier is private, so set its fields directly (same as the Dream Toxin liquid).
        Liquid liquid = new Liquid();
        liquid.displayName = Text.Get(nameKey);
        liquid.identifier = id;
        liquid.part = "PART_" + id.ToUpperInvariant();
        liquid.currentPart = "CURRENT_PART_" + id.ToUpperInvariant();
        liquid.baseMaxPurityFromMachine = 12;
        liquid.basePurifierRemoveAmount = 4000;
        liquid.particleSize = particleSize;
        liquid.filterMinimumPurity = 0.0016;
        liquid.filterVolumeRemoved = 1500;
        liquid.color = color;
        liquid.colorMultiplier = colorMultiplier;
        Liquid.Liquids.Add(liquid);
        Liquid.LiquidDict[id] = liquid;
    }
}

public static class OreItems
{
    public const string ORE_ID = "common_ore";
    public const int REQUIRED_BLENDER_MODULES = 4;

    // Millilitres of powder per metal ore as { liquid, typical, min, max }. Ores in inventory carry the
    // typical values (so scanners show something sensible); the actual mix is rolled when the blender crushes one.
    private static readonly object[][] COMPOSITION = new object[][]
    {
        new object[] { OreLiquids.CALCITE, 12.0, 9.0, 15.0 },
        new object[] { OreLiquids.QUARTZ, 14.0, 11.0, 17.0 },
        new object[] { "mineral", 6.0, 4.0, 8.0 },
        new object[] { "heavy_metal", 4.0, 2.5, 5.5 },
        new object[] { "physical_contaminant", 4.0, 2.0, 6.0 }
    };

    private static readonly Random random = new Random();

    public static void InitOre(GameItem ore)
    {
        ChemicalHelper.InitChemItem(ore, true, false);
        foreach (object[] part in COMPOSITION)
        {
            ChemicalHelper.SetChemContentML(ore, (string)part[0], (double)part[1]);
        }
    }

    public static void RollComposition(GameItem ore)
    {
        foreach (object[] part in COMPOSITION)
        {
            double min = (double)part[2];
            double max = (double)part[3];
            ChemicalHelper.SetChemContentML(ore, (string)part[0], min + random.NextDouble() * (max - min));
        }
    }

    // Chem modules in the blender holding this ore, or -1 when the blender can't be found.
    public static int BlenderModules(GameItem ore)
    {
        GameInventory inventory = ore.parentInventory;
        GameItem blender = inventory == null ? null : inventory.GetParentItem();
        return blender == null ? -1 : MachineBlender.GetUpgradeCount(blender);
    }

    // Vanilla passes the blender's content loss, which is 0 only with 4 or more modules (MachineBlender.GetLoss).
    // It is computed from the machine itself, so it still works when the ore's parent can't be resolved.
    public static bool CanBlend(GameItem ore, int loss)
    {
        return loss <= 0 || BlenderModules(ore) >= REQUIRED_BLENDER_MODULES;
    }

    // Ores already in a save from before the mod was installed never went through the MetalOre patch, so they
    // were not blendable at all and the blender silently skipped them.
    public static void FixLoadedItems()
    {
        try
        {
            int count = 0;
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (item != null && item.identifier == ORE_ID && !item.IsTag("BLENDABLE_TAG"))
                {
                    InitOre(item);
                    count++;
                }
            }
            if (count > 0)
            {
                Log.Info("made " + count + " saved metal ore(s) crushable");
            }
        }
        catch (Exception e)
        {
            Log.Error("fixing saved ore failed", e);
        }
    }

    private static float lastNotice = -100f;

    // Tells the player why an ore stayed in the blender (once per button press, at most every 2 seconds).
    public static void NoticeNotCrushed(GameItem ore)
    {
        try
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now - lastNotice < 2f || StoreUIManager.Instance == null)
            {
                return;
            }
            lastNotice = now;
            int modules = BlenderModules(ore);
            StoreUIManager.Instance.Notify(Text.Get("ore_needs_modules", REQUIRED_BLENDER_MODULES, modules < 0 ? 0 : modules), "default");
        }
        catch (Exception e)
        {
            Log.Error("ore notice failed", e);
        }
    }
}

[HarmonyPatch(typeof(MaterialDirectory), "MetalOre")]
public static class MetalOrePatch
{
    private static void Postfix(GameItem __result)
    {
        try
        {
            if (__result != null)
            {
                OreItems.InitOre(__result);
            }
        }
        catch (Exception e)
        {
            Log.Error("ore composition failed", e);
        }
    }
}

[HarmonyPatch(typeof(ChemicalHelper), "BlendInto")]
public static class BlendOrePatch
{
    // Ore is only crushed by a blender carrying all four chem modules; otherwise it stays in the input grid.
    // Each crushed ore gets its own randomly rolled composition.
    private static bool Prefix(GameItem chemicalItem, int loss)
    {
        try
        {
            if (chemicalItem != null && chemicalItem.identifier == OreItems.ORE_ID)
            {
                if (!OreItems.CanBlend(chemicalItem, loss))
                {
                    OreItems.NoticeNotCrushed(chemicalItem);
                    return false;
                }
                OreItems.RollComposition(chemicalItem);
            }
        }
        catch (Exception e)
        {
            Log.Error("ore blend check failed", e);
        }
        return true;
    }
}

public static class OreReactions
{
    public const double CALCINE_TEMP = 150.0;
    public const double QUICKLIME_YIELD = 0.55;
    public const double CAUSTICIZE_FAST_TEMP = 80.0;
    public const double WATER_GLASS_TEMP = 120.0;
    public const double DEFLUORINATE_TEMP = 100.0;
    public const double FREEZE_MAX_TEMP = -5.0;
    public const double STRONG_BASE_PERCENT = 50.0;
    public const double STRONG_ACID_PERCENT = 80.0;
    public const int CONTAMINANT_PER_GEL = 4;

    // Set by the fridge freezer zone every tick it is powered; counts down in the container's own tick.
    public const string FREEZE_TAG = "VCE_FREEZE_TICKS_INT";
    public const int FREEZE_TICKS = 3;

    public static int Run(GameItem container)
    {
        if (container == null || WaterHelper.IsEmpty(container))
        {
            return 0;
        }
        bool frozen = ConsumeFreezeTick(container);
        double temp = container.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
        int changed = 0;
        changed |= Calcine(container, temp);
        changed |= MakeWaterGlass(container, temp);
        changed |= MakeSilicaGel(container);
        changed |= PurifyBase(container, temp);
        changed |= PurifyAcid(container, temp, frozen);
        return changed;
    }

    // Calcite -> quicklime (the rest leaves as CO2). Needs a dry container and an upgraded heating plate.
    private static int Calcine(GameItem c, double temp)
    {
        int calcite = Get(c, OreLiquids.CALCITE);
        if (calcite <= 0 || temp < CALCINE_TEMP || WaterHelper.IsWet(c))
        {
            return 0;
        }
        int amount = Step(calcite, 0.05, 200);
        Set(c, OreLiquids.CALCITE, calcite - amount);
        Add(c, OreLiquids.QUICKLIME, (int)Math.Round(amount * QUICKLIME_YIELD));
        return 1;
    }

    // Quartz dissolves in hot base as water glass, 1:1.
    private static int MakeWaterGlass(GameItem c, double temp)
    {
        int quartz = Get(c, OreLiquids.QUARTZ);
        int baseAmount = Get(c, "base");
        if (quartz <= 0 || baseAmount <= 0 || temp < WATER_GLASS_TEMP)
        {
            return 0;
        }
        int amount = Step(Math.Min(quartz, baseAmount), 0.05, 200);
        Set(c, OreLiquids.QUARTZ, quartz - amount);
        Set(c, "base", baseAmount - amount);
        Add(c, OreLiquids.WATER_GLASS, amount);
        return 1;
    }

    // Water glass + acid -> silica gel + salt (debris), 1:1.
    private static int MakeSilicaGel(GameItem c)
    {
        int waterGlass = Get(c, OreLiquids.WATER_GLASS);
        int acid = Get(c, "acid");
        if (waterGlass <= 0 || acid <= 0)
        {
            return 0;
        }
        int amount = Step(Math.Min(waterGlass, acid), 0.10, 200);
        Set(c, OreLiquids.WATER_GLASS, waterGlass - amount);
        Set(c, "acid", acid - amount);
        Add(c, OreLiquids.SILICA_GEL, amount);
        Add(c, "debris", amount);
        return 1;
    }

    // Strong base: quicklime causticizes the chemical contaminant (treated as carbonate) back into base plus
    // calcium carbonate; heavy metal settles out as hydroxide on its own.
    private static int PurifyBase(GameItem c, double temp)
    {
        int total = WaterHelper.GetTotalVolume(c);
        int baseAmount = Get(c, "base");
        if (total <= 0 || baseAmount * 100.0 / total < STRONG_BASE_PERCENT)
        {
            return 0;
        }
        int changed = 0;
        int quicklime = Get(c, OreLiquids.QUICKLIME);
        int contaminant = Get(c, "chemical_contaminant");
        if (quicklime > 0 && contaminant > 0)
        {
            double rate = temp >= CAUSTICIZE_FAST_TEMP ? 0.10 : 0.05;
            int amount = Step(Math.Min(quicklime, contaminant), rate, 100);
            Set(c, OreLiquids.QUICKLIME, quicklime - amount);
            Set(c, "chemical_contaminant", contaminant - amount);
            Set(c, "base", baseAmount + amount);
            Add(c, OreLiquids.CALCIUM_CARBONATE, amount);
            changed = 1;
        }
        int heavyMetal = Get(c, "heavy_metal");
        if (heavyMetal > 0)
        {
            int amount = Step(heavyMetal, 0.05, 100);
            Set(c, "heavy_metal", heavyMetal - amount);
            Add(c, "debris", amount);
            changed = 1;
        }
        return changed;
    }

    // Strong acid: hot silica gel drives the chemical contaminant off as a gas (1 gel per 4 contaminant);
    // frozen acid crystallizes, leaving heavy metal (and an equal amount of acid) in the mother liquor.
    private static int PurifyAcid(GameItem c, double temp, bool frozen)
    {
        int total = WaterHelper.GetTotalVolume(c);
        int acid = Get(c, "acid");
        if (total <= 0 || acid * 100.0 / total < STRONG_ACID_PERCENT)
        {
            return 0;
        }
        int changed = 0;
        int gel = Get(c, OreLiquids.SILICA_GEL);
        int contaminant = Get(c, "chemical_contaminant");
        if (gel > 0 && contaminant > 0 && temp >= DEFLUORINATE_TEMP)
        {
            int amount = Math.Min(Step(contaminant, 0.05, 100), gel * CONTAMINANT_PER_GEL);
            int gelUsed = Math.Min(gel, (amount + CONTAMINANT_PER_GEL - 1) / CONTAMINANT_PER_GEL);
            Set(c, "chemical_contaminant", contaminant - amount);
            Set(c, OreLiquids.SILICA_GEL, gel - gelUsed);
            changed = 1;
        }
        if (frozen || temp <= FREEZE_MAX_TEMP)
        {
            int heavyMetal = Get(c, "heavy_metal");
            if (heavyMetal > 0)
            {
                int amount = Math.Min(Step(heavyMetal, 0.05, 100), acid);
                Set(c, "heavy_metal", heavyMetal - amount);
                Set(c, "acid", acid - amount);
                Add(c, "debris", amount * 2);
                changed = 1;
            }
            int leftoverGel = Get(c, OreLiquids.SILICA_GEL);
            if (leftoverGel > 0)
            {
                int amount = Step(leftoverGel, 0.05, 100);
                Set(c, OreLiquids.SILICA_GEL, leftoverGel - amount);
                Add(c, "debris", amount);
                changed = 1;
            }
        }
        return changed;
    }

    public static void MarkFrozen(GameItem container)
    {
        container.ModifyTag(FREEZE_TAG, delegate(TagState state)
        {
            state.Enable().SetInt(FREEZE_TICKS);
        }, true);
    }

    private static bool ConsumeFreezeTick(GameItem container)
    {
        if (!container.IsTag(FREEZE_TAG))
        {
            return false;
        }
        int ticks = container.GetTagReadonly(FREEZE_TAG).GetInt();
        if (ticks <= 0)
        {
            return false;
        }
        container.ModifyTag(FREEZE_TAG, delegate(TagState state)
        {
            state.SetInt(ticks - 1);
        }, true);
        return true;
    }

    // A fraction of the available amount per tick, at least minUl, never more than available.
    private static int Step(int available, double rate, int minUl)
    {
        return Math.Min(available, Math.Max(minUl, (int)Math.Round(available * rate)));
    }

    private static int Get(GameItem c, string liquidId)
    {
        return c.GetTagReadonly(Liquid.GetCurrentPartFromId(liquidId)).GetInt();
    }

    private static void Set(GameItem c, string liquidId, int value)
    {
        c.ModifyTag(Liquid.GetCurrentPartFromId(liquidId), delegate(TagState state)
        {
            state.Enable().SetInt(Math.Max(0, value));
        }, true);
    }

    private static void Add(GameItem c, string liquidId, int value)
    {
        Set(c, liquidId, Get(c, liquidId) + value);
    }
}

public static class OreDebugKit
{
    public static void SpawnOreKit()
    {
        Give(DirectoryMaster.Item(OreItems.ORE_ID, true));
        Give(DirectoryMaster.Item(OreItems.ORE_ID, true));
        Give(DirectoryMaster.Item("med_bottle_red", true));
        Give(DirectoryMaster.Item("med_bottle_red", true));
        Give(PreBuildChemHelper.CreateAcidBottle());
        Give(PreBuildChemHelper.CreateBaseBottle());
    }

    // What one ore gives after filtering and calcining: quicklime plus the ore's unfilterable impurities.
    public static void SpawnQuicklimeReagent()
    {
        GameItem bottle = DirectoryMaster.Item("med_bottle_red", true);
        WaterHelper.AddLiquid(bottle, OreLiquids.QUICKLIME, 6600);
        WaterHelper.AddLiquid(bottle, "heavy_metal", 4000);
        AudioHelper.InitPlasticContainer(bottle);
        Give(bottle);
    }

    // What one ore gives after the water glass route: silica gel, impurities and wash water.
    public static void SpawnNotes()
    {
        Give(DirectoryMaster.Item(Notes.OreNotes.ItemId, true));
    }

    public static void SpawnSilicaGelReagent()
    {
        GameItem bottle = DirectoryMaster.Item("med_bottle_red", true);
        WaterHelper.AddLiquid(bottle, OreLiquids.SILICA_GEL, 14000);
        WaterHelper.AddLiquid(bottle, "heavy_metal", 4000);
        WaterHelper.AddLiquid(bottle, "water", 50000);
        AudioHelper.InitPlasticContainer(bottle);
        Give(bottle);
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
