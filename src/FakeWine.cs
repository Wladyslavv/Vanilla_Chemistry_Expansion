// Flavour chemistry and blended wine. Plant matter is dry-distilled into acetic acid, acetic acid and
// alcohol are esterified into ethyl acetate, and the Chem Finisher bottles alcohol + water + ethyl acetate
// as a wine whose label can claim more than it is. Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

public static class FlavorChemistry
{
    public const string ACETIC_ACID = "ia_acetic_acid";
    public const string ETHYL_ACETATE = "ia_ethyl_acetate";

    // Dry distillation: dry plant matter at 150 C or more -> acetic acid + methanol + char (debris).
    public const double DRY_DISTILL_TEMP = 150.0;
    public const double ACETIC_PER_PLANT = 0.3;
    public const double METHANOL_PER_PLANT = 0.1;
    public const double DRY_DISTILL_RATE = 0.05;
    public const int DRY_DISTILL_MIN_UL = 500;

    // Fischer esterification: acetic acid + alcohol (1:1) -> ethyl acetate, at least 1% acid as catalyst,
    // 60-75 C (ethyl acetate boils at 77 C).
    public const double ESTER_ACID_SHARE = 0.01;
    public const double ESTER_MIN_TEMP = 60.0;
    public const double ESTER_MAX_TEMP = 75.0;
    public const double ESTER_RATE = 0.05;
    public const int ESTER_MIN_UL = 100;

    public static void RegisterLiquids()
    {
        IaLiquids.AddLiquid(ACETIC_ACID, "liquid_acetic_acid", 5, "f2efe0", 1);
        Liquid ester = IaLiquids.AddLiquid(ETHYL_ACETATE, "liquid_ethyl_acetate", 5, "f5fbff", 1);
        if (ester != null)
        {
            ester.boilingPoint = 77.1;
            ester.isEvaporative = true;
        }
    }

    public static int Run(GameItem container, double temp)
    {
        return DryDistill(container, temp) | Esterify(container, temp);
    }

    private static int DryDistill(GameItem container, double temp)
    {
        int plant = IaLiquids.Get(container, "plant_matter");
        if (plant <= 0 || temp < DRY_DISTILL_TEMP || IsWetIgnoringMethanol(container))
        {
            return 0;
        }
        int amount = Math.Min(plant, Math.Max(DRY_DISTILL_MIN_UL, (int)(plant * DRY_DISTILL_RATE)));
        int acetic = (int)(amount * ACETIC_PER_PLANT);
        // Methanol boils at 65 C, so at 150 C+ it leaves as vapour straight away instead of collecting.
        int methanol = (int)(amount * METHANOL_PER_PLANT);
        IaLiquids.Set(container, "plant_matter", plant - amount);
        IaLiquids.Set(container, ACETIC_ACID, IaLiquids.Get(container, ACETIC_ACID) + acetic);
        IaLiquids.Set(container, "debris", IaLiquids.Get(container, "debris") + amount - acetic - methanol);
        return 1;
    }

    // Like WaterHelper.IsWet (more than 0.1 mL of evaporative liquids), but leftover methanol, which boils off
    // by itself at these temperatures, does not stop the distillation.
    private static bool IsWetIgnoringMethanol(GameItem container)
    {
        int wet = 0;
        foreach (Liquid liquid in Liquid.Liquids)
        {
            if (liquid.isEvaporative && liquid.identifier != IaLiquids.METHANOL)
            {
                wet += container.GetTagReadonly(liquid.currentPart).GetInt();
            }
        }
        return wet > 100;
    }

