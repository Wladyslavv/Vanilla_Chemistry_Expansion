// Xinke shrub: a perennial, drought-tolerant hydroponic crop whose leaves are a cheap, mild narcotic. The
// leaves carry an alkaloid through an innate "Alkaloid Production" gene, which also makes them contraband;
// refining the alkaloid into a strong product is a later step. Written for C# 5 (see DreamToxinMod.cs).
using System;
using HarmonyLib;
using UnityEngine;

public static class Xinke
{
    public const string SEED_ID = "vce_xinke_seed";
    public const string LEAF_ID = "vce_xinke_leaf";
    public const string ALKALOID_ID = "vce_alkaloid";

    // Sprite keys (Textures/Items/<key>.png). The hydroponic machine draws "<harvest id>_p" while the plant
    // grows and "<harvest id>_p2" while a perennial ripens; XinkeMachineSpritePatch serves those two.
    public const string SEED_SPRITE = "vce_xinke_seed";
    public const string LEAF_SPRITE = "vce_xinke_leaf";
    public const string LEAF_SPRITE_S = "vce_xinke_leaf_s";
    public const string LEAF_SPRITE_L = "vce_xinke_leaf_l";
    public const string GROWING_PREFIX = LEAF_ID + "_p";

    // Plant stats, as agreed: like Dream Cap, but perennial and drought tolerant.
    public const int REQUIRED_PROGRESS = 24;
    public const int HARVEST_QUANTITY = 8;
    public const int REFRUIT_PROGRESS = 12;
    public const int NUTRIMENT_LEVEL = 250;
    public const int NUTRIMENT_RANGE = 75;
    public const int NUTRIMENT_USE = 30;
    public const double CONTAMINANT_EFFECT = 1.01;
    public const int WATER_USE = 20;

    // Alkaloid: mL per normal leaf at gene rank 1; the innate rank 3 x Weak 1 gives 0.5 x 3^0.75 x 0.75 = about
    // 0.85 mL (rank 2 before 1.6.0 gave 0.63 mL); euphoria per uL.
    public const float ALKALOID_ML_RANK_ONE = 0.5f;
    public const int ALKALOID_INNATE_RANK = 3;
    public const int ALKALOID_OLD_INNATE_RANK = 2;
    private const string RANK_MIGRATION_KEY = "xinke_alkaloid_rank3";
    public const int ALKALOID_COMPLEXITY = 4;
    public const int LEAF_CONTRABAND_LEVEL = 1;
    // Mild in the leaf; the refined powder (Crackine.SALT_ID) carries the real euphoria.
    public const double ALKALOID_EUPHORIA = 0.5;

    // Leaf sizes: share of a normal leaf, stored on the item so saved leaves keep their size.
    public const string SIZE_TAG = "VCE_XINKE_SIZE_INT";
    public const int SMALL = 60;
    public const int NORMAL = 100;
    public const int LARGE = 150;

    // A normal leaf sells for about half a Dream Dust after contraband markups:
    // Dream Dust 18 x 1.70 (level 2) = 30.6; leaf 11 x 1.35 (level 1) = 14.9.
    public const long LEAF_VALUE_S = 7;
    public const long LEAF_VALUE = 11;
    public const long LEAF_VALUE_L = 17;

    public const long SEED_VALUE = 30;
    public const int SEED_CONTRABAND_LEVEL = 1;

    public static string TraitId
    {
        get { return PlantTraitList.ProductionId(ALKALOID_ID); }
    }

    // ---- registration (from IndustrialAlcohol.RegisterContent, once the game language is set) -----------------

    public static void Register()
    {
        RegisterLiquid();
        Crackine.Register();
        if (!PlantTraitRegistry.Contains(TraitId))
        {
            PlantTraitRegistry.Add(CreateTrait());
            // Potent/Weak scale the active ingredients already in the produce, so they run after production genes.
            MoveToEnd("potent");
            MoveToEnd("weak");
        }
        if (PlantRegistry.Get(SEED_ID) == null)
        {
            PlantRegistry.Add(CreatePlant());
        }
        IaLog.Info("registered the Xinke shrub");
    }

    public static void Unregister()
    {
        PlantRegistry.Remove(SEED_ID);
        Crackine.Unregister();
        PlantTraitRegistry.Remove(TraitId);
    }

    public static void RefreshNames()
    {
        Liquid liquid;
        if (Liquid.LiquidDict.TryGetValue(ALKALOID_ID, out liquid))
        {
            liquid.displayName = IaText.Get("liquid_alkaloid");
        }
        PlantDefinition plant = PlantRegistry.Get(SEED_ID);
        if (plant != null)
        {
            plant.displayName = IaText.Get("xinke_plant_name");
        }
        Crackine.RefreshNames();
    }

