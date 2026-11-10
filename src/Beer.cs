// Beer: the Chem Finisher fills a 2 L keg from any liquid holding alcohol; the keg is graded into three
// tiers (undrinkable / ordinary / premium). Double-clicking the keg splits it into 6 cans, or, while the keg
// is at chill temperature (2-8 C, e.g. in a fridge chill zone), taps it into 4 draft beers. Draft beer goes
// flat overnight and then sells like a can. Cans are vanilla beverages, so fridges chill them as usual.
// Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using HarmonyLib;

public static class Beer
{
    public const string KEG_ID = "ia.beer_keg";
    public const string CAN_ID = "ia.beer_can";
    public const string MUG_ID = "ia.draft_beer";

    public const int KEG_UL = 2000000;
    public const int CANS_PER_KEG = 6;
    public const int MUGS_PER_KEG = 4;
    // Base values (ordinary tier); premium adds 40%, undrinkable is worth nothing.
    public const long KEG_VALUE = 150L;
    public const long CAN_VALUE = 25L;
    public const long MUG_VALUE = 50L;

    public const string PREFIX = "IA_BEER_";
    public const string TIER_TAG = "IA_BEER_TIER_INT";
    public const string FLAT_TAG = "IA_BEER_FLAT_BOOL";
    public const string TEMP_TAG = "CONTAINER_TEMP_TAG";

    public const double COLD_MIN = 2.0;
    public const double COLD_MAX = 8.0;
    public const double ROOM_TEMP = 25.0;
    public const double WARM_PER_SECOND = 0.2;

    public const string KEG_SPRITE = "ia_beer_keg";
    public const string CAN_SPRITE = "ia_beer_can";
    public const string MUG_SPRITE = "ia_beer_mug";
    public const string FLAT_SPRITE = "ia_beer_mug_flat";

    // ---- items -------------------------------------------------------------------------------------------

    public static bool IsKeg(GameItem item) { return item != null && item.identifier == KEG_ID; }
    public static bool IsCan(GameItem item) { return item != null && item.identifier == CAN_ID; }
    public static bool IsMug(GameItem item) { return item != null && item.identifier == MUG_ID; }
    public static bool IsBeer(GameItem item) { return IsKeg(item) || IsCan(item) || IsMug(item); }

    public static GameItem CreateKeg()
    {
        GameItem item = ItemDirectory.CreateEmptyItem(KEG_ID);
        try
        {
            item.name = IaText.Get("beer_keg_name");
            item.shortDescription = IaText.Get("beer_keg_desc");
            item.flavorText = IaText.Get("beer_keg_flavor");
            SetBeverageTypes(item);
            item.unitValue = KEG_VALUE;
            AudioHelper.InitSolidMetal(item);
            item.ModifyTag(TEMP_TAG, delegate(TagState state)
            {
                state.Enable().SetDouble(ROOM_TEMP);
            }, false);
            item.canActivateSlotItemFunc = (GameItem a, GameInventory b, SlotMarker c) => GeneralHelper.IsItemOwned(a);
            item.mayActivateSlotItemFunc = (GameItem a, GameInventory b, SlotMarker c) => GeneralHelper.IsItemOwned(a);
            item.onActivateSlotItemFunc = delegate(GameItem keg, GameInventory inventory, SlotMarker slot)
            {
                Tap(keg, inventory);
            };
            item.onOneSecondItemFunc = delegate(GameItem keg, GameInventory inventory, SlotMarker slot)
            {
                WarmUp(keg);
            };
            // Default contents for kegs not made in the Chem Finisher (debug spawns): a premium beer.
            Dictionary<string, int> beer = new Dictionary<string, int>();
            beer["alcohol"] = 110000;
            beer[IaLiquids.YEAST_WINE] = 7000;
            beer["protein"] = 10000;
            beer["sugar"] = 10000;
            beer["water"] = KEG_UL - 137000;
            SetContents(item, beer);
            BeerSprites.Apply(item, KEG_SPRITE);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            IaLog.Error("create " + KEG_ID + " failed", e);
        }
        return item;
    }

    public static GameItem CreateCan()
    {
        GameItem item = ItemDirectory.CreateEmptyItem(CAN_ID);
        try
        {
            item.name = IaText.Get("beer_can_name");
            item.shortDescription = IaText.Get("beer_can_desc");
            item.flavorText = IaText.Get("beer_can_flavor");
            SetBeverageTypes(item);
            ConsumableHelper.InitAlcohol(item, 1);
            item.unitValue = CAN_VALUE;
            AudioHelper.InitAluminumCan(item);
            SetTier(item, BeerQuality.NORMAL);
            BeerSprites.Apply(item, CAN_SPRITE);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            IaLog.Error("create " + CAN_ID + " failed", e);
        }
        return item;
    }