    private static int Esterify(GameItem container, double temp)
    {
        if (temp < ESTER_MIN_TEMP || temp > ESTER_MAX_TEMP)
        {
            return 0;
        }
        int acetic = IaLiquids.Get(container, ACETIC_ACID);
        int alcohol = IaLiquids.Get(container, "alcohol");
        if (acetic <= 0 || alcohol <= 0)
        {
            return 0;
        }
        if (IaLiquids.Get(container, "acid") < WaterHelper.GetTotalVolume(container) * ESTER_ACID_SHARE)
        {
            return 0;
        }
        int limiting = Math.Min(acetic, alcohol);
        int amount = Math.Min(limiting, Math.Max(ESTER_MIN_UL, (int)(limiting * ESTER_RATE)));
        IaLiquids.Set(container, ACETIC_ACID, acetic - amount);
        IaLiquids.Set(container, "alcohol", alcohol - amount);
        IaLiquids.Set(container, ETHYL_ACETATE, IaLiquids.Get(container, ETHYL_ACETATE) + amount);
        return 1;
    }
}

// Blended wine carries two labels, each editable with the labeler: its wine quality (the six vanilla tiers)
// and whether it is blended. The real conditions are the actual tier and "blended"; the labels start out
// the same and can be changed to claim more.
public static class BlendedWineLabels
{
    public const string QUALITY = "CATEGORY_BLENDED_WINE";
    public const string BLEND = "CATEGORY_WINE_BLEND";
    // An honest "blended" label sells for 15 percentage points less.
    public const int BLENDED_PENALTY = 15;

    // Vanilla tiers 0-5: undrinkable, bucket brew, homebrew, production, reserve, master vintage.
    private static readonly string[] TIER_KEYS = {
        "undrinkable", "bucket_brew", "homebrew", "production", "reserve", "master_vintage" };
    private static readonly int[] TIER_MODS = { -100, -15, 0, 25, 50, 75 };

    public static readonly List<ItemCondition> QualityConditions = new List<ItemCondition>();
    public static readonly List<ItemCondition> BlendConditions = new List<ItemCondition>();

    public static void Register()
    {
        if (QualityConditions.Count == 0)
        {
            // Best first, like the vanilla dropdowns.
            for (int tier = 5; tier >= 0; tier--)
            {
                ItemCondition condition = new ItemCondition();
                condition.category = QUALITY;
                condition.identifier = "ia_wine_" + TIER_KEYS[tier];
                condition.modValue = TIER_MODS[tier];
                condition.compareValue = tier;
                QualityConditions.Add(condition);
            }
            BlendConditions.Add(CreateBlend("ia_blend_genuine", 0, 1));
            BlendConditions.Add(CreateBlend("ia_blend_blended", -BLENDED_PENALTY, 0));
        }
        ItemConditionList.conditionLookup[QUALITY] = QualityConditions;
        ItemConditionList.conditionLookup[BLEND] = BlendConditions;
        RefreshDisplays();
    }

    private static ItemCondition CreateBlend(string id, int modValue, int compareValue)
    {
        ItemCondition condition = new ItemCondition();
        condition.category = BLEND;
        condition.identifier = id;
        condition.modValue = modValue;
        condition.compareValue = compareValue;
        return condition;
    }

    public static void Unregister()
    {
        ItemConditionList.conditionLookup.Remove(QUALITY);
        ItemConditionList.conditionLookup.Remove(BLEND);
        ItemFeatureList.CategoryNames.Remove(QUALITY);
        ItemFeatureList.CategoryNames.Remove(BLEND);
    }

    public static void RefreshDisplays()
    {
        foreach (ItemCondition condition in QualityConditions)
        {
            condition.display = LocHelper.GetLocalizedMechanic("mech_condition_wine_" + TIER_KEYS[TierOf(condition)]);
        }
        BlendConditions[0].display = IaText.Get("label_genuine");
        BlendConditions[1].display = IaText.Get("label_blended");
        string categoryName;
        ItemFeatureList.CategoryNames[QUALITY] = ItemFeatureList.CategoryNames.TryGetValue("CATEGORY_WINE_QUALITY", out categoryName)
            ? categoryName : IaText.Get("category_blended_wine");
        ItemFeatureList.CategoryNames[BLEND] = IaText.Get("category_wine_blend");
    }

    public static ItemCondition Quality(int tier)
    {
        string id = "ia_wine_" + TIER_KEYS[tier];
        return QualityConditions.Find(c => c.identifier == id);
    }

