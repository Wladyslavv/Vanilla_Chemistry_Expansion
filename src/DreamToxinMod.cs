// Vanilla Chemistry Expansion (vanilla_chem_ext). Mod entry point plus Dream Toxin, a legal medical product
// from Dream Cap and Nightmare Cap extracts: Dream Cap Extract + Nightmare Cap Extract (1:1), 0.5-5% acid,
// refrigerated (2-8 C or a powered fridge chill zone) slowly turn into Dream Toxin plus debris, which the
// Chem Finisher presses into injectors that only sell well at pharma grade or better.
// Other features live in OreChemistry.cs, StarchGenes.cs, IndustrialAlcohol.cs, FakeWine.cs,
// AdvancedFabric.cs, Lantern.cs and Settings.cs.
// Written for the stock .NET Framework csc, so C# 5 syntax only. Optional arguments are passed
// explicitly on tag helpers: C# 5 ignores `where T : Enum` and would bind the generic overloads.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

public class DreamToxinMod : IMod
{
    public const string MOD_ID = "vanilla_chem_ext";
    public const string MOD_NAME = "Vanilla Chemistry Expansion";

    public void Init(ModManifest manifest)
    {
        Log.Info("init " + manifest.ID + " v" + manifest.ModVersion);
    }

    public void OnEnable()
    {
        StarchGenesSetup.Enable();
        IndustrialAlcohol.Enable();
        NuclearWaste.Enable();
        Lantern.Enable();
        SaltOre.Enable();
        GasCanister.Enable();
        Electrolyzer.Enable();
        PuBattery.Enable();
        CartelNetwork.Enable();
        CartelRaid.Enable();
        CartelSweep.Enable();
        CartelNetworkPerks.Enable();
        CartelTrader.Enable();
        CartelSupply.Enable();
        CartelPeer.Enable();
        CartelTourist.Enable();
        // Liquids, recipes, genes and wine labels are registered later, in Content.EnsureRegistered.
        ModHook.OnModItemDirectoryInit += OnItemDirectoryInit;
        ModHook.OnModAssetsLoaded += OnModAssetsLoaded;
        ModHook.OnNewGameStarted += OnNewGameStarted;
        ModHook.OnGameLoadedInit += RefreshLocalizedNames;
        ModHook.OnGameLoadedLate += ToxinSprite.FixLoadedItems;
        ModHook.OnGameLoadedLate += OreItems.FixLoadedItems;
        ModHook.OnPlaceInventorInventoryItemEarly += GuideBook.OnPlaceInventorInventory;
        try
        {
            // "Modding" is one of the debug panel's fixed categories; unknown ones fall back to "Special".
            ModHelper.AddDebugFunction(MOD_ID, "Modding", "Dream Toxin: test kit", DebugKit.SpawnTestKit);
            ModHelper.AddDebugFunction(MOD_ID, "Modding", "Dream Toxin: pre-mixed bottle (2% acid)", DebugKit.SpawnPremixed);
            ModHelper.AddDebugFunction(MOD_ID, "Modding", "Dream Toxin: formula note", DebugKit.SpawnFormulaNote);
            ModHelper.AddDebugFunction(MOD_ID, "Modding", "Ore Chem: ore kit", OreDebugKit.SpawnOreKit);
            ModHelper.AddDebugFunction(MOD_ID, "Modding", "Ore Chem: quicklime reagent", OreDebugKit.SpawnQuicklimeReagent);
            ModHelper.AddDebugFunction(MOD_ID, "Modding", "Ore Chem: silica gel reagent", OreDebugKit.SpawnSilicaGelReagent);
            ModHelper.AddDebugFunction(MOD_ID, "Modding", "Ore Chem: notes", OreDebugKit.SpawnNotes);
            ModHelper.AddDebugFunction(MOD_ID, "Modding", "Chem: advanced chemistry handbook", DebugKit.SpawnHandbook);
        }
        catch (Exception e)
        {
            Log.Error("debug function registration failed", e);
        }
    }