    public static GameItem CreateMug()
    {
        GameItem item = ItemDirectory.CreateEmptyItem(MUG_ID);
        try
        {
            item.name = IaText.Get("draft_beer_name");
            item.shortDescription = IaText.Get("draft_beer_desc");
            item.flavorText = IaText.Get("draft_beer_flavor");
            SetBeverageTypes(item);
            ConsumableHelper.InitAlcohol(item, 1);
            item.unitValue = MUG_VALUE;
            AudioHelper.InitGlass(item);
            SetTier(item, BeerQuality.NORMAL);
            BeerSprites.Apply(item, MUG_SPRITE);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            IaLog.Error("create " + MUG_ID + " failed", e);
        }
        return item;
    }

    private static void SetBeverageTypes(GameItem item)
    {
        item.SetGameItemType("SUBSTANCE");
        item.SetGameItemType("ALCOHOL");
        item.SetGameItemType("BREVAGE");
        // Water buyers shop by this type too (see BeerWaterBuyersPatch for the ones that check water quality).
        item.SetGameItemType("WATER");
    }

    // ---- grading -----------------------------------------------------------------------------------------

    // Stores the keg's contents in this mod's own tags (so it is not an open container), grades it and
    // marks it as endangering the public when methanol is above 0.1%.
    public static void SetContents(GameItem keg, Dictionary<string, int> contents)
    {
        foreach (Liquid liquid in Liquid.Liquids)
        {
            int amount;
            contents.TryGetValue(liquid.identifier, out amount);
            int value = amount;
            keg.ModifyTag(PREFIX + liquid.identifier, delegate(TagState state)
            {
                state.Enable().SetInt(value);
            }, false);
        }
        int tier = BeerGrade.Tier(contents);
        SetTier(keg, tier);
        SetEndangering(keg, BeerGrade.MethanolDanger(contents));
    }

    // The Chem Finisher writes the pressed liquid into the keg's liquid tags; move it into the keg's own tags.
    public static void FillFromFinisher(GameItem keg)
    {
        Dictionary<string, int> contents = new Dictionary<string, int>();
        foreach (Liquid liquid in Liquid.Liquids)
        {
            int amount = keg.GetTagReadonly(liquid.currentPart).GetInt();
            if (amount > 0)
            {
                contents[liquid.identifier] = amount;
            }
            IaLiquids.Set(keg, liquid.identifier, 0);
        }
        SetContents(keg, contents);
        IaLog.Info("pressed a beer keg: " + BeerGrade.Describe(contents));
    }

    public static Dictionary<string, int> Contents(GameItem keg)
    {
        Dictionary<string, int> contents = new Dictionary<string, int>();
        foreach (Liquid liquid in Liquid.Liquids)
        {
            string tag = PREFIX + liquid.identifier;
            if (keg.IsTag(tag))
            {
                int amount = keg.GetTagReadonly(tag).GetInt();
                if (amount > 0)
                {
                    contents[liquid.identifier] = amount;
                }
            }
        }
        return contents;
    }

    public static int TierOf(GameItem item)
    {
        return item.IsTag(TIER_TAG) ? item.GetTagReadonly(TIER_TAG).GetInt() : BeerQuality.NORMAL;
    }

    public static void SetTier(GameItem item, int tier)
    {
        item.ModifyTag(TIER_TAG, delegate(TagState state)
        {
            state.Enable().SetInt(tier);
        }, false);
        item.RemoveFeatureByCategory(BeerQuality.CATEGORY);
        item.AddItemFeature(BlendedWineLabels.CreateFeature(BeerQuality.CATEGORY, BeerQuality.Condition(tier)));
    }

    private static void SetEndangering(GameItem item, bool endangering)
    {
        if (endangering)
        {
            item.EnableTag("ENDANGERMENT_ITEM_TAG", false);
        }
        else
        {
            item.DisableTag("ENDANGERMENT_ITEM_TAG", false);
        }
    }

    // ---- temperature and tapping -------------------------------------------------------------------------

    public static double TempOf(GameItem keg)
    {
        return keg.IsTag(TEMP_TAG) ? keg.GetTagReadonly(TEMP_TAG).GetDouble() : ROOM_TEMP;
    }

