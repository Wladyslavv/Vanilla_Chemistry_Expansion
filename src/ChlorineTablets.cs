// Disinfectant tablets from chlorine gas: a gas canister (GasCanister.cs) goes straight into the Chem Finisher's
// container slot, and the recipe "Disinfectant tablets" presses 100 mL of its chlorine into a vanilla box of nine
// water purification tablets (each vanilla tablet holds 15 mL of the vanilla liquid "chlorine").
// How it hooks into the vanilla Chem Finisher, which only knows liquid containers:
//   - its container slot also takes an owned gas canister;
//   - CanOutput: with a canister in the slot only this recipe can run, and only with 100 mL of chlorine or more;
//     this recipe never runs from a liquid container;
//   - the press calls TransferLiquidToChem(container, product, 100 mL): from a canister it takes the chlorine gas
//     instead and gives the product as much of the vanilla liquid "chlorine";
//   - the finisher then adds its usual impurity (module loss) and calls GradeProduct, where the pressed product is
//     swapped for a tablet box in the same output grid, each tablet holding a ninth of what was pressed.
// Written for C# 5 (see DreamToxinMod.cs).
using System;
using HarmonyLib;

public static class ChlorineTablets
{
    public const string ITEM_ID = "vce_chlorine_tablets";      // the recipe, and the item the finisher presses
    public const int REQUIRED_VOLUME = 100000;
    public const string LIQUID = "chlorine";                    // vanilla: what a water purification tablet holds
    public const int TABLETS = 9;

    public static void Register()
    {
        if (FinisherRecipes.Find(ITEM_ID) != null)
        {
            return;
        }
        ChemFinisherRecipe recipe = new ChemFinisherRecipe();
        recipe.identifier = ITEM_ID;
        recipe.displayName = Text.Get("chlorine_tablets_name");
        recipe.requiredVolume = REQUIRED_VOLUME;
        recipe.isRequireDry = false;
        recipe.activeIngredientId = LIQUID;
        ChemFinisherRecipe.chemFinisherRecipes.Add(recipe);
    }

    public static void Unregister()
    {
        ChemFinisherRecipe.chemFinisherRecipes.RemoveAll(r => r.identifier == ITEM_ID);
    }

    public static void RefreshNames()
    {
        ChemFinisherRecipe recipe = FinisherRecipes.Find(ITEM_ID);
        if (recipe != null)
        {
            recipe.displayName = Text.Get("chlorine_tablets_name");
        }
    }

    public static void AddItems(ModItemDirectory directory)
    {
        directory.Add(ITEM_ID, Create);
    }

    // Only lives for a moment between the press and GradeProduct; if the swap fails it stays a plain chemical item.
    public static GameItem Create()
    {
        GameItem item = ItemDirectory.CreateEmptyItem(ITEM_ID);
        try
        {
            item.SetSpriteAndShape("Items/items_amenities", "water_tablet_container");
            item.name = Text.Get("chlorine_tablets_name");
            item.shortDescription = Text.Get("chlorine_tablets_desc");
            item.unitValue = 50;
            item.SetGameItemType("HOUSEHOLD_GOOD");
            ChemicalHelper.InitChemItem(item, true, true);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            Log.Error("create " + ITEM_ID + " failed", e);
        }
        return item;
    }

    public static bool IsSelected(GameItem machine)
    {
        return machine != null && machine.IsTag("SELECTED_CHEM_ID_TAG")
            && machine.GetTagReadonly("SELECTED_CHEM_ID_TAG").GetString() == ITEM_ID;
    }

    // The pressed item into a vanilla box of tablets, in the same place. Returns false if it could not.
    public static bool SwapForBox(GameItem pressed)
    {
        GraphNodeStorage parent = pressed.parentInventory;
        if (parent == null)
        {
            return false;
        }
        bool owned = GeneralHelper.IsItemOwned(pressed);
        GameItem box = DirectoryMaster.Item("water_tablet_container", owned);
        if (box == null)
        {
            return false;
        }
        for (int i = 0; i < TABLETS; i++)
        {
            GameItem tablet = DirectoryMaster.Item("water_tablet", owned);
            if (tablet == null)
            {
                continue;
            }
            ChemicalHelper.RemoveAllLiquid(tablet);
            foreach (Liquid liquid in Liquid.Liquids)
            {
                int amount = pressed.GetTagReadonly(liquid.currentPart).GetInt();
                if (amount > 0)
                {
                    ChemicalHelper.SetChemContentUL(tablet, liquid.identifier, amount / TABLETS);
                }
            }
            GraphUtils.TryAcceptAll(box, tablet);
        }
        pressed.Destroy();
        GraphUtils.TryAcceptAll(parent, box);
        if (box.parentInventory == null)
        {
            GraphUtils.EmporiumTryAdd(box);
        }
        return true;
    }
}

