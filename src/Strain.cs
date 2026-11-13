// Acremonium strains: the breeding stock of the antibiotics line (docs/ANTIBIOTICS_DESIGN.md).
// A strain carries a 12-letter genome (which only decides resistance) and five traits in steps of 0.1:
// purity, yield, growth speed, carbon-source efficiency and the extraction target. Blending a strain gives
// cephalosporin C plus a little protein; a strain left in a powered freezer overnight becomes a frozen strain,
// which double-clicks out an identical strain for ever (the clone is a seed strain and blends to protein only).
// Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;

// The data model. Traits live on the item as tenths of a point, so 0.1 steps stay exact across saves.
public static class StrainTraits
{
    public const string GENOME_TAG = "VCE_STRAIN_GENOME_TAG";
    public const string PURITY_TAG = "VCE_STRAIN_PURITY_TAG";
    public const string YIELD_TAG = "VCE_STRAIN_YIELD_TAG";
    public const string SPEED_TAG = "VCE_STRAIN_SPEED_TAG";
    public const string CARBON_TAG = "VCE_STRAIN_CARBON_TAG";
    public const string TARGET_TAG = "VCE_STRAIN_TARGET_TAG";
    // Clones out of a frozen strain: they only ever go back into a fermenter, blending them gives 1 mL of protein.
    public const string SEED_TAG = "VCE_STRAIN_SEED_TAG";

    public const int GENOME_LENGTH = 12;
    public const string GENOME_LETTERS = "ATCG";

    // Extraction targets.
    public const int TARGET_LIQUID = 0;
    public const int TARGET_SURFACE = 1;
    public const int TARGET_INTRACELLULAR = 2;

    // Bought strains: each of the four numeric traits in [-2.0, +2.0] by 0.1, their sum exactly 0.
    public const int DRAW_LIMIT_TENTHS = 20;

    // Carbon-source efficiency: 50 kcal a night at 0, 5 kcal less per point, never below zero.
    public const int BASE_KCAL = 50;
    public const double KCAL_PER_POINT = 5.0;

    public static readonly string[] NUMERIC_TAGS = { PURITY_TAG, YIELD_TAG, SPEED_TAG, CARBON_TAG };

    public static int Tenths(GameItem item, string tag)
    {
        return item != null && item.IsTag(tag) ? item.GetTagReadonly(tag).GetInt() : 0;
    }

    public static void SetTenths(GameItem item, string tag, int tenths)
    {
        if (item == null)
        {
            return;
        }
        int value = tenths;
        item.ModifyTag(tag, delegate(TagState state)
        {
            state.Enable().SetInt(value);
        }, true);
        item.ValidateShapeState();
    }

    public static void AddTenths(GameItem item, string tag, int delta)
    {
        SetTenths(item, tag, Tenths(item, tag) + delta);
    }

    public static int Purity(GameItem item) { return Tenths(item, PURITY_TAG); }
    public static int Yield(GameItem item) { return Tenths(item, YIELD_TAG); }
    public static int Speed(GameItem item) { return Tenths(item, SPEED_TAG); }
    public static int Carbon(GameItem item) { return Tenths(item, CARBON_TAG); }

    public static int Target(GameItem item)
    {
        return Tenths(item, TARGET_TAG);
    }

    public static void SetTarget(GameItem item, int target)
    {
        SetTenths(item, TARGET_TAG, Math.Max(TARGET_LIQUID, Math.Min(TARGET_INTRACELLULAR, target)));
    }

    public static bool IsSeedStock(GameItem item)
    {
        return item != null && item.IsTag(SEED_TAG);
    }

    public static void MarkSeedStock(GameItem item, bool seed)
    {
        if (item == null)
        {
            return;
        }
        if (seed)
        {
            item.EnableTag(SEED_TAG, true);
        }
        else
        {
            item.DisableTag(SEED_TAG, true);
        }
        item.ValidateShapeState();
    }

    public static string Genome(GameItem item)
    {
        if (item == null || !item.IsTag(GENOME_TAG))
        {
            return "";
        }
        string genome = item.GetTagReadonly(GENOME_TAG).GetString();
        return genome ?? "";
    }

    public static void SetGenome(GameItem item, string genome)
    {
        if (item == null || string.IsNullOrEmpty(genome))
        {
            return;
        }
        string value = genome;
        item.ModifyTag(GENOME_TAG, delegate(TagState state)
        {
            state.Enable().SetString(value);
        }, true);
        item.ValidateShapeState();
    }

