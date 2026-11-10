// Rock salt ore, the raw material for the planned brine electrolysis line. It is ordered from GP Mining like the
// vanilla metal ore: two extra entries on Caulson's phone menu (6 ores in 1 day, or 12 in 2 days once 350 has been
// spent with GP Mining), delivered by the same courier at the same wholesale price, sharing the 5-day cooldown.
// Salt is softer than metal ore: any blender crushes it (with vanilla's content loss for missing chem modules),
// giving mostly salt plus a little clay (physical contaminant) and mineral. Written for C# 5 (see DreamToxinMod.cs).
using System;
using HarmonyLib;

public static class SaltOre
{
    public const string ITEM_ID = "vce_salt_ore";
    public const string SPRITE = "vce_salt_ore";
    // Same as the vanilla metal ore.
    public const long VALUE = 15;

    // Orders mirror vanilla's: GP Mining's stockType 0 / 1 are the 6 / 12 metal ore orders; ours use their own values.
    public const int STOCK_SMALL = 1001;
    public const int STOCK_LARGE = 1002;
    public const int SMALL_QUANTITY = 6;
    public const int SMALL_DAYS = 1;
    public const int LARGE_QUANTITY = 12;
    public const int LARGE_DAYS = 2;
    // Vanilla's threshold for the 12-ore order (PhoneDialogList.MINING_UNLOCK_THRESHOLD).
    public const int LARGE_UNLOCK_SPENT = 350;
    public const string COURIER_ID = "courrierMining";

    // Salt dissolves (particle size 5, passes every filter) and does not boil off.
    public const string SALT_ID = "vce_salt";
    // Millilitres per ore at no blender loss: twice the powder of a metal ore (about 40 mL), nearly all salt.
    public const double SALT_ML = 80.0;
    public const double CLAY_ML = 4.0;
    public const double MINERAL_ML = 3.0;

    // From Content.EnsureRegistered, once the game language is set.
    public static void Register()
    {
        Liquid salt = IaLiquids.AddLiquid(SALT_ID, "liquid_salt", 5, "f1ece6", 1);
        if (salt != null)
        {
            salt.isEvaporative = false;
        }
    }

    public static void RefreshNames()
    {
        Liquid liquid;
        if (Liquid.LiquidDict.TryGetValue(SALT_ID, out liquid))
        {
            liquid.displayName = Text.Get("liquid_salt");
        }
    }

    public static void Enable()
    {
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Salt: 3 salt ores", SpawnKit, false);
        }
        catch (Exception e)
        {
            Log.Error("salt ore debug function registration failed", e);
        }
    }

    public static void AddItems(ModItemDirectory directory)
    {
        directory.Add(ITEM_ID, Create);
    }

    public static GameItem Create()
    {
        GameItem item = ItemDirectory.CreateEmptyItem(ITEM_ID);
        try
        {
            item.name = Text.Get("salt_ore_name");
            item.shortDescription = Text.Get("salt_ore_desc");
            item.unitValue = VALUE;
            // An ore like the vanilla ones: the mining ship buys it and station ore discounts apply.
            item.EnableTag("ORE_TAG", false);
            item.SetGameItemType("MATERIAL");
            item.SetGameItemType("BASIC_MATERIAL");
            AudioHelper.InitSolidMetal(item);
            // Blendable in any blender; vanilla's module loss (35% with none, 0 with four) still applies.
            ChemicalHelper.InitChemItem(item, true, false);
            ChemicalHelper.SetChemContentML(item, SALT_ID, SALT_ML);
            ChemicalHelper.SetChemContentML(item, "physical_contaminant", CLAY_ML);
            ChemicalHelper.SetChemContentML(item, "mineral", MINERAL_ML);
            BeerSprites.Apply(item, SPRITE);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            Log.Error("create " + ITEM_ID + " failed", e);
        }
        return item;
    }

    public static int QuantityFor(int stockType)
    {
        return stockType == STOCK_SMALL ? SMALL_QUANTITY : stockType == STOCK_LARGE ? LARGE_QUANTITY : 0;
    }

    public static void SpawnKit()
    {
        try
        {
            for (int i = 0; i < 3; i++)
            {
                GameItem item = DirectoryMaster.Item(ITEM_ID, true);
                GraphUtils.TryAcceptAll(EmporiumEntry.Instance.invElement, item);
            }
        }
        catch (Exception e)
        {
            Log.Error("salt ore debug kit failed", e);
        }
    }
}