    public void OnDisable()
    {
        ModHook.OnModItemDirectoryInit -= OnItemDirectoryInit;
        ModHook.OnModAssetsLoaded -= OnModAssetsLoaded;
        ModHook.OnNewGameStarted -= OnNewGameStarted;
        ModHook.OnGameLoadedInit -= RefreshLocalizedNames;
        ModHook.OnGameLoadedLate -= ToxinSprite.FixLoadedItems;
        ModHook.OnGameLoadedLate -= OreItems.FixLoadedItems;
        ModHook.OnPlaceInventorInventoryItemEarly -= GuideBook.OnPlaceInventorInventory;
        Content.Unregister();
        StarchGenesSetup.Disable();
        IndustrialAlcohol.Disable();
        NuclearWaste.Disable();
        Lantern.Disable();
        GasCanister.Disable();
        Electrolyzer.Disable();
        PuBattery.Disable();
        CartelNetwork.Disable();
        CartelRaid.Disable();
        CartelSweep.Disable();
        CartelNetworkPerks.Disable();
        CartelTrader.Disable();
        CartelSupply.Disable();
        CartelPeer.Disable();
        CartelTourist.Disable();
        try
        {
            ModHelper.RemoveDebugFunctions(MOD_ID);
        }
        catch
        {
        }
    }

    private static void OnItemDirectoryInit(ModItemDirectory directory)
    {
        if (directory == null)
        {
            return;
        }
        // Items are only ever built after the item directory exists, saved ones included, so registering
        // here is both late enough (the game language is set) and early enough (before any save loads).
        Content.EnsureRegistered("item directory init");
        try
        {
            directory.Add(ToxinProduct.ITEM_ID, ToxinProduct.CreateInjector);
            IndustrialAlcohol.AddItems(directory);
            NuclearWaste.AddItems(directory);
            Lantern.AddItems(directory);
            SaltOre.AddItems(directory);
            GasCanister.AddItems(directory);
            Electrolyzer.AddItems(directory);
            PuBattery.AddItems(directory);
            CartelNetwork.AddItems(directory);
            directory.Add(GuideBook.ITEM_ID, GuideBook.CreateItem);
            // Registered as its own item so it survives saves and shows up in item spawners;
            // the note still reads like any vanilla handnote (the reader only looks at its tags).
            foreach (HandNote note in Notes.All)
            {
                directory.Add(note.ItemId, note.Create);
            }
            Log.Info("registered " + ToxinProduct.ITEM_ID + " and " + Notes.All.Length + " notes");
        }
        catch (Exception e)
        {
            Log.Error("item registration failed", e);
        }
    }

    // Mod localization loads during startup, long before the content is registered; Content.EnsureRegistered
    // refreshes the names and builds the handbook itself. Afterwards, a late mod may still add a handbook section.
    private static void OnModAssetsLoaded(LoadedMod mod)
    {
        if (mod == null || mod.Manifest == null || !Content.Registered)
        {
            return;
        }
        RefreshLocalizedNames();
        GuideBook.Register();
        CartelNote.Register();
    }

    private static void OnNewGameStarted(string scenarioId)
    {
        Content.EnsureRegistered("new game");
        RefreshLocalizedNames();
        GuideBook.GiveChemistStart(scenarioId);
    }

    internal static void RefreshLocalizedNames()
    {
        if (!Content.Registered)
        {
            return;
        }
        ToxinLiquid.RefreshName();
        OreLiquids.RefreshNames();
        ToxinProduct.RefreshRecipeName();
        IaLiquids.RefreshNames();
        Xinke.RefreshNames();
        NuclearWaste.RefreshNames();
        Lantern.RefreshNames();
        SaltOre.RefreshNames();
    }
}

// Patches on methods of ItemFeatureList and ChemFinisherRecipe are not installed with the rest at load; they are
// installed here, once the game language is set.
// Why: installing a patch makes Mono resolve the patched method (GetFunctionPointer), which runs the static
// initializer of the method's own class. Both classes translate texts there, synchronously (ItemFeatureList's
// CategoryNames, ChemFinisherRecipe's InitRecipes). At load the localization system may not be initialized yet: then
// Unity throws "Attempting to use an invalid operation handle" and the game's localization stays broken, the whole
// game stuck in English. It only happens sometimes, depending on how far localization got (a player's log showed it
// on CartelDistrustLowPatch).
// How: plain Harmony, no reflection. Each of these patch classes keeps its [HarmonyPatch] attribute and has a
// Prepare() that returns Ready; Harmony asks Prepare before it touches the target, so the loader's PatchAll skips
// them. Apply then sets Ready and has Harmony process each class. Same Harmony ID as the loader's ("mod." + mod ID), so
// unloading the mod removes these patches too.
public static class DeferredPatches
{
    public static bool Ready { get; private set; }