    public static ItemCondition Blend(bool blended)
    {
        return blended ? BlendConditions[1] : BlendConditions[0];
    }

    public static bool IsBlended(ItemCondition condition)
    {
        return condition != null && condition.identifier == "ia_blend_blended";
    }

    public static int TierOf(ItemCondition condition)
    {
        if (condition == null)
        {
            return 0;
        }
        for (int tier = 0; tier < TIER_KEYS.Length; tier++)
        {
            if (condition.identifier.EndsWith("_" + TIER_KEYS[tier]))
            {
                return tier;
            }
        }
        return 0;
    }

    public static ItemFeature CreateFeature(string category, ItemCondition real)
    {
        ItemFeature feature = new ItemFeature(category);
        feature.identifier = category;
        feature.useCondition = true;
        feature.SetConditions(real, real);
        return feature;
    }
}

public static class BlendedWine
{
    public const string ITEM_ID = "ia.blended_wine";
    public const int BOTTLE_UL = 750000;
    public const string ACTUAL_SCORE_TAG = "IA_BLEND_SCORE_INT";
    public const string BLEND_PREFIX = "IA_BLEND_";

    // Quality score, on the vanilla wine scale: base 1; alcohol 20-25% +3, 5-20% or above 25% +2, below 5% -3
    // (so anything under 5% alcohol tops out at 0, undrinkable); ethyl acetate and impurities +-1, no ester -2.
    public const int BASE_SCORE = 1;
    public const double ALCOHOL_MIN_WINE = 0.05;
    public const double ALCOHOL_MIN_SHARE = 0.20;
    public const double ALCOHOL_MAX_SHARE = 0.25;
    // Ethyl acetate per mille of the bottle: 1-2 ideal, 0.5-4 acceptable.
    public const double ESTER_IDEAL_MIN = 1.0;
    public const double ESTER_IDEAL_MAX = 2.0;
    public const double ESTER_OK_MIN = 0.5;
    public const double ESTER_OK_MAX = 4.0;
    // Anything but water, alcohol and ethyl acetate; thresholds match vanilla water quality (99% / 98%).
    public const double IMPURITY_GOOD_SHARE = 0.01;
    public const double IMPURITY_BAD_SHARE = 0.02;
    public const double METHANOL_DANGER_SHARE = 0.001;

    public static bool IsBlendedWine(GameItem item)
    {
        return item != null && item.identifier == ITEM_ID;
    }

    public static GameItem Create()
    {
        GameItem item = DirectoryMaster.Item("wine_bottle", true);
        try
        {
            item.identifier = ITEM_ID;
            // Default contents for bottles not made in the Chem Finisher (debug spawns): a perfect blend.
            Dictionary<string, int> blend = new Dictionary<string, int>();
            blend["alcohol"] = 165000;
            blend[FlavorChemistry.ETHYL_ACETATE] = 1100;
            blend["water"] = BOTTLE_UL - 165000 - 1100;
            Fill(item, blend);
        }
        catch (Exception e)
        {
            IaLog.Error("create " + ITEM_ID + " failed", e);
        }
        return item;
    }

