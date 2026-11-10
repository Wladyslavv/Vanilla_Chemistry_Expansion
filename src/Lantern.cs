// Hollow's Lantern: a lab-bred annual chili whose only chemistry is capsaicin (innate "Capsaicin Production" gene).
// Route (see art/chili/DESIGN.md):
//   1. blend chilis -> capsaicin only                  2. add at least 4x as much alcohol
//   3. heat to 50 C+: capsaicin -> Lantern Ester (short on alcohol: part of it turns into physical contaminant)
//   4. the evaporator drives off the alcohol           5. Chem Finisher: "a small vial of incapacitant"
// The vial is legal (medical + weapon). Upper Level and Security clients buy it and it wipes evidence (crime
// values) and then investigation progress; mercenaries buy it as ordinary goods; everyone else refuses it.
// Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

public static class Lantern
{
    public const string SEED_ID = "vce_chili_seed";
    public const string CHILI_ID = "vce_chili";
    public const string CAPSAICIN_ID = "vce_capsaicin";
    public const string ESTER_ID = "vce_lantern_ester";
    public const string ITEM_ID = "vce_incapacitant_vial";

    // Sprite keys (Textures/Items/<key>.png); the hydroponic machine draws "<harvest id>_p" while the plant grows.
    public const string SEED_SPRITE = "vce_chili_seed";
    public const string CHILI_SPRITE = "vce_chili";
    public const string CHILI_SPRITE_S = "vce_chili_s";
    public const string CHILI_SPRITE_L = "vce_chili_l";
    public const string GROWING_PREFIX = CHILI_ID + "_p";
    // Amber reagent bottle drawn by art/chili/DrawVial.cs.
    public const string VIAL_SPRITE = "vce_incapacitant_vial";

    // Plant stats (DESIGN.md, crop section).
    public const int REQUIRED_PROGRESS = 24;
    public const int HARVEST_QUANTITY = 8;
    public const int NUTRIMENT_LEVEL = 320;
    public const int NUTRIMENT_RANGE = 50;
    public const int NUTRIMENT_USE = 60;
    public const double CONTAMINANT_EFFECT = 1.15;
    public const int WATER_USE = 100;

    // Capsaicin per normal chili at gene rank 1; rank 3 (innate) gives 0.5 x 3^0.75 = about 1.14 mL,
    // so a harvest of 8 normal chilis makes about 9 mL, five pharma-grade vials.
    public const float CAPSAICIN_ML_RANK_ONE = 0.5f;
    public const int CAPSAICIN_INNATE_RANK = 3;
    public const int CAPSAICIN_COMPLEXITY = 4;

    public const string SIZE_TAG = "VCE_CHILI_SIZE_INT";
    public const int SMALL = 60;
    public const int NORMAL = 100;
    public const int LARGE = 150;

    // Raw chilis are worth little: about 2 normal chilis go into one vial (base 28).
    public const long CHILI_VALUE_S = 3;
    public const long CHILI_VALUE = 5;
    public const long CHILI_VALUE_L = 7;
    public const long SEED_VALUE = 20;

    // Vial: same volume and purity tiers as Dream Dust (2000 uL, vanilla grading). Not contraband, so no markup:
    // 28 against Dream Dust's 18 x 1.70 = 30.6.
    public const int REQUIRED_VOLUME = 2000;
    public const long VIAL_VALUE = 28;

    // Ester reaction: at 50 C or hotter with alcohol present; 5% of the capsaicin per tick (min 200 uL), each uL
    // takes 0.1 uL of alcohol. Below 4x alcohol, (4 - ratio) / 4 of the converted part becomes physical contaminant.
    public const double REACT_MIN_TEMP = 50.0;
    public const double REACT_RATE = 0.05;
    public const int REACT_MIN_UL = 200;
    public const double ALCOHOL_PER_CAPSAICIN = 0.1;
    public const double FULL_ALCOHOL_RATIO = 4.0;

    public static string TraitId
    {
        get { return PlantTraitList.ProductionId(CAPSAICIN_ID); }
    }