    public static void Apply()
    {
        if (Ready)
        {
            return;
        }
        Ready = true;
        Harmony harmony = new Harmony("mod." + DreamToxinMod.MOD_ID);
        Patch(harmony, typeof(CartelDistrustLowPatch));
        Patch(harmony, typeof(CartelDistrustMidPatch));
        Patch(harmony, typeof(CartelDistrustHighPatch));
        Patch(harmony, typeof(CartelDistrustCriticalPatch));
        Patch(harmony, typeof(AmpouleRecipePatch));
    }

    private static void Patch(Harmony harmony, Type patchClass)
    {
        try
        {
            harmony.CreateClassProcessor(patchClass).Patch();
            Log.Info("deferred patch installed: " + patchClass.Name);
        }
        catch (Exception e)
        {
            Log.Error("deferred patch " + patchClass.Name + " failed", e);
        }
    }
}

// Content is registered once, at the first item directory init or new game: by then the game language is
// set, and no item (saved ones included) has been built yet. Registration also re-translates vanilla texts
// that were cached in the system language during mod loading (see VanillaTextRepair.cs); in 1.3.0 and older,
// with the system and game languages differing, that left water unsellable and liquid names wrong or blank.
public static class Content
{
    public static bool Registered { get; private set; }

    public static void EnsureRegistered(string trigger)
    {
        if (Registered)
        {
            return;
        }
        Registered = true;
        DeferredPatches.Apply();
        try
        {
            ToxinLiquid.Register();
            OreLiquids.Register();
            ToxinProduct.RegisterRecipe();
            StarchGenes.Register();
            IndustrialAlcohol.RegisterContent();
            NuclearWaste.RegisterContent();  // after IndustrialAlcohol: it uses ethyl acetate
            Lantern.Register();
            SaltOre.Register();
            DreamToxinMod.RefreshLocalizedNames();
            GuideBook.Register();
            CartelNote.Register();
            Log.Info("content registered on " + trigger);
        }
        catch (Exception e)
        {
            Log.Error("content registration failed", e);
        }
        LanguageCheck.Report("before repair");
        VanillaTextRepair.Run();
        LanguageCheck.Report("after repair");
    }

    public static void Unregister()
    {
        if (!Registered)
        {
            return;
        }
        ToxinProduct.UnregisterRecipe();
        StarchGenes.Unregister();
        IndustrialAlcohol.UnregisterContent();
        NuclearWaste.UnregisterContent();
        Lantern.Unregister();
        Registered = false;
    }
}

// Writes the languages involved to Player.log, so a report can tell whether the vanilla water labels were
// cached in the right language: "cached" is what vanilla froze, "current" what the game language says now.
// Looks a Chem Finisher recipe up in the list itself. ChemFinisherRecipe.GetRecipeById can be patched by other mods
// (Chem Finisher Recipe Guard returns a placeholder instead of null), which would make "already registered" checks
// skip our recipes and make name refreshes rename that placeholder.
public static class FinisherRecipes
{
    public static ChemFinisherRecipe Find(string id)
    {
        foreach (ChemFinisherRecipe recipe in ChemFinisherRecipe.chemFinisherRecipes)
        {
            if (recipe != null && recipe.identifier == id)
            {
                return recipe;
            }
        }
        return null;
    }
}

public static class LanguageCheck
{
    public static void Report(string stage)
    {
        try
        {
            string cached = WaterFeatureHelper.PURE_WATER_DISPLAY;
            string current = LocHelper.GetLocalizedMechanic("mech_condition_pure_water");
            Liquid acid = Liquid.GetLiquidFromId("acid");
            Log.Info("language check (" + stage + "): system=" + Application.systemLanguage
                + ", pure water label cached='" + cached + "' current='" + current + "'"
                + ", acid name='" + (acid != null ? acid.displayName : "?") + "'"
                + (cached == current ? ", OK" : ", MISMATCH (cached before the game language was set)"));
        }
        catch (Exception e)
        {
            Log.Error("language check failed", e);
        }
    }
}

public static class ToxinLiquid
{
    public const string ID = "dream_toxin";

