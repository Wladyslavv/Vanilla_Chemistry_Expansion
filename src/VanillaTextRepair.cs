// Re-translates vanilla texts that are built once and cached in static fields: liquid names, the water
// quality labels, item feature category names, the Chem Finisher's vanilla recipe names and the module effects'
// names and descriptions. (Vanilla's other static tables translate lazily, inside lambdas, and need nothing.)
//
// Why: when a mod's Harmony patches are applied (ModLoader.Awake, before the game applies the player's
// language), Mono compiles the patched vanilla methods, and that runs the static initializers of the classes
// they use, Liquid and WaterFeatureHelper among them. Those initializers translate their texts right then,
// so they end up in the operating system's language. Every mod that patches such methods does this, this one
// included, so it cannot be avoided; instead the texts are translated again once the language is set.
// With the system and game languages differing, the stale water labels meant clients compared a container's
// label (translated fresh, in the game language) with the cached ones and refused all water, and liquid names
// showed in the other language, or blank when the game font has no glyphs for it.
// Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;

public static class VanillaTextRepair
{
    // Liquid identifier -> localization key, as in Liquid.InitLiquid.
    private static readonly string[,] LiquidKeys =
    {
        { "water", "liquid_water" }, { "heavy_metal", "liquid_heavy_metal" }, { "organic_waste", "liquid_organic_waste" },
        { "microplastic", "liquid_microplastic" }, { "microbe", "liquid_microbes" },
        { "chemical_contaminant", "liquid_chemical_contaminant" }, { "physical_contaminant", "liquid_physical_contaminant" },
        { "mineral", "liquid_mineral" }, { "dream_cap_mush", "liquid_dream_cap_mush" }, { "dream_cap_extract", "liquid_dream_cap_extract" },
        { "plant_matter", "liquid_plant_matter" }, { "alcohol", "liquid_alcohol" }, { "caffeine", "liquid_caffeine" },
        { "protein", "liquid_protein" }, { "fat", "liquid_fat_oil" }, { "starch", "liquid_starch" }, { "sugar", "liquid_sugar" },
        { "coffee_mush", "liquid_coffee_bean_mush" }, { "contraceptive", "liquid_contraceptive_hormones" },
        { "zyanide", "liquid_zyanide" }, { "nightmare_extract", "liquid_nightmare_extract" },
        { "nightmare_cap_mush", "liquid_nightmare_cap_mush" }, { "chlorine", "liquid_chlorine" }, { "synaprest", "liquid_synaprest" },
        { "fanta", "liquid_fanta" }, { "methylol", "liquid_n_methylol_base" }, { "reducing_agent", "liquid_reducing_agent" },
        { "acid", "liquid_acid" }, { "base", "liquid_base" }, { "stabilizing_agent", "liquid_stabilizing_agent" },
        { "debris", "liquid_insoluble_debris" }, { "lipobond_agent", "liquid_lipobond_agent" },
        { "livestock_steroid", "liquid_livestock_steroid" }, { "livestock_immunity", "liquid_livestock_immunity" },
        { "wine_extract", "liquid_wine_extract" }, { "nomore", "liquid_nomore" }
    };

    // Water quality keys, best first: the order of WaterFeatureHelper.WATER_DISPLAY and
    // ItemConditionList.conditionWaterPurity.
    private static readonly string[] WaterKeys =
    {
        "mech_condition_pure_water", "mech_condition_high_quality_water", "mech_condition_basewater",
        "mech_condition_ghostwater", "mech_condition_rustwater", "mech_condition_gutterflow"
    };

