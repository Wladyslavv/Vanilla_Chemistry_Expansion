// Every item this mod adds, in every picture it can show, laid out in three vanilla Hypertech Storage Bays (two
// 16x30 tabs each, and they take machines) that go into the player's inventory. For checking art:
//   bay 1  machines:      the electrolyzer in all eight slot states (_000.._111), the fermenter idle and running, the
//                         HPLC, the reloading press, the separatory funnel empty and with two layers, the condenser dry
//                         and filled | the still in its four pictures (dry / coolant, with and without a container on
//                         the plate) and four hydroponic machines growing chili and Xinke, each growing (_p) and
//                         ripening (_p2; the chili seed is made perennial for that, as only perennials ripen there)
//   bay 2  crops & goods: chili and Xinke leaves small / normal / large, seeds, strain and frozen strain, salt ore,
//                         nitrocellulose small / normal / large, nuclear waste opened and vitrified, plutonium ampoule
//                         | the injector, incapacitant, crackine vials, antibiotic, dynamite, blended wine, chlorine
//                         tablets, advanced Kotton fabric, beer keg, can, draft mug and flat mug
//   bay 3  canisters etc: gas canister empty and with each gas and a mix, the plutonium battery at every charge picture
//                         | the cartel envelope and six supply packs, the handbook and every handnote
// Machines get no battery, so nothing runs overnight and the pictures stay (the draft mug still goes flat overnight,
// as every draft beer does). Textures that are never an item picture are left out: the electrolyzer's lines and
// switches, the HPLC column segments and the customer portraits; the plain vce_electrolyzer is cover art only.
// Given from the debug panel or, once, when a save carrying the mod data flag "vanilla_chem_ext:showcase_kit" = "1"
// is loaded (the flag turns to "given" first, like AntibioticTestKit). Whatever fits nowhere is logged. Written for
// C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;

public static class ShowcaseKit
{
    public const string FLAG_KEY = "showcase_kit";
    public const string BAY_ID = "hypertech_bay";
    public const string HYDROPONIC_ID = "hydroponic";
    public const string CONTAINER_ID = "bottled_water";

