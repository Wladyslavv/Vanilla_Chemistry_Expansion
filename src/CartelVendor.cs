// The Cartel Vendor: the only seller of this mod's machines, handbook and starting stock. He first comes on day 5
// and then every 7 days, and every visit lays out the whole range on the front counter, big items first:
//   fermenter, electrolyzer (each only 50% of visits once the player owns one), the HPLC and the handbook (each only
//   while the player has none), reloading press (50% once owned), a plutonium battery, an empty gas canister, a random strain, two Xinke seeds, two Hollow's Lantern
//   seeds.
// These used to come from Dr. Jackson, the retired chemist, the retired farmer and battery loot; none of them carry
// these goods any more. He does not depend on the Cartel Expansion setting (the handbook must stay obtainable), and
// he counts as a Lower Levels citizen so the cartel boycott perks never keep him away. Written for C# 5 (see
// DreamToxinMod.cs).
using System;
using System.Collections.Generic;

public static class CartelVendor
{
    public const string CLIENT_ID = "vce_cartel_vendor";
    public const string LAST_VISIT_KEY = "cartel_vendor_last_visit";
    public const int FIRST_DAY = 5;
    public const int INTERVAL_DAYS = 7;
    // Chance that a machine the player already owns is in stock again.
    public const int OWNED_MACHINE_CHANCE = 50;

    // The Ammo Printer mod (same author): when it is installed and enabled, its six ammo chips are in stock too.
    // Its items are only asked for while it runs, so players without it never see a missing-item error.
    public const string AMMO_PRINTER_MOD_ID = "ammo_printer";
    private static readonly string[] AmmoPrinterChips =
    {
        "ammo_printer.chip_22_nl", "ammo_printer.chip_10_nl", "ammo_printer.chip_22",
        "ammo_printer.chip_10", "ammo_printer.chip_22_ap", "ammo_printer.chip_10_ap",
    };

    public static void Enable()
    {
        ModHook.OnGenerateCustomerNormal += OnGenerateCustomers;
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Cartel vendor: visit now",
                delegate { PlayerStore.Instance.storeClientManager.AddClient(Create()); }, false);
        }
        catch (Exception e)
        {
            Log.Error("cartel vendor debug registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnGenerateCustomerNormal -= OnGenerateCustomers;
    }

    public static void OnGenerateCustomers(StoreClientManager manager)
    {
        try
        {
            int day = StoreStation.GetDayCounter();
            int last = ModHelper.GetModDataInt(DreamToxinMod.MOD_ID, LAST_VISIT_KEY, -INTERVAL_DAYS);
            if (manager == null || day < FIRST_DAY || day - last < INTERVAL_DAYS)
            {
                return;
            }
            ModHelper.SetModDataInt(DreamToxinMod.MOD_ID, LAST_VISIT_KEY, day);
            manager.AddClient(Create());
            Log.Info("cartel vendor visits on day " + day);
        }
        catch (Exception e)
        {
            Log.Error("cartel vendor visit failed", e);
        }
    }

    public static StoreClient Create()
    {
        StoreClient client = new StoreClient();
        client.identifier = CLIENT_ID;
        client.nonShootable = true;
        client.displayName = Text.Get("cartel_vendor_name");
        client.spriteName = SpriteDict.GetRandomShadySpriteName();
        client.clientFaction = "FACTION_LOWER_LEVEL";
        StoreClientFactionSetup.InitLowerLevelCitizen(client);
        client.clientIntent = StoreClient.ClientIntent.SELL;
        client.mainDialogue.SetText(client.displayName, Text.Get("cartel_vendor_line_1"))
            .NextDialogue().SetText(client.displayName, Text.Get("cartel_vendor_line_2"))
            .SetEndAction(LayOut);
        client.AddBasicDialog();
        client.CompleteClientCreation(false);
        ModHelper.SetClientSource(client, DreamToxinMod.MOD_ID);
        return client;
    }

    // Biggest first, so the machines get the room; whatever still does not fit is left off and logged.
    private static void LayOut()
    {
        try
        {
            List<GameItem> stock = new List<GameItem>();
            if (!Owns(Fermenter.IsFermenter) || RNG.Roll(OWNED_MACHINE_CHANCE))
            {
                stock.Add(Fermenter.CreateForSale());
            }
            if (!Owns(Electrolyzer.IsElectrolyzer) || RNG.Roll(OWNED_MACHINE_CHANCE))
            {
                stock.Add(Electrolyzer.CreateForSale());
            }
            // The HPLC, like the handbook, only while the player has none.
            if (!Owns(Hplc.IsHplc))
            {
                stock.Add(DirectoryMaster.Item(Hplc.ITEM_ID, false));
            }
            if (!Owns(ReloadingPress.IsReloadingPress) || RNG.Roll(OWNED_MACHINE_CHANCE))
            {
                stock.Add(DirectoryMaster.Item(ReloadingPress.ITEM_ID, false));
            }
            stock.Add(GuideBook.CreateForSaleIfUnowned());
            stock.Add(DirectoryMaster.Item(PuBattery.ITEM_ID, false));
            stock.Add(GasCanister.CreateForSale());
            stock.Add(Strain.Spawn(false, null, false));
            for (int i = 0; i < 2; i++)
            {
                stock.Add(Xinke.CreateSeed());
            }
            for (int i = 0; i < 2; i++)
            {
                stock.Add(Lantern.CreateSeed());
            }
            AddAmmoPrinterChips(stock);
            int placed = 0;
            foreach (GameItem item in stock)
            {
                if (item == null)
                {
                    continue;
                }
                string id = item.identifier;
                if (PlayerStore.Instance.TryAddDirectSellingItemToTable(item, false, false, false, 0))
                {
                    placed++;
                }
                else
                {
                    Log.Info("cartel vendor: no room on the front counter for " + id);
                }
            }
            Log.Info("cartel vendor laid out " + placed + " item(s)");
        }
        catch (Exception e)
        {
            Log.Error("cartel vendor stock failed", e);
        }
    }

    private static void AddAmmoPrinterChips(List<GameItem> stock)
    {
        if (!ModHelper.IsModRunning(AMMO_PRINTER_MOD_ID))
        {
            return;
        }
        foreach (string id in AmmoPrinterChips)
        {
            try
            {
                // A later Ammo Printer version might rename a chip: skip it rather than fail the whole stock.
                if (!DirectoryMaster.GetIdentifierList<GameItem>("ModItemDirectory").Contains(id))
                {
                    Log.Info("cartel vendor: Ammo Printer has no item " + id);
                    continue;
                }
                stock.Add(DirectoryMaster.Item(id, false));
            }
            catch (Exception e)
            {
                Log.Error("cartel vendor: Ammo Printer chip " + id + " failed", e);
            }
        }
    }

    private static bool Owns(Func<GameItem, bool> isMachine)
    {
        foreach (GameItem item in EmporiumEntry.Instance.GetAllOwnedItems())
        {
            if (item != null && isMachine(item))
            {
                return true;
            }
        }
        return false;
    }
}