    private static void RegisterLiquid()
    {
        Liquid liquid = IaLiquids.AddLiquid(ALKALOID_ID, "liquid_alkaloid", 5, "e8e4d0", 1);
        if (liquid == null)
        {
            return;
        }
        // Dissolved and not boiled off: filters and plants leave it in the water, evaporation concentrates it.
        liquid.canBeRemovedByPurifier = false;
        liquid.basePurifierRemoveAmount = 0;
        liquid.isEvaporative = false;
        liquid.euphoria = ALKALOID_EUPHORIA;
    }

    private static void MoveToEnd(string traitId)
    {
        PlantTrait trait = PlantTraitRegistry.Get(traitId);
        if (trait != null)
        {
            PlantTraitRegistry.Add(trait);
        }
    }

    private static PlantTrait CreateTrait()
    {
        return new PlantTrait(TraitId, "Alkaloid Production", ALKALOID_COMPLEXITY, "")
            .Named(() => PlantGeneticsText.ProductionName(AlkaloidName()), () => PlantGeneticsText.ProductionDescription(AlkaloidName()))
            .Infuses(ALKALOID_ID)
            .Unlimited()
            .AppliesToSeedProduce()
            .OnHarvestItem(Infuse)
            .Describe(rank => PlantGeneticsText.EffectProductionContraband(
                (ALKALOID_ML_RANK_ONE * PlantGeneticsHelper.RankScale(rank)).ToString("0.#"), AlkaloidName()));
    }

    private static string AlkaloidName()
    {
        Liquid liquid = Liquid.GetLiquidFromId(ALKALOID_ID);
        return liquid != null ? liquid.displayName : ALKALOID_ID;
    }