    // ItemFeatureList.CategoryNames: category -> localization key.
    private static readonly string[,] CategoryKeys =
    {
        { "CATEGORY_OPEN_BOX", "item_feature_category_open_box" }, { "CATEGORY_RUSTED", "item_feature_category_rusted" },
        { "CATEGORY_EXPIRED", "item_feature_category_expired" }, { "CATEGORY_DENTED", "item_feature_category_dented" },
        { "CATEGORY_INGOT_PURITY", "item_feature_category_ingot_purity" },
        { "CATEGORY_WATER_PURITY", "item_feature_category_water_purity" },
        { "CATEGORY_FRESHNESS", "item_feature_category_freshness" },
        { "CATEGORY_GENUINE_CIGARETTE", "item_feature_category_cigarette" },
        { "CATEGORY_GENUINE_STAMP", "item_feature_category_stamp" },
        { "CATEGORY_GENUINE_INJECTOR", "item_feature_category_injector" },
        { "CATEGORY_MODULE_STUCK", "item_feature_category_module_stuck" }, { "CATEGORY_FREE", "item_feature_category_free" },
        { "CATEGORY_CHEMICAL_PURITY", "item_feature_category_chemical_purity" },
        { "CATEGORY_SHABBY", "item_feature_category_shabby" }, { "CATEGORY_SECOND_HAND", "mech_condition_second_hand" }
    };

    // ChemFinisherRecipe.InitRecipes: vanilla recipe -> item name key (the product names in the Chem Finisher).
    private static readonly string[,] RecipeKeys =
    {
        { "dream_dust", "item_dream_dust_name" }, { "nightmare_dust", "item_nightmare_dust_name" },
        { "caffeine_pill", "item_caffeine_pill_name" }, { "zyanide_pill", "item_zyanide_pill_name" },
        { "birth_control_pill", "item_birth_control_pill_name" }, { "fanta_pill", "item_fanta_pill_name" },
        { "pink_alt_injector", "item_pink_alt_injector_name" }
    };

    // ModuleEffectHelper.moduleEffects: effect -> name key, description key and the description's arguments, as
    // vanilla builds them. Only these entries are touched: other mods may have added effects of their own.
    private static readonly object[][] ModuleEffectKeys =
    {
        new object[] { "MODULE_EFFECT_PREMIUM", "mech_module_effect_premium_name", "mech_module_effect_premium_desc", new object[] { 20 } },
        new object[] { "MODULE_EFFECT_INFERIOR", "mech_module_effect_inferior_name", "mech_module_effect_inferior_desc", new object[] { 19 } },
        new object[] { "MODULE_EFFECT_RECEIVER", "mech_module_effect_receiver_name", "mech_module_effect_receiver_desc", new object[] { 10 } },
        new object[] { "MODULE_EFFECT_TOP_MOUNT", "mech_module_effect_top_mount_name", "mech_module_effect_top_mount_desc", new object[] { 20 } },
        new object[] { "MODULE_EFFECT_SIDE_MOUNT", "mech_module_effect_side_mount_name", "mech_module_effect_side_mount_desc", new object[] { 20 } },
        new object[] { "MODULE_EFFECT_OVERVOLTED", "mech_module_effect_overvolted_name", "mech_module_effect_overvolted_desc", new object[] { 200, 10 } },
        new object[] { "MODULE_EFFECT_DEGRADING", "mech_module_effect_degrading_name", "mech_module_effect_degrading_desc", new object[] { 1 } },
        new object[] { "MODULE_EFFECT_LEARNING_ALGORITHM", "mech_module_effect_learning_algorithm_name", "mech_module_effect_learning_algorithm_desc", new object[] { 1 } },
        new object[] { "MODULE_EFFECT_NEGATIVE_FEEDBACK", "mech_module_effect_negative_feedback_name", "mech_module_effect_negative_feedback_desc", new object[] { 25, 25 } },
        new object[] { "NODE_EFFECT_SUPPORT", "mech_module_effect_support_node_name", "mech_module_effect_support_node_desc", new object[] { 20 } },
    };