    public static void Register()
    {
        if (Liquid.LiquidDict.ContainsKey(ID))
        {
            return;
        }
        // Mirrors the vanilla Nightmare Cap Extract entry; SetIdentifier is private, so set its fields directly.
        Liquid liquid = new Liquid();
        liquid.displayName = Text.Get("liquid_dream_toxin");
        liquid.identifier = ID;
        liquid.part = "PART_" + ID.ToUpperInvariant();
        liquid.currentPart = "CURRENT_PART_" + ID.ToUpperInvariant();
        liquid.baseMaxPurityFromMachine = 12;
        liquid.basePurifierRemoveAmount = 4000;
        liquid.particleSize = 5;
        // The game counts lethality x microlitres against 750 (toxic) and 1000 (lethal) for sold products. A full
        // injector holds 5000 uL, so this keeps it at 500: harmless as a cosmetic dose, deadly by the litre.
        liquid.lethality = 0.1;
        liquid.color = "6b3fa0";
        liquid.colorMultiplier = 4;
        Liquid.Liquids.Add(liquid);
        Liquid.LiquidDict[ID] = liquid;
        Log.Info("registered liquid " + ID);
    }

    public static void RefreshName()
    {
        Liquid liquid;
        if (Liquid.LiquidDict.TryGetValue(ID, out liquid))
        {
            liquid.displayName = Text.Get("liquid_dream_toxin");
        }
    }
}

public static class ToxinReaction
{
    public const double ACID_MIN_PERCENT = 0.5;
    public const double ACID_MAX_PERCENT = 5.0;
    public const double COLD_MIN_TEMP = 2.0;
    public const double COLD_MAX_TEMP = 8.0;

    // Per tick: 5% of the limiting extract (min 400 uL).
    public const double REACT_RATE = 0.05;
    public const int REACT_MIN_UL = 400;
    public const double DEGRADE_RATE = 0.02;

    // Set by the fridge chill zone every tick it is powered; counts down in the container's own tick.
    public const string CHILL_TAG = "DREAM_TOXIN_CHILL_TICKS_INT";
    public const int CHILL_TICKS = 3;

    public static int Run(GameItem container)
    {
        if (container == null || WaterHelper.IsEmpty(container))
        {
            return 0;
        }
        int total = WaterHelper.GetTotalVolume(container);
        if (total <= 0)
        {
            return 0;
        }
        bool chilled = ConsumeChillTick(container);
        double temp = container.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
        int dream = Get(container, "dream_cap_extract");
        int nightmare = Get(container, "nightmare_extract");
        int toxin = Get(container, ToxinLiquid.ID);
        int acid = Get(container, "acid");
        double acidPercent = acid * 100.0 / total;

        // The toxin is heat-stable on purpose: heating destroys leftover extracts (vanilla degradation,
        // dream >= 60 C, nightmare >= 50 C, no residue) without touching the toxin. Only excess acid hurts it.
        if (toxin > 0 && acidPercent > ACID_MAX_PERCENT)
        {
            int loss = Math.Min(toxin, Math.Max(1, (int)Math.Round(toxin * DEGRADE_RATE)));
            Set(container, ToxinLiquid.ID, toxin - loss);
            Add(container, "debris", loss);
            return 1;
        }

        bool cold = chilled || (temp >= COLD_MIN_TEMP && temp <= COLD_MAX_TEMP);
        if (dream <= 0 || nightmare <= 0 || !cold || acidPercent < ACID_MIN_PERCENT || acidPercent > ACID_MAX_PERCENT)
        {
            return 0;
        }
        int limiting = Math.Min(dream, nightmare);
        int amount = Math.Min(limiting, Math.Max(REACT_MIN_UL, (int)Math.Round(limiting * REACT_RATE)));
        // One part of each extract becomes one part toxin and one part debris (filterable, particle size 200).
        Set(container, "dream_cap_extract", dream - amount);
        Set(container, "nightmare_extract", nightmare - amount);
        Set(container, ToxinLiquid.ID, toxin + amount);
        Add(container, "debris", amount);
        return 1;
    }

    public static void MarkChilled(GameItem container)
    {
        container.ModifyTag(CHILL_TAG, delegate(TagState state)
        {
            state.Enable().SetInt(CHILL_TICKS);
        }, true);
    }