    // Same as the vanilla production genes, scaled by leaf size; any crop given this gene turns contraband.
    private static void Infuse(PlantTraitContext ctx)
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
        float ml = ALKALOID_ML_RANK_ONE * PlantGeneticsHelper.RankScale(ctx.rank) * SizeOf(item) / 100f;
        ChemicalHelper.AddChemContentUL(item, ALKALOID_ID, Mathf.RoundToInt(ml * 1000f));
        if (!item.IsTag("CONTRABAND_ITEM_TAG"))
        {
            ContrabandHelper.InitContrabandItem(item, LEAF_CONTRABAND_LEVEL);
        }
    }

    private static PlantDefinition CreatePlant()
    {
        return new PlantDefinition(SEED_ID, IaText.Get("xinke_plant_name"), LEAF_ID, REQUIRED_PROGRESS, HARVEST_QUANTITY)
            .Nutriment(NUTRIMENT_LEVEL, NUTRIMENT_RANGE, NUTRIMENT_USE)
            .Contaminant(CONTAMINANT_EFFECT)
            .Water(WATER_USE)
            .Refruit(REFRUIT_PROGRESS)
            .ReturnsSeeds()
            .Innate(TraitId, ALKALOID_INNATE_RANK)
            .Innate("weak", 1)
            .Innate("hardy", 1)
            .Innate("drought_resistant", 1)
            .Innate("simple_genome", 1)
            .Innate("perennial", 1)
            .OnHarvest(Harvest);
    }

    // One seed (the perennial gene halves the chance), then leaves sized by plant health like Kotton.
    private static void Harvest(PlantHarvestContext ctx)
    {
        ctx.OutputById(SEED_ID, 1);
        PlantHarvest.SizeSplit split = PlantHarvest.SplitByHealth(ctx.harvestCount, ctx.health, true, false, 0);
        PlantHarvest.OutputSplit(ctx, split, () => HarvestLeaf(SMALL), () => HarvestLeaf(NORMAL), () => HarvestLeaf(LARGE), null);
    }

    // CreateLeaf builds the item directly, so it skips what DirectoryMaster.Item does for every vanilla harvest:
    // without it, harvested leaves show as not owned. Shop leaves stay unowned until bought.
    private static GameItem HarvestLeaf(int size)
    {
        GameItem item = CreateLeaf(size);
        GeneralHelper.SetItemOwned(item, true);
        if (item.uniqueId == 0)
        {
            item.uniqueId = PlayerStore.Instance.GetUniqueID();
        }
        PermitHelper.OnNewItemCreated(item);
        return item;
    }

    // ---- items ------------------------------------------------------------------------------------------------

    public static void AddItems(ModItemDirectory directory)
    {
        directory.Add(SEED_ID, CreateSeed);
        directory.Add(LEAF_ID, () => CreateLeaf(NORMAL));
        directory.Add(Crackine.ITEM_ID, Crackine.CreateVial);
        directory.Add(Crackine.LARGE_ID, Crackine.CreateLargeVial);
    }

    public static GameItem CreateSeed()
    {
        GameItem item = ItemDirectory.CreateEmptyItem(SEED_ID);
        try
        {
            item.name = IaText.Get("xinke_seed_name");
            item.shortDescription = IaText.Get("xinke_seed_desc");
            HydroponicHelper.InitSeedItem(item, SEED_ID);
            ContrabandHelper.InitContrabandItem(item, SEED_CONTRABAND_LEVEL);
            item.SetGameItemType("SEED");
            item.unitValue = SEED_VALUE;
            BeerSprites.Apply(item, SEED_SPRITE);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            IaLog.Error("create " + SEED_ID + " failed", e);
        }
        return item;
    }

    public static GameItem CreateLeaf(int size)
    {
        GameItem item = ItemDirectory.CreateEmptyItem(LEAF_ID);
        try
        {
            item.ModifyTag(SIZE_TAG, delegate(TagState state)
            {
                state.Enable().SetInt(size);
            }, false);
            ApplySize(item);
            item.shortDescription = IaText.Get("xinke_leaf_desc");
            item.flavorText = IaText.Get("xinke_leaf_flavor");
            item.SetGameItemType("NARCOTIC");
            item.SetGameItemType("SUBSTANCE");
            // Chewed by an addicted player: holds off withdrawal for a day, like a weak narcotic.
            ConsumableHelper.InitNarcotic(item, 1);
            AudioHelper.InitLeafy(item);
            float k = size / 100f;
            ChemicalHelper.InitChemItem(item, true, false);
            // Blended: water and protein, plus the alkaloid from the gene below.
            ChemicalHelper.SetChemContentML(item, "water", 8f * k);
            ChemicalHelper.SetChemContentML(item, "protein", 1.5f * k);
            // Bought or spawned leaves get the innate genes (alkaloid, contraband); harvested ones get the seed's.
            PlantGeneticsHelper.ApplyFactoryGenetics(item, SEED_ID);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            IaLog.Error("create " + LEAF_ID + " failed", e);
        }
        return item;
    }

    public static int SizeOf(GameItem item)
    {
        if (item == null || item.identifier != LEAF_ID || !item.IsTag(SIZE_TAG))
        {
            return NORMAL;
        }
        int size = item.GetTagReadonly(SIZE_TAG).GetInt();
        return size > 0 ? size : NORMAL;
    }

    // Name, value and sprite follow the size tag.
    public static void ApplySize(GameItem item)
    {
        int size = SizeOf(item);
        if (size <= SMALL)
        {
            item.name = IaText.Get("xinke_leaf_name_s");
            item.unitValue = LEAF_VALUE_S;
            BeerSprites.Apply(item, LEAF_SPRITE_S);
        }
        else if (size >= LARGE)
        {
            item.name = IaText.Get("xinke_leaf_name_l");
            item.unitValue = LEAF_VALUE_L;
            BeerSprites.Apply(item, LEAF_SPRITE_L);
        }
        else
        {
            item.name = IaText.Get("xinke_leaf_name");
            item.unitValue = LEAF_VALUE;
            BeerSprites.Apply(item, LEAF_SPRITE);
        }
    }

    // ModHook.OnGameLoadedLate: saved small and large leaves are rebuilt as normal ones; restore their size.
    // Leaves harvested before 1.4.1 were never marked owned, and the player inventories only accept owned items,
    // so they sit stuck in the hydroponic output grid; mark the ones in the player's own machines as owned.
    public static void FixLoadedItems()
    {
        try
        {
            int repaired = 0;
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (item != null && item.identifier == LEAF_ID)
                {
                    ApplySize(item);
                    if (!GeneralHelper.IsItemOwned(item) && InOwnedHydroponic(item))
                    {
                        GeneralHelper.SetItemOwned(item, true);
                        repaired++;
                    }
                }
            }
            if (repaired > 0)
            {
                IaLog.Info("marked " + repaired + " harvested Xinke leaves as owned");
            }
            MigrateAlkaloidRank();
        }
        catch (Exception e)
        {
            IaLog.Error("fixing loaded Xinke leaves failed", e);
        }
    }

    // 1.6.0 raised the innate Alkaloid Production from II to III. Seeds carry their ranks, so seeds in a save
    // (planted ones included) still hold II; lift those once per save. Later edits by the player are left alone.
    private static void MigrateAlkaloidRank()
    {
        if (ModHelper.GetModDataInt(DreamToxinMod.MOD_ID, RANK_MIGRATION_KEY, 0) != 0)
        {
            return;
        }
        int lifted = 0;
        foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
        {
            if (item != null && item.identifier == SEED_ID
                && PlantGeneticsHelper.GetTraitRank(item, TraitId) == ALKALOID_OLD_INNATE_RANK)
            {
                PlantGeneticsHelper.SetTraitRank(item, TraitId, ALKALOID_INNATE_RANK);
                lifted++;
            }
        }
        ModHelper.SetModDataInt(DreamToxinMod.MOD_ID, RANK_MIGRATION_KEY, 1);
        if (lifted > 0)
        {
            IaLog.Info("raised Alkaloid Production II -> III on " + lifted + " saved Xinke seed(s)");
        }
    }

    private static bool InOwnedHydroponic(GameItem item)
    {
        GameInventory inventory = item.parentInventory;
        GameItem machine = inventory == null ? null : inventory.GetParentItem();
        return machine != null && machine.IsTag("HYDROPONIC_MACHINE_TAG") && GeneralHelper.IsItemOwned(machine);
    }

    public static Sprite ModSprite(string key)
    {
        LoadedMod mod = ModLoader.Mods.Find(m => m.Manifest != null && m.Manifest.ID == DreamToxinMod.MOD_ID);
        Sprite sprite;
        if (mod != null && mod.Sprites.TryGetValue(key, out sprite))
        {
            return sprite;
        }
        return null;
    }

    // ---- shops and debug ---------------------------------------------------------------------------------------

    public static void OfferSeeds()
    {
        for (int i = 0; i < 2; i++)
        {
            PlayerStore.Instance.AddDirectSellingItemToTable(CreateSeed(), false, false, false, 0);
        }
    }

    public static void OfferLeaves()
    {
        PlayerStore.Instance.AddDirectSellingItemToTable(CreateLeaf(SMALL), false, false, false, 0);
        PlayerStore.Instance.AddDirectSellingItemToTable(CreateLeaf(NORMAL), false, false, false, 0);
        if (RNG.Roll(75))
        {
            PlayerStore.Instance.AddDirectSellingItemToTable(CreateLeaf(NORMAL), false, false, false, 0);
        }
        if (RNG.Roll(50))
        {
            PlayerStore.Instance.AddDirectSellingItemToTable(CreateLeaf(LARGE), false, false, false, 0);
        }
    }

    public static void SpawnKit()
    {
        try
        {
            GameItem[] items = { CreateSeed(), CreateSeed(), CreateLeaf(SMALL), CreateLeaf(NORMAL), CreateLeaf(LARGE) };
            foreach (GameItem item in items)
            {
                GraphUtils.TryAcceptAll(EmporiumEntry.Instance.invElement, item);
            }
        }
        catch (Exception e)
        {
            IaLog.Error("Xinke debug kit failed", e);
        }
    }
}