// Caulson's menu: our two orders go after vanilla's, before "end call".
[HarmonyPatch(typeof(PhoneDialogList), "MiningRegular")]
public static class SaltOrderPhonePatch
{
    private static void Postfix(Dialogue __result)
    {
        try
        {
            // A quick-dial call returns the menu itself; a normal call returns the greeting that leads to it.
            Dialogue menu = __result;
            if (menu != null && menu.choices.Count == 0)
            {
                menu = menu.nextDialogue;
            }
            if (menu == null || menu.choices.Count == 0)
            {
                Log.Info("GP Mining menu not found; salt ore orders not added");
                return;
            }
            StorePhoneClient mining = StorePhoneClient.GetMiningPhoneClient();
            int insertAt = menu.choices.Count - 1;
            AddOrder(menu, insertAt++, SaltOre.STOCK_SMALL, "salt_order_small", SaltOre.SMALL_QUANTITY, SaltOre.SMALL_DAYS);
            if (mining != null && mining.amountSpent >= SaltOre.LARGE_UNLOCK_SPENT)
            {
                AddOrder(menu, insertAt, SaltOre.STOCK_LARGE, "salt_order_large", SaltOre.LARGE_QUANTITY, SaltOre.LARGE_DAYS);
            }
        }
        catch (Exception e)
        {
            Log.Error("adding salt ore orders failed", e);
        }
    }

    // Same shape as vanilla's orders: the player's line, then Caulson's "ready", which ends the call.
    private static void AddOrder(Dialogue menu, int index, int stockType, string textKey, int quantity, int days)
    {
        string caulson = LocHelper.GetLocalizedName("name_caulson");
        Dialogue order = new Dialogue();
        order.SetText("PLAYER", Text.Get(textKey, quantity, days));
        // The "$" icon vanilla uses for its orders; isOwned is passed so C# 5 binds the string overload.
        menu.AddChoice(DirectoryMaster.Item("$", true), "", "", order, delegate
        {
            StorePhoneClient mining = StorePhoneClient.GetMiningPhoneClient();
            mining.stockType = stockType;
            mining.UseService();
            PlayerStore.Instance.QueueFuturClient(SaltOre.COURIER_ID, days);
            order.NextDialogue().SetText(caulson, LocHelper.GetLocalizedDialoguePhone("caulson_ready")).SetEndAction(delegate
            {
                PhoneUIManager.Instance.OnPhoneDialogEnd();
            });
        });
        // AddChoice appends; move it in front of "end call".
        int last = menu.choices.Count - 1;
        var choice = menu.choices[last];
        menu.choices.RemoveAt(last);
        menu.choices.Insert(Math.Max(0, Math.Min(index, menu.choices.Count)), choice);
    }
}

// The GP Mining courier lays out the ordered stock in its greeting's end action; vanilla only knows stock types 0
// and 1, so for ours it lays out nothing and we add the salt ores after it.
[HarmonyPatch(typeof(StoreClientListSupplier), "CreateCourrierMining")]
public static class SaltOrderCourierPatch
{
    private static void Postfix(StoreClient __result)
    {
        try
        {
            if (__result == null || __result.mainDialogue == null)
            {
                return;
            }
            Dialogue line = __result.mainDialogue;
            while (line != null && line.endAction == null)
            {
                line = line.nextDialogue;
            }
            if (line == null)
            {
                Log.Info("GP Mining courier has no stocking action; salt ore orders cannot be delivered");
                return;
            }
            Action vanilla = line.endAction;
            line.endAction = delegate
            {
                vanilla();
                try
                {
                    StorePhoneClient mining = StorePhoneClient.GetMiningPhoneClient();
                    int count = mining != null ? SaltOre.QuantityFor(mining.stockType) : 0;
                    for (int i = 0; i < count; i++)
                    {
                        PlayerStore.Instance.AddDirectSellingItemToTable(SaltOre.ITEM_ID, false, false, false, 0);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("delivering salt ore failed", e);
                }
            };
        }
        catch (Exception e)
        {
            Log.Error("GP Mining courier patch failed", e);
        }
    }
}