    private static bool ConsumeChillTick(GameItem container)
    {
        if (!container.IsTag(CHILL_TAG))
        {
            return false;
        }
        int ticks = container.GetTagReadonly(CHILL_TAG).GetInt();
        if (ticks <= 0)
        {
            return false;
        }
        container.ModifyTag(CHILL_TAG, delegate(TagState state)
        {
            state.SetInt(ticks - 1);
        }, true);
        return true;
    }

    private static int Get(GameItem container, string liquidId)
    {
        return container.GetTagReadonly(Liquid.GetCurrentPartFromId(liquidId)).GetInt();
    }

    private static void Set(GameItem container, string liquidId, int value)
    {
        container.ModifyTag(Liquid.GetCurrentPartFromId(liquidId), delegate(TagState state)
        {
            state.Enable().SetInt(Math.Max(0, value));
        }, true);
    }

    private static void Add(GameItem container, string liquidId, int value)
    {
        Set(container, liquidId, Get(container, liquidId) + value);
    }
}

public static class ToxinProduct
{
    public const string ITEM_ID = "dream_toxin.injector";
    public const int REQUIRED_VOLUME = 5000;
    public const long BASE_VALUE = 150L;

    public static GameItem CreateInjector()
    {
        GameItem item = ItemDirectory.CreateEmptyItem(ITEM_ID);
        try
        {
            ToxinSprite.Apply(item);
            item.name = Text.Get("injector_name");
            item.shortDescription = Text.Get("injector_desc");
            item.flavorText = Text.Get("injector_flavor");
            item.unitValue = BASE_VALUE;
            item.bonusAccuracy = 1000;
            item.EnableTag("injector", false);
            item.EnableTag("ITEM_MEDICAL_TAG", false);
            item.EnableProficiency("MEDICAL_PROFICIENCY");
            item.SetGameItemType("MEDICAL");
            ChemicalHelper.InitChemItem(item, true, true);
            // Default contents for items not made in the Chem Finisher (debug spawns, shops).
            ChemFinisherRecipe.SetCompositionByPurity(item, 90, 96, REQUIRED_VOLUME, ToxinLiquid.ID);
            ChemicalProductHelper.InitChemicalProduct(item, ToxinLiquid.ID);
            ChemicalFeedbackHelper.InitChemicalIntentItem(item, ChemicalFeedbackHelper.ChemicalIntent.MEDICAL);
            AudioHelper.InitInjectorAudio(item);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            Log.Error("create " + ITEM_ID + " failed", e);
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
        recipe.displayName = Text.Get("injector_name");
        recipe.requiredVolume = REQUIRED_VOLUME;
        recipe.isRequireDry = true;
        recipe.activeIngredientId = ToxinLiquid.ID;
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
            recipe.displayName = Text.Get("injector_name");
        }
    }

    // Medical grading: same bonuses as vanilla at pharma grade and above, steep penalties below.
    public static ItemCondition ConditionFor(double purity)
    {
        if (purity >= 99.0)
        {
            return Condition("lab_pure", "mech_condition_lab_pure", 135);
        }
        if (purity >= 90.0)
        {
            return Condition("pharma_grade", "mech_condition_pharma_grade", 85);
        }
        if (purity >= 80.0)
        {
            return Condition("near_pharma", "mech_condition_near_pharma", -60);
        }
        if (purity >= 60.0)
        {
            return Condition("street_standard", "mech_condition_street_standard", -90);
        }
        if (purity >= 45.0)
        {
            return Condition("cut_product", "mech_condition_cut_product", -95);
        }
        if (purity >= 30.0)
        {
            return Condition("junk_grade", "mech_condition_junk_grade", -95);
        }
        return Condition("starch_grade", "mech_condition_starch_grade", -95);
    }

    private static ItemCondition Condition(string identifier, string displayKey, int modValue)
    {
        ItemCondition condition = new ItemCondition();
        condition.category = "CATEGORY_CHEMICAL_PURITY";
        condition.display = LocHelper.GetLocalizedMechanic(displayKey);
        condition.modValue = modValue;
        condition.identifier = identifier;
        return condition;
    }
}

[HarmonyPatch(typeof(ChemicalHelper), "HandleTemperatureReaction")]
public static class TemperatureReactionPatch
{
    private static void Postfix(GameItem container)
    {
        try
        {
            int changed = ToxinReaction.Run(container) | OreReactions.Run(container) | Crackine.Run(container) | Lantern.Run(container);
            if (changed > 0)
            {
                ItemFeature waterFeature = WaterFeatureHelper.GetWaterFeature(container);
                WaterFeatureHelper.OnLiquidChanged(container, waterFeature != null && waterFeature.IsExposed());
            }
        }
        catch (Exception e)
        {
            Log.Error("reaction failed", e);
        }
    }
}

