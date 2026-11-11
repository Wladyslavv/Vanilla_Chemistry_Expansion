// Crackine: the refined product of Xinke leaves. Route (game chemistry, see the handbook):
//   1. blend leaves (water, protein, alkaloid)        2. acid or base denatures the protein (vanilla), filter
//   3. acid to about 5% of the liquid                  4. boil dry: alkaloid + acid -> Crackine powder
//   5. the evaporator drives off the leftover acid     6. Chem Finisher: "a small vial of Crackine"
// Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using HarmonyLib;

public static class Crackine
{
    public const string SALT_ID = "vce_crackine_powder";
    public const string ITEM_ID = "vce_crackine_vial";
    public const string SPRITE = "vce_crackine_vial";
    // Large vial: 1 mL, 1x2 cells, four small vials' worth of value.
    public const string LARGE_ID = "vce_crackine_vial_l";
    public const string LARGE_SPRITE = "vce_crackine_vial_l";
    public const int LARGE_VOLUME = 1000;
    public const long LARGE_VALUE = 108;

    // One vial holds 0.25 mL of powder; euphoria 12 per uL puts 90% purity at 2700 (vanilla's top tier, >= 2200),
    // 60% at 1800 and 30% at 900 (weak).
    public const int REQUIRED_VOLUME = 250;
    public const double SALT_EUPHORIA = 12.0;

    // About 1.5 Dream Dust after markups (both contraband level 2): 27 vs 18.
    public const long BASE_VALUE = 27;
    public const int CONTRABAND_LEVEL = 2;
    public const int NARCOTIC_VALUE = 3;

    // Salt forming: at a boil, once the water is (almost) gone, each uL of alkaloid takes 0.1 uL of acid.
    public const double SALT_MIN_TEMP = 100.0;
    public const double SALT_MAX_WATER_PERCENT = 1.0;
    public const double ACID_PER_ALKALOID = 0.1;
    public const double SALT_RATE = 0.1;
    public const int SALT_MIN_UL = 200;

    // Evaporator: drives acid out of dry Crackine powder at the vanilla evaporation rate (300 mL per cycle).
    public const int EVAPORATOR_UL_PER_CYCLE = 300000;
    public const int EVAPORATOR_CONTAINER_SLOT = 2;

    public static void Register()
    {
        Liquid salt = IaLiquids.AddLiquid(SALT_ID, "liquid_crackine_powder", 5, "f0e2a8", 1);
        if (salt != null)
        {
            salt.canBeRemovedByPurifier = false;
            salt.basePurifierRemoveAmount = 0;
            salt.isEvaporative = false;
            salt.euphoria = SALT_EUPHORIA;
        }
        AddRecipe(ITEM_ID, "crackine_vial_name", REQUIRED_VOLUME);
        AddRecipe(LARGE_ID, "crackine_vial_l_name", LARGE_VOLUME);
    }

    private static void AddRecipe(string id, string nameKey, int volume)
    {
        if (FinisherRecipes.Find(id) != null)
        {
            return;
        }
        ChemFinisherRecipe recipe = new ChemFinisherRecipe();
        recipe.identifier = id;
        recipe.displayName = IaText.Get(nameKey);
        recipe.requiredVolume = volume;
        recipe.isRequireDry = true;
        recipe.activeIngredientId = SALT_ID;
        ChemFinisherRecipe.chemFinisherRecipes.Add(recipe);
    }

    public static void Unregister()
    {
        ChemFinisherRecipe.chemFinisherRecipes.RemoveAll(r => r.identifier == ITEM_ID || r.identifier == LARGE_ID);
    }

    public static void RefreshNames()
    {
        Liquid liquid;
        if (Liquid.LiquidDict.TryGetValue(SALT_ID, out liquid))
        {
            liquid.displayName = IaText.Get("liquid_crackine_powder");
        }
        ChemFinisherRecipe recipe = FinisherRecipes.Find(ITEM_ID);
        if (recipe != null)
        {
            recipe.displayName = IaText.Get("crackine_vial_name");
        }
        recipe = FinisherRecipes.Find(LARGE_ID);
        if (recipe != null)
        {
            recipe.displayName = IaText.Get("crackine_vial_l_name");
        }
    }

    public static GameItem CreateVial()
    {
        return Create(ITEM_ID, "crackine_vial_name", REQUIRED_VOLUME, BASE_VALUE, SPRITE);
    }

    public static GameItem CreateLargeVial()
    {
        return Create(LARGE_ID, "crackine_vial_l_name", LARGE_VOLUME, LARGE_VALUE, LARGE_SPRITE);
    }