    public static string RandomGenome()
    {
        StringBuilder builder = new StringBuilder(GENOME_LENGTH);
        for (int i = 0; i < GENOME_LENGTH; i++)
        {
            builder.Append(GENOME_LETTERS[RNG.GetRandomIntExculsive(0, GENOME_LETTERS.Length)]);
        }
        return builder.ToString();
    }

    // How many letters two genomes differ in; genomes of unequal length count the overflow as different.
    public static int Distance(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
        {
            return GENOME_LENGTH;
        }
        int length = Math.Min(a.Length, b.Length);
        int distance = Math.Abs(a.Length - b.Length);
        for (int i = 0; i < length; i++)
        {
            if (a[i] != b[i])
            {
                distance++;
            }
        }
        return distance;
    }

    // A bought strain: extraction target fixed to the liquid phase, a random genome, and four traits that
    // sum to zero. Drawn by picking three freely and deriving the fourth, retried until it is in range, so
    // every valid combination stays reachable.
    public static void Randomize(GameItem item)
    {
        if (item == null)
        {
            return;
        }
        int[] values = new int[NUMERIC_TAGS.Length];
        for (int attempt = 0; attempt < 200; attempt++)
        {
            int sum = 0;
            for (int i = 0; i < values.Length - 1; i++)
            {
                values[i] = RNG.GetRandomInt(-DRAW_LIMIT_TENTHS, DRAW_LIMIT_TENTHS);
                sum += values[i];
            }
            values[values.Length - 1] = -sum;
            if (Math.Abs(values[values.Length - 1]) <= DRAW_LIMIT_TENTHS)
            {
                break;
            }
            Array.Clear(values, 0, values.Length);
        }
        // Which trait pays for the others should not depend on the order of the array.
        Shuffle(values);
        for (int i = 0; i < NUMERIC_TAGS.Length; i++)
        {
            SetTenths(item, NUMERIC_TAGS[i], values[i]);
        }
        SetTarget(item, TARGET_LIQUID);
        SetGenome(item, RandomGenome());
    }

    private static void Shuffle(int[] values)
    {
        for (int i = values.Length - 1; i > 0; i--)
        {
            int j = RNG.GetRandomInt(0, i);
            int swap = values[i];
            values[i] = values[j];
            values[j] = swap;
        }
    }

    public static void CopyTo(GameItem from, GameItem to)
    {
        if (from == null || to == null)
        {
            return;
        }
        foreach (string tag in NUMERIC_TAGS)
        {
            SetTenths(to, tag, Tenths(from, tag));
        }
        SetTarget(to, Target(from));
        SetGenome(to, Genome(from));
    }

    // ---- what the traits do ---------------------------------------------------------------------------------------

    // Strains harvested per batch: 4 at standard, +10% per point, rounded, at least one.
    public static int HarvestCount(GameItem strain, int baseCount)
    {
        double count = baseCount * (1.0 + Yield(strain) / 100.0);
        return Math.Max(1, (int)Math.Round(count, MidpointRounding.AwayFromZero));
    }

    // Nights per batch: 2 at standard, +10% speed per point, rounded, never below one night.
    public static int BatchNights(GameItem strain, int baseNights)
    {
        double factor = 1.0 + Speed(strain) / 100.0;
        if (factor <= 0.0)
        {
            return baseNights * 4;
        }
        return Math.Max(1, (int)Math.Round(baseNights / factor, MidpointRounding.AwayFromZero));
    }

    // kcal drawn from the feed dispenser each night.
    public static int KcalPerNight(GameItem strain)
    {
        double kcal = BASE_KCAL - KCAL_PER_POINT * Carbon(strain) / 10.0;
        return Math.Max(0, (int)Math.Round(kcal, MidpointRounding.AwayFromZero));
    }

    // ---- display --------------------------------------------------------------------------------------------------

    public static string Format(int tenths)
    {
        return (tenths / 10.0).ToString("+0.0;-0.0;0.0");
    }

    public static string TargetName(int target)
    {
        return Text.Get(target == TARGET_SURFACE ? "strain_target_surface"
            : target == TARGET_INTRACELLULAR ? "strain_target_intracellular"
            : "strain_target_liquid");
    }
}

