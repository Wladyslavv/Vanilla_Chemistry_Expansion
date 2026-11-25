// Brine electrolyzer (art/salt/DESIGN.md). A battery-powered machine built like the vanilla evaporator:
//   battery | modules | brine container | red/blue connector | product A (hydrogen) / product B (chlorine) | note
// Once a night (the vanilla machine cycle) it electrolyses the salt in the container:
//   1 mL salt + 0.3 mL water -> 1 mL base + 0.5 mL hydrogen + 0.5 mL chlorine
// 240 mL of salt a night (three salt ores) x performance; products x quality (-100 -> x0.5, 0 -> x1, +100 -> x1.5);
// efficiency lowers the power draw (24 a night) through vanilla's module code. It stops for want of salt, water,
// battery charge or canister room. Sold by the Cartel Vendor with an empty canister in each product slot.
// With a liquid container in product A instead of a canister, it electrolyses fat from the input container:
//   1 mL fat -> 0.8 mL toluene (into the container in product A) + 0.5 mL hydrogen (into the canister in product B)
// 240 mL of fat a night x performance, products x quality, as above. Product B then takes empty, chlorine or
// hydrogen canisters (a canister holding the other gas stops the run). With nothing in the feed slot at all, it makes
// 375 mL of nitric oxide a night from air into an empty or nitric oxide canister in product A, but only while the
// switch in the feed panel is on (off by default); brine and fat electrolysis ignore the switch (it gated every run in
// 1.9.0 - 1.10.1, which stopped every electrolyzer until switched on). Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

public static class Electrolyzer
{
    public const string ITEM_ID = "vce_electrolyzer";
    public const string SPRITE = "vce_electrolyzer";
    public const string LINES_SPRITE = "vce_electrolyzer_lines";

    // Evaporator / Chem Finisher are 300; this one is 100 more.
    public const long VALUE = 400;
    public const int POWER_PER_NIGHT = 24;

    public const int SALT_PER_NIGHT_UL = 240000;
    public const double WATER_PER_SALT = 0.3;
    public const double BASE_PER_SALT = 1.0;
    public const double GAS_PER_SALT = 0.5;
    // Quality +-100 changes every product by +-50%.
    public const double QUALITY_SPAN = 0.5;

    // Fat electrolysis (a liquid container in product A).
    public const int FAT_PER_NIGHT_UL = 240000;
    public const double TOLUENE_PER_FAT = 0.8;
    public const double HYDROGEN_PER_FAT = 0.5;

    // Switched on with nothing in the feed slot, the arc burns air into nitric oxide: 375 mL a night into the canister
    // in product A, x performance and x quality like the other runs.
    public const int NO_PER_NIGHT_UL = 375000;

    private class Slots
    {
        public GameSlotInventory Battery;
        public GameGridInventory Modules;
        public GameSlotInventory Container;
        public GameSlotInventory ProductA;
        public GameSlotInventory ProductB;
        public GameSlotInventory Note;
        public MachineryPanel FeedPanel;
        public MachineryButton Switch;
        public bool SwitchShowsOff;
    }

    // The nitric oxide switch in the feed panel's header (blue = on, red = off). Set while it is switched on, so every
    // machine starts switched off; machine tags come back with a save, so the switch keeps its position.
    public const string ON_TAG = "VCE_ELECTROLYZER_ON_TAG";
    public const string SWITCH_SPRITE = "vce_electrolyzer_switch";
    public const int SWITCH_WIDTH = 18;
    public const int SWITCH_HEIGHT = 10;
    // The panel's own padding (MachineryPanel.Validate) and the gap between the label and the switch.
    private const int PANEL_PAD = 4;
    private const int SWITCH_GAP = 3;

    // The universal-module bay of the vanilla hydroponic machine (a 7x5 cross; 1 = slot).
    // Set once the manual and the two canisters have been put in, so they are never added twice.
    public const string STOCKED_TAG = "VCE_ELECTROLYZER_STOCKED_TAG";

    // Vanilla MachineTurboBoosterAdv.TURBO_BOOST_ELIGIBLE_TAG.
    public const string TURBO_ELIGIBLE_TAG = "TURBO_BOOST_ELIGIBLE_TAG";