    public static void RegisterRecipe()
    {
        if (FinisherRecipes.Find(ITEM_ID) != null)
        {
            return;
        }
        ChemFinisherRecipe recipe = new ChemFinisherRecipe();
        recipe.identifier = ITEM_ID;
        recipe.displayName = IaText.Get("recipe_blended_wine");
        recipe.requiredVolume = BOTTLE_UL;
        recipe.isRequireDry = false;
        recipe.activeIngredientId = "alcohol";
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
            recipe.displayName = IaText.Get("recipe_blended_wine");
        }
    }

    // The Chem Finisher writes the pressed liquid into the bottle's liquid tags; move it into this mod's
    // own tags so the finished bottle does not behave like an open container.
    public static void FillFromFinisher(GameItem item)
    {
        Dictionary<string, int> blend = new Dictionary<string, int>();
        foreach (Liquid liquid in Liquid.Liquids)
        {
            int amount = item.GetTagReadonly(liquid.currentPart).GetInt();
            if (amount > 0)
            {
                blend[liquid.identifier] = amount;
            }
            IaLiquids.Set(item, liquid.identifier, 0);
        }
        Fill(item, blend);
    }

    public static void Fill(GameItem item, Dictionary<string, int> blend)
    {
        foreach (Liquid liquid in Liquid.Liquids)
        {
            int amount;
            blend.TryGetValue(liquid.identifier, out amount);
            int value = amount;
            item.ModifyTag(BLEND_PREFIX + liquid.identifier, delegate(TagState state)
            {
                state.Enable().SetInt(value);
            }, true);
        }
        int score = Score(blend);
        item.ModifyTag(ACTUAL_SCORE_TAG, delegate(TagState state)
        {
            state.Enable().SetInt(score);
        }, true);
        item.RemoveFeatureByCategory(BlendedWineLabels.QUALITY);
        item.RemoveFeatureByCategory(BlendedWineLabels.BLEND);
        item.AddItemFeature(BlendedWineLabels.CreateFeature(BlendedWineLabels.QUALITY, BlendedWineLabels.Quality(TierFromScore(score))));
        item.AddItemFeature(BlendedWineLabels.CreateFeature(BlendedWineLabels.BLEND, BlendedWineLabels.Blend(true)));

        int total = Total(blend);
        int methanol;
        blend.TryGetValue(IaLiquids.METHANOL, out methanol);
        if (total > 0 && methanol > total * METHANOL_DANGER_SHARE)
        {
            item.EnableTag("ENDANGERMENT_ITEM_TAG", true);
        }
        else
        {
            item.DisableTag("ENDANGERMENT_ITEM_TAG", true);
        }
        ApplyLabel(item);
    }

    public static int Score(Dictionary<string, int> blend)
    {
        int total = Total(blend);
        if (total <= 0)
        {
            return 0;
        }
        int water, alcohol, ester;
        blend.TryGetValue("water", out water);
        blend.TryGetValue("alcohol", out alcohol);
        blend.TryGetValue(FlavorChemistry.ETHYL_ACETATE, out ester);
        double alcoholShare = (double)alcohol / total;
        double esterPerMille = 1000.0 * ester / total;
        double impurityShare = (double)(total - water - alcohol - ester) / total;

        int score = BASE_SCORE;
        if (alcoholShare < ALCOHOL_MIN_WINE)
        {
            score -= 3;
        }
        else if (alcoholShare < ALCOHOL_MIN_SHARE || alcoholShare > ALCOHOL_MAX_SHARE)
        {
            score += 2;
        }
        else
        {
            score += 3;
        }
        if (ester <= 0)
        {
            score -= 2;
        }
        else if (esterPerMille < ESTER_OK_MIN || esterPerMille > ESTER_OK_MAX)
        {
            score -= 1;
        }
        else if (esterPerMille >= ESTER_IDEAL_MIN && esterPerMille <= ESTER_IDEAL_MAX)
        {
            score += 1;
        }
        if (impurityShare > IMPURITY_BAD_SHARE)
        {
            score -= 1;
        }
        else if (impurityShare <= IMPURITY_GOOD_SHARE)
        {
            score += 1;
        }
        return score;
    }

    private static int Total(Dictionary<string, int> blend)
    {
        int total = 0;
        foreach (int amount in blend.Values)
        {
            total += amount;
        }
        return total;
    }

    // Same mapping as WineHelper.GetWineQualityTier.
    public static int TierFromScore(int score)
    {
        if (score >= 6)
        {
            return 5;
        }
        if (score == 5)
        {
            return 4;
        }
        if (score >= 3)
        {
            return 3;
        }
        return Math.Max(0, score);
    }

    // Lowest vanilla score that reaches each tier, so the bottle's sprite and label follow the label tier.
    private static readonly int[] TIER_SCORES = { 0, 1, 2, 3, 5, 6 };

    // Makes the bottle look like a finished vanilla wine of its label tier.
    public static void ApplyLabel(GameItem item)
    {
        if (item == null)
        {
            return;
        }
        ItemFeature quality = item.FindItemFeatureByCategory(BlendedWineLabels.QUALITY);
        ItemFeature blend = item.FindItemFeatureByCategory(BlendedWineLabels.BLEND);
        if (quality == null || blend == null)
        {
            return;
        }
        int labelTier = BlendedWineLabels.TierOf(quality.fakeCondition);
        bool labelledBlended = BlendedWineLabels.IsBlended(blend.fakeCondition);
        int labelScore = TIER_SCORES[labelTier];
        item.ModifyTag("WINE_CONTAINER_STATE", delegate(TagState state)
        {
            state.Enable().SetInt(2);
        }, true);
        item.ModifyTag("WINE_SCORE_INT", delegate(TagState state)
        {
            state.Enable().SetInt(labelScore);
        }, true);
        // Sealed like a vanilla bottle that started fermenting (WineHelper's onLoaded does the same):
        // no longer a liquid container, no ingredient window.
        item.DisableTag("LIQUID_CONTAINER_TAG", true);
        // The empty wine bottle is a household good; vanilla drops that type when the bottle is sealed.
        item.RemoveGameItemType("HOUSEHOLD_GOOD");
        ContainerHelper.RemoveContainerTooltipTag(item);
        item.CloseContentWindow(true);
        item.SetContentWindow(null);
        // The empty bottle only accepts targeting by liquids it can be filled with, and vanilla requires
        // both sides to agree, which would block the labeler.
        item.mayItemTargetThisFunc = null;
        item.canItemTargetThisFunc = null;
        item.unitValue = 100L;
        string wineName = LocHelper.GetLocalizedItem("item_wine_bloomberry_name");
        item.name = labelledBlended ? IaText.Get("blended_wine_name", wineName) : wineName;
        item.shortDescription = labelledBlended
            ? IaText.Get("blended_wine_desc") : LocHelper.GetLocalizedItem("item_wine_bloomberry_desc");
        item.RemoveGameItemType("LUXURY_ITEM");
        if (labelTier > 0)
        {
            WineHelper.InitDrinkableWine(item, 4 + labelTier);
            item.SetGameItemType("SUBSTANCE");
            item.SetGameItemType("ALCOHOL");
            if (labelTier >= 4)
            {
                item.SetGameItemType("LUXURY_ITEM");
            }
        }
        item.SetSpriteAndShape("Items/items_winemaking", "bottle" + labelTier);
        item.Validate();
    }

    public static int BlendAmount(GameItem item, string liquidId)
    {
        string tag = BLEND_PREFIX + liquidId;
        return item.IsTag(tag) ? item.GetTagReadonly(tag).GetInt() : 0;
    }

    public static void PourInto(GameItem bottle, GameItem container)
    {
        foreach (Liquid liquid in Liquid.Liquids)
        {
            int amount = BlendAmount(bottle, liquid.identifier);
            if (amount > 0)
            {
                WaterHelper.AddLiquid(container, liquid.identifier, amount);
            }
        }
    }

    // Once poured out or drunk, the bottle is a plain empty vanilla wine bottle again.
    public static void ResetToVanilla(GameItem item)
    {
        item.RemoveFeatureByCategory(BlendedWineLabels.QUALITY);
        item.RemoveFeatureByCategory(BlendedWineLabels.BLEND);
        foreach (Liquid liquid in Liquid.Liquids)
        {
            item.DisableTag(BLEND_PREFIX + liquid.identifier, true);
        }
        item.DisableTag(ACTUAL_SCORE_TAG, true);
        item.identifier = "wine_bottle";
    }

    // Whether this client sees through the bottle's labels; judged for the bottle as a whole from both labels.
    // When it does, each label that lies (and claims more than the truth) is exposed.
    public static bool SeesThrough(GameItem item, StoreClient client)
    {
        ItemFeature quality = item.FindItemFeatureByCategory(BlendedWineLabels.QUALITY);
        ItemFeature blend = item.FindItemFeatureByCategory(BlendedWineLabels.BLEND);
        if (quality == null || blend == null)
        {
            return false;
        }
        // Trade board orders inspect everything, as in vanilla.
        Func<ItemFeature, StoreClient, bool> vanillaCheck = client.canClientExposeFeature;
        if (vanillaCheck != null && vanillaCheck.Method.Name == "TradeBoardExposeCapacity")
        {
            return true;
        }
        bool hidesBlend = !blend.IsExposed() && !BlendedWineLabels.IsBlended(blend.fakeCondition);
        int actualTier = BlendedWineLabels.TierOf(quality.realCondition);
        int diff = quality.IsExposed() ? 0 : BlendedWineLabels.TierOf(quality.fakeCondition) - actualTier;
        bool connoisseur = client.IsFaction("FACTION_UPPER_LEVEL") || client.IsFaction("FACTION_TOURIST");
        if (diff >= 2)
        {
            return true;
        }
        if (diff == 1)
        {
            return connoisseur || client.CanExposeChem;
        }
        if (!hidesBlend)
        {
            return false;
        }
        // Sold as genuine at or below its actual quality.
        if (diff < 0 && actualTier >= 4)
        {
            return false;
        }
        return client.CanExposeChem;
    }

    public static bool IsLying(ItemFeature feature)
    {
        return feature != null && !feature.IsExposed() && !feature.IsFeatureMatch() && feature.IsRealFeatureCheaper();
    }

    // Upgrades saved bottles: blended wine sealed before 1.3.0 kept the empty bottle's household good type,
    // and bottles made by the first test build had a single combined label.
    public static void UpgradeLoadedItems()
    {
        try
        {
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (IsBlendedWine(item))
                {
                    item.RemoveGameItemType("HOUSEHOLD_GOOD");
                }
                if (IsBlendedWine(item) &&item.FindItemFeatureByCategory(BlendedWineLabels.BLEND) == null)
                {
                    Dictionary<string, int> blend = new Dictionary<string, int>();
                    foreach (Liquid liquid in Liquid.Liquids)
                    {
                        int amount = BlendAmount(item, liquid.identifier);
                        if (amount > 0)
                        {
                            blend[liquid.identifier] = amount;
                        }
                    }
                    Fill(item, blend);
                }
            }
        }
        catch (Exception e)
        {
            IaLog.Error("upgrading saved blended wine failed", e);
        }
    }
}