// The hydroponic machine loads "<harvest id>_p"/"_p2" from the vanilla plant atlas; serve ours from the mod.
[HarmonyPatch(typeof(RenderHandler), "LoadFromAtlas")]
public static class XinkeMachineSpritePatch
{
    private static bool Prefix(string name, ref Sprite __result)
    {
        if (name == null || !name.StartsWith(Xinke.GROWING_PREFIX))
        {
            return true;
        }
        Sprite sprite = Xinke.ModSprite(name);
        if (sprite == null)
        {
            return true;
        }
        __result = sprite;
        return false;
    }
}

// The retired farmer (vanilla seed seller) also brings two Xinke seeds.
[HarmonyPatch(typeof(StoreClientListSpec), "CreateRetiredFarmer")]
public static class XinkeRetiredFarmerPatch
{
    private static void Postfix(StoreClient __result)
    {
        AppendToStock(__result, Xinke.OfferSeeds, "retired farmer");
    }

    public static void AppendToStock(StoreClient client, Action offer, string who)
    {
        try
        {
            if (client == null || client.mainDialogue == null)
            {
                return;
            }
            Dialogue dialogue = client.mainDialogue;
            Action vanilla = dialogue.endAction;
            dialogue.endAction = delegate
            {
                if (vanilla != null)
                {
                    vanilla();
                }
                try
                {
                    offer();
                }
                catch (Exception e)
                {
                    IaLog.Error("adding Xinke stock to the " + who + " failed", e);
                }
            };
        }
        catch (Exception e)
        {
            IaLog.Error(who + " patch failed", e);
        }
    }
}

// The shady farmer (sells Dream Caps, coffee and nutrifruit to players with chemical supplies) also sells leaves.
[HarmonyPatch(typeof(StoreClientListCartel), "CreateShadyFarmer")]
public static class XinkeShadyFarmerPatch
{
    private static void Postfix(StoreClient __result)
    {
        XinkeRetiredFarmerPatch.AppendToStock(__result, Xinke.OfferLeaves, "shady farmer");
    }
}