    public static void Enable()
    {
        ModHook.OnGameLoadedLate += OnGameLoaded;
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Showcase: every item in storage bays", Give, false);
        }
        catch (Exception e)
        {
            Log.Error("showcase kit debug function registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnGameLoadedLate -= OnGameLoaded;
    }

    private static void OnGameLoaded()
    {
        try
        {
            if (ModHelper.GetModData(DreamToxinMod.MOD_ID, FLAG_KEY, null) == AntibioticTestKit.FLAG_PENDING)
            {
                ModHelper.SetModData(DreamToxinMod.MOD_ID, FLAG_KEY, AntibioticTestKit.FLAG_GIVEN);
                Log.Info("this save asked for the showcase kit");
                Give();
            }
        }
        catch (Exception e)
        {
            Log.Error("giving the showcase kit on load failed", e);
        }
    }

    private static List<string> missing;
    private static int placed;

    public static void Give()
    {
        missing = new List<string>();
        placed = 0;
        try
        {
            GameItem machines = Bay("机器", "蒸馏与水培");
            FillMachines(Tab(machines, 0));
            FillStillsAndPlants(Tab(machines, 1));
            Store(machines, "machines bay");

            GameItem goods = Bay("作物与原料", "产品");
            FillCrops(Tab(goods, 0));
            FillProducts(Tab(goods, 1));
            Store(goods, "crops and goods bay");

            GameItem misc = Bay("气罐与电池", "卡特尔与资料");
            FillCanisters(Tab(misc, 0));
            FillCartelAndNotes(Tab(misc, 1));
            Store(misc, "canisters bay");
        }
        catch (Exception e)
        {
            Log.Error("showcase kit failed", e);
        }
        if (missing.Count == 0)
        {
            Log.Info("showcase kit: all " + placed + " item(s) placed");
        }
        else
        {
            Log.Info("showcase kit: " + placed + " item(s) placed; not placed: " + string.Join(", ", missing.ToArray()));
        }
    }

    // ---- the bays ----------------------------------------------------------------------------------------------

    private static GameItem Bay(string tab0, string tab1)
    {
        GameItem bay = DirectoryMaster.Item(BAY_ID, true);
        if (bay == null)
        {
            missing.Add("storage bay");
            return null;
        }
        StorageExpansionHelper.SetTabName(bay, 0, tab0);
        StorageExpansionHelper.SetTabName(bay, 1, tab1);
        return bay;
    }

    // A bay tab's grid: the window holds a row with a "storage" panel whose content is the tabs (vanilla
    // StorageExpansionHelper.GetTabs), each page an intake around the grid.
    private static GameGridInventory Tab(GameItem bay, int index)
    {
        if (bay == null || bay.contentWindow == null)
        {
            return null;
        }
        MachineryPanelRow row = bay.contentWindow.childElement as MachineryPanelRow;
        MachineryPanel panel = row != null ? row.GetPanel("storage") : null;
        MachineryTabs tabs = panel != null ? panel.content as MachineryTabs : null;
        if (tabs == null || index >= tabs.count)
        {
            return null;
        }
        MachineryIntake intake = tabs.GetPage(index) as MachineryIntake;
        return intake != null ? intake.content as GameGridInventory : null;
    }

    private static void Store(GameItem bay, string what)
    {
        if (bay == null)
        {
            return;
        }
        GraphUtils.EmporiumTryAdd(bay);
        if (bay.parentInventory == null)
        {
            missing.Add(what + " (no room in the shop)");
        }
    }

    private static void Put(GameGridInventory grid, GameItem item, string what)
    {
        if (item == null || grid == null)
        {
            missing.Add(what);
            return;
        }
        try
        {
            GraphUtils.TryAcceptAll(grid, item);
            if (item.parentInventory == null)
            {
                missing.Add(what);
                item.Destroy();
                return;
            }
            placed++;
        }
        catch (Exception e)
        {
            Log.Error("showcase kit: placing " + what + " failed", e);
            missing.Add(what);
        }
    }

    private static GameItem Item(string id)
    {
        GameItem item = DirectoryMaster.Item(id, true);
        if (item == null)
        {
            Log.Info("showcase kit: no item called " + id);
        }
        return item;
    }

    private static void Slot(GameItem machine, int storage, GameItem item)
    {
        GraphNodeStorage slot = MachineHelper.GetBodyStorage(machine, storage);
        if (slot != null && item != null)
        {
            GraphUtils.TryAcceptAll(slot, item);
        }
    }

    // ---- bay 1: machines ---------------------------------------------------------------------------------------

    private static void FillMachines(GameGridInventory grid)
    {
        // Electrolyzer: _XYZ = feed container (storage 2), product A (3), product B (4) occupied. Marked stocked so the
        // first tick does not fill the product slots with canisters.
        for (int bits = 0; bits < 8; bits++)
        {
            GameItem e = Item(Electrolyzer.ITEM_ID);
            if (e != null)
            {
                e.EnableTag(Electrolyzer.STOCKED_TAG, true);
                if ((bits & 4) != 0)
                {
                    Slot(e, 2, Item(CONTAINER_ID));
                }
                if ((bits & 2) != 0)
                {
                    Slot(e, 3, Item(GasCanister.ITEM_ID));
                }
                if ((bits & 1) != 0)
                {
                    Slot(e, 4, Item(GasCanister.ITEM_ID));
                }
                Electrolyzer.ApplySprite(e);
            }
            Put(grid, e, "electrolyzer _" + ((bits >> 2) & 1) + ((bits >> 1) & 1) + (bits & 1));
        }

        // Fermenter idle, and running (a strain in storage 3).
        for (int running = 0; running < 2; running++)
        {
            GameItem f = Item(Fermenter.ITEM_ID);
            if (f != null)
            {
                f.EnableTag(Fermenter.STOCKED_TAG, true);
                if (running == 1)
                {
                    Slot(f, 3, Strain.Spawn(true, null, false));
                }
                Fermenter.ApplySprite(f);
            }
            Put(grid, f, running == 1 ? "fermenter (running)" : "fermenter");
        }

        Put(grid, Item(Hplc.ITEM_ID), "HPLC");
        Put(grid, Item(ReloadingPress.ITEM_ID), "reloading press");

        Put(grid, Item(SepFunnel.ITEM_ID), "separatory funnel (empty)");
        GameItem funnel = Item(SepFunnel.ITEM_ID);
        if (funnel != null)
        {
            WaterHelper.AddLiquid(funnel, FlavorChemistry.TOLUENE, 600000);
            WaterHelper.AddLiquid(funnel, "water", 900000);
        }
        Put(grid, funnel, "separatory funnel (toluene over water)");

        Put(grid, Item(Condenser.ITEM_ID), "condenser (dry)");
        GameItem condenser = Item(Condenser.ITEM_ID);
        if (condenser != null)
        {
            WaterHelper.AddLiquid(condenser, "water", Condenser.CAPACITY_ML * 1000);
        }
        Put(grid, condenser, "condenser (water)");
        Put(grid, Item(Hose.ITEM_ID), "flow hose");
    }

    private static void FillStillsAndPlants(GameGridInventory grid)
    {
        // The still: _b with a container on the plate (storage 1), _water with coolant in the still itself.
        for (int i = 0; i < 4; i++)
        {
            bool onPlate = (i & 2) != 0, water = (i & 1) != 0;
            GameItem still = Item(Condenser.STILL_ID);
            if (still != null)
            {
                if (water)
                {
                    WaterHelper.AddLiquid(still, "water", Condenser.CAPACITY_ML * 1000);
                }
                if (onPlate)
                {
                    Slot(still, 1, Item(CONTAINER_ID));
                }
            }
            Put(grid, still, "still" + (onPlate ? " _b" : "") + (water ? " _water" : " _dry"));
        }

        Hydroponic(grid, Lantern.SEED_ID, false, "chili");
        Hydroponic(grid, Lantern.SEED_ID, true, "chili");
        Hydroponic(grid, Xinke.SEED_ID, false, "Xinke");
        Hydroponic(grid, Xinke.SEED_ID, true, "Xinke");
    }

    // A vanilla hydroponic machine with the seed growing: the plant picture shows while the machine is working.
    // Ripening (_p2) needs a perennial seed at 4/5 of its growth. No water or battery, so nights do not move it on.
    private static void Hydroponic(GameGridInventory grid, string seedId, bool ripening, string what)
    {
        string label = "hydroponic " + what + (ripening ? " (ripening)" : " (growing)");
        GameItem machine = Item(HYDROPONIC_ID);
        GameItem seed = Item(seedId);
        if (machine == null || seed == null)
        {
            missing.Add(label);
            return;
        }
        if (ripening && !HydroponicHelper.IsPerennial(seed))
        {
            seed.EnableTag("PERENNIAL_TAG", true);
        }
        GraphUtils.TryAcceptAll(MachineHydroponic.GetInputSlot(machine), seed);
        if (seed.parentInventory == null)
        {
            seed.Destroy();
            machine.Destroy();
            missing.Add(label + ": the seed did not go in");
            return;
        }
        HydroponicHelper.StartGrowing(seed, machine);
        if (ripening)
        {
            int required = seed.GetTagReadonly("REQUIRED_PROGRESS").GetInt();
            int ripe = Math.Max(1, required * 4 / 5);
            seed.ModifyTag("GROWING_PROGRESS", delegate(TagState state) { state.Enable().SetInt(ripe); }, false);
        }
        Put(grid, machine, label);
    }

    // ---- bay 2: crops and goods --------------------------------------------------------------------------------

    private static void FillCrops(GameGridInventory grid)
    {
        int[] sizes = { Xinke.SMALL, Xinke.NORMAL, Xinke.LARGE };
        foreach (int size in sizes)
        {
            GameItem leaf = Item(Xinke.LEAF_ID);
            if (leaf != null)
            {
                int s = size;
                leaf.ModifyTag(Xinke.SIZE_TAG, delegate(TagState state) { state.Enable().SetInt(s); }, false);
                Xinke.ApplySize(leaf);
            }
            Put(grid, leaf, "Xinke leaf " + size);
        }
        Put(grid, Item(Xinke.SEED_ID), "Xinke seed");

        int[] chiliSizes = { Lantern.SMALL, Lantern.NORMAL, Lantern.LARGE };
        foreach (int size in chiliSizes)
        {
            GameItem chili = Item(Lantern.CHILI_ID);
            if (chili != null)
            {
                int s = size;
                chili.ModifyTag(Lantern.SIZE_TAG, delegate(TagState state) { state.Enable().SetInt(s); }, false);
                Lantern.ApplySize(chili);
            }
            Put(grid, chili, "chili " + size);
        }
        Put(grid, Item(Lantern.SEED_ID), "chili seed");

        Put(grid, Strain.Spawn(true, null, false), "strain");
        Put(grid, Item(Strain.FROZEN_ID), "frozen strain");
        Put(grid, Item(SaltOre.ITEM_ID), "rock salt ore");
        foreach (string id in new string[] { "vce_nitrocellulose_s", "vce_nitrocellulose", "vce_nitrocellulose_l" })
        {
            Put(grid, Item(id), id);
        }
        Put(grid, Item(Barrel.OPENED_ID), "opened nuclear waste");
        Put(grid, Item(Barrel.VITRIFIED_ID), "vitrified waste");
        Put(grid, Item(Ampoule.ITEM_ID), "plutonium ampoule");
    }

    private static void FillProducts(GameGridInventory grid)
    {
        Put(grid, Item(ToxinProduct.ITEM_ID), "Dream Toxin injector");
        Put(grid, Item(Lantern.ITEM_ID), "incapacitant vial");
        Put(grid, Item(Crackine.ITEM_ID), "crackine vial");
        Put(grid, Item(Crackine.LARGE_ID), "large crackine vial");
        Put(grid, Item(Antibiotics.TABLET_ID), "antibiotic tablet");
        Put(grid, Item(Dynamite.ITEM_ID), "dynamite");
        Put(grid, Item(BlendedWine.ITEM_ID), "blended wine");
        Put(grid, Item(ChlorineTablets.ITEM_ID), "disinfectant tablets");
        Put(grid, Item(AdvancedFabric.ITEM_ID), "advanced Kotton fabric");
        Put(grid, Item(Beer.KEG_ID), "beer keg");
        Put(grid, Item(Beer.CAN_ID), "beer can");
        Put(grid, Item(Beer.MUG_ID), "draft beer");
        GameItem flat = Item(Beer.MUG_ID);
        if (flat != null)
        {
            Beer.MakeFlat(flat);
        }
        Put(grid, flat, "flat beer");
    }

    // ---- bay 3: canisters, batteries, cartel, notes ------------------------------------------------------------

    private static void FillCanisters(GameGridInventory grid)
    {
        Put(grid, Item(GasCanister.ITEM_ID), "gas canister (empty)");
        string[] gases = { GasCanister.HYDROGEN, GasCanister.CHLORINE, GasCanister.HCL, GasCanister.NITRIC_OXIDE, GasCanister.METHANOL };
        foreach (string gas in gases)
        {
            GameItem canister = Item(GasCanister.ITEM_ID);
            if (canister != null)
            {
                GasCanister.Fill(canister, gas, GasCanister.CAPACITY_UL);
            }
            Put(grid, canister, "gas canister " + gas);
        }
        GameItem mix = Item(GasCanister.ITEM_ID);
        if (mix != null)
        {
            GasCanister.Fill(mix, GasCanister.HYDROGEN, GasCanister.CAPACITY_UL / 2);
            GasCanister.Fill(mix, GasCanister.CHLORINE, GasCanister.CAPACITY_UL / 2);
        }
        Put(grid, mix, "gas canister (hydrogen + chlorine)");

        // Plutonium battery (200 kJ): one charge per picture, 0 / 10 / 20 / 40 / 60 / 80 / 100.
        int[] energies = { 0, 10, 30, 80, 120, 160, 200 };
        foreach (int energy in energies)
        {
            GameItem battery = Item(PuBattery.ITEM_ID);
            if (battery != null)
            {
                int e = energy;
                battery.ModifyTag(PuBattery.ENERGY_TAG, delegate(TagState state) { state.Enable().SetInt(e); }, false);
                PuBattery.ApplySprite(battery);
            }
            Put(grid, battery, "plutonium battery " + energy + " kJ");
        }
    }

    private static void FillCartelAndNotes(GameGridInventory grid)
    {
        Put(grid, Item(CartelNote.ITEM_ID), "cartel envelope");
        for (int stock = 0; stock < 6; stock++)
        {
            string id = CartelPack.ItemId(stock);
            Put(grid, Item(id), id);
        }
        Put(grid, Item(GuideBook.ITEM_ID), "Advanced Chemistry Handbook");
        foreach (HandNote note in Notes.All)
        {
            Put(grid, Item(note.ItemId), note.ItemId);
        }
    }
}
