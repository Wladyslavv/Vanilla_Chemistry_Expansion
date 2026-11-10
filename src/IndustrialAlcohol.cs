// Industrial alcohol: starch -> sugar by acid hydrolysis, live yeast cultures that grow and ferment overnight,
// methanol from microbes, and freeze concentration with ice. Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

// Called from DreamToxinMod: industrial alcohol, flavour chemistry, blended wine and the advanced fabric.
public static class IndustrialAlcohol
{
    public static void Enable()
    {
        IaSettings.Register();
        ModHook.OnGameLoadedLate += YeastItems.SetupLoadedItems;
        ModHook.OnGameLoadedLate += BlendedWine.UpgradeLoadedItems;
        ModHook.OnGameLoadedLate += AdvancedFabric.FixLoadedItems;
        ModHook.OnGameLoadedLate += Beer.FixLoadedItems;
        ModHook.OnGameLoadedLate += Xinke.FixLoadedItems;
        ModHook.OnHandlingNightlyServicesEarly += Beer.OnNight;
        ModHook.OnCreateTooltipLate += BeerTooltip.OnCreateTooltip;
        try
        {
            string id = DreamToxinMod.MOD_ID;
            ModHelper.AddDebugFunction(id, "Modding", "Alcohol: ingredients kit", IaDebugKit.SpawnIngredients, false);
            ModHelper.AddDebugFunction(id, "Modding", "Alcohol: hydrolyzed mash (6 L jug)", IaDebugKit.SpawnMash, false);
            ModHelper.AddDebugFunction(id, "Modding", "Alcohol: 9% wash (2 L bottle)", IaDebugKit.SpawnWash, false);
            ModHelper.AddDebugFunction(id, "Modding", "Alcohol: run one night", IaDebugKit.RunNight, false);
            ModHelper.AddDebugFunction(id, "Modding", "Wine: flavour kit", IaDebugKit.SpawnFlavourKit, false);
            ModHelper.AddDebugFunction(id, "Modding", "Wine: blend base + ester", IaDebugKit.SpawnBlendBase, false);
            ModHelper.AddDebugFunction(id, "Modding", "Wine: perfect blended wine", IaDebugKit.SpawnPerfectWine, false);
            ModHelper.AddDebugFunction(id, "Modding", "Wine: both notes", IaDebugKit.SpawnNotes, false);
            ModHelper.AddDebugFunction(id, "Modding", "Filter: advanced Kotton fabric", IaDebugKit.SpawnAdvancedFabric, false);
            ModHelper.AddDebugFunction(id, "Modding", "Xinke: 2 seeds + 3 leaves", Xinke.SpawnKit, false);
            ModHelper.AddDebugFunction(id, "Modding", "Crackine: small + large street vial", Crackine.SpawnKit, false);
            ModHelper.AddDebugFunction(id, "Modding", "Crackine: 2 pharma-grade vials", Crackine.SpawnPharma, false);
            ModHelper.AddDebugFunction(id, "Modding", "Beer: premium keg", IaDebugKit.SpawnBeerKeg, false);
            ModHelper.AddDebugFunction(id, "Modding", "Beer: 2 L wort to press (5.5%)", IaDebugKit.SpawnBeerWort, false);
            ModHelper.AddDebugFunction(id, "Modding", "Beer: chill held keg", IaDebugKit.ChillKegs, false);
        }
        catch (Exception e)
        {
            IaLog.Error("debug function registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnGameLoadedLate -= YeastItems.SetupLoadedItems;
        ModHook.OnGameLoadedLate -= BlendedWine.UpgradeLoadedItems;
        ModHook.OnGameLoadedLate -= AdvancedFabric.FixLoadedItems;
        ModHook.OnGameLoadedLate -= Beer.FixLoadedItems;
        ModHook.OnGameLoadedLate -= Xinke.FixLoadedItems;
        ModHook.OnHandlingNightlyServicesEarly -= Beer.OnNight;
        ModHook.OnCreateTooltipLate -= BeerTooltip.OnCreateTooltip;
    }

    // Liquids, wine labels and the wine recipe; called once from Content.EnsureRegistered (see DreamToxinMod.cs).
    public static void RegisterContent()
    {
        IaLiquids.Register();
        FlavorChemistry.RegisterLiquids();
        BlendedWineLabels.Register();
        BlendedWine.RegisterRecipe();
        BeerQuality.Register();
        Beer.RegisterRecipe();
        Xinke.Register();
    }

    public static void UnregisterContent()
    {
        Xinke.Unregister();
        BlendedWine.UnregisterRecipe();
        BlendedWineLabels.Unregister();
        Beer.UnregisterRecipe();
        BeerQuality.Unregister();
    }

    public static void AddItems(ModItemDirectory directory)
    {
        directory.Add(BlendedWine.ITEM_ID, BlendedWine.Create);
        Beer.AddItems(directory);
        directory.Add(AdvancedFabric.ITEM_ID, AdvancedFabric.Create);
        Xinke.AddItems(directory);
    }
}

public static class IaLiquids
{
    public const string YEAST_WINE = "ia_yeast_wine";
    public const string YEAST_SUPER = "ia_yeast_super";
    public const string YEAST_RED = "ia_yeast_red";
    public const string METHANOL = "ia_methanol";
    public const string ICE = "ia_ice";

    public static void Register()
    {
        // Yeast is as coarse as vanilla microbes (50): the water filters (22) catch it, Kotton fabric (150) does not.
        AddLiquid(YEAST_WINE, "liquid_yeast_wine", 50, "e8d9a8", 4);
        AddLiquid(YEAST_SUPER, "liquid_yeast_super", 50, "f0c870", 4);
        AddLiquid(YEAST_RED, "liquid_yeast_red", 50, "c0504a", 4);
        Liquid methanol = AddLiquid(METHANOL, "liquid_methanol", 5, "dfe9ef", 1);
        if (methanol != null)
        {
            // Vanilla boiling (2% per tick above the boiling point) removes it from 65 C, below alcohol's 78 C.
            methanol.boilingPoint = 64.7;
            methanol.isEvaporative = true;
            methanol.lethality = 6.0;
        }
        Liquid ice = AddLiquid(ICE, "liquid_ice", 200, "e6f4ff", 2);
        if (ice != null)
        {
            // Filters wear per filterVolumeRemoved; ice is bulky, so let one Kotton fabric take about 5 L.
            ice.filterVolumeRemoved = 10000;
        }
    }

    private static bool namesLocalized;

    // Mod localization is not loaded yet during OnEnable, and OnGameLoadedInit only fires when a save is
    // loaded (not for a new game), so names are also fixed the first time a reaction tick runs.
    public static void EnsureNames()
    {
        if (!namesLocalized)
        {
            RefreshNames();
        }
    }

    public static void RefreshNames()
    {
        namesLocalized = IaText.Get("liquid_yeast_wine") != "liquid_yeast_wine";
        Rename(YEAST_WINE, "liquid_yeast_wine");
        Rename(YEAST_SUPER, "liquid_yeast_super");
        Rename(YEAST_RED, "liquid_yeast_red");
        Rename(METHANOL, "liquid_methanol");
        Rename(ICE, "liquid_ice");
        Rename(FlavorChemistry.ACETIC_ACID, "liquid_acetic_acid");
        Rename(FlavorChemistry.ETHYL_ACETATE, "liquid_ethyl_acetate");
        BlendedWineLabels.RefreshDisplays();
        BlendedWine.RefreshRecipeName();
        BeerQuality.RefreshDisplays();
        Beer.RefreshRecipeName();
    }

    public static Liquid AddLiquid(string id, string nameKey, int particleSize, string color, int colorMultiplier)
    {
        if (Liquid.LiquidDict.ContainsKey(id))
        {
            return null;
        }
        // SetIdentifier is private, so set its fields directly (same as Vanilla Chemistry Expansion).
        Liquid liquid = new Liquid();
        liquid.displayName = IaText.Get(nameKey);
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
        return liquid;
    }

    private static void Rename(string id, string nameKey)
    {
        Liquid liquid;
        if (Liquid.LiquidDict.TryGetValue(id, out liquid))
        {
            liquid.displayName = IaText.Get(nameKey);
        }
    }

    public static int Get(GameItem container, string id)
    {
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

    public static void Move(GameItem container, string from, string to, int amount)
    {
        if (amount <= 0)
        {
            return;
        }
        Set(container, from, Get(container, from) - amount);
        if (to != null)
        {
            Set(container, to, Get(container, to) + amount);
        }
    }
}

public class YeastStrain
{
    public string LiquidId;
    // Fermentation stops at this alcohol share of the total volume; above it 25% of the yeast dies each night.
    public double AlcoholTolerance;
    // Growth factor per night while it ferments.
    public double GrowthPerNight;

    public YeastStrain(string liquidId, double alcoholTolerance, double growthPerNight)
    {
        LiquidId = liquidId;
        AlcoholTolerance = alcoholTolerance;
        GrowthPerNight = growthPerNight;
    }
}

public static class Fermentation
{
    public static readonly YeastStrain WineYeast = new YeastStrain(IaLiquids.YEAST_WINE, 0.12, 2.0);
    public static readonly YeastStrain SuperYeast = new YeastStrain(IaLiquids.YEAST_SUPER, 0.18, 2.0);
    public static readonly YeastStrain RedYeast = new YeastStrain(IaLiquids.YEAST_RED, 0.15, 3.0);
    public static readonly YeastStrain[] Strains = { WineYeast, SuperYeast, RedYeast };

    public const int NIGHT_TICKS = 360;

    // Hydrolysis: starch -> sugar (1:1) with at least 0.5% acid at 90 C or more.
    public const double HYDROLYSIS_ACID_SHARE = 0.005;
    public const double HYDROLYSIS_TEMP = 90.0;
    public const double HYDROLYSIS_RATE = 0.1;
    public const int HYDROLYSIS_MIN_UL = 1000;

    // Yeast death outside the night: heat, and acid/base above 1% of the volume.
    public const double YEAST_HEAT_TEMP = 45.0;
    public const double YEAST_HEAT_RATE = 0.2;
    public const double ACID_BASE_TOLERANCE = 0.01;
    // Same as vanilla microbes: each uL of excess acid/base kills 20 uL; 1 uL is used up per 100 uL killed.
    public const int ACID_BASE_KILL_PER_UL = 20;

    // Overnight fermentation, 20-35 C (night ambient is 26 C).
    public const double FERMENT_MIN_TEMP = 20.0;
    public const double FERMENT_MAX_TEMP = 35.0;
    // Live yeast is capped at 2% of the volume; each uL of new yeast uses 1 uL of sugar.
    public const double YEAST_CAP_SHARE = 0.02;
    // Each uL of yeast turns 24 uL of sugar into 24 uL of alcohol per night (scaled by the fermentation speed
    // setting). 1.2.x used 8, which players found too slow: 20 mL of yeast made only 80 mL of alcohol a night.
    public const double SUGAR_PER_YEAST_PER_NIGHT = 24.0;
    // 1:1 by volume: 1 mL of sugar (about 1.5 g) ferments into about 0.78 g, i.e. 1 mL, of ethanol. 1.2.x used
    // 0.5 (the mass ratio), which made nutrifruit alcohol cost several times as much as bought alcohol.
    public const double ALCOHOL_PER_SUGAR = 1.0;
    public const double DEATH_PER_NIGHT_OVER_TOLERANCE = 0.25;
    // Microbes in a fermenting mash turn into methanol (1:1), 5% per tick.
    public const double MICROBE_RATE = 0.05;
    public const int MICROBE_MIN_UL = 100;

    // Freeze concentration: below 0 C water turns into ice until non-water liquids make up 25% of the
    // liquid (ice excluded); above 0 C ice melts back. 5% per tick, at least 0.1 mL.
    public const double FREEZE_TEMP = 0.0;
    public const double FREEZE_MAX_SOLUTE_SHARE = 0.25;
    public const double FREEZE_RATE = 0.05;
    public const int FREEZE_MIN_UL = 100;

    // True only while ChemicalHelper.RunNightlyChemicalSimulation runs its 360 ticks.
    public static bool IsNight;

    private static readonly System.Random random = new System.Random();

    public static int Run(GameItem container)
    {
        if (container == null || WaterHelper.IsEmpty(container))
        {
            return 0;
        }
        double temp = container.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
        int changed = Hydrolyze(container, temp);
        changed |= KillYeastByHeat(container, temp);
        changed |= KillYeastByAcidBase(container);
        if (IsNight)
        {
            changed |= Ferment(container, temp);
        }
        changed |= FreezeOrMelt(container, temp);
        changed |= FlavorChemistry.Run(container, temp);
        return changed;
    }

    private static int Hydrolyze(GameItem container, double temp)
    {
        int starch = IaLiquids.Get(container, "starch");
        if (starch <= 0 || temp < HYDROLYSIS_TEMP)
        {
            return 0;
        }
        int total = WaterHelper.GetTotalVolume(container);
        if (IaLiquids.Get(container, "acid") < total * HYDROLYSIS_ACID_SHARE)
        {
            return 0;
        }
        IaLiquids.Move(container, "starch", "sugar", Step(starch, HYDROLYSIS_RATE, HYDROLYSIS_MIN_UL));
        return 1;
    }

    private static int KillYeastByHeat(GameItem container, double temp)
    {
        if (temp < YEAST_HEAT_TEMP)
        {
            return 0;
        }
        int changed = 0;
        foreach (YeastStrain strain in Strains)
        {
            int yeast = IaLiquids.Get(container, strain.LiquidId);
            if (yeast > 0)
            {
                IaLiquids.Move(container, strain.LiquidId, "debris", Step(yeast, YEAST_HEAT_RATE, MICROBE_MIN_UL));
                changed = 1;
            }
        }
        return changed;
    }

    private static int KillYeastByAcidBase(GameItem container)
    {
        int yeastTotal = TotalYeast(container);
        if (yeastTotal <= 0)
        {
            return 0;
        }
        int acid = IaLiquids.Get(container, "acid");
        int alkali = IaLiquids.Get(container, "base");
        int excess = acid + alkali - (int)(WaterHelper.GetTotalVolume(container) * ACID_BASE_TOLERANCE);
        if (excess <= 0)
        {
            return 0;
        }
        int toKill = (int)Math.Min(yeastTotal, (long)excess * ACID_BASE_KILL_PER_UL);
        foreach (YeastStrain strain in Strains)
        {
            int yeast = IaLiquids.Get(container, strain.LiquidId);
            IaLiquids.Move(container, strain.LiquidId, "debris", (int)((long)toKill * yeast / yeastTotal));
        }
        int used = (toKill + 99) / 100;
        int fromAcid = Math.Min(acid, used);
        IaLiquids.Set(container, "acid", acid - fromAcid);
        IaLiquids.Set(container, "base", alkali - Math.Min(alkali, used - fromAcid));
        return 1;
    }

    // One of the 360 night ticks: each strain below its alcohol tolerance grows toward the cap and
    // ferments; strains above it slowly die off.
    private static int Ferment(GameItem container, double temp)
    {
        if (TotalYeast(container) <= 0)
        {
            return 0;
        }
        bool warm = temp >= FERMENT_MIN_TEMP && temp <= FERMENT_MAX_TEMP;
        bool fermenting = false;
        int changed = 0;
        foreach (YeastStrain strain in Strains)
        {
            int yeast = IaLiquids.Get(container, strain.LiquidId);
            if (yeast <= 0)
            {
                continue;
            }
            int total = WaterHelper.GetTotalVolume(container);
            double alcoholShare = (double)IaLiquids.Get(container, "alcohol") / total;
            if (alcoholShare >= strain.AlcoholTolerance)
            {
                double dying = 1.0 - Math.Pow(1.0 - DEATH_PER_NIGHT_OVER_TOLERANCE, 1.0 / NIGHT_TICKS);
                IaLiquids.Move(container, strain.LiquidId, "debris", RoundRandom(yeast * dying));
                changed = 1;
                continue;
            }
            int sugar = IaLiquids.Get(container, "sugar");
            if (!warm || sugar <= 0)
            {
                continue;
            }
            int room = (int)(total * YEAST_CAP_SHARE) - TotalYeast(container);
            double growthRate = Math.Pow(strain.GrowthPerNight, 1.0 / NIGHT_TICKS) - 1.0;
            int growth = Math.Min(Math.Min(RoundRandom(yeast * growthRate), Math.Max(0, room)), sugar);
            if (growth > 0)
            {
                IaLiquids.Move(container, "sugar", strain.LiquidId, growth);
                sugar -= growth;
                yeast += growth;
            }
            int ferment = Math.Min(RoundRandom(yeast * SUGAR_PER_YEAST_PER_NIGHT * IaSettings.FermentPercent / 100.0 / NIGHT_TICKS), sugar);
            if (ferment > 0)
            {
                IaLiquids.Set(container, "sugar", sugar - ferment);
                IaLiquids.Set(container, "alcohol", IaLiquids.Get(container, "alcohol") + (int)Math.Round(ferment * ALCOHOL_PER_SUGAR));
                fermenting = true;
            }
            changed = 1;
        }
        if (fermenting)
        {
            int microbe = IaLiquids.Get(container, "microbe");
            if (microbe > 0)
            {
                IaLiquids.Move(container, "microbe", IaLiquids.METHANOL, Step(microbe, MICROBE_RATE, MICROBE_MIN_UL));
            }
        }
        return changed;
    }

    private static int FreezeOrMelt(GameItem container, double temp)
    {
        if (container.IsTag("WINE_CONTAINER_TAG"))
        {
            return 0;
        }
        // A container in a powered freezer may settle above 0 C against ambient warming, so the
        // freezer zone also marks it (same approach as Vanilla Chemistry Expansion's freeze step).
        if (temp > FREEZE_TEMP && !ConsumeFreezeTick(container))
        {
            int ice = IaLiquids.Get(container, IaLiquids.ICE);
            if (ice <= 0)
            {
                return 0;
            }
            IaLiquids.Move(container, IaLiquids.ICE, "water", Step(ice, FREEZE_RATE, FREEZE_MIN_UL));
            return 1;
        }
        int water = IaLiquids.Get(container, "water");
        if (water <= 0)
        {
            return 0;
        }
        int liquid = WaterHelper.GetTotalVolume(container) - IaLiquids.Get(container, IaLiquids.ICE);
        int solutes = liquid - water;
        // Water may freeze until solutes / (solutes + water) reaches 25%, i.e. water falls to 3x the solutes.
        int freezable = water - (int)(solutes * (1.0 - FREEZE_MAX_SOLUTE_SHARE) / FREEZE_MAX_SOLUTE_SHARE);
        if (freezable <= 0)
        {
            return 0;
        }
        IaLiquids.Move(container, "water", IaLiquids.ICE, Math.Min(freezable, Step(water, FREEZE_RATE, FREEZE_MIN_UL)));
        return 1;
    }

    // Set by a powered fridge's freezer zone every tick; counts down in the container's own tick.
    public const string FREEZE_TAG = "IA_FREEZE_TICKS_INT";
    public const int FREEZE_TICKS = 3;

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

    public static int TotalYeast(GameItem container)
    {
        int total = 0;
        foreach (YeastStrain strain in Strains)
        {
            total += IaLiquids.Get(container, strain.LiquidId);
        }
        return total;
    }

    private static int Step(int available, double rate, int minUl)
    {
        return Math.Min(available, Math.Max(minUl, (int)(available * rate)));
    }

    // Per-tick amounts are often a few uL; random rounding keeps the expected value exact.
    private static int RoundRandom(double value)
    {
        if (value <= 0.0)
        {
            return 0;
        }
        int whole = (int)value;
        return whole + (random.NextDouble() < value - whole ? 1 : 0);
    }
}

// Yeast items dissolve into a liquid container when dragged onto it, like vanilla water tablets.
public static class YeastItems
{
    public const string BIOREACTOR_ID = "wine_yeast_infinite";
    public const string BIOREACTOR_USED_TAG = "IA_BIOREACTOR_USED_BOOL";
    public const string HINT_TAG = "IA_YEAST_HINT_STRING";

    public static void Setup(GameItem item)
    {
        if (item == null || !item.IsTag("WINE_YEAST_TAG"))
        {
            return;
        }
        item.maySelectSlotItemFunc = (GameItem a, GameInventory b, SlotMarker c) => true;
        item.canSelectSlotItemFunc = (GameItem a, GameInventory b, SlotMarker c) => true;
        item.mayThisTargetItemFunc = (GameItem source, GameItem target) => CanDissolveInto(source, target);
        item.canThisTargetItemFunc = (GameItem source, GameItem target) => CanDissolveInto(source, target);
        item.onItemTargetItemFunc = delegate(GameItem source, GameItem target)
        {
            DissolveInto(source, target);
        };
        // Setup runs again once the illegal/reusable tags are on, so replace the previous hint.
        string description = item.shortDescription ?? "";
        string oldHint = item.IsTag(HINT_TAG) ? item.GetTagReadonly(HINT_TAG).GetString() : "";
        if (oldHint.Length > 0)
        {
            description = description.Replace("\n" + oldHint, "").Replace(oldHint, "");
        }
        string hintKey = "yeast_hint";
        if (item.IsTag("WINE_YEAST_REUSABLE_TAG"))
        {
            hintKey = "yeast_hint_reusable";
        }
        else if (MicrobeUl(item) > 0)
        {
            hintKey = "yeast_hint_microbes";
        }
        string hint = IaText.Get(hintKey, Format(YeastUl(item)), Format(MicrobeUl(item)));
        item.shortDescription = description.Length == 0 ? hint : description + "\n" + hint;
        item.ModifyTag(HINT_TAG, delegate(TagState state)
        {
            state.Enable().SetString(hint);
        }, true);
    }

    public static void SetupLoadedItems()
    {
        try
        {
            IaLiquids.EnsureNames();
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                Setup(item);
            }
        }
        catch (Exception e)
        {
            IaLog.Error("setting up saved yeast items failed", e);
        }
    }

    public static void ResetBioreactors()
    {
        foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
        {
            if (item != null && item.IsTag(BIOREACTOR_USED_TAG))
            {
                item.DisableTag(BIOREACTOR_USED_TAG, true);
            }
        }
    }

    private static string Format(int ul)
    {
        return (ul / 1000.0).ToString("0.#");
    }

    // Vanilla yeast power: wine yeast 2, super yeast 4, red yeast 2, bioreactor 2.
    public static int YeastUl(GameItem item)
    {
        int power = item.GetTagReadonly("WINE_YEAST_POWER_INT").GetInt();
        return item.IsTag("WINE_YEAST_ILLEGAL_TAG") ? power * 750 : power * 1000;
    }

    public static int MicrobeUl(GameItem item)
    {
        return item.IsTag("WINE_YEAST_ILLEGAL_TAG") ? item.GetTagReadonly("WINE_YEAST_POWER_INT").GetInt() * 250 : 0;
    }

    public static string StrainLiquid(GameItem item)
    {
        if (item.IsTag("WINE_YEAST_ILLEGAL_TAG"))
        {
            return IaLiquids.YEAST_RED;
        }
        return item.GetTagReadonly("WINE_YEAST_POWER_INT").GetInt() >= 4 ? IaLiquids.YEAST_SUPER : IaLiquids.YEAST_WINE;
    }

    private static bool CanDissolveInto(GameItem source, GameItem target)
    {
        if (target == null || !target.IsTag("LIQUID_CONTAINER_TAG") || target.IsTag("WINE_CONTAINER_TAG"))
        {
            return false;
        }
        if (!GeneralHelper.IsItemOwned(source) || !GeneralHelper.IsItemOwned(target))
        {
            return false;
        }
        if (source.IsTag(BIOREACTOR_USED_TAG))
        {
            return false;
        }
        return WaterHelper.GetFreeCapacity(target) >= YeastUl(source) + MicrobeUl(source);
    }

    private static void DissolveInto(GameItem source, GameItem target)
    {
        if (!CanDissolveInto(source, target))
        {
            return;
        }
        WaterHelper.AddLiquid(target, StrainLiquid(source), YeastUl(source));
        int microbe = MicrobeUl(source);
        if (microbe > 0)
        {
            WaterHelper.AddLiquid(target, "microbe", microbe);
        }
        WaterFeatureHelper.UpdateWaterFeature(target, false);
        SpriteHelper.UpdateWaterContainerSprite(target);
        if (source.IsTag("WINE_YEAST_REUSABLE_TAG"))
        {
            // The bioreactor keeps working but only once per night.
            source.EnableTag(BIOREACTOR_USED_TAG, true);
        }
        else
        {
            source.Destroy();
        }
    }
}

[HarmonyPatch(typeof(WineHelper), "InitYeast")]
public static class InitYeastPatch
{
    public static void Postfix(GameItem item)
    {
        YeastItems.Setup(item);
    }
}

// The illegal and reusable tags are set after InitYeast, so set the item up again once they are on.
[HarmonyPatch(typeof(WineHelper), "InitIllegalYeast")]
public static class InitIllegalYeastPatch
{
    public static void Postfix(GameItem item)
    {
        YeastItems.Setup(item);
    }
}

[HarmonyPatch(typeof(WineHelper), "InitInfiniteYeast")]
public static class InitInfiniteYeastPatch
{
    public static void Postfix(GameItem item)
    {
        YeastItems.Setup(item);
    }
}

[HarmonyPatch(typeof(ChemicalHelper), "HandleTemperatureReaction")]
public static class IaTemperatureReactionPatch
{
    private static void Postfix(GameItem container)
    {
        try
        {
            IaLiquids.EnsureNames();
            if (Fermentation.Run(container) > 0)
            {
                ItemFeature waterFeature = WaterFeatureHelper.GetWaterFeature(container);
                WaterFeatureHelper.OnLiquidChanged(container, waterFeature != null && waterFeature.IsExposed());
            }
        }
        catch (Exception e)
        {
            IaLog.Error("reaction failed", e);
        }
    }
}

[HarmonyPatch(typeof(MachineFridge), "CoolZoneContainers")]
public static class IaFreezerPatch
{
    // Only called while the fridge has power; the chill zone targets 3 C, the freezer -18 C.
    private static void Postfix(GameGridInventory zone, double targetTemp)
    {
        if (targetTemp > 0.0 || zone == null)
        {
            return;
        }
        foreach (GameItem item in zone.childItems)
        {
            if (item != null && item.IsTag("LIQUID_CONTAINER_TAG") && !WaterHelper.IsEmpty(item))
            {
                Fermentation.MarkFrozen(item);
            }
        }
    }
}

[HarmonyPatch(typeof(ChemicalHelper), "RunNightlyChemicalSimulation")]
public static class NightSimulationPatch
{
    private static void Prefix()
    {
        Fermentation.IsNight = true;
    }

    private static void Finalizer()
    {
        Fermentation.IsNight = false;
        try
        {
            YeastItems.ResetBioreactors();
        }
        catch (Exception e)
        {
            IaLog.Error("resetting bioreactors failed", e);
        }
    }
}

// The analyzer's tested readout ("Tested / purity / water grade") also shows the container temperature.
[HarmonyPatch(typeof(WaterScannerUIManager), "GetTestedString")]
public static class ScannerTemperaturePatch
{
    private static void Postfix(GameItem currentWaterContainer, ref string __result)
    {
        if (currentWaterContainer == null || !currentWaterContainer.IsTag("CONTAINER_TEMP_TAG"))
        {
            return;
        }
        double temp = currentWaterContainer.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
        __result += IaText.Get("scanner_temperature", temp.ToString("0.#"));
    }
}

public static class IaDebugKit
{
    public static void SpawnIngredients()
    {
        for (int i = 0; i < 5; i++)
        {
            Give(DirectoryMaster.Item("nutrifruit", true));
        }
        Give(EmptyContainer("water_jug"));
        Give(PreBuildChemHelper.CreateAcidBottle());
        Give(PreBuildChemHelper.CreateBaseBottle());
        Give(DirectoryMaster.Item("wine_yeast", true));
        Give(DirectoryMaster.Item("wine_superyeast", true));
        Give(DirectoryMaster.Item("wine_yeast_red", true));
        Give(DirectoryMaster.Item(YeastItems.BIOREACTOR_ID, true));
    }

    // Five nutrifruit after hydrolysis and neutralization: ready for yeast.
    public static void SpawnMash()
    {
        GameItem jug = EmptyContainer("water_jug");
        WaterHelper.AddLiquid(jug, "water", 982000);
        WaterHelper.AddLiquid(jug, "sugar", 225000);
        WaterHelper.AddLiquid(jug, "protein", 25000);
        WaterHelper.AddLiquid(jug, "mineral", 8000);
        Give(jug);
    }

    // One litre at about 9% alcohol, for freeze concentration.
    public static void SpawnWash()
    {
        GameItem bottle = EmptyContainer("large_bottled_water");
        WaterHelper.AddLiquid(bottle, "water", 910000);
        WaterHelper.AddLiquid(bottle, "alcohol", 90000);
        Give(bottle);
    }

    // Raw materials for ethyl acetate: edible Dream Caps (plant matter), alcohol and acid.
    public static void SpawnFlavourKit()
    {
        for (int i = 0; i < 3; i++)
        {
            Give(PreBuiltItemHelper.CreateDreamCap(true));
        }
        Give(EmptyContainer("large_bottled_water"));
        Give(PreBuiltItemHelper.CreateChemicalBottle("alcohol"));
        Give(PreBuildChemHelper.CreateAcidBottle());
        Give(PreBuildChemHelper.CreateBaseBottle());
    }

    // 749 mL at 22% alcohol (no ethyl acetate yet) and a vial of 10 mL ethyl acetate: add about 1.1 mL
    // of the ester to the base and press it in the Chem Finisher for a master-vintage blend.
    public static void SpawnBlendBase()
    {
        GameItem jug = EmptyContainer("large_bottled_water");
        WaterHelper.AddLiquid(jug, "water", 583900);
        WaterHelper.AddLiquid(jug, "alcohol", 165000);
        Give(jug);
        GameItem vial = EmptyContainer("med_bottle_red");
        WaterHelper.AddLiquid(vial, FlavorChemistry.ETHYL_ACETATE, 10000);
        Give(vial);
    }

    public static void SpawnAdvancedFabric()
    {
        Give(DirectoryMaster.Item(AdvancedFabric.ITEM_ID, true));
    }

    public static void SpawnBeerKeg()
    {
        Give(DirectoryMaster.Item(Beer.KEG_ID, true));
    }

    // A premium wort for the Chem Finisher's beer keg recipe: 5.5% alcohol, 0.35% live yeast, 0.5% sugar,
    // 0.5% protein, the rest pure water.
    public static void SpawnBeerWort()
    {
        GameItem jug = EmptyContainer("water_jug");
        WaterHelper.AddLiquid(jug, "alcohol", 110000);
        WaterHelper.AddLiquid(jug, IaLiquids.YEAST_WINE, 7000);
        WaterHelper.AddLiquid(jug, "sugar", 10000);
        WaterHelper.AddLiquid(jug, "protein", 10000);
        WaterHelper.AddLiquid(jug, "water", Beer.KEG_UL - 137000);
        Give(jug);
    }

    // Sets every keg the player owns to 5 C, as if it had been in a fridge chill zone.
    public static void ChillKegs()
    {
        foreach (GameItem item in EmporiumEntry.Instance.GetAllOwnedItems())
        {
            if (Beer.IsKeg(item))
            {
                Beer.Cool(item, 5.0, 100.0);
            }
        }
    }

    public static void SpawnNotes()
    {
        Give(DirectoryMaster.Item(Notes.WineMaking.ItemId, true));
        Give(DirectoryMaster.Item(Notes.WineSelling.ItemId, true));
    }

    public static void SpawnPerfectWine()
    {
        Give(DirectoryMaster.Item(BlendedWine.ITEM_ID, true));
    }

    public static void RunNight()
    {
        ChemicalHelper.RunNightlyChemicalSimulation();
        IaLog.Info("ran one night of chemistry");
    }

    private static GameItem EmptyContainer(string id)
    {
        GameItem container = DirectoryMaster.Item(id, true);
        foreach (Liquid liquid in Liquid.Liquids)
        {
            IaLiquids.Set(container, liquid.identifier, 0);
        }
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

public static class IaText
{
    public static string Get(string key, params object[] args)
    {
        return ModHelper.GetLocalized(DreamToxinMod.MOD_ID, key, args);
    }
}

public static class IaLog
{
    public static void Info(string message)
    {
        UnityEngine.Debug.Log("[VanillaChemExt] " + message);
    }

    public static void Error(string context, Exception e)
    {
        UnityEngine.Debug.LogError("[VanillaChemExt] " + context + ": " + e);
    }
}
