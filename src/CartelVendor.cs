// The Cartel Vendor: the only seller of this mod's machines, handbook and starting stock. He first comes on day 5
// and then every 7 days once the player has the Retired Chemist (Joe Wild's upgrade, or the chemist start),
// every 28 days without it. Every visit lays out his goods on the front counter: the handbook first and for certain
// while the player has none (so it is never the item crowded out), then, each rolled on its own (chances below), big
// items first: fermenter, electrolyzer, the HPLC, the condenser, reloading press, up to two separatory funnels, a
// plutonium battery, an empty gas canister, a random strain, up to two Xinke seeds and two Hollow's Lantern seeds, up
// to two flow hoses.
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
    // Weekly once the player has the Retired Chemist (Joe Wild's network upgrade; the chemist start comes with it),
    // otherwise once a "month": four weeks, so he keeps coming on the same weekday. The game has no month of its own.
    public const int INTERVAL_DAYS = 7;
    public const int MONTHLY_INTERVAL_DAYS = 28;
    // The last visit assumed before the first one: far enough back that day FIRST_DAY is due either way.
    public const int NEVER_VISITED = -MONTHLY_INTERVAL_DAYS;
    // Stock chances, per item and per visit (the handbook alone is certain while the player has none):
    //   a machine the player does not own: 70%; one already owned: 25% (fermenter, electrolyzer, reloading press) or
    //   never (HPLC, condenser); separatory funnels, flow hoses, the battery and the gas canister: 50% each; the strain
    //   and the seeds: 70% each.
    public const int NEW_MACHINE_CHANCE = 70;
    public const int OWNED_MACHINE_CHANCE = 25;
    public const int SUPPLY_CHANCE = 50;
    public const int LIVING_CHANCE = 70;
    // Up to two separatory funnels (2x4 each: with every machine in stock the 18x4 counter is nearly full).
    public const int SEP_FUNNELS = 2;
    // Up to two flow hoses (2x2), last of the mod's goods: on a crowded visit they are what is left off, not the seeds
    // or the strain.
    public const int HOSES = 2;

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
            int last = ModHelper.GetModDataInt(DreamToxinMod.MOD_ID, LAST_VISIT_KEY, NEVER_VISITED);
            if (manager == null || day < FIRST_DAY || day - last < IntervalDays())
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

    // Days between visits, checked on each day's customers: buying the Retired Chemist upgrade between two visits
    // shortens the wait at once.
    public static int IntervalDays()
    {
        try
        {
            return NetworkUpgrade.IsRetiredChemistUnlocked() ? INTERVAL_DAYS : MONTHLY_INTERVAL_DAYS;
        }
        catch (Exception e)
        {
            Log.Error("cartel vendor: reading the Retired Chemist upgrade failed", e);
            return INTERVAL_DAYS;
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

    // The handbook, then biggest first, so the machines get the room; whatever still does not fit is left off and logged.
    private static void LayOut()
    {
        try
        {
            List<GameItem> stock = new List<GameItem>();
            // The handbook first, and always, while the player has none: it must never be the item that finds no room.
            stock.Add(GuideBook.CreateForSaleIfUnowned());
            // Everything else is rolled item by item. Machines: likelier while the player has none; the HPLC and the
            // condenser never once owned (a condenser fitted to a heating plate, a still, counts as owned).
            if (RNG.Roll(MachineChance(Owns(Fermenter.IsFermenter), true)))
            {
                stock.Add(Fermenter.CreateForSale());
            }
            if (RNG.Roll(MachineChance(Owns(Electrolyzer.IsElectrolyzer), true)))
            {
                stock.Add(Electrolyzer.CreateForSale());
            }
            if (RNG.Roll(MachineChance(Owns(Hplc.IsHplc), false)))
            {
                stock.Add(DirectoryMaster.Item(Hplc.ITEM_ID, false));
            }
            if (RNG.Roll(MachineChance(Owns(item => Condenser.IsCondenser(item) || Condenser.IsStill(item)), false)))
            {
                stock.Add(DirectoryMaster.Item(Condenser.ITEM_ID, false));
            }
            if (RNG.Roll(MachineChance(Owns(ReloadingPress.IsReloadingPress), true)))
            {
                stock.Add(DirectoryMaster.Item(ReloadingPress.ITEM_ID, false));
            }
            for (int i = 0; i < SEP_FUNNELS; i++)
            {
                if (RNG.Roll(SUPPLY_CHANCE))
                {
                    stock.Add(DirectoryMaster.Item(SepFunnel.ITEM_ID, false));
                }
            }
            if (RNG.Roll(SUPPLY_CHANCE))
            {
                stock.Add(DirectoryMaster.Item(PuBattery.ITEM_ID, false));
            }
            if (RNG.Roll(SUPPLY_CHANCE))
            {
                stock.Add(GasCanister.CreateForSale());
            }
            if (RNG.Roll(LIVING_CHANCE))
            {
                stock.Add(Strain.Spawn(false, null, false));
            }
            for (int i = 0; i < 2; i++)
            {
                if (RNG.Roll(LIVING_CHANCE))
                {
                    stock.Add(Xinke.CreateSeed());
                }
            }
            for (int i = 0; i < 2; i++)
            {
                if (RNG.Roll(LIVING_CHANCE))
                {
                    stock.Add(Lantern.CreateSeed());
                }
            }
            for (int i = 0; i < HOSES; i++)
            {
                if (RNG.Roll(SUPPLY_CHANCE))
                {
                    stock.Add(DirectoryMaster.Item(Hose.ITEM_ID, false));
                }
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

    // A machine's chance this visit: NEW_MACHINE_CHANCE while the player has none; once owned, OWNED_MACHINE_CHANCE if
    // it may be sold again, else 0.
    private static int MachineChance(bool owned, bool soldAgain)
    {
        if (!owned)
        {
            return NEW_MACHINE_CHANCE;
        }
        return soldAgain ? OWNED_MACHINE_CHANCE : 0;
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