[HarmonyPatch(typeof(ChemicalProductHelper), "GradeProduct")]
public static class BlendedWineGradePatch
{
    private static void Postfix(GameItem item)
    {
        try
        {
            if (BlendedWine.IsBlendedWine(item))
            {
                BlendedWine.FillFromFinisher(item);
            }
        }
        catch (Exception e)
        {
            IaLog.Error("grading blended wine failed", e);
        }
    }
}

[HarmonyPatch(typeof(WineHelper), "OnAgeWine")]
public static class BlendedWineAgingPatch
{
    // Blended wine does not improve in the wine rack.
    private static bool Prefix(GameItem item)
    {
        return !BlendedWine.IsBlendedWine(item);
    }
}

// The vanilla wine tooltip says the bottle gains value over time; blended wine does not, so drop that line
// (WineHelper.CreateTooltip is the only caller of AddAgingInfoTooltipLine).
[HarmonyPatch(typeof(WineHelper), "CreateTooltip")]
public static class BlendedWineTooltipPatch
{
    public static bool SkipAgingLine;

    private static void Prefix(GameItem item)
    {
        SkipAgingLine = BlendedWine.IsBlendedWine(item);
    }

    private static void Finalizer()
    {
        SkipAgingLine = false;
    }
}

[HarmonyPatch(typeof(AgableHelper), "AddAgingInfoTooltipLine")]
public static class BlendedWineAgingLinePatch
{
    private static bool Prefix()
    {
        return !BlendedWineTooltipPatch.SkipAgingLine;
    }
}