    public static void Run()
    {
        int liquids = 0, water = 0, categories = 0, recipes = 0, effects = 0;
        try
        {
            for (int i = 0; i < LiquidKeys.GetLength(0); i++)
            {
                Liquid liquid;
                if (Liquid.LiquidDict.TryGetValue(LiquidKeys[i, 0], out liquid) && liquid != null)
                {
                    string name = Translate(LiquidKeys[i, 1]);
                    if (name != null && name != liquid.displayName)
                    {
                        liquid.displayName = name;
                        liquids++;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("re-translating liquid names failed", e);
        }
        try
        {
            water = RepairWater();
        }
        catch (Exception e)
        {
            Log.Error("re-translating water labels failed", e);
        }
        try
        {
            for (int i = 0; i < CategoryKeys.GetLength(0); i++)
            {
                string category = CategoryKeys[i, 0];
                string name = Translate(CategoryKeys[i, 1]);
                string old;
                if (name != null && ItemFeatureList.CategoryNames.TryGetValue(category, out old) && old != name)
                {
                    ItemFeatureList.CategoryNames[category] = name;
                    categories++;
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("re-translating feature categories failed", e);
        }
        try
        {
            for (int i = 0; i < RecipeKeys.GetLength(0); i++)
            {
                ChemFinisherRecipe recipe = FinisherRecipes.Find(RecipeKeys[i, 0]);
                string name = TranslateItem(RecipeKeys[i, 1]);
                if (recipe != null && name != null && name != recipe.displayName)
                {
                    recipe.displayName = name;
                    recipes++;
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("re-translating finisher recipe names failed", e);
        }
        try
        {
            foreach (object[] entry in ModuleEffectKeys)
            {
                ModuleEffectHelper.ModuleEffect effect;
                if (!ModuleEffectHelper.moduleEffects.TryGetValue((string)entry[0], out effect) || effect == null)
                {
                    continue;
                }
                string name = Translate((string)entry[1]);
                string desc = TranslateMechanic((string)entry[2], (object[])entry[3]);
                bool changed = false;
                if (name != null && name != effect.displayName)
                {
                    effect.displayName = name;
                    changed = true;
                }
                if (desc != null && desc != effect.description)
                {
                    effect.description = desc;
                    changed = true;
                }
                if (changed)
                {
                    effects++;
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("re-translating module effects failed", e);
        }
        Log.Info("re-translated vanilla texts: " + liquids + " liquid names, " + water + " water labels, " + categories
            + " categories, " + recipes + " finisher recipes, " + effects + " module effects");
    }

    private static int RepairWater()
    {
        string[] old = (string[])WaterFeatureHelper.WATER_DISPLAY.Clone();
        string[] fresh = new string[WaterKeys.Length];
        for (int i = 0; i < WaterKeys.Length; i++)
        {
            fresh[i] = Translate(WaterKeys[i]);
            if (fresh[i] == null)
            {
                return 0;  // translations not ready; leave everything as it is
            }
        }
        int changed = 0;
        for (int i = 0; i < fresh.Length; i++)
        {
            if (old[i] != fresh[i])
            {
                changed++;
            }
        }
        if (changed == 0)
        {
            return 0;
        }
        WaterFeatureHelper.PURE_WATER_DISPLAY = fresh[0];
        WaterFeatureHelper.HIGH_QUALITY_WATER_DISPLAY = fresh[1];
        WaterFeatureHelper.BASE_WATER_DISPLAY = fresh[2];
        WaterFeatureHelper.GHOSTWATER_DISPLAY = fresh[3];
        WaterFeatureHelper.RUST_WATER_DISPLAY = fresh[4];
        WaterFeatureHelper.GUTTERFLOW_WATER_DISPLAY = fresh[5];
        // Update the array in place: other code may hold a reference to it.
        for (int i = 0; i < fresh.Length && i < WaterFeatureHelper.WATER_DISPLAY.Length; i++)
        {
            WaterFeatureHelper.WATER_DISPLAY[i] = fresh[i];
        }
        // The water purity conditions use the label as their identifier; match them by the old label.
        foreach (ItemCondition condition in ItemConditionList.conditionWaterPurity)
        {
            if (condition == null)
            {
                continue;
            }
            int index = Array.IndexOf(old, condition.identifier);
            if (index >= 0)
            {
                condition.identifier = fresh[index];
                condition.display = fresh[index];
            }
        }
        return changed;
    }

    // The translation in the current game language, or null when it is missing (the key comes back).
    private static string Translate(string key)
    {
        string text = LocHelper.GetLocalizedMechanic(key);
        return string.IsNullOrEmpty(text) || text == key ? null : text;
    }

    private static string TranslateMechanic(string key, object[] args)
    {
        string text = LocHelper.GetLocalizedMechanic(key, args);
        return string.IsNullOrEmpty(text) || text == key ? null : text;
    }

    private static string TranslateItem(string key)
    {
        string text = LocHelper.GetLocalizedItem(key);
        return string.IsNullOrEmpty(text) || text == key ? null : text;
    }
}