// The container slot also takes a gas canister.
[HarmonyPatch(typeof(MachineChemFinisher), "ChemFinisher")]
public static class ChemFinisherCanisterSlotPatch
{
    private static void Postfix(GameItem __result)
    {
        try
        {
            GameSlotInventory slot = null;
            for (int i = 0; i < 6 && slot == null; i++)
            {
                slot = MachineHelper.GetBodyStorage(__result, i) as GameSlotInventory;
            }
            if (slot == null)
            {
                Log.Info("chem finisher container slot not found; gas canisters cannot go in");
                return;
            }
            Func<GameItem, GameInventory, bool> mayAdd = slot.mayInventoryAddItemFunc;
            slot.mayInventoryAddItemFunc = (GameItem item, GameInventory inventory) =>
                (mayAdd != null && mayAdd(item, inventory)) || (GasCanister.IsCanister(item) && GeneralHelper.IsItemOwned(item));
        }
        catch (Exception e)
        {
            Log.Error("chem finisher canister slot patch failed", e);
        }
    }
}

// A canister can only feed the tablet recipe, and the tablet recipe only takes a canister.
[HarmonyPatch(typeof(MachineChemFinisher), "CanOutput")]
public static class ChemFinisherCanisterOutputPatch
{
    private static bool Prefix(GameItem machine, GameSlotInventory containerSlot, ref bool __result)
    {
        try
        {
            GameItem child = containerSlot != null ? containerSlot.childItem : null;
            bool tablets = ChlorineTablets.IsSelected(machine);
            if (GasCanister.IsCanister(child))
            {
                __result = tablets && GasCanister.Amount(child, GasCanister.CHLORINE) >= ChlorineTablets.REQUIRED_VOLUME;
                return false;
            }
            if (tablets)
            {
                __result = false;
                return false;
            }
        }
        catch (Exception e)
        {
            Log.Error("chem finisher canister check failed", e);
        }
        return true;
    }
}

// Pressing from a canister takes chlorine gas and gives the product as much vanilla chlorine.
[HarmonyPatch(typeof(ChemicalHelper), "TransferLiquidToChem")]
public static class ChemFinisherCanisterTransferPatch
{
    private static bool Prefix(GameItem container, GameItem chemicalItem, int volumeInPart)
    {
        if (!GasCanister.IsCanister(container))
        {
            return true;
        }
        try
        {
            int taken = GasCanister.Take(container, GasCanister.CHLORINE, volumeInPart);
            ChemicalHelper.SetChemContentUL(chemicalItem, ChlorineTablets.LIQUID, taken);
        }
        catch (Exception e)
        {
            Log.Error("taking chlorine from a canister failed", e);
        }
        return false;
    }
}

// Right after pressing, the finisher grades the product: swap ours for a box of tablets instead.
[HarmonyPatch(typeof(ChemicalProductHelper), "GradeProduct")]
public static class ChlorineTabletsGradePatch
{
    private static bool Prefix(GameItem item)
    {
        if (item == null || item.identifier != ChlorineTablets.ITEM_ID)
        {
            return true;
        }
        try
        {
            ChlorineTablets.SwapForBox(item);
        }
        catch (Exception e)
        {
            Log.Error("swapping pressed chlorine for a tablet box failed", e);
        }
        return false;
    }
}

// The finisher checks the (now swapped) product for the lab-purity achievement last; skip ours.
[HarmonyPatch(typeof(AchievementTracker), "CheckLabPure")]
public static class ChlorineTabletsAchievementPatch
{
    private static bool Prefix(GameItem outputItem)
    {
        return outputItem == null || outputItem.identifier != ChlorineTablets.ITEM_ID;
    }
}