[HarmonyPatch(typeof(WineHelper), "PourWineIntoContainer")]
public static class BlendedWinePourPatch
{
    private static bool Prefix(GameItem bottle, GameItem container)
    {
        if (!BlendedWine.IsBlendedWine(bottle))
        {
            return true;
        }
        BlendedWine.PourInto(bottle, container);
        return false;
    }
}

[HarmonyPatch(typeof(WineHelper), "ResetToEmptyBottle")]
public static class BlendedWineResetPatch
{
    private static void Postfix(GameItem item)
    {
        if (BlendedWine.IsBlendedWine(item))
        {
            BlendedWine.ResetToVanilla(item);
        }
    }
}

[HarmonyPatch(typeof(StoreClient), "CanClientExposeThisFeature")]
public static class BlendedWineExposePatch
{
    private static void Postfix(StoreClient __instance, ItemFeature itemFeature, ref bool __result)
    {
        if (itemFeature == null || itemFeature.isDisabled
            || (itemFeature.category != BlendedWineLabels.QUALITY && itemFeature.category != BlendedWineLabels.BLEND))
        {
            return;
        }
        GameItem item = itemFeature.GetParentItem();
        __result = item != null && BlendedWine.IsLying(itemFeature) && BlendedWine.SeesThrough(item, __instance);
    }
}