[HarmonyPatch(typeof(MachineFridge), "CoolZoneContainers")]
public static class FridgeChillPatch
{
    // Only called while the fridge has power; the chill zone targets 3 C, the freezer -18 C.
    private static void Postfix(GameGridInventory zone, double targetTemp)
    {
        try
        {
            if (zone == null)
            {
                return;
            }
            foreach (GameItem item in zone.childItems)
            {
                if (item == null || !item.IsTag("LIQUID_CONTAINER_TAG") || WaterHelper.IsEmpty(item))
                {
                    continue;
                }
                if (targetTemp > 0.0)
                {
                    ToxinReaction.MarkChilled(item);
                }
                else
                {
                    OreReactions.MarkFrozen(item);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("fridge chill mark failed", e);
        }
    }
}

[HarmonyPatch(typeof(ChemicalProductHelper), "GradeProduct")]
public static class GradeProductPatch
{
    private static void Postfix(GameItem item)
    {
        try
        {
            if (item == null || item.identifier != ToxinProduct.ITEM_ID || !item.IsTag("CHEMICAL_PRODUCT_TAG"))
            {
                return;
            }
            double purity = item.GetTagReadonly("CHEMICAL_PRODUCT_PURITY_TAG").GetDouble();
            ChemicalProductHelper.SetPurityFeature(item, ToxinProduct.ConditionFor(purity));
        }
        catch (Exception e)
        {
            Log.Error("grading failed", e);
        }
    }
}

// The injector sprite ships as Textures/Items/dream_toxin_injector.png (the vanilla Nomore injector,
// recoloured off-white with a faint yellow), which ModLoader loads into this mod's item sprites.
// The vanilla "injector_white" atlas sprite is fully transparent, so it cannot be used as a base.
public static class ToxinSprite
{
    public const string KEY = "dream_toxin_injector";
    public const string FALLBACK_ATLAS = "Items/items_medical";
    public const string FALLBACK_SPRITE = "injector_pink_alt";

    public static bool IsToxin(GameItem item)
    {
        return item != null && item.identifier == ToxinProduct.ITEM_ID;
    }

    public static void Apply(GameItem item)
    {
        LoadedMod mod = ModLoader.Mods.Find(m => m.Manifest != null && m.Manifest.ID == DreamToxinMod.MOD_ID);
        if (mod != null && mod.Sprites.ContainsKey(KEY))
        {
            item.SetSpriteAndShapeFromMod(DreamToxinMod.MOD_ID, KEY);
            return;
        }
        Log.Info("sprite " + KEY + " not loaded, using " + FALLBACK_SPRITE);
        // SetSpriteAndShape does not clear the mod-sprite flag, so reset it explicitly.
        item.useSpriteFromMod = false;
        item.SetSpriteAndShape(FALLBACK_ATLAS, FALLBACK_SPRITE);
    }

    // Injectors saved by 0.2.x point at sprites that no longer exist (or the transparent vanilla
    // white injector); give every loaded injector the current sprite.
    public static void FixLoadedItems()
    {
        try
        {
            int count = 0;
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (IsToxin(item) && (!item.useSpriteFromMod || item.spriteAtlasPath != DreamToxinMod.MOD_ID || item.spritePath != KEY))
                {
                    Apply(item);
                    count++;
                }
            }
            if (count > 0)
            {
                Log.Info("updated the sprite of " + count + " saved injector(s)");
            }
        }
        catch (Exception e)
        {
            Log.Error("updating saved injector sprites failed", e);
        }
    }
}

// Handwritten notes in the style of Li Bei Wen's Nomore formula, sold by the retired chemist.
// Each is registered as its own item; the vanilla note reader only looks at the handnote tags.
public class HandNote
{
    public string ItemId;
    public string Tag;
    public string NameKey;
    public string TitleKey;
    public string[] LineKeys;
    public long Value = 400L;

    public string Body()
    {
        List<string> lines = new List<string>();
        foreach (string key in LineKeys)
        {
            lines.Add(GuideBook.StripLayoutMarkup(Text.Get(key)));
        }
        return string.Join("\n", lines.ToArray());
    }

