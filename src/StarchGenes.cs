// Starch genes: moves the starch and sugar of nutrifruit out of its fixed composition and into two innate
// "production" genes, so they can be extracted onto DNA disks and spliced into other crops. Totals for
// nutrifruit stay exactly as in vanilla. Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

// Called from DreamToxinMod.
public static class StarchGenesSetup
{
    public static void Enable()
    {
        ModHook.OnGameLoadedLate += StarchGenes.MigrateSavedSeeds;
        try
        {
            string id = DreamToxinMod.MOD_ID;
            ModHelper.AddDebugFunction(id, "Modding", "Starch Genes: test kit", SgDebugKit.SpawnTestKit, false);
            ModHelper.AddDebugFunction(id, "Modding", "Starch Genes: starch disk", SgDebugKit.SpawnStarchDisk, false);
            ModHelper.AddDebugFunction(id, "Modding", "Starch Genes: log nutrifruit sizes", SgDebugKit.LogNutrifruitSizes, false);
        }
        catch (Exception e)
        {
            SgLog.Error("debug function registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnGameLoadedLate -= StarchGenes.MigrateSavedSeeds;
    }
}

public class GeneSpec
{
    public string LiquidId;
    // mL added per 100 g of nutrifruit at rank 1; vanilla ranks scale by rank^0.75.
    public float MlPer100g;
    public int Complexity;
    public int NutrifruitRank;

    public GeneSpec(string liquidId, float mlPer100g, int complexity, int nutrifruitRank)
    {
        LiquidId = liquidId;
        MlPer100g = mlPer100g;
        Complexity = complexity;
        NutrifruitRank = nutrifruitRank;
    }

    public string TraitId
    {
        get { return PlantTraitList.ProductionId(LiquidId); }
    }

    public string LiquidName()
    {
        Liquid liquid = Liquid.GetLiquidFromId(LiquidId);
        return liquid != null ? liquid.displayName : LiquidId;
    }
}

public static class StarchGenes
{
    public const string NUTRIFRUIT_ID = "nutrifruit";
    public const string NUTRIFRUIT_SEED_ID = "nutrifruit_seed";
    // Vanilla potato composition: 78.55 mL water per 100 g, used to recover a fruit's weight.
    public const float WATER_ML_PER_100G = 78.55f;
    // Weight assumed for crops without a nutrifruit-style composition (a normal nutrifruit).
    public const float DEFAULT_GRAMS = 250f;
    // Seeds that already carry this mod's innate genes; older saved seeds get them added once on load.
    public const string MIGRATED_TAG = "STARCH_GENES_MIGRATED_BOOL";

    // Rank 4 starch gives 6.127 * 4^0.75 = 17.33 mL per 100 g, the vanilla amount.
    public static readonly GeneSpec Starch = new GeneSpec("starch", 6.127f, 3, 4);
    public static readonly GeneSpec Sugar = new GeneSpec("sugar", 0.67f, 2, 1);
    public static readonly GeneSpec[] All = { Starch, Sugar };

    public static void Register()
    {
        foreach (GeneSpec gene in All)
        {
            PlantTraitRegistry.Add(CreateTrait(gene));
        }
        // Harvest hooks run in registry order, and Potent/Weak scale whatever active ingredients are
        // already in the produce, so they must come after these production genes.
        MoveToEnd("potent");
        MoveToEnd("weak");

        PlantDefinition nutrifruit = PlantRegistry.Get(NUTRIFRUIT_SEED_ID);
        if (nutrifruit == null)
        {
            SgLog.Info("nutrifruit plant not found, genes registered without an innate source");
            return;
        }
        foreach (GeneSpec gene in All)
        {
            nutrifruit.Innate(gene.TraitId, gene.NutrifruitRank);
        }
        SgLog.Info("registered starch and sugar production genes on nutrifruit");
    }

    public static void Unregister()
    {
        PlantDefinition nutrifruit = PlantRegistry.Get(NUTRIFRUIT_SEED_ID);
        foreach (GeneSpec gene in All)
        {
            PlantTraitRegistry.Remove(gene.TraitId);
            if (nutrifruit != null)
            {
                nutrifruit.innateTraits.Remove(gene.TraitId);
            }
        }
    }

    private static void MoveToEnd(string traitId)
    {
        PlantTrait trait = PlantTraitRegistry.Get(traitId);
        if (trait != null)
        {
            PlantTraitRegistry.Add(trait);
        }
    }

    private static PlantTrait CreateTrait(GeneSpec gene)
    {
        return new PlantTrait(gene.TraitId, gene.LiquidId + " Production", gene.Complexity, "")
            .Named(() => PlantGeneticsText.ProductionName(gene.LiquidName()), () => PlantGeneticsText.ProductionDescription(gene.LiquidName()))
            .Infuses(gene.LiquidId)
            .Unlimited()
            .AppliesToSeedProduce()
            .OnHarvestItem(ctx => Infuse(ctx, gene))
            .Describe(rank => SgText.Get("effect_production",
                Format(MlPer100g(gene, rank)), gene.LiquidName(), Format(MlPer100g(gene, rank) * DEFAULT_GRAMS / 100f)));
    }

    public static float MlPer100g(GeneSpec gene, int rank)
    {
        return gene.MlPer100g * PlantGeneticsHelper.RankScale(rank);
    }

    private static string Format(float ml)
    {
        return ml.ToString("0.#");
    }

    private static void Infuse(PlantTraitContext ctx, GeneSpec gene)
    {
        GameItem item = ctx.item;
        if (item == null)
        {
            return;
        }
        if (!item.IsTag("CHEM_ITEM_TAG"))
        {
            ChemicalHelper.InitChemItem(item, true, false);
        }
        float ml = MlPer100g(gene, ctx.rank) * GramsOf(item) / 100f;
        ChemicalHelper.AddChemContentUL(item, gene.LiquidId, Mathf.RoundToInt(ml * 1000f));
    }

    // Nutrifruit comes in four sizes (150-800 g) that only differ in their composition, so its weight is
    // recovered from its water content; anything else counts as one normal-sized fruit.
    public static float GramsOf(GameItem item)
    {
        if (item.identifier == NUTRIFRUIT_ID)
        {
            string waterPart = Liquid.GetCurrentPartFromId("water");
            if (item.IsTag(waterPart))
            {
                float waterMl = item.GetTagReadonly(waterPart).GetInt() / 1000f;
                if (waterMl > 0f)
                {
                    return waterMl / WATER_ML_PER_100G * 100f;
                }
            }
        }
        return DEFAULT_GRAMS;
    }

    public static void MarkMigrated(GameItem seed)
    {
        seed.EnableTag(MIGRATED_TAG, true);
    }

    // Seeds saved before this mod lack the new innate genes, which vanilla would count as edits
    // (extra complexity, "modified" seed). Add them once per seed; a later extraction is respected.
    public static void MigrateSavedSeeds()
    {
        try
        {
            int count = 0;
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (item == null || !item.IsTag("SEED_ITEM_TAG") || item.IsTag(MIGRATED_TAG)
                    || HydroponicHelper.GetPlantId(item) != NUTRIFRUIT_SEED_ID)
                {
                    continue;
                }
                foreach (GeneSpec gene in All)
                {
                    if (PlantGeneticsHelper.GetTraitRank(item, gene.TraitId) <= 0)
                    {
                        PlantGeneticsHelper.SetTraitRank(item, gene.TraitId, gene.NutrifruitRank);
                    }
                }
                MarkMigrated(item);
                count++;
            }
            if (count > 0)
            {
                SgLog.Info("added starch and sugar genes to " + count + " saved nutrifruit seed(s)");
            }
        }
        catch (Exception e)
        {
            SgLog.Error("migrating saved nutrifruit seeds failed", e);
        }
    }
}

// Starch and sugar now come from the genes, so the fixed composition leaves them at zero. Every
// nutrifruit runs this before its genes (factory genetics or harvest hooks), so they are added exactly once.
[HarmonyPatch(typeof(ChemicalCompositionHelper), "SetPotatoComposition")]
public static class PotatoCompositionPatch
{
    public static void Postfix(GameItem item)
    {
        foreach (GeneSpec gene in StarchGenes.All)
        {
            ChemicalHelper.SetChemContentUL(item, gene.LiquidId, 0);
        }
    }
}

// New seeds get their innate genes here, so they never need the save migration.
[HarmonyPatch(typeof(PlantGeneticsHelper), "InitInnateTraits")]
public static class InnateTraitsPatch
{
    public static void Postfix(GameItem seed, PlantDefinition plant)
    {
        if (seed != null && plant != null && plant.id == StarchGenes.NUTRIFRUIT_SEED_ID)
        {
            StarchGenes.MarkMigrated(seed);
        }
    }
}

public static class SgDebugKit
{
    public static void SpawnTestKit()
    {
        Give(DirectoryMaster.Item(StarchGenes.NUTRIFRUIT_SEED_ID, true));
        Give(DirectoryMaster.Item("fillerweed_seed", true));
        Give(DirectoryMaster.Item("plant_dna_disk", true));
        Give(DirectoryMaster.Item("plant_dna_disk", true));
        Give(DirectoryMaster.Item(StarchGenes.NUTRIFRUIT_ID, true));
        Give(PreBuiltItemHelper.CreateNutrifruitHuge());
    }