    public static bool IsCold(GameItem keg)
    {
        double temp = TempOf(keg);
        return temp >= COLD_MIN && temp <= COLD_MAX;
    }

    private static void SetTemp(GameItem keg, double temp)
    {
        keg.ModifyTag(TEMP_TAG, delegate(TagState state)
        {
            state.Enable().SetDouble(temp);
        }, false);
    }

    public static bool InFridge(GameItem item)
    {
        GameInventory inventory = item.parentInventory;
        GameItem parent = inventory == null ? null : inventory.GetParentItem();
        return parent != null && parent.IsTag("FRIDGE_TAG");
    }

    // Outside a fridge the keg slowly warms back to room temperature.
    private static void WarmUp(GameItem keg)
    {
        if (keg == null || InFridge(keg))
        {
            return;
        }
        double temp = TempOf(keg);
        if (Math.Abs(temp - ROOM_TEMP) < 0.01)
        {
            return;
        }
        double step = Math.Min(WARM_PER_SECOND, Math.Abs(ROOM_TEMP - temp));
        SetTemp(keg, temp + (temp < ROOM_TEMP ? step : -step));
    }

    // Called from the powered fridge's cooling tick for each zone (see BeerFridgePatch).
    public static void Cool(GameItem keg, double target, double degreesPerSecond)
    {
        double temp = TempOf(keg);
        if (Math.Abs(temp - target) < 0.01)
        {
            return;
        }
        double step = Math.Min(degreesPerSecond, Math.Abs(target - temp));
        SetTemp(keg, temp + (temp < target ? step : -step));
    }

    public const string REMAINING_TAG = "IA_BEER_REMAINING_INT";
    // A can is a sixth of the keg (333 mL), a draft beer a quarter (500 mL).
    public const int CAN_UL = KEG_UL / CANS_PER_KEG;
    public const int MUG_UL = KEG_UL / MUGS_PER_KEG;

    public static int Remaining(GameItem keg)
    {
        return keg.IsTag(REMAINING_TAG) ? keg.GetTagReadonly(REMAINING_TAG).GetInt() : KEG_UL;
    }

    private static void SetRemaining(GameItem keg, int remaining)
    {
        keg.ModifyTag(REMAINING_TAG, delegate(TagState state)
        {
            state.Enable().SetInt(remaining);
        }, false);
        // The keg is worth what is left in it.
        keg.unitValue = Math.Max(1L, KEG_VALUE * remaining / KEG_UL);
    }

    // Each double-click pours one serving: a draft beer while the keg is chilled (2-8 C), otherwise a can.
    // When there is not enough left for a draft beer, a cold keg pours a can instead; the keg is used up once
    // less than a can is left.
    private static void Tap(GameItem keg, GameInventory inventory)
    {
        try
        {
            int remaining = Remaining(keg);
            bool draft = IsCold(keg) && remaining >= MUG_UL;
            int serving = draft ? MUG_UL : CAN_UL;
            if (remaining < serving)
            {
                return;
            }
            GameItem beer = DirectoryMaster.Item(draft ? MUG_ID : CAN_ID, true);
            if (beer == null)
            {
                return;
            }
            SetTier(beer, TierOf(keg));
            SetEndangering(beer, keg.IsTag("ENDANGERMENT_ITEM_TAG"));
            ModHelper.SetItemOwnerShip(beer, true);
            remaining -= serving;
            GameInventory target = inventory ?? keg.parentInventory;
            bool empty = remaining < CAN_UL;
            if (empty)
            {
                if (target != null)
                {
                    target.Expel(keg);
                }
                keg.Destroy();
            }
            else
            {
                SetRemaining(keg, remaining);
            }
            // Next to the keg first; EmporiumTryAdd falls back to the other store inventories.
            if (target != null)
            {
                GraphUtils.EmporiumTryAdd(target, beer);
            }
            else
            {
                ModHelper.MoveItemToInventory(beer);
            }
            string key = empty ? "beer_keg_empty" : (draft ? "beer_tapped" : "beer_split");
            StoreUIManager.Instance.Notify(IaText.Get(key, (remaining / 1000).ToString()), "default");
        }
        catch (Exception e)
        {
            IaLog.Error("tapping a keg failed", e);
        }
    }

    // ---- draft beer going flat ---------------------------------------------------------------------------

    public static void MakeFlat(GameItem mug)
    {
        mug.EnableTag(FLAT_TAG, false);
        mug.name = IaText.Get("flat_beer_name");
        mug.shortDescription = IaText.Get("flat_beer_desc");
        mug.unitValue = CAN_VALUE;
        BeerSprites.Apply(mug, FLAT_SPRITE);
    }