    public GameItem Create()
    {
        List<string> lines = new List<string>();
        foreach (string key in LineKeys)
        {
            lines.Add(GuideBook.StripLayoutMarkup(Text.Get(key)));
        }
        GameItem note = PreBuiltItemHelper.CreateHandnote(string.Join("\n", lines.ToArray()), Text.Get(TitleKey), "", 2);
        note.name = Text.Get(NameKey);
        note.unitValue = Value;
        note.EnableTag(Tag, false);
        ModHelper.SetSource(note, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        return note;
    }

    public bool PlayerOwnsOne()
    {
        foreach (GameItem item in EmporiumEntry.Instance.GetAllOwnedItems())
        {
            if (item != null && item.IsTag(Tag))
            {
                return true;
            }
        }
        return false;
    }
}

public static class Notes
{
    public static readonly HandNote ToxinFormula = new HandNote
    {
        ItemId = "dream_toxin.formula",
        Tag = "DREAM_TOXIN_FORMULA_TAG",
        NameKey = "formula_name",
        TitleKey = "injector_name",
        LineKeys = new string[] { "formula_step_1", "formula_step_2", "formula_step_3", "formula_step_4", "formula_step_5", "formula_step_6" }
    };

    public static readonly HandNote OreNotes = new HandNote
    {
        ItemId = "vce.ore_notes",
        Tag = "VCE_ORE_NOTES_TAG",
        NameKey = "ore_notes_name",
        TitleKey = "ore_notes_name",
        LineKeys = new string[]
        {
            "ore_notes_intro", "ore_notes_base", "ore_notes_base_1", "ore_notes_base_2", "ore_notes_base_3", "ore_notes_base_4",
            "ore_notes_acid", "ore_notes_acid_1", "ore_notes_acid_2", "ore_notes_acid_3", "ore_notes_acid_4"
        }
    };

    public static readonly HandNote WineMaking = new HandNote
    {
        ItemId = "ia.wine_making_notes",
        Tag = "IA_WINE_MAKING_NOTES_TAG",
        NameKey = "making_notes_name",
        TitleKey = "making_notes_name",
        LineKeys = new string[]
        {
            "making_alcohol", "making_alcohol_1", "making_alcohol_2", "making_alcohol_3", "making_alcohol_4", "making_alcohol_5",
            "making_ester", "making_1", "making_2", "making_3",
            "making_wine", "making_4", "making_5", "making_6"
        }
    };

    public static readonly HandNote WineSelling = new HandNote
    {
        ItemId = "ia.wine_selling_notes",
        Tag = "IA_WINE_SELLING_NOTES_TAG",
        NameKey = "selling_notes_name",
        TitleKey = "selling_notes_name",
        LineKeys = new string[] { "selling_1", "selling_2", "selling_3", "selling_4", "selling_5", "selling_6", "selling_7" }
    };

    public static readonly HandNote Reprocessing = new HandNote
    {
        ItemId = "nc.reprocessing_notes",
        Tag = "NC_REPROCESSING_NOTES_TAG",
        NameKey = "nc_notes_name",
        TitleKey = "nc_notes_name",
        LineKeys = new string[]
        {
            "nc_notes_warning", "nc_notes_1", "nc_notes_2", "nc_notes_3", "nc_notes_4", "nc_notes_5",
            "nc_notes_6", "nc_notes_7", "nc_notes_8", "nc_notes_9"
        }
    };

    public static readonly HandNote BeerBrewing = new HandNote
    {
        ItemId = "ia.beer_notes",
        Tag = "IA_BEER_NOTES_TAG",
        NameKey = "beer_notes_name",
        TitleKey = "beer_notes_name",
        LineKeys = new string[] { "beer_1", "beer_4", "beer_5", "beer_6", "beer_7" }
    };

    // The quality table lines use TextMeshPro <pos> tags to line up the columns on the handbook page.
    public static readonly HandNote BeerTable = new HandNote
    {
        ItemId = "ia.beer_quality_notes",
        Tag = "IA_BEER_QUALITY_NOTES_TAG",
        NameKey = "beer_table_name",
        TitleKey = "beer_table_name",
        LineKeys = new string[]
        {
            "beer_table_head", "beer_table_alcohol", "beer_table_yeast", "beer_table_sugar", "beer_table_protein",
            "beer_table_impurity", "beer_table_methanol", "beer_table_note_1", "beer_table_note_2", "beer_table_note_3"
        }
    };