    // ---- setup -------------------------------------------------------------------------------------------------

    public static void Enable()
    {
        ModHook.OnGameLoadedLate += FixLoadedItems;
        try
        {
            string id = DreamToxinMod.MOD_ID;
            ModHelper.AddDebugFunction(id, "Modding", "Lantern: 2 seeds + 3 chilis", SpawnKit, false);
            ModHelper.AddDebugFunction(id, "Modding", "Lantern: capsaicin in 4x alcohol (heat to 50-78 C)", SpawnMix, false);
            ModHelper.AddDebugFunction(id, "Modding", "Lantern: 2 pharma-grade incapacitant vials", SpawnPharma, false);
        }
        catch (Exception e)
        {
            Log.Error("Lantern debug function registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnGameLoadedLate -= FixLoadedItems;
    }

    // From Content.EnsureRegistered, once the game language is set.
    public static void Register()
    {
        RegisterLiquids();
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
        if (FinisherRecipes.Find(ITEM_ID) == null)
        {
            ChemFinisherRecipe recipe = new ChemFinisherRecipe();
            recipe.identifier = ITEM_ID;
            recipe.displayName = Text.Get("incapacitant_vial_name");
            recipe.requiredVolume = REQUIRED_VOLUME;
            recipe.isRequireDry = true;
            recipe.activeIngredientId = ESTER_ID;
            ChemFinisherRecipe.chemFinisherRecipes.Add(recipe);
        }
        Log.Info("registered Hollow's Lantern");
    }

    public static void Unregister()
    {
        PlantRegistry.Remove(SEED_ID);
        PlantTraitRegistry.Remove(TraitId);
        ChemFinisherRecipe.chemFinisherRecipes.RemoveAll(r => r.identifier == ITEM_ID);
    }

    public static void RefreshNames()
    {
        Rename(CAPSAICIN_ID, "liquid_capsaicin");
        Rename(ESTER_ID, "liquid_lantern_ester");
        PlantDefinition plant = PlantRegistry.Get(SEED_ID);
        if (plant != null)
        {
            plant.displayName = Text.Get("lantern_plant_name");
        }
        ChemFinisherRecipe recipe = FinisherRecipes.Find(ITEM_ID);
        if (recipe != null)
        {
            recipe.displayName = Text.Get("incapacitant_vial_name");
        }
    }

    private static void Rename(string id, string key)
    {
        Liquid liquid;
        if (Liquid.LiquidDict.TryGetValue(id, out liquid))
        {
            liquid.displayName = Text.Get(key);
        }
    }

    private static void RegisterLiquids()
    {
        // Both dissolved and non-volatile: filters and the purifier leave them, the evaporator concentrates them.
        foreach (Liquid liquid in new Liquid[]
        {
            IaLiquids.AddLiquid(CAPSAICIN_ID, "liquid_capsaicin", 5, "d8452e", 2),
            IaLiquids.AddLiquid(ESTER_ID, "liquid_lantern_ester", 5, "b8336f", 2)
        })
        {
            if (liquid != null)
            {
                liquid.canBeRemovedByPurifier = false;
                liquid.basePurifierRemoveAmount = 0;
                liquid.isEvaporative = false;
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

    private static string CapsaicinName()
    {
        Liquid liquid = Liquid.GetLiquidFromId(CAPSAICIN_ID);
        return liquid != null ? liquid.displayName : CAPSAICIN_ID;
    }

    private static PlantTrait CreateTrait()
    {
        return new PlantTrait(TraitId, "Capsaicin Production", CAPSAICIN_COMPLEXITY, "")
            .Named(() => PlantGeneticsText.ProductionName(CapsaicinName()), () => PlantGeneticsText.ProductionDescription(CapsaicinName()))
            .Infuses(CAPSAICIN_ID)
            .Unlimited()
            .AppliesToSeedProduce()
            .OnHarvestItem(Infuse)
            .Describe(rank => PlantGeneticsText.EffectProduction(
                (CAPSAICIN_ML_RANK_ONE * PlantGeneticsHelper.RankScale(rank)).ToString("0.#"), CapsaicinName()));
    }

    // Like the vanilla production genes, scaled by chili size.
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
        float ml = CAPSAICIN_ML_RANK_ONE * PlantGeneticsHelper.RankScale(ctx.rank) * SizeOf(item) / 100f;
        ChemicalHelper.AddChemContentUL(item, CAPSAICIN_ID, Mathf.RoundToInt(ml * 1000f));
    }

    private static PlantDefinition CreatePlant()
    {
        return new PlantDefinition(SEED_ID, Text.Get("lantern_plant_name"), CHILI_ID, REQUIRED_PROGRESS, HARVEST_QUANTITY)
            .Nutriment(NUTRIMENT_LEVEL, NUTRIMENT_RANGE, NUTRIMENT_USE)
            .Contaminant(CONTAMINANT_EFFECT)
            .Water(WATER_USE)
            .ReturnsSeeds()
            .Innate(TraitId, CAPSAICIN_INNATE_RANK)
            .Innate(PlantTraitList.POLLUTING, 1)
            .OnHarvest(Harvest);
    }

    // One seed back (annual), then chilis sized by plant health like Kotton and Xinke.
    private static void Harvest(PlantHarvestContext ctx)
    {
        ctx.OutputById(SEED_ID, 1);
        PlantHarvest.SizeSplit split = PlantHarvest.SplitByHealth(ctx.harvestCount, ctx.health, true, false, 0);
        PlantHarvest.OutputSplit(ctx, split, () => HarvestChili(SMALL), () => HarvestChili(NORMAL), () => HarvestChili(LARGE), null);
    }

    // Built directly, so do what DirectoryMaster.Item does for vanilla harvests (see Xinke.HarvestLeaf).
    private static GameItem HarvestChili(int size)
    {
        GameItem item = CreateChili(size);
        GeneralHelper.SetItemOwned(item, true);
        if (item.uniqueId == 0)
        {
            item.uniqueId = PlayerStore.Instance.GetUniqueID();
        }
        PermitHelper.OnNewItemCreated(item);
        return item;
    }

    // ---- items -------------------------------------------------------------------------------------------------

    public static void AddItems(ModItemDirectory directory)
    {
        directory.Add(SEED_ID, CreateSeed);
        directory.Add(CHILI_ID, () => CreateChili(NORMAL));
        directory.Add(ITEM_ID, CreateVial);
    }

    public static GameItem CreateSeed()
    {
        GameItem item = ItemDirectory.CreateEmptyItem(SEED_ID);
        try
        {
            item.name = Text.Get("lantern_seed_name");
            item.shortDescription = Text.Get("lantern_seed_desc");
            HydroponicHelper.InitSeedItem(item, SEED_ID);
            item.SetGameItemType("SEED");
            item.unitValue = SEED_VALUE;
            BeerSprites.Apply(item, SEED_SPRITE);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);        }
        catch (Exception e)
        {
            Log.Error("create " + SEED_ID + " failed", e);
        }
        return item;
    }

    public static GameItem CreateChili(int size)
    {
        GameItem item = ItemDirectory.CreateEmptyItem(CHILI_ID);
        try
        {
            item.ModifyTag(SIZE_TAG, delegate(TagState state)
            {
                state.Enable().SetInt(size);
            }, false);
            ApplySize(item);
            item.shortDescription = Text.Get("lantern_chili_desc");
            item.flavorText = Text.Get("lantern_chili_flavor");
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            AudioHelper.InitProduce(item);
            // Blending gives capsaicin only (from the gene below): no water, no protein.
            ChemicalHelper.InitChemItem(item, true, false);
            // Bought or spawned chilis get the innate genes; harvested ones get the seed's.
            PlantGeneticsHelper.ApplyFactoryGenetics(item, SEED_ID);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            Log.Error("create " + CHILI_ID + " failed", e);
        }
        return item;
    }

    public static int SizeOf(GameItem item)
    {
        if (item == null || item.identifier != CHILI_ID || !item.IsTag(SIZE_TAG))
        {
            return NORMAL;
        }
        int size = item.GetTagReadonly(SIZE_TAG).GetInt();
        return size > 0 ? size : NORMAL;
    }

    public static void ApplySize(GameItem item)
    {
        int size = SizeOf(item);
        if (size <= SMALL)
        {
            item.name = Text.Get("lantern_chili_name_s");
            item.unitValue = CHILI_VALUE_S;
            BeerSprites.Apply(item, CHILI_SPRITE_S);
        }
        else if (size >= LARGE)
        {
            item.name = Text.Get("lantern_chili_name_l");
            item.unitValue = CHILI_VALUE_L;
            BeerSprites.Apply(item, CHILI_SPRITE_L);
        }
        else
        {
            item.name = Text.Get("lantern_chili_name");
            item.unitValue = CHILI_VALUE;
            BeerSprites.Apply(item, CHILI_SPRITE);
        }
    }

    public static GameItem CreateVial()
    {
        GameItem item = ItemDirectory.CreateEmptyItem(ITEM_ID);
        try
        {
            BeerSprites.Apply(item, VIAL_SPRITE);
            item.name = Text.Get("incapacitant_vial_name");
            item.shortDescription = Text.Get("incapacitant_vial_desc");
            item.flavorText = Text.Get("incapacitant_vial_flavor");
            item.unitValue = VIAL_VALUE;
            item.SetGameItemType("MEDICAL");
            item.SetGameItemType("WEAPON");
            ChemicalHelper.InitChemItem(item, true, true);
            // Default contents for vials not made in the Chem Finisher (debug spawns): street grade.
            ChemFinisherRecipe.SetCompositionByPurity(item, 60, 80, REQUIRED_VOLUME, ESTER_ID);
            ChemicalProductHelper.InitChemicalProduct(item, ESTER_ID);
            ChemicalFeedbackHelper.InitChemicalIntentItem(item, ChemicalFeedbackHelper.ChemicalIntent.MEDICAL);
            AudioHelper.InitGlassVial(item);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            Log.Error("create " + ITEM_ID + " failed", e);
        }
        return item;
    }

    public static bool IsVial(GameItem item)
    {
        return item != null && item.identifier == ITEM_ID;
    }

    // Saved small and large chilis are rebuilt as normal ones; restore their size.
    public static void FixLoadedItems()
    {
        try
        {
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (item != null && item.identifier == CHILI_ID)
                {
                    ApplySize(item);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("fixing loaded chilis failed", e);
        }
    }

    // ---- ester reaction (from TemperatureReactionPatch) ---------------------------------------------------------

    public static int Run(GameItem container)
    {
        if (container == null || WaterHelper.IsEmpty(container))
        {
            return 0;
        }
        int capsaicin = IaLiquids.Get(container, CAPSAICIN_ID);
        int alcohol = IaLiquids.Get(container, "alcohol");
        if (capsaicin <= 0 || alcohol <= 0)
        {
            return 0;
        }
        if (container.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble() < REACT_MIN_TEMP)
        {
            return 0;
        }
        double ratio = (double)alcohol / capsaicin;
        int amount = Math.Min(capsaicin, Math.Max(REACT_MIN_UL, (int)Math.Round(capsaicin * REACT_RATE)));
        int alcoholUsed = (int)Math.Ceiling(amount * ALCOHOL_PER_CAPSAICIN);
        if (alcoholUsed > alcohol)
        {
            amount = (int)Math.Floor(alcohol / ALCOHOL_PER_CAPSAICIN);
            alcoholUsed = alcohol;
        }
        if (amount <= 0)
        {
            return 0;
        }
        double spoiled = ratio >= FULL_ALCOHOL_RATIO ? 0.0 : (FULL_ALCOHOL_RATIO - ratio) / FULL_ALCOHOL_RATIO;
        int contaminant = (int)Math.Round(amount * spoiled);
        IaLiquids.Set(container, CAPSAICIN_ID, capsaicin - amount);
        IaLiquids.Set(container, "alcohol", alcohol - alcoholUsed);
        IaLiquids.Set(container, ESTER_ID, IaLiquids.Get(container, ESTER_ID) + amount - contaminant);
        if (contaminant > 0)
        {
            IaLiquids.Set(container, "physical_contaminant", IaLiquids.Get(container, "physical_contaminant") + contaminant);
        }
        return 1;
    }

    // ---- shops and debug ----------------------------------------------------------------------------------------

    public static void OfferSeeds()
    {
        for (int i = 0; i < 2; i++)
        {
            PlayerStore.Instance.AddDirectSellingItemToTable(CreateSeed(), false, false, false, 0);
        }
    }

    public static void SpawnKit()
    {
        try
        {
            GameItem[] items = { CreateSeed(), CreateSeed(), CreateChili(SMALL), CreateChili(NORMAL), CreateChili(LARGE) };
            foreach (GameItem item in items)
            {
                GraphUtils.TryAcceptAll(EmporiumEntry.Instance.invElement, item);
            }
        }
        catch (Exception e)
        {
            Log.Error("Lantern debug kit failed", e);
        }
    }

    // 2.5 mL capsaicin in 10 mL alcohol: enough for one vial once reacted and dried.
    public static void SpawnMix()
    {
        try
        {
            GameItem bottle = DirectoryMaster.Item("med_bottle_red", true);
            WaterHelper.AddLiquid(bottle, CAPSAICIN_ID, 2500);
            WaterHelper.AddLiquid(bottle, "alcohol", 10000);
            AudioHelper.InitPlasticContainer(bottle);
            ModHelper.SetItemOwnerShip(bottle, true);
            ModHelper.MoveItemToInventory(bottle);
        }
        catch (Exception e)
        {
            Log.Error("Lantern mix kit failed", e);
        }
    }

    public static void SpawnPharma()
    {
        try
        {
            for (int i = 0; i < 2; i++)
            {
                GameItem item = CreateVial();
                ChemFinisherRecipe.SetCompositionByPurity(item, 92, 97, REQUIRED_VOLUME, ESTER_ID);
                ChemicalProductHelper.GradeProduct(item);
                GeneralHelper.SetItemOwned(item, true);
                GraphUtils.TryAcceptAll(EmporiumEntry.Instance.invElement, item);
            }
        }
        catch (Exception e)
        {
            Log.Error("Lantern pharma kit failed", e);
        }
    }
}

// The hydroponic machine loads "<harvest id>_p"/"_p2" from the vanilla plant atlas; serve ours from the mod.
[HarmonyPatch(typeof(RenderHandler), "LoadFromAtlas")]
public static class LanternMachineSpritePatch
{
    private static bool Prefix(string name, ref Sprite __result)
    {
        if (name == null || !name.StartsWith(Lantern.GROWING_PREFIX))
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

// The retired farmer also brings two Hollow's Lantern seeds, like the Xinke seeds.
[HarmonyPatch(typeof(StoreClientListSpec), "CreateRetiredFarmer")]
public static class LanternRetiredFarmerPatch
{
    private static void Postfix(StoreClient __result)
    {
        XinkeRetiredFarmerPatch.AppendToStock(__result, Lantern.OfferSeeds, "retired farmer (Lantern)");
    }
}

// ---- who buys the incapacitant ------------------------------------------------------------------------------

public static class LanternBuyers
{
    // Mercenaries have no faction of their own: these three buy ordinary goods (the gun order and gun selling
    // mercenaries do not). They buy the vial through its WEAPON / MEDICAL type, as plain goods.
    private static readonly HashSet<string> Mercenaries = new HashSet<string> { "mercenaryBuyer", "minorMerc", "criminalBuyerMerc" };

    public static bool IsMercenary(StoreClient client)
    {
        return client != null && client.identifier != null && Mercenaries.Contains(client.identifier);
    }

    // Upper Level (private security contractors included) and Security: buy it and get evidence wiped.
    public static bool IsAuthority(StoreClient client)
    {
        return client != null && (client.IsFaction("FACTION_UPPER_LEVEL") || client.IsFaction("FACTION_SECURITY"));
    }

    public static bool Refuses(StoreClient client, GameItem item)
    {
        return client != null && Lantern.IsVial(item) && !IsAuthority(client) && !IsMercenary(client);
    }

    // Revolutionaries have their own line; everyone else who refuses says the Lower Level one.
    public static string RefusalKey(StoreClient client)
    {
        return client.IsFaction("FACTION_REVOLUTION") ? "refuse_lantern_revolution" : "refuse_lantern_lower";
    }
}

[HarmonyPatch(typeof(StoreClient), "IsClientRefusingItem")]
public static class LanternRefusePatch
{
    private static void Postfix(StoreClient __instance, GameItem gameItem, ref bool __result)
    {
        if (!__result && LanternBuyers.Refuses(__instance, gameItem))
        {
            __result = true;
        }
    }
}

// Every Upper Level and Security buyer wants it, whatever their usual shopping list.
[HarmonyPatch(typeof(StoreClient), "IsClientBuyingThisItem", new Type[] { typeof(GameItem), typeof(bool) })]
public static class LanternBuyingPatch
{
    private static void Postfix(StoreClient __instance, GameItem gameItem, bool ignoreContents, ref bool __result)
    {
        if (!__result && Lantern.IsVial(gameItem) && LanternBuyers.IsAuthority(__instance)
            && !__instance.IsClientRefusingItem(gameItem, ignoreContents))
        {
            __result = true;
        }
    }
}

// ---- evidence wipe on sale ------------------------------------------------------------------------------------

public static class LanternEvidence
{
    public const int PER_PRICE_PURITY = 5;
    public const int OVERFLOW_PER_PROGRESS = 100;

    // The accept line we swapped in for the current sale, until it starts playing (then the client is restored).
    private static StoreClient swappedClient;
    private static Dialogue swappedOriginal;
    private static Dialogue swappedLine;
    private static bool swappedWasPlayed;

    // Reduction = sold price x purity (0-1) x 5, taken from the largest crime first. Whatever is left once all
    // crimes are at 0 takes 1 investigation progress per full 100 (the rest is dropped).
    public static void OnSold(GameItem item, StoreClient client)
    {
        if (!Lantern.IsVial(item) || !LanternBuyers.IsAuthority(client))
        {
            return;
        }
        SecData sec = PlayerStore.Instance.secData;
        if (sec == null || sec.crimeAmount == null)
        {
            return;
        }
        double purity = ChemicalProductHelper.GetPurity(item) / 100.0;
        int reduction = (int)Math.Round(item.GetNegociatedValue() * purity * PER_PRICE_PURITY);
        int crimeBefore = sec.GetTotalCrimeValue();
        int progressBefore = sec.evidenceLevel;

        int left = reduction;
        while (left > 0)
        {
            int largest = -1;
            for (int i = 0; i < sec.crimeAmount.Count; i++)
            {
                if (sec.crimeAmount[i] > 0 && (largest < 0 || sec.crimeAmount[i] > sec.crimeAmount[largest]))
                {
                    largest = i;
                }
            }
            if (largest < 0)
            {
                break;
            }
            int taken = Math.Min(left, sec.crimeAmount[largest]);
            sec.crimeAmount[largest] -= taken;
            left -= taken;
        }
        int progressCut = 0;
        if (left > 0 && sec.evidenceLevel > 0)
        {
            progressCut = Math.Min(sec.evidenceLevel, left / OVERFLOW_PER_PROGRESS);
            sec.evidenceLevel -= progressCut;
        }

        string outcome;
        if (crimeBefore <= 0 && progressBefore <= 0)
        {
            outcome = "clean";
        }
        else if (sec.GetTotalCrimeValue() > 0)
        {
            outcome = "partial";
        }
        else if (progressCut > 0)
        {
            outcome = "slowed";
        }
        else
        {
            outcome = "cleared";
        }
        string faction = client.IsFaction("FACTION_SECURITY") ? "security" : "upper";
        bool line = SwapSaleLine(client, Text.Get("lantern_sale_" + faction + "_" + outcome));
        Log.Info("incapacitant sold to " + client.identifier + " for " + item.GetNegociatedValue() + " at "
            + (purity * 100).ToString("0.#") + "% purity: reduction " + reduction + ", evidence -" + (reduction - left)
            + " (" + crimeBefore + " -> " + sec.GetTotalCrimeValue() + "), investigation -" + progressCut + " ("
            + progressBefore + " -> " + sec.evidenceLevel + "), line " + (line ? outcome : "skipped (story client)"));
    }

    // Vanilla says a client's accept line ("deal") once per visit and, as it starts, grafts the follow-up ("anything
    // else?" or the goodbye) behind it; later sales go straight to the follow-up. Our line stands in for that accept
    // line on every incapacitant sale: swapped in while the item is sold (counter: inside SellItem; barter: inside
    // OnItemsSold), i.e. before vanilla plays it, carrying the same graft, and swapped back as soon as it starts.
    // Only plain accept lines are replaced; story clients whose accept line leads on or triggers something keep
    // theirs. (1.5.0 started its own dialogue after vanilla's, which cut the chain, so the line never showed.)
    private static bool SwapSaleLine(StoreClient client, string text)
    {
        RestoreStaleSwap();
        Dialogue original = client.acceptDealDialogue;
        if (original == null || client.acceptDealDialogueLastLine != null || original.endAction != null)
        {
            return false;
        }
        // An accept line already played carries its last graft; anything else behind it belongs to a story.
        Dialogue graft = Traverse.Create(client).Field("lastPostDealGraft").GetValue<Dialogue>();
        if (original.nextDialogue != null && original.nextDialogue != graft)
        {
            return false;
        }
        Dialogue line = new Dialogue().SetText("CLIENT", text);
        line.priority = original.priority;
        line.startAction = delegate
        {
            if (swappedLine == line)
            {
                swappedClient = null;
                swappedOriginal = null;
                swappedLine = null;
            }
            client.acceptDealDialogue = original;
            client.acceptDealDialoguePlayed = true;
            client.OnDealAccepted(line);
        };
        swappedClient = client;
        swappedOriginal = original;
        swappedLine = line;
        swappedWasPlayed = client.acceptDealDialoguePlayed;
        client.acceptDealDialogue = line;
        client.acceptDealDialoguePlayed = false;
        return true;
    }

    // A swapped line that never started (a more important dialogue was up) is undone before the next sale.
    public static void RestoreStaleSwap()
    {
        if (swappedLine == null)
        {
            return;
        }
        if (swappedClient != null && swappedClient.acceptDealDialogue == swappedLine)
        {
            swappedClient.acceptDealDialogue = swappedOriginal;
            swappedClient.acceptDealDialoguePlayed = swappedWasPlayed;
        }
        swappedClient = null;
        swappedOriginal = null;
        swappedLine = null;
    }
}

// Every way of selling (counter, barter, orders) goes through CheckSoldContraband with the buyer.
[HarmonyPatch(typeof(SecData), "CheckSoldContraband")]
public static class LanternSoldPatch
{
    private static void Postfix(GameItem gameItem, StoreClient client)
    {
        try
        {
            LanternEvidence.OnSold(gameItem, client);
        }
        catch (Exception e)
        {
            Log.Error("incapacitant evidence wipe failed", e);
        }
    }
}

// Before each counter sale or barter: undo a swapped sale line that never got to play.
[HarmonyPatch(typeof(PlayerStore), "SellItemFromTable")]
public static class LanternSaleLinePatch
{
    private static void Prefix()
    {
        try
        {
            LanternEvidence.RestoreStaleSwap();
        }
        catch (Exception e)
        {
            Log.Error("incapacitant sale line restore failed", e);
        }
    }
}

[HarmonyPatch(typeof(PlayerStore), "AcceptBarter")]
public static class LanternBarterLinePatch
{
    private static void Prefix()
    {
        try
        {
            LanternEvidence.RestoreStaleSwap();
        }
        catch (Exception e)
        {
            Log.Error("incapacitant barter line restore failed", e);
        }
    }
}