    public static void OnNight()
    {
        try
        {
            int count = 0;
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (IsMug(item) && !item.IsTag(FLAT_TAG))
                {
                    MakeFlat(item);
                    count++;
                }
            }
            if (count > 0)
            {
                IaLog.Info(count + " draft beer(s) went flat overnight");
            }
        }
        catch (Exception e)
        {
            IaLog.Error("flattening draft beer failed", e);
        }
    }

    // Saved items are rebuilt by the factories; restore what the factory doesn't know (flat draft, sprites).
    public static void FixLoadedItems()
    {
        try
        {
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (IsMug(item) && item.IsTag(FLAT_TAG))
                {
                    MakeFlat(item);
                }
                else if (IsKeg(item))
                {
                    BeerSprites.Apply(item, KEG_SPRITE);
                    SetRemaining(item, Remaining(item));
                }
                else if (IsCan(item))
                {
                    BeerSprites.Apply(item, CAN_SPRITE);
                }
                else if (IsMug(item))
                {
                    BeerSprites.Apply(item, MUG_SPRITE);
                }
            }
        }
        catch (Exception e)
        {
            IaLog.Error("updating saved beer failed", e);
        }
    }

    // ---- Chem Finisher recipe ----------------------------------------------------------------------------

    public static void RegisterRecipe()
    {
        if (FinisherRecipes.Find(KEG_ID) != null)
        {
            return;
        }
        ChemFinisherRecipe recipe = new ChemFinisherRecipe();
        recipe.identifier = KEG_ID;
        recipe.displayName = IaText.Get("recipe_beer_keg");
        recipe.requiredVolume = KEG_UL;
        recipe.isRequireDry = false;
        recipe.activeIngredientId = "alcohol";
        ChemFinisherRecipe.chemFinisherRecipes.Add(recipe);
    }

    public static void UnregisterRecipe()
    {
        ChemFinisherRecipe.chemFinisherRecipes.RemoveAll(r => r.identifier == KEG_ID);
    }

    public static void RefreshRecipeName()
    {
        ChemFinisherRecipe recipe = FinisherRecipes.Find(KEG_ID);
        if (recipe != null)
        {
            recipe.displayName = IaText.Get("recipe_beer_keg");
        }
    }

    public static void AddItems(ModItemDirectory directory)
    {
        directory.Add(KEG_ID, CreateKeg);
        directory.Add(CAN_ID, CreateCan);
        directory.Add(MUG_ID, CreateMug);
    }
}

// Three tiers, shown as an item feature like the vanilla wine quality.
public static class BeerQuality
{
    public const string CATEGORY = "CATEGORY_BEER_QUALITY";
    public const int UNDRINKABLE = 0;
    public const int NORMAL = 1;
    public const int PREMIUM = 2;
    private static readonly string[] KEYS = { "undrinkable", "normal", "premium" };
    private static readonly int[] MODS = { -100, 0, 40 };

    public static readonly List<ItemCondition> Conditions = new List<ItemCondition>();

    public static void Register()
    {
        if (Conditions.Count == 0)
        {
            for (int tier = PREMIUM; tier >= UNDRINKABLE; tier--)
            {
                ItemCondition condition = new ItemCondition();
                condition.category = CATEGORY;
                condition.identifier = "ia_beer_" + KEYS[tier];
                condition.modValue = MODS[tier];
                condition.compareValue = tier;
                Conditions.Add(condition);
            }
        }
        ItemConditionList.conditionLookup[CATEGORY] = Conditions;
        RefreshDisplays();
    }

    public static void Unregister()
    {
        ItemConditionList.conditionLookup.Remove(CATEGORY);
        ItemFeatureList.CategoryNames.Remove(CATEGORY);
    }

    public static void RefreshDisplays()
    {
        foreach (ItemCondition condition in Conditions)
        {
            condition.display = IaText.Get("beer_tier_" + KEYS[condition.compareValue]);
        }
        ItemFeatureList.CategoryNames[CATEGORY] = IaText.Get("category_beer_quality");
    }

    public static ItemCondition Condition(int tier)
    {
        string id = "ia_beer_" + KEYS[Math.Max(UNDRINKABLE, Math.Min(PREMIUM, tier))];
        return Conditions.Find(c => c.identifier == id);
    }
}