public static class Strain
{
    public const string ITEM_ID = "vce_strain";
    public const string FROZEN_ID = "vce_strain_frozen";
    public const string SPRITE = "vce_strain";
    public const string FROZEN_SPRITE = "vce_strain_frozen";

    public const long VALUE = 60;
    public const long FROZEN_VALUE = 120;

    // Blending one harvested strain: 27 mL of cephalosporin C and 3 mL of protein, so 30 mL at exactly 90%.
    public const int CEPH_UL = 27000;
    public const int PROTEIN_UL = 3000;
    // A clone out of the freezer is breeding stock, not product.
    public const int SEED_PROTEIN_UL = 1000;

    // Set by the fridge's freeze zone every tick it has power; a strain still marked at nightfall freezes.
    public const string FROZEN_MARK_TAG = "VCE_STRAIN_FREEZE_MARK_TAG";
    public const int FREEZE_MARK_TICKS = 3;

    public static void Enable()
    {
        ModHook.OnHandlingNightlyServicesEarly += OnNight;
        ModHook.OnCreateTooltipLate += OnCreateTooltip;
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Antibiotics: 4 random strains", SpawnStrains, false);
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Antibiotics: standard strain", SpawnStandard, false);
        }
        catch (Exception e)
        {
            Log.Error("strain debug function registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnHandlingNightlyServicesEarly -= OnNight;
        ModHook.OnCreateTooltipLate -= OnCreateTooltip;
    }

    public static void AddItems(ModItemDirectory directory)
    {
        directory.Add(ITEM_ID, Create);
        directory.Add(FROZEN_ID, CreateFrozen);
    }

    public static bool IsStrain(GameItem item)
    {
        return item != null && item.identifier == ITEM_ID;
    }

    public static bool IsFrozen(GameItem item)
    {
        return item != null && item.identifier == FROZEN_ID;
    }

    // ---- the items ------------------------------------------------------------------------------------------------

    public static GameItem Create()
    {
        GameItem item = ItemDirectory.CreateEmptyItem(ITEM_ID);
        try
        {
            item.name = Text.Get("strain_name");
            item.shortDescription = Text.Get("strain_desc");
            item.flavorText = Text.Get("strain_flavor");
            item.unitValue = VALUE;
            item.SetGameItemType("MATERIAL");
            AudioHelper.InitTinyItem(item);
            // Any blender crushes it; vanilla's module loss applies as it does to ores and plants.
            ChemicalHelper.InitChemItem(item, true, false);
            ApplyContents(item);
            item.onOneSecondItemFunc = delegate(GameItem strain, GameInventory inventory, SlotMarker marker)
            {
                EnsureIdentity(strain);
                CountDownFreezeMark(strain);
            };
            // Dropping a liquid container on the strain doses it with a mutagen (StrainMutation.cs).
            item.mayItemTargetThisFunc = (GameItem source, GameItem target) => StrainMutation.CanDose(source, target);
            item.canItemTargetThisFunc = (GameItem source, GameItem target) => StrainMutation.CanDose(source, target);
            item.onItemTargetThisFunc = delegate(GameItem source, GameItem target)
            {
                StrainMutation.Dose(source, target);
            };
            BeerSprites.Apply(item, SPRITE);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            Log.Error("create " + ITEM_ID + " failed", e);
        }
        return item;
    }

    public static GameItem CreateFrozen()
    {
        GameItem item = ItemDirectory.CreateEmptyItem(FROZEN_ID);
        try
        {
            item.name = Text.Get("strain_frozen_name");
            item.shortDescription = Text.Get("strain_frozen_desc");
            item.flavorText = Text.Get("strain_frozen_flavor");
            item.unitValue = FROZEN_VALUE;
            item.SetGameItemType("STORAGE");
            AudioHelper.InitSolidMetal(item);
            item.canActivateSlotItemFunc = (GameItem a, GameInventory b, SlotMarker c) => GeneralHelper.IsItemOwned(a);
            item.mayActivateSlotItemFunc = (GameItem a, GameInventory b, SlotMarker c) => GeneralHelper.IsItemOwned(a);
            item.onActivateSlotItemFunc = delegate(GameItem dewar, GameInventory inventory, SlotMarker marker)
            {
                Inoculate(dewar, inventory);
            };
            item.onOneSecondItemFunc = delegate(GameItem dewar, GameInventory inventory, SlotMarker marker)
            {
                EnsureIdentity(dewar);
            };
            BeerSprites.Apply(item, FROZEN_SPRITE);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            Log.Error("create " + FROZEN_ID + " failed", e);
        }
        return item;
    }

    // Strains that reach the player without going through Spawn - the item spawner, other mods' loot tables,
    // anything that calls the factory directly - have no genome and no traits at all. The first tick after
    // the saved tags are back gives them a random identity, the same as a bought strain.
    public static void EnsureIdentity(GameItem strain)
    {
        if (strain == null || StrainTraits.Genome(strain).Length >= StrainTraits.GENOME_LENGTH)
        {
            return;
        }
        StrainTraits.Randomize(strain);
        if (IsStrain(strain))
        {
            ApplyContents(strain);
        }
        Log.Info("gave a strain without one a random identity: genome " + StrainTraits.Genome(strain));
    }

    // What a blender gets out of this strain. Seed stock only gives protein, so the freezer cannot be cashed in
    // directly and the fermenter stays on the critical path.
    public static void ApplyContents(GameItem strain)
    {
        if (strain == null)
        {
            return;
        }
        bool seed = StrainTraits.IsSeedStock(strain);
        ChemicalHelper.SetChemContentUL(strain, Antibiotics.CEPH_ID, seed ? 0 : CEPH_UL);
        ChemicalHelper.SetChemContentUL(strain, "protein", seed ? SEED_PROTEIN_UL : PROTEIN_UL);
    }

    // A fresh strain with the given traits; used by the fermenter, the shops and the freezer.
    public static GameItem Spawn(bool owned, GameItem parent, bool seedStock)
    {
        GameItem strain = DirectoryMaster.Item(ITEM_ID, owned);
        if (strain == null)
        {
            return null;
        }
        if (parent != null)
        {
            StrainTraits.CopyTo(parent, strain);
        }
        else
        {
            StrainTraits.Randomize(strain);
        }
        StrainTraits.MarkSeedStock(strain, seedStock);
        ApplyContents(strain);
        return strain;
    }

    // ---- freezing and inoculating ---------------------------------------------------------------------------------

    // From the fridge patch, every tick the freeze zone has power.
    public static void MarkForFreezing(GameItem strain)
    {
        if (!IsStrain(strain))
        {
            return;
        }
        strain.ModifyTag(FROZEN_MARK_TAG, delegate(TagState state)
        {
            state.Enable().SetInt(FREEZE_MARK_TICKS);
        }, true);
        strain.ValidateShapeState();
    }

    private static void CountDownFreezeMark(GameItem strain)
    {
        if (strain == null || !strain.IsTag(FROZEN_MARK_TAG))
        {
            return;
        }
        int ticks = strain.GetTagReadonly(FROZEN_MARK_TAG).GetInt();
        if (ticks <= 0)
        {
            return;
        }
        int left = ticks - 1;
        strain.ModifyTag(FROZEN_MARK_TAG, delegate(TagState state)
        {
            state.Enable().SetInt(left);
        }, true);
        strain.ValidateShapeState();
    }

    public static bool IsBeingFrozen(GameItem strain)
    {
        return strain != null && strain.IsTag(FROZEN_MARK_TAG) && strain.GetTagReadonly(FROZEN_MARK_TAG).GetInt() > 0;
    }

    // Overnight: every strain sitting in a powered freezer becomes a frozen strain, if the 3x3 dewar fits where
    // the strain was. When it does not fit the strain simply stays a strain, so nothing is ever lost.
    public static void OnNight()
    {
        try
        {
            List<GameItem> freezing = new List<GameItem>();
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (IsStrain(item) && IsBeingFrozen(item))
                {
                    freezing.Add(item);
                }
            }
            int frozen = 0;
            foreach (GameItem strain in freezing)
            {
                if (Freeze(strain))
                {
                    frozen++;
                }
            }
            if (freezing.Count > 0)
            {
                Log.Info("strains in the freezer: " + freezing.Count + ", frozen " + frozen);
            }
        }
        catch (Exception e)
        {
            Log.Error("freezing strains failed", e);
        }
    }