    private static GameItem Create(string id, string nameKey, int volume, long value, string sprite)
    {
        GameItem item = ItemDirectory.CreateEmptyItem(id);
        try
        {
            item.name = IaText.Get(nameKey);
            item.shortDescription = IaText.Get("crackine_vial_desc");
            item.flavorText = IaText.Get("crackine_vial_flavor");
            item.unitValue = value;
            item.SetGameItemType("NARCOTIC");
            item.SetGameItemType("SUBSTANCE");
            ContrabandHelper.InitContrabandItem(item, CONTRABAND_LEVEL);
            ConsumableHelper.InitNarcotic(item, NARCOTIC_VALUE);
            ChemicalHelper.InitChemItem(item, true, true);
            // Default contents for vials not made in the Chem Finisher (debug spawns, shops): street grade.
            ChemFinisherRecipe.SetCompositionByPurity(item, 60, 80, volume, SALT_ID);
            ChemicalProductHelper.InitChemicalProduct(item, SALT_ID);
            ChemicalFeedbackHelper.InitChemicalIntentItem(item, ChemicalFeedbackHelper.ChemicalIntent.NARCOTIC);
            AudioHelper.InitTinyItem(item);
            BeerSprites.Apply(item, sprite);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            IaLog.Error("create " + id + " failed", e);
        }
        return item;
    }

    // From the temperature reaction hook (DreamToxinMod.cs).
    public static int Run(GameItem container)
    {
        if (container == null || WaterHelper.IsEmpty(container))
        {
            return 0;
        }
        int alkaloid = IaLiquids.Get(container, Xinke.ALKALOID_ID);
        int acid = IaLiquids.Get(container, "acid");
        if (alkaloid <= 0 || acid <= 0)
        {
            return 0;
        }
        if (container.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble() < SALT_MIN_TEMP)
        {
            return 0;
        }
        int total = WaterHelper.GetTotalVolume(container);
        int water = IaLiquids.Get(container, "water");
        if (total <= 0 || water * 100.0 / total > SALT_MAX_WATER_PERCENT)
        {
            return 0;
        }
        int amount = Math.Min(alkaloid, Math.Max(SALT_MIN_UL, (int)Math.Round(alkaloid * SALT_RATE)));
        // Acid owed by the alkaloid before this step minus acid owed by what is left after it: the per-step
        // rounding telescopes away, so a full run takes exactly 0.1 uL of acid per uL of alkaloid (50 mL : 5 mL).
        int acidUsed = AcidFor(alkaloid) - AcidFor(alkaloid - amount);
        if (acidUsed > acid)
        {
            // Integer math (acid / 0.1 can land just below a whole number and floor one short).
            amount = Math.Min(alkaloid, (int)Math.Round(acid / ACID_PER_ALKALOID));
            acidUsed = acid;
        }
        if (amount <= 0)
        {
            return 0;
        }
        IaLiquids.Set(container, Xinke.ALKALOID_ID, alkaloid - amount);
        IaLiquids.Set(container, "acid", acid - acidUsed);
        IaLiquids.Set(container, SALT_ID, IaLiquids.Get(container, SALT_ID) + amount);
        return 1;
    }

    private static int AcidFor(int alkaloid)
    {
        return (int)Math.Round(alkaloid * ACID_PER_ALKALOID);
    }

    // Dry Crackine powder with acid left in it: the only case where the evaporator removes acid.
    public static bool HasAcidToDriveOff(GameItem container)
    {
        if (container == null || WaterHelper.IsEmpty(container))
        {
            return false;
        }
        if (IaLiquids.Get(container, SALT_ID) <= 0 || IaLiquids.Get(container, "acid") <= 0)
        {
            return false;
        }
        foreach (Liquid liquid in Liquid.Liquids)
        {
            if (liquid.isEvaporative && IaLiquids.Get(container, liquid.identifier) > 0)
            {
                return false;
            }
        }
        return true;
    }