// Grading rules, as shares of the whole keg unless noted:
//   alcohol          premium 5-8%,      ordinary 8-12%,                   otherwise undrinkable
//   live yeast       premium 0.1-1%,    ordinary below 0.1% or 1-2%,      above 2% undrinkable
//   sugar            premium up to 4%,  ordinary 4-8%,                    above 8% undrinkable
//   protein          premium up to 1%,  ordinary 1-2%,                    above 2% undrinkable
//   impurities (anything but water, alcohol, yeast, sugar and protein), as a share of water + impurities:
//                    premium up to 1% (high-quality water), ordinary up to 2% (base water), else undrinkable
//   methanol above 0.1% makes it undrinkable and endangers the public.
public static class BeerGrade
{
    private static readonly HashSet<string> Body = new HashSet<string> { "water", "alcohol", "sugar", "protein", IaLiquids.ICE };

    public static int Total(Dictionary<string, int> c)
    {
        int total = 0;
        foreach (int v in c.Values)
        {
            total += v;
        }
        return total;
    }

    private static int Get(Dictionary<string, int> c, string id)
    {
        int v;
        return c.TryGetValue(id, out v) ? v : 0;
    }

    public static int Yeast(Dictionary<string, int> c)
    {
        return Get(c, IaLiquids.YEAST_WINE) + Get(c, IaLiquids.YEAST_SUPER) + Get(c, IaLiquids.YEAST_RED);
    }

    public static int Impurities(Dictionary<string, int> c)
    {
        int yeast = Yeast(c);
        int total = Total(c);
        int body = 0;
        foreach (string id in Body)
        {
            body += Get(c, id);
        }
        return total - body - yeast;
    }

    public static bool MethanolDanger(Dictionary<string, int> c)
    {
        int total = Total(c);
        return total > 0 && Get(c, IaLiquids.METHANOL) > total * 0.001;
    }

    private static int Band(double value, double premiumMin, double premiumMax, double okMin, double okMax)
    {
        if (value >= premiumMin && value <= premiumMax)
        {
            return BeerQuality.PREMIUM;
        }
        return value >= okMin && value <= okMax ? BeerQuality.NORMAL : BeerQuality.UNDRINKABLE;
    }

    public static int Tier(Dictionary<string, int> c)
    {
        double total = Total(c);
        if (total <= 0 || MethanolDanger(c))
        {
            return BeerQuality.UNDRINKABLE;
        }
        double water = Get(c, "water") + Get(c, IaLiquids.ICE);
        double impurities = Impurities(c);
        int tier = BeerQuality.PREMIUM;
        tier = Math.Min(tier, Band(Get(c, "alcohol") / total, 0.05, 0.08, 0.05, 0.12));
        tier = Math.Min(tier, Band(Yeast(c) / total, 0.001, 0.01, 0.0, 0.02));
        tier = Math.Min(tier, Band(Get(c, "sugar") / total, 0.0, 0.04, 0.0, 0.08));
        tier = Math.Min(tier, Band(Get(c, "protein") / total, 0.0, 0.01, 0.0, 0.02));
        tier = Math.Min(tier, Band(water + impurities > 0 ? impurities / (water + impurities) : 1.0, 0.0, 0.01, 0.0, 0.02));
        return tier;
    }

    public static string Describe(Dictionary<string, int> c)
    {
        double total = Math.Max(1, Total(c));
        double water = Get(c, "water") + Get(c, IaLiquids.ICE);
        double impurities = Impurities(c);
        return string.Format("total {0:0} mL, alcohol {1:0.00}%, yeast {2:0.00}%, sugar {3:0.00}%, protein {4:0.00}%, "
            + "impurities {5:0.00}% of water+impurities, tier {6}",
            total / 1000, 100 * Get(c, "alcohol") / total, 100 * Yeast(c) / total, 100 * Get(c, "sugar") / total,
            100 * Get(c, "protein") / total, water + impurities > 0 ? 100 * impurities / (water + impurities) : 100, Tier(c));
    }
}

public static class BeerSprites
{
    public static void Apply(GameItem item, string key)
    {
        LoadedMod mod = ModLoader.Mods.Find(m => m.Manifest != null && m.Manifest.ID == DreamToxinMod.MOD_ID);
        if (mod != null && mod.Sprites.ContainsKey(key) && (!item.useSpriteFromMod || item.spritePath != key))
        {
            // The shape comes from the pixel at the centre of each 16 px cell; the keg and mug sprites carry an
            // invisible (alpha 1) pixel where their outline leaves a cell centre empty, so they get full rectangles.
            item.SetSpriteAndShapeFromMod(DreamToxinMod.MOD_ID, key);
        }
    }
}