    public static readonly HandNote CrackineNotes = new HandNote
    {
        ItemId = "vce.crackine_notes",
        Tag = "VCE_CRACKINE_NOTES_TAG",
        NameKey = "crackine_notes_name",
        TitleKey = "crackine_notes_name",
        LineKeys = new string[] { "crackine_1", "crackine_2", "crackine_3", "crackine_4", "crackine_5", "crackine_6", "crackine_7" }
    };

    public static readonly HandNote LanternNotes = new HandNote
    {
        ItemId = "vce.lantern_notes",
        Tag = "VCE_LANTERN_NOTES_TAG",
        NameKey = "lantern_notes_name",
        TitleKey = "lantern_notes_name",
        LineKeys = new string[] { "lantern_1", "lantern_2", "lantern_3", "lantern_4", "lantern_5", "lantern_6", "lantern_7", "lantern_8" }
    };

    public static readonly HandNote ElectrolysisNotes = new HandNote
    {
        ItemId = "vce.electrolysis_notes",
        Tag = "VCE_ELECTROLYSIS_NOTES_TAG",
        NameKey = "electrolysis_notes_name",
        TitleKey = "electrolysis_notes_name",
        LineKeys = new string[]
        {
            "electrolysis_1", "electrolysis_2", "electrolysis_3", "electrolysis_4", "electrolysis_5",
            "electrolysis_6", "electrolysis_7", "electrolysis_8", "electrolysis_9"
        }
    };

    public static readonly HandNote[] All = new HandNote[] { ToxinFormula, OreNotes, WineMaking, WineSelling, BeerBrewing, BeerTable, Reprocessing, CrackineNotes, LanternNotes, ElectrolysisNotes };
}

// The retired chemist always brings the advanced chemistry handbook while the player has none.
[HarmonyPatch(typeof(StoreClientListSpec), "CreateRetiredChemist")]
public static class RetiredChemistPatch
{
    private static void Postfix(StoreClient __result)
    {
        try
        {
            if (__result == null || __result.mainDialogue == null)
            {
                return;
            }
            Dialogue dialogue = __result.mainDialogue;
            Action vanilla = dialogue.endAction;
            dialogue.endAction = delegate
            {
                if (vanilla != null)
                {
                    vanilla();
                }
                GuideBook.OfferIfUnowned();
            };
        }
        catch (Exception e)
        {
            Log.Error("retired chemist patch failed", e);
        }
    }
}

public static class DebugKit
{
    // 60 mL of each extract: enough volume to stay inside 2-8 C in a fridge.
    public const int EXTRACT_UL = 60000;

    public static void SpawnTestKit()
    {
        GameItem bottle = DirectoryMaster.Item("med_bottle_red", true);
        WaterHelper.AddLiquid(bottle, "dream_cap_extract", EXTRACT_UL);
        WaterHelper.AddLiquid(bottle, "nightmare_extract", EXTRACT_UL);
        AudioHelper.InitPlasticContainer(bottle);
        Give(bottle);
        Give(PreBuildChemHelper.CreateAcidBottle());
        Give(PreBuildChemHelper.CreateBaseBottle());
    }

    public static void SpawnPremixed()
    {
        GameItem bottle = DirectoryMaster.Item("med_bottle_red", true);
        WaterHelper.AddLiquid(bottle, "dream_cap_extract", EXTRACT_UL);
        WaterHelper.AddLiquid(bottle, "nightmare_extract", EXTRACT_UL);
        // 2% of the final volume: acid / (120 mL + acid) = 0.02
        WaterHelper.AddLiquid(bottle, "acid", 2449);
        AudioHelper.InitPlasticContainer(bottle);
        Give(bottle);
    }

    public static void SpawnHandbook()
    {
        Give(DirectoryMaster.Item(GuideBook.ITEM_ID, true));
    }

    public static void SpawnFormulaNote()
    {
        Give(DirectoryMaster.Item(Notes.ToxinFormula.ItemId, true));
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

public static class Text
{
    public static string Get(string key, params object[] args)
    {
        return ModHelper.GetLocalized(DreamToxinMod.MOD_ID, key, args);
    }
}

public static class Log
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