    public static void DriveOffAcid(GameItem machine)
    {
        GameSlotInventory slot = MachineHelper.GetSlot(machine, EVAPORATOR_CONTAINER_SLOT) as GameSlotInventory;
        GameItem container = slot != null ? slot.childItem : null;
        if (!HasAcidToDriveOff(container))
        {
            return;
        }
        GameSlotInventory battery = MachineHelper.GetBatterySlot(machine);
        if (battery == null || battery.childItem == null
            || !PowerHelper.DrawPowerSource(battery.childItem, MachineryHelper.GetMachinePowerUsage(machine)))
        {
            return;
        }
        float speed = Math.Max(0f, 1f + MachineryHelper.GetCurrentPerformanceBonus(machine) / 100f);
        int acid = IaLiquids.Get(container, "acid");
        int removed = Math.Min(acid, (int)Math.Round(EVAPORATOR_UL_PER_CYCLE * speed));
        IaLiquids.Set(container, "acid", acid - removed);
        WaterFeatureHelper.OnLiquidContainerPourOutOf(container);
        SpriteHelper.UpdateWaterContainerSprite(container);
    }

    public static void SpawnKit()
    {
        try
        {
            GraphUtils.TryAcceptAll(EmporiumEntry.Instance.invElement, CreateVial());
            GraphUtils.TryAcceptAll(EmporiumEntry.Instance.invElement, CreateLargeVial());
        }
        catch (Exception e)
        {
            IaLog.Error("Crackine debug kit failed", e);
        }
    }

    // Two small pharma-grade vials (92-97%), for testing the regular buyers.
    public static void SpawnPharma()
    {
        try
        {
            for (int i = 0; i < 2; i++)
            {
                GameItem item = CreateVial();
                ChemFinisherRecipe.SetCompositionByPurity(item, 92, 97, REQUIRED_VOLUME, SALT_ID);
                ChemicalProductHelper.GradeProduct(item);
                GraphUtils.TryAcceptAll(EmporiumEntry.Instance.invElement, item);
            }
        }
        catch (Exception e)
        {
            IaLog.Error("Crackine pharma kit failed", e);
        }
    }
}

// The evaporator lets acid go from dry Crackine powder; everywhere else acid stays non-volatile as in vanilla.
[HarmonyPatch(typeof(MachineEvaporator), "CanEvaporateContainer")]
public static class CrackineEvaporatorCheckPatch
{
    private static void Postfix(GameItem container, ref bool __result)
    {
        if (!__result && Crackine.HasAcidToDriveOff(container))
        {
            __result = true;
        }
    }
}

[HarmonyPatch(typeof(MachineEvaporator), "Evaporator")]
public static class CrackineEvaporatorCyclePatch
{
    private static void Postfix(GameItem __result)
    {
        try
        {
            if (__result == null)
            {
                return;
            }
            GameItem machine = __result;
            Action<GameItem, GameInventory, SlotMarker> vanilla = machine.onCycleEndSlotItemFunc;
            machine.onCycleEndSlotItemFunc = delegate(GameItem item, GameInventory inventory, SlotMarker slot)
            {
                GameSlotInventory containerSlot = MachineHelper.GetSlot(item, Crackine.EVAPORATOR_CONTAINER_SLOT) as GameSlotInventory;
                GameItem container = containerSlot != null ? containerSlot.childItem : null;
                if (Crackine.HasAcidToDriveOff(container))
                {
                    try
                    {
                        Crackine.DriveOffAcid(item);
                    }
                    catch (Exception e)
                    {
                        IaLog.Error("evaporating acid from Crackine powder failed", e);
                    }
                    return;
                }
                if (vanilla != null)
                {
                    vanilla(item, inventory, slot);
                }
            };
        }
        catch (Exception e)
        {
            IaLog.Error("evaporator patch failed", e);
        }
    }
}

// Pharma-grade Crackine hooks the street: on any day the player sells a vial at 90%+ real purity (whatever the label
// says) to a personal-use buyer, exactly one Crackine buyer comes the next day, however many vials were sold. The
// streak belongs to the store, not to a person: each consecutive day it triggers, the budget grows by 25%, up to
// 2000. The buyer wears the face of the best-paying of yesterday's buyers and gets lines that grow more desperate
// with the streak.
public static class CrackineRegulars
{
    public const double PHARMA_PURITY = 90.0;
    public const double BUDGET_GROWTH = 1.25;
    public const int BUDGET_CAP = 2000;
    private const string STREAK_KEY = "crackine_regular_streak";

    public static bool IsCrackine(string itemId)
    {
        return itemId == Crackine.ITEM_ID || itemId == Crackine.LARGE_ID;
    }