[HarmonyPatch(typeof(ChemicalProductHelper), "GradeProduct")]
public static class BeerGradePatch
{
    private static void Postfix(GameItem item)
    {
        try
        {
            if (Beer.IsKeg(item))
            {
                Beer.FillFromFinisher(item);
            }
        }
        catch (Exception e)
        {
            IaLog.Error("grading a beer keg failed", e);
        }
    }
}

// Powered fridges cool kegs in their zones like they cool liquid containers.
[HarmonyPatch(typeof(MachineFridge), "CoolZoneContainers")]
public static class BeerFridgePatch
{
    private static void Postfix(GameGridInventory zone, double targetTemp, double degreesPerSecond)
    {
        try
        {
            if (zone == null)
            {
                return;
            }
            foreach (GameItem item in zone.childItems)
            {
                if (Beer.IsKeg(item))
                {
                    Beer.Cool(item, targetTemp, degreesPerSecond);
                }
            }
        }
        catch (Exception e)
        {
            IaLog.Error("cooling a keg failed", e);
        }
    }
}

// Keg tooltip: temperature and what a double-click will do.
public static class BeerTooltip
{
    public static void OnCreateTooltip(RichTextBuilder builder, GameItem item)
    {
        try
        {
            if (builder == null || !Beer.IsKeg(item))
            {
                return;
            }
            string key = Beer.IsCold(item) ? "beer_keg_tooltip_cold" : "beer_keg_tooltip_warm";
            builder.AddLine(IaText.Get(key, Beer.TempOf(item).ToString("0.#"), (Beer.Remaining(item) / 1000).ToString()), true, RenderHandler.ColorPalette.LightCyan);
        }
        catch (Exception e)
        {
            IaLog.Error("beer tooltip failed", e);
        }
    }
}

// The regular water buyers decide with their own check of the water quality, which beer (not an open
// container) never passes. Let them take beer of ordinary quality or better as well. Story and event water
// buyers are left alone so their quests count only water. Plain Harmony patches, no reflection: wrapping the
// stored preference instead recursed forever in a test build and crashed the game.
public static class BeerWaterBuyers
{
    private static readonly HashSet<string> Ids = new HashSet<string>
    {
        "waterScavenger", "thirstySpacer", "scavWaterShopper", "waterSpace2", "waterStockpiler", "generic_water_buyer"
    };

    public static bool Takes(StoreClient client, GameItem item)
    {
        return client != null && client.identifier != null && Ids.Contains(client.identifier)
            && Beer.IsBeer(item) && Beer.TierOf(item) >= BeerQuality.NORMAL;
    }
}

// The buying decision.
[HarmonyPatch(typeof(StoreClient), "IsClientBuyingThisItem", new Type[] { typeof(GameItem), typeof(bool) })]
public static class BeerWaterBuyersBuyPatch
{
    private static void Postfix(StoreClient __instance, GameItem gameItem, bool ignoreContents, ref bool __result)
    {
        try
        {
            if (!__result && BeerWaterBuyers.Takes(__instance, gameItem) && !__instance.IsClientRefusingItem(gameItem, ignoreContents))
            {
                __result = true;
            }
        }
        catch (Exception e)
        {
            IaLog.Error("water buyer beer check failed", e);
        }
    }
}

// willAcceptItem decides the "right item / wrong item" lines when beer is put on the table.
[HarmonyPatch(typeof(StoreClient), "willAcceptItem", MethodType.Getter)]
public static class BeerWaterBuyersAcceptPatch
{
    private static void Postfix(StoreClient __instance, ref Func<GameItem, bool> __result)
    {
        Func<GameItem, bool> vanilla = __result;
        StoreClient client = __instance;
        if (vanilla == null || client == null || client.identifier == null)
        {
            return;
        }
        __result = delegate(GameItem item)
        {
            if (BeerWaterBuyers.Takes(client, item) && (!GeneralHelper.IsItemOwned(item) || !client.IsClientRefusingItem(item, false)))
            {
                return true;
            }
            return vanilla(item);
        };
    }
}

// The Chem Finisher normally replaces a share of the product with physical contaminant depending on its chem
// modules (12% with none, 0% with 4). Beer kegs are filled without it.
[HarmonyPatch(typeof(MachineChemFinisher), "IntroduceImpurity")]
public static class BeerNoFinisherImpurityPatch
{
    private static bool Prefix(GameItem chem)
    {
        return !Beer.IsKeg(chem);
    }
}