    // A rank-1 starch disk with 4 uses, the same as extracting it from an unedited nutrifruit seed.
    public static void SpawnStarchDisk()
    {
        GameItem disk = DirectoryMaster.Item("plant_dna_disk", true);
        PlantGeneticsHelper.WriteDisk(disk, PlantTraitRegistry.Get(StarchGenes.Starch.TraitId), 1, StarchGenes.Starch.NutrifruitRank);
        Give(disk);
    }

    public static void LogNutrifruitSizes()
    {
        LogFruit("small", PreBuiltItemHelper.CreateNutrifruitSmall());
        LogFruit("normal", PreBuiltItemHelper.CreateNutrifruitNormal());
        LogFruit("large", PreBuiltItemHelper.CreateNutrifruitLarge());
        LogFruit("huge", PreBuiltItemHelper.CreateNutrifruitHuge());
    }

    private static void LogFruit(string size, GameItem fruit)
    {
        SgLog.Info(size + ": " + StarchGenes.GramsOf(fruit).ToString("0") + " g, starch " + Ml(fruit, "starch")
            + " mL, sugar " + Ml(fruit, "sugar") + " mL (vanilla: starch " + (StarchGenes.GramsOf(fruit) * 0.1733f).ToString("0.##")
            + ", sugar " + (StarchGenes.GramsOf(fruit) * 0.0067f).ToString("0.##") + ")");
        fruit.Destroy();
    }

    private static string Ml(GameItem item, string liquidId)
    {
        string part = Liquid.GetCurrentPartFromId(liquidId);
        return item.IsTag(part) ? (item.GetTagReadonly(part).GetInt() / 1000f).ToString("0.##") : "0";
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

public static class SgText
{
    public static string Get(string key, params object[] args)
    {
        return ModHelper.GetLocalized(DreamToxinMod.MOD_ID, key, args);
    }
}

public static class SgLog
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