[HarmonyPatch(typeof(StoreClient), "IsClientRefusingItem")]
public static class BlendedWineRefusePatch
{
    // Upper Level clients never buy wine labelled as blended.
    private static void Postfix(StoreClient __instance, GameItem gameItem, ref bool __result)
    {
        if (!__result && Refuses(__instance, gameItem))
        {
            __result = true;
        }
    }

    public static bool Refuses(StoreClient client, GameItem gameItem)
    {
        if (client == null || !BlendedWine.IsBlendedWine(gameItem) || !client.IsFaction("FACTION_UPPER_LEVEL"))
        {
            return false;
        }
        ItemFeature feature = gameItem.FindItemFeatureByCategory(BlendedWineLabels.BLEND);
        return feature != null && BlendedWineLabels.IsBlended(feature.GetCurrentItemCondition());
    }
}

[HarmonyPatch(typeof(SecData), "CheckSoldEndangermentItem")]
public static class BlendedWineFraudPatch
{
    // Like mislabelled water, selling a lying label that nobody caught counts toward fraud.
    private static void Postfix(SecData __instance, GameItem gameItem, StoreClient client)
    {
        if (!BlendedWine.IsBlendedWine(gameItem) || (client != null && client.isNoContributeToEvidence))
        {
            return;
        }
        if (BlendedWine.IsLying(gameItem.FindItemFeatureByCategory(BlendedWineLabels.QUALITY))
            || BlendedWine.IsLying(gameItem.FindItemFeatureByCategory(BlendedWineLabels.BLEND)))
        {
            __instance.badChemTrackerFraud += (int)gameItem.GetNegociatedValue();
        }
    }
}

[HarmonyPatch(typeof(LabelerElement), "OnDropdownValueChanged")]
public static class BlendedWineLabelerPatch
{
    private static void Postfix(ItemFeature ___thisFeature)
    {
        if (___thisFeature != null
            && (___thisFeature.category == BlendedWineLabels.QUALITY || ___thisFeature.category == BlendedWineLabels.BLEND))
        {
            BlendedWine.ApplyLabel(___thisFeature.GetParentItem());
        }
    }
}

[HarmonyPatch(typeof(LabelerUIManager), "Repeat")]
public static class BlendedWineLabelerRepeatPatch
{
    private static void Postfix(GameItem ___curItem)
    {
        if (BlendedWine.IsBlendedWine(___curItem))
        {
            BlendedWine.ApplyLabel(___curItem);
        }
    }
}