    public const string MODULE_BAY_SHAPE = "10000010001000001110000010001000001";
    public const int MODULE_BAY_WIDTH = 7;

    private static readonly ConditionalWeakTable<GameItem, Slots> slots = new ConditionalWeakTable<GameItem, Slots>();

    public static void Enable()
    {
        ModHook.OnGameLoadedLate += FixLoadedItems;
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Electrolysis: electrolyzer + battery + brine", SpawnKit, false);
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Electrolysis: run one night", RunNight, false);
        }
        catch (Exception e)
        {
            Log.Error("electrolyzer debug function registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnGameLoadedLate -= FixLoadedItems;
    }

    public static void AddItems(ModItemDirectory directory)
    {
        directory.Add(ITEM_ID, Create);
    }

    public static bool IsElectrolyzer(GameItem item)
    {
        return item != null && item.identifier == ITEM_ID;
    }

    // ---- the machine ----------------------------------------------------------------------------------------------

    public static GameItem Create()
    {
        Slots s = new Slots();
        s.Battery = new GameSlotInventory();
        // Universal modules, so the hydroponic machine's module bay rather than the evaporator's chem module grid.
        s.Modules = new GameGridInventory(MODULE_BAY_SHAPE, MODULE_BAY_WIDTH);
        s.Container = new GameSlotInventory();
        s.ProductA = new GameSlotInventory();
        s.ProductB = new GameSlotInventory();
        s.Note = new GameSlotInventory();
        GameSlotInventory note = s.Note;

        // The red/blue fork sits inside the products panel, left of the two canister slots (a panel of its own
        // showed an empty label).
        // A vertical stack hands all spare height to its first intake, so each slot gets a stack of its own: the two
        // product slots then stay the same size, centred one above the other.
        PixelElement products = MachineryStack.Horizontal(
            new ElectrolysisLines(),
            MachineryStack.Vertical(
                MachineryStack.Vertical(MachineHelper.WrapInIntake(s.ProductA, true)),
                MachineryStack.Vertical(MachineHelper.WrapInIntake(s.ProductB, true))));
        s.FeedPanel = MachineHelper.Panel(Text.Get("electrolyzer_label_container"), s.Container, "container", true);
        PixelWindow window = MachineHelper.CreateMachineryWindow(true,
            MachineHelper.PowerPanel(s.Battery),
            MachineHelper.Panel(Text.Get("electrolyzer_label_module"), s.Modules, "module", true),
            s.FeedPanel,
            MachineHelper.Panel(Text.Get("electrolyzer_label_products"), products, "products", true),
            MachineHelper.NotePanel(note));

        GameItem machine = ItemDirectory.CreateEmptyItem(ITEM_ID).SetContentWindow(window);
        slots.Remove(machine);
        slots.Add(machine, s);
        try
        {
            machine.name = Text.Get("electrolyzer_name");
            machine.shortDescription = Text.Get("electrolyzer_desc");
            machine.flavorText = "";
            machine.unitValue = VALUE;
            machine.SetGameItemType("MACHINE");
            machine.EnableTag("CONTAINER_TAG", false);
            AudioHelper.InitMetalMachine(machine);
            ApplySprite(machine);

            s.Container.mayInventoryAddItemFunc = (GameItem item, GameInventory _) => GeneralHelper.IsItemOwned(item) && item.IsTag("LIQUID_CONTAINER_TAG");
            s.Container.SetBackgroundFadeSprite("Items/water_container4", "xl");
            s.Container.slotPriorityFunc = (GameItem _, GameInventory __, SlotMarker ___) => 3;
            // Product A takes empty or pure hydrogen canisters, or an owned liquid container (fat electrolysis: the
            // toluene goes there). Product B takes empty, pure chlorine or pure hydrogen canisters (hydrogen comes out
            // there when product A holds a container). Canister ownership is not checked: shop machines come with
            // unowned canisters inside, and ElectrolyzerOwnershipPatch below hands them over with the machine.
            s.ProductA.mayInventoryAddItemFunc = (GameItem item, GameInventory _) => GasCanister.IsPure(item, GasCanister.HYDROGEN)
                || GasCanister.IsPure(item, GasCanister.NITRIC_OXIDE)
                || (IsTolueneContainer(item) && GeneralHelper.IsItemOwned(item));
            s.ProductB.mayInventoryAddItemFunc = (GameItem item, GameInventory _) => GasCanister.IsPure(item, GasCanister.CHLORINE)
                || GasCanister.IsPure(item, GasCanister.HYDROGEN);
            foreach (GameSlotInventory product in new GameSlotInventory[] { s.ProductA, s.ProductB })
            {
                product.slotPriorityFunc = (GameItem _, GameInventory __, SlotMarker ___) => 2;
            }
            // The machine's picture follows what is plugged in.
            foreach (GameSlotInventory watched in new GameSlotInventory[] { s.Container, s.ProductA, s.ProductB })
            {
                watched.onSlotAddItemEndFunc = delegate(GameItem _, GameInventory __, SlotMarker ___) { ApplySprite(machine); };
                watched.onSlotRemoveItemEndFunc = delegate(GameItem _, GameInventory __, SlotMarker ___) { ApplySprite(machine); };
            }

            MachineHelper.SetupBatterySlot(s.Battery, machine);
            // Vanilla's module bay setup, as on the hydroponic machine: universal modules, effects and power update.
            MachineHelper.SetupModuleBay(s.Modules, machine, null, new string[] { "MODULE_TYPE_UNIVERSAL" });
            MachineHelper.SetupManualSlot(note);
            AddSwitch(machine, s);

            // However the machine came about (shop, debug, item spawners, older saves), its first tick puts in the
            // manual and two empty canisters, once; DirectoryMaster.Item strips children from new items, so this
            // cannot happen in the factory.
            machine.onOneSecondItemFunc = delegate(GameItem item, GameInventory inventory, SlotMarker marker)
            {
                try
                {
                    if (!item.IsTag(STOCKED_TAG))
                    {
                        AddCanisters(item, GeneralHelper.IsItemOwned(item));
                    }
                }
                catch (Exception e)
                {
                    Log.Error("stocking the electrolyzer failed", e);
                }
                // The window may have been resized, and a load restores the off tag after the window was built.
                SyncSwitch(item);
            };

            MachineryHelper.InitMachinery(machine, POWER_PER_NIGHT, false);
            // Turbo boosters (all kinds) only target machines with STANDARD_MACHINE_TAG or this tag, then run the
            // machine's cycle-end funcs once more (MachineTurboBoosterAdv.BoostMachine): one extra night of
            // electrolysis.
            machine.EnableTag(TURBO_ELIGIBLE_TAG, false);
            // A standard machine as far as vanilla is concerned: the module bay expansion kit targets it and widens
            // the module bay (MachineHelper.ExpandModule on body storage 1, which is the module bay: battery is 0), and
            // a load restores the widened shape (MachineHelper.LoadUpgrade). The bay starts as the hydroponic
            // machine's, which is the shape both expansion stages are drawn for.
            machine.EnableTag(MachineHelper.STANDARD_MACHINE_TAG, false);
            machine.onCycleEndSlotItemFunc = delegate(GameItem item, GameInventory inventory, SlotMarker marker)
            {
                try
                {
                    RunCycle(item);
                }
                catch (Exception e)
                {
                    Log.Error("electrolyzer cycle failed", e);
                }
            };
            ModHelper.SetSource(machine, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            Log.Error("create " + ITEM_ID + " failed", e);
        }
        return machine;
    }

    // ---- the on/off switch ----------------------------------------------------------------------------------------

    public static bool IsOff(GameItem machine)
    {
        return machine == null || !machine.IsTag(ON_TAG);
    }

    private static void AddSwitch(GameItem machine, Slots s)
    {
        try
        {
            Sprite on = Xinke.ModSprite(SWITCH_SPRITE + "_on");
            if (on == null || s.FeedPanel == null)
            {
                return;
            }
            Sprite onHover = Xinke.ModSprite(SWITCH_SPRITE + "_on_hl");
            s.Switch = new MachineryButton(on, onHover, onHover, on, null);
            s.Switch.SetCallback(delegate
            {
                try
                {
                    if (IsOff(machine))
                    {
                        machine.EnableTag(ON_TAG, false);
                    }
                    else
                    {
                        machine.DisableTag(ON_TAG, false);
                    }
                    SyncSwitch(machine);
                }
                catch (Exception e)
                {
                    Log.Error("electrolyzer switch failed", e);
                }
            });
            s.Switch.handler.SetParent(s.FeedPanel.handler);
            // Room for the switch on both sides of the label, so the label stays centred.
            int labelWidth = s.FeedPanel.label != null ? s.FeedPanel.label.widthPixels : 0;
            s.FeedPanel.SetMinWidth(labelWidth + 2 * (SWITCH_WIDTH + SWITCH_GAP) + 2 * PANEL_PAD);
            // The opposite of the current state, so the first sync sets the face and the tooltip.
            s.SwitchShowsOff = !IsOff(machine);
            SyncSwitch(machine);
        }
        catch (Exception e)
        {
            Log.Error("electrolyzer switch setup failed", e);
        }
    }

    // Face from the off tag, position from the panel's current width (top right, level with the label).
    private static void SyncSwitch(GameItem machine)
    {
        try
        {
            Slots s = SlotsOf(machine);
            if (s == null || s.Switch == null || s.FeedPanel == null)
            {
                return;
            }
            bool off = IsOff(machine);
            if (off != s.SwitchShowsOff)
            {
                string key = SWITCH_SPRITE + (off ? "_off" : "_on");
                Sprite normal = Xinke.ModSprite(key);
                Sprite hover = Xinke.ModSprite(key + "_hl");
                s.Switch.SetFaceSprites(normal, hover, hover, normal);
                s.Switch.SetDescription(Text.Get(off ? "electrolyzer_switch_off" : "electrolyzer_switch_on"));
                s.SwitchShowsOff = off;
            }
            MachineryStyle.Place(s.Switch.handler, s.FeedPanel.widthPixels - PANEL_PAD - SWITCH_WIDTH, PANEL_PAD, SWITCH_WIDTH, SWITCH_HEIGHT);
        }
        catch (Exception e)
        {
            Log.Error("electrolyzer switch sync failed", e);
        }
    }

    private static Slots SlotsOf(GameItem machine)
    {
        Slots s;
        return machine != null && slots.TryGetValue(machine, out s) ? s : null;
    }

    // Eight pictures: vce_electrolyzer_<container><product A><product B>, 1 where something is plugged in.
    public static void ApplySprite(GameItem machine)
    {
        Slots s = SlotsOf(machine);
        string key = SPRITE + "_"
            + (s != null && s.Container.childItem != null ? "1" : "0")
            + (s != null && s.ProductA.childItem != null ? "1" : "0")
            + (s != null && s.ProductB.childItem != null ? "1" : "0");
        BeerSprites.Apply(machine, key);
    }

    // Slot contents come back with a save without the add callbacks; redo the pictures.
    public static void FixLoadedItems()
    {
        try
        {
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (IsElectrolyzer(item))
                {
                    // Electrolyzers saved before 1.7.0 come back with their saved tags, without this one.
                    item.EnableTag(TURBO_ELIGIBLE_TAG, false);
                    // Saved before the module bay could be expanded: never expanded, so the bay needs no reshaping.
                    item.EnableTag(MachineHelper.STANDARD_MACHINE_TAG, false);
                    ApplySprite(item);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("fixing loaded electrolyzers failed", e);
        }
    }

    public static double PerformanceFactor(GameItem machine)
    {
        return Math.Max(0.0, 1.0 + MachineryHelper.GetCurrentPerformanceBonus(machine) / 100.0);
    }

    public static double QualityFactor(GameItem machine)
    {
        int quality = Math.Max(-100, Math.Min(100, MachineryHelper.GetCurrentQualityBonus(machine)));
        return 1.0 + QUALITY_SPAN * quality / 100.0;
    }

    // uL of salt this machine would electrolyse tonight, limited by salt, water, speed and canister room.
    private static int Plan(GameItem machine, Slots s)
    {
        GameItem brine = s.Container.childItem;
        if (brine == null || WaterHelper.IsEmpty(brine))
        {
            return 0;
        }
        int salt = IaLiquids.Get(brine, SaltOre.SALT_ID);
        int water = IaLiquids.Get(brine, "water");
        if (salt <= 0 || water <= 0)
        {
            return 0;
        }
        double gasPerSalt = GAS_PER_SALT * QualityFactor(machine);
        long amount = Math.Min(salt, (long)Math.Round(SALT_PER_NIGHT_UL * PerformanceFactor(machine)));
        amount = Math.Min(amount, (long)Math.Floor(water / WATER_PER_SALT));
        if (gasPerSalt > 0)
        {
            amount = Math.Min(amount, (long)Math.Floor(GasCanister.FreeFor(s.ProductA.childItem, GasCanister.HYDROGEN) / gasPerSalt));
            amount = Math.Min(amount, (long)Math.Floor(GasCanister.FreeFor(s.ProductB.childItem, GasCanister.CHLORINE) / gasPerSalt));
        }
        return (int)Math.Max(0, amount);
    }

    // A liquid container that is not a gas canister: what product A takes for fat electrolysis.
    public static bool IsTolueneContainer(GameItem item)
    {
        return item != null && !GasCanister.IsCanister(item) && item.IsTag("LIQUID_CONTAINER_TAG");
    }

    // uL of fat this machine would electrolyse tonight, limited by fat, speed, room in the container in product A
    // and hydrogen room in the canister in product B.
    private static int PlanFat(GameItem machine, Slots s)
    {
        GameItem input = s.Container.childItem;
        GameItem target = s.ProductA.childItem;
        if (input == null || target == null || WaterHelper.IsEmpty(input))
        {
            return 0;
        }
        int fat = IaLiquids.Get(input, "fat");
        if (fat <= 0)
        {
            return 0;
        }
        double quality = QualityFactor(machine);
        long amount = Math.Min(fat, (long)Math.Round(FAT_PER_NIGHT_UL * PerformanceFactor(machine)));
        double toluenePerFat = TOLUENE_PER_FAT * quality;
        double hydrogenPerFat = HYDROGEN_PER_FAT * quality;
        if (toluenePerFat > 0)
        {
            amount = Math.Min(amount, (long)Math.Floor(WaterHelper.GetFreeCapacity(target) / toluenePerFat));
        }
        if (hydrogenPerFat > 0)
        {
            amount = Math.Min(amount, (long)Math.Floor(GasCanister.FreeFor(s.ProductB.childItem, GasCanister.HYDROGEN) / hydrogenPerFat));
        }
        return (int)Math.Max(0, amount);
    }

    private static void RunFatCycle(GameItem machine, Slots s)
    {
        int amount = PlanFat(machine, s);
        if (amount <= 0)
        {
            return;
        }
        if (!PowerHelper.DrawPowerSource(s.Battery.childItem, MachineryHelper.GetMachinePowerUsage(machine)))
        {
            return;
        }
        GameItem input = s.Container.childItem;
        GameItem target = s.ProductA.childItem;
        double quality = QualityFactor(machine);
        int toluene = (int)Math.Round(amount * TOLUENE_PER_FAT * quality);
        int hydrogen = (int)Math.Round(amount * HYDROGEN_PER_FAT * quality);
        IaLiquids.Set(input, "fat", IaLiquids.Get(input, "fat") - amount);
        if (toluene > 0)
        {
            WaterHelper.AddLiquid(target, FlavorChemistry.TOLUENE, toluene);
        }
        GasCanister.Fill(s.ProductB.childItem, GasCanister.HYDROGEN, hydrogen);
        WaterFeatureHelper.OnLiquidContainerPourOutOf(input);
        SpriteHelper.UpdateWaterContainerSprite(input);
        SpriteHelper.UpdateWaterContainerSprite(target);
        MachineHelper.OnMachineActioned(machine, s.Modules);
        Log.Info("electrolyzer: " + amount + " uL fat -> " + toluene + " uL toluene, " + hydrogen + " uL hydrogen");
    }

    // Nitric oxide into an empty or nitric oxide canister in product A, as much as fits.
    private static void RunNoCycle(GameItem machine, Slots s)
    {
        GameItem canister = s.ProductA.childItem;
        long amount = (long)Math.Round(NO_PER_NIGHT_UL * PerformanceFactor(machine) * QualityFactor(machine));
        amount = Math.Min(amount, GasCanister.FreeFor(canister, GasCanister.NITRIC_OXIDE));
        if (amount <= 0)
        {
            return;
        }
        if (!PowerHelper.DrawPowerSource(s.Battery.childItem, MachineryHelper.GetMachinePowerUsage(machine)))
        {
            return;
        }
        GasCanister.Fill(canister, GasCanister.NITRIC_OXIDE, (int)amount);
        MachineHelper.OnMachineActioned(machine, s.Modules);
        Log.Info("electrolyzer: " + amount + " uL nitric oxide from air");
    }

    public static void RunCycle(GameItem machine)
    {
        Slots s = SlotsOf(machine);
        if (s == null || s.Battery.childItem == null)
        {
            return;
        }
        // Nothing in the feed slot: nitric oxide from air, only while the switch is on (an idle machine stays idle).
        if (s.Container.childItem == null)
        {
            if (!IsOff(machine))
            {
                RunNoCycle(machine, s);
            }
            return;
        }
        // A liquid container in product A switches the machine to fat electrolysis.
        if (IsTolueneContainer(s.ProductA.childItem))
        {
            RunFatCycle(machine, s);
            return;
        }
        int amount = Plan(machine, s);
        if (amount <= 0)
        {
            return;
        }
        if (!PowerHelper.DrawPowerSource(s.Battery.childItem, MachineryHelper.GetMachinePowerUsage(machine)))
        {
            return;
        }
        GameItem brine = s.Container.childItem;
        double quality = QualityFactor(machine);
        int water = (int)Math.Round(amount * WATER_PER_SALT);
        int baseMade = (int)Math.Round(amount * BASE_PER_SALT * quality);
        int gas = (int)Math.Round(amount * GAS_PER_SALT * quality);
        IaLiquids.Set(brine, SaltOre.SALT_ID, IaLiquids.Get(brine, SaltOre.SALT_ID) - amount);
        IaLiquids.Set(brine, "water", IaLiquids.Get(brine, "water") - water);
        IaLiquids.Set(brine, "base", IaLiquids.Get(brine, "base") + baseMade);
        GasCanister.Fill(s.ProductA.childItem, GasCanister.HYDROGEN, gas);
        GasCanister.Fill(s.ProductB.childItem, GasCanister.CHLORINE, gas);
        WaterFeatureHelper.OnLiquidContainerPourOutOf(brine);
        SpriteHelper.UpdateWaterContainerSprite(brine);
        MachineHelper.OnMachineActioned(machine, s.Modules);
        Log.Info("electrolyzer: " + amount + " uL salt -> " + baseMade + " uL base, " + gas + " uL each of hydrogen and chlorine");
    }

    // ---- for sale (the Cartel Vendor, CartelVendor.cs) ------------------------------------------------------------

    public static GameItem CreateForSale()
    {
        GameItem machine = DirectoryMaster.Item(ITEM_ID, false);
        // DirectoryMaster.Item strips child items from new items, so the canisters and manual go in afterwards.
        AddCanisters(machine, false);
        return machine;
    }

    private static void AddCanisters(GameItem machine, bool owned)
    {
        Slots s = SlotsOf(machine);
        if (s == null)
        {
            return;
        }
        foreach (GameSlotInventory product in new GameSlotInventory[] { s.ProductA, s.ProductB })
        {
            if (product.childItem == null)
            {
                GameItem canister = DirectoryMaster.Item(GasCanister.ITEM_ID, owned);
                GraphUtils.TryAcceptAll(product, canister);
            }
        }
        if (s.Note.childItem == null)
        {
            GraphUtils.TryAcceptAll(s.Note, CreateManual(owned));
        }
        machine.EnableTag(STOCKED_TAG, true);
        ApplySprite(machine);
    }

    // The instruction note in the manual slot, like the vanilla machines' (MachineHelper.CreateManual, with our texts).
    public static GameItem CreateManual(bool owned)
    {
        GameItem note = DirectoryMaster.Item("postit", owned);
        note.name = Text.Get("electrolyzer_manual_name");
        note.shortDescription = Text.Get("electrolyzer_manual_short");
        note.flavorText = Text.Get("electrolyzer_manual_flavor");
        note.SetGameItemType("DOCUMENT");
        note.EnableTag("IMPORTANT_TAG", false);
        note.EnableTag("MACHINE_MANUAL_TAG", false);
        note.unitValue = 0L;
        return note;
    }

    // Vanilla marks only the bought item itself as owned; pass it on to the canisters and the manual inside.
    public static void SyncCanisterOwnership(GameItem machine, bool own)
    {
        Slots s = SlotsOf(machine);
        if (s == null)
        {
            return;
        }
        foreach (GameSlotInventory slot in new GameSlotInventory[] { s.ProductA, s.ProductB, s.Note })
        {
            if (slot.childItem != null && GeneralHelper.IsItemOwned(slot.childItem) != own)
            {
                GeneralHelper.SetItemOwned(slot.childItem, own);
            }
        }
    }

    // ---- debug ----------------------------------------------------------------------------------------------------

    // An owned electrolyzer with two empty canisters and a charged battery, plus 160 mL of salt in 100 mL of water.
    public static void SpawnKit()
    {
        try
        {
            GameItem machine = DirectoryMaster.Item(ITEM_ID, true);
            AddCanisters(machine, true);
            Slots s = SlotsOf(machine);
            if (s != null)
            {
                GraphUtils.TryAcceptAll(s.Battery, PreBuiltItemHelper.EnergyCredit(100));
            }
            ModHelper.MoveItemToInventory(machine);
            GameItem bottle = DirectoryMaster.Item("med_bottle_red", true);
            WaterHelper.AddLiquid(bottle, SaltOre.SALT_ID, 160000);
            WaterHelper.AddLiquid(bottle, "water", 100000);
            AudioHelper.InitPlasticContainer(bottle);
            ModHelper.MoveItemToInventory(bottle);
        }
        catch (Exception e)
        {
            Log.Error("electrolyzer debug kit failed", e);
        }
    }

    public static void RunNight()
    {
        try
        {
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (IsElectrolyzer(item))
                {
                    RunCycle(item);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("electrolyzer debug night failed", e);
        }
    }
}

// The red/blue fork between the brine slot and the product slots; hovering it says which gas goes where.
public class ElectrolysisLines : SpritePixelElement, TooltipListener
{
    public ElectrolysisLines() : base("Items/items_tool", "postit")
    {
        Sprite sprite = Xinke.ModSprite(Electrolyzer.LINES_SPRITE);
        if (sprite != null)
        {
            SetSprite(sprite);
        }
    }

    public bool HasTooltip()
    {
        return true;
    }

    public RichText GetTooltip()
    {
        RichTextBuilder builder = new RichTextBuilder(100);
        builder.AddLine(Text.Get("electrolyzer_lines_title"), true, RenderHandler.ColorPalette.White);
        builder.AddLine(Text.Get("electrolyzer_lines_a"), true, RenderHandler.ColorPalette.LightRed);
        builder.AddLine(Text.Get("electrolyzer_lines_b"), true, RenderHandler.ColorPalette.LightBlue);
        builder.AddLine(Text.Get("electrolyzer_lines_base"), true, RenderHandler.ColorPalette.LightCyan);
        builder.AddLine(Text.Get("electrolyzer_lines_fat"), true, RenderHandler.ColorPalette.LightYellow);
        builder.AddLine(Text.Get("electrolyzer_lines_no"), true, RenderHandler.ColorPalette.LightPurple);
        return builder.Build(-1);
    }

    public RichText GetTooltipAdvanced()
    {
        return GetTooltip();
    }
}

[HarmonyPatch(typeof(GeneralHelper), "SetItemOwned", new Type[] { typeof(GameItem), typeof(bool) })]
public static class ElectrolyzerOwnershipPatch
{
    private static void Postfix(GameItem item, bool own)
    {
        try
        {
            if (Electrolyzer.IsElectrolyzer(item))
            {
                Electrolyzer.SyncCanisterOwnership(item, own);
            }
        }
        catch (Exception e)
        {
            Log.Error("electrolyzer canister ownership failed", e);
        }
    }
}