    private static bool Freeze(GameItem strain)
    {
        GameInventory parent = strain.parentInventory;
        GameItem dewar = DirectoryMaster.Item(FROZEN_ID, GeneralHelper.IsItemOwned(strain));
        if (dewar == null)
        {
            return false;
        }
        StrainTraits.CopyTo(strain, dewar);
        // The dewar is breeding stock whatever the strain was.
        StrainTraits.MarkSeedStock(dewar, false);
        if (parent != null)
        {
            parent.Expel(strain);
            if (GraphUtils.TryAcceptAll(parent, dewar) > 0)
            {
                strain.Destroy();
                return true;
            }
            // No room for 3x3 where the strain was: put it back and leave it alone.
            GraphUtils.TryAcceptAll(parent, strain);
            dewar.Destroy();
            return false;
        }
        GraphUtils.EmporiumTryAdd(dewar);
        strain.Destroy();
        return true;
    }

    // Double-clicking a frozen strain: one identical strain, marked as seed stock.
    private static void Inoculate(GameItem dewar, GameInventory inventory)
    {
        try
        {
            GameItem strain = Spawn(true, dewar, true);
            if (strain == null)
            {
                return;
            }
            GameInventory target = inventory ?? dewar.parentInventory;
            if (target != null && GraphUtils.TryAcceptAll(target, strain) > 0)
            {
                return;
            }
            GraphUtils.EmporiumTryAdd(strain);
        }
        catch (Exception e)
        {
            Log.Error("inoculating from a frozen strain failed", e);
        }
    }