    // Before vanilla handles yesterday's sales: queue today's one Crackine buyer and keep vanilla from also reacting.
    public static void QueueRegulars()
    {
        List<SoldItem> sold = PlayerStore.Instance.soldItemsTracker;
        if (sold == null || sold.Count == 0)
        {
            return;
        }
        List<SoldItem> hooked = new List<SoldItem>();
        SoldItem face = null;
        foreach (SoldItem soldItem in sold)
        {
            if (soldItem == null || !IsCrackine(soldItem.itemId) || !soldItem.CanReturn || !soldItem.isPersonalUse
                || soldItem.factionId == "FACTION_TOURIST")
            {
                continue;
            }
            GameItem item = soldItem.GetGameItem();
            if (item == null || ChemicalProductHelper.GetPurity(item) < PHARMA_PURITY)
            {
                continue;
            }
            hooked.Add(soldItem);
            if (face == null || soldItem.soldPrice > face.soldPrice)
            {
                face = soldItem;
            }
        }
        if (face == null)
        {
            return;
        }
        int streak = NextStreak(StoreStation.Instance.dayCounter);
        StoreClient client = CreateRegular(face, streak);
        if (client != null)
        {
            PlayerStore.Instance.storeClientManager.chemicalFeedbackStack.Add(client);
            IaLog.Info("Crackine buyer queued: day " + streak + ", budget " + client.GetBudget() + ", face " + face.clientName
                + " (" + hooked.Count + " qualifying sale(s) yesterday)");
        }
        // Vanilla should not also send yesterday's Crackine buyers back for it, nor bring the chosen face a second
        // time for anything else that person bought.
        string faceKey = PersonKey(face);
        foreach (SoldItem soldItem in sold)
        {
            if (soldItem != null && (hooked.Contains(soldItem) || PersonKey(soldItem) == faceKey))
            {
                soldItem.CanReturn = false;
                soldItem.CanReturnAsBuyer = false;
            }
        }
    }

    private static string PersonKey(SoldItem soldItem)
    {
        return soldItem.clientName + "|" + soldItem.clientSprite;
    }

    // Consecutive days: if a Crackine buyer was queued yesterday, today's count is one more; otherwise it starts over.
    // (1.6.1 and older kept one key per person, "crackine_regular|name|sprite"; those are simply no longer read.)
    private static int NextStreak(int today)
    {
        string key = STREAK_KEY;
        int streak = 1;
        string saved = ModHelper.GetModData(DreamToxinMod.MOD_ID, key, null);
        if (!string.IsNullOrEmpty(saved))
        {
            string[] parts = saved.Split(',');
            int lastDay, lastStreak;
            if (parts.Length == 2 && int.TryParse(parts[0], out lastDay) && int.TryParse(parts[1], out lastStreak)
                && lastDay == today - 1)
            {
                streak = lastStreak + 1;
            }
        }
        ModHelper.SetModData(DreamToxinMod.MOD_ID, key, today + "," + streak);
        return streak;
    }

    public static int BudgetFor(int baseBudget, int streak)
    {
        double budget = baseBudget * Math.Pow(BUDGET_GROWTH, Math.Max(0, streak - 1));
        return (int)Math.Min(BUDGET_CAP, Math.Round(budget));
    }

    private static StoreClient CreateRegular(SoldItem soldItem, int streak)
    {
        // Vanilla's returning buyer (same face, name and faction), with our budget, wish list and lines.
        StoreClient client = StoreClientListSpec.CreateChemicalReturningClient(soldItem);
        if (client == null)
        {
            return null;
        }
        int baseBudget = Math.Max(50 + StoreStation.Instance.dayCounter / 5, soldItem.soldPrice * 3);
        client.SetBudget(BudgetFor(baseBudget, streak));
        if (client.GetBudget() > BUDGET_CAP)
        {
            client.OverrideBudget(BUDGET_CAP);
        }
        foreach (string id in new string[] { Crackine.ITEM_ID, Crackine.LARGE_ID })
        {
            if (!client.clientBuyingIdList.Contains(id))
            {
                client.clientBuyingIdList.Add(id);
            }
        }
        string tier = streak >= 7 ? "4" : streak >= 4 ? "3" : streak >= 2 ? "2" : "1";
        string name = soldItem.itemName;
        Dialogue line = client.mainDialogue;
        for (int i = 1; i <= 3 && line != null; i++)
        {
            line.SetText(client.displayName, IaText.Get("crackine_regular_" + tier + "_" + i, name, client.GetBudget()));
            line = line.nextDialogue;
        }
        return client;
    }
}

[HarmonyPatch(typeof(SoldItem), "HandleSoldItem")]
public static class CrackineRegularsPatch
{
    private static void Prefix()
    {
        try
        {
            CrackineRegulars.QueueRegulars();
        }
        catch (Exception e)
        {
            IaLog.Error("queueing Crackine regulars failed", e);
        }
    }
}