    // ---- tooltip --------------------------------------------------------------------------------------------------

    public static void OnCreateTooltip(RichTextBuilder builder, GameItem item)
    {
        try
        {
            if (builder == null || (!IsStrain(item) && !IsFrozen(item)))
            {
                return;
            }
            builder.AddLine(Text.Get("strain_tooltip_genome", StrainTraits.Genome(item)), true, RenderHandler.ColorPalette.LightCyan);
            builder.AddLine(Text.Get("strain_tooltip_traits",
                StrainTraits.Format(StrainTraits.Purity(item)),
                StrainTraits.Format(StrainTraits.Yield(item)),
                StrainTraits.Format(StrainTraits.Speed(item)),
                StrainTraits.Format(StrainTraits.Carbon(item))), true, RenderHandler.ColorPalette.LightCyan);
            builder.AddLine(Text.Get("strain_tooltip_target", StrainTraits.TargetName(StrainTraits.Target(item))), true,
                RenderHandler.ColorPalette.LightCyan);
            if (IsFrozen(item))
            {
                builder.AddLine(Text.Get("strain_tooltip_inoculate"), true, RenderHandler.ColorPalette.LightGreen);
            }
            else if (StrainTraits.IsSeedStock(item))
            {
                builder.AddLine(Text.Get("strain_tooltip_seed"), true, RenderHandler.ColorPalette.LightYellow);
            }
        }
        catch (Exception e)
        {
            Log.Error("strain tooltip failed", e);
        }
    }

    // ---- debug ----------------------------------------------------------------------------------------------------

    public static void SpawnStrains()
    {
        try
        {
            for (int i = 0; i < 4; i++)
            {
                GameItem strain = Spawn(true, null, false);
                if (strain != null)
                {
                    ModHelper.MoveItemToInventory(strain);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("strain debug kit failed", e);
        }
    }

    public static void SpawnStandard()
    {
        try
        {
            GameItem strain = DirectoryMaster.Item(ITEM_ID, true);
            if (strain == null)
            {
                return;
            }
            foreach (string tag in StrainTraits.NUMERIC_TAGS)
            {
                StrainTraits.SetTenths(strain, tag, 0);
            }
            StrainTraits.SetTarget(strain, StrainTraits.TARGET_LIQUID);
            StrainTraits.SetGenome(strain, StrainTraits.RandomGenome());
            ApplyContents(strain);
            ModHelper.MoveItemToInventory(strain);
        }
        catch (Exception e)
        {
            Log.Error("standard strain debug kit failed", e);
        }
    }
}

// Powered freezers mark the strains in their freeze zone; the mark expires a few ticks after the power goes.
[HarmonyPatch(typeof(MachineFridge), "CoolZoneContainers")]
public static class StrainFridgePatch
{
    private static void Postfix(GameGridInventory zone, double targetTemp)
    {
        try
        {
            if (zone == null || targetTemp > 0.0)
            {
                return;
            }
            foreach (GameItem item in zone.childItems)
            {
                if (Strain.IsStrain(item))
                {
                    Strain.MarkForFreezing(item);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("marking strains for freezing failed", e);
        }
    }
}
