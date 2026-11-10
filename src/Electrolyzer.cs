// Brine electrolyzer (art/salt/DESIGN.md). A battery-powered machine built like the vanilla evaporator:
//   battery | modules | brine container | red/blue connector | product A (hydrogen) / product B (chlorine) | note
// Once a night (the vanilla machine cycle) it electrolyses the salt in the container:
//   1 mL salt + 0.3 mL water -> 1 mL base + 0.5 mL hydrogen + 0.5 mL chlorine
// 240 mL of salt a night (three salt ores) x performance; products x quality (-100 -> x0.5, 0 -> x1, +100 -> x1.5);
// efficiency lowers the power draw (24 a night) through vanilla's module code. It stops for want of salt, water,
// battery charge or canister room. Sold by Dr. Jackson with an empty canister in each product slot.
// Written for C# 5 (see DreamToxinMod.cs).
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

    // Dr. Jackson: always while the player has none, otherwise 25% per restock (like his storage bays).
    public const int RESTOCK_CHANCE_OWNED = 25;

    private class Slots
    {
        public GameSlotInventory Battery;
        public GameGridInventory Modules;
        public GameSlotInventory Container;
        public GameSlotInventory ProductA;
        public GameSlotInventory ProductB;
        public GameSlotInventory Note;
    }

    // The universal-module bay of the vanilla hydroponic machine (a 7x5 cross; 1 = slot).
    // Set once the manual and the two canisters have been put in, so they are never added twice.
    public const string STOCKED_TAG = "VCE_ELECTROLYZER_STOCKED_TAG";

    public const string MODULE_BAY_SHAPE = "10000010001000001110000010001000001";
    public const int MODULE_BAY_WIDTH = 7;

    private static readonly ConditionalWeakTable<GameItem, Slots> slots = new ConditionalWeakTable<GameItem, Slots>();

    public static void Enable()
    {
        ModHook.OnPlaceInventorInventoryItemEarly += OnPlaceInventorInventory;
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
        ModHook.OnPlaceInventorInventoryItemEarly -= OnPlaceInventorInventory;
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
        PixelWindow window = MachineHelper.CreateMachineryWindow(true,
            MachineHelper.PowerPanel(s.Battery),
            MachineHelper.Panel(Text.Get("electrolyzer_label_module"), s.Modules, "module", true),
            MachineHelper.Panel(Text.Get("electrolyzer_label_container"), s.Container, "container", true),
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
            // Product A takes empty or pure hydrogen canisters, B empty or pure chlorine ones. Ownership is not checked:
            // Dr. Jackson's machines come with unowned canisters inside, and ElectrolyzerOwnershipPatch below hands
            // them over with the machine.
            s.ProductA.mayInventoryAddItemFunc = (GameItem item, GameInventory _) => GasCanister.IsPure(item, GasCanister.HYDROGEN);
            s.ProductB.mayInventoryAddItemFunc = (GameItem item, GameInventory _) => GasCanister.IsPure(item, GasCanister.CHLORINE);
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
            };

            MachineryHelper.InitMachinery(machine, POWER_PER_NIGHT, false);
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

    public static void RunCycle(GameItem machine)
    {
        Slots s = SlotsOf(machine);
        if (s == null || s.Battery.childItem == null)
        {
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

    // ---- Dr. Jackson ---------------------------------------------------------------------------------------------

    public static void OnPlaceInventorInventory(List<GameItem> ownedItems)
    {
        try
        {
            bool owned = ownedItems != null && ownedItems.Exists(i => IsElectrolyzer(i));
            if (owned && !RNG.Roll(RESTOCK_CHANCE_OWNED))
            {
                return;
            }
            GameItem machine = DirectoryMaster.Item(ITEM_ID, false);
            // DirectoryMaster.Item strips child items from new items, so the canisters and manual go in afterwards.
            AddCanisters(machine, false);
            PlayerStore.Instance.AddDirectSellingItemToTable(machine, false, false, false, 0);
        }
        catch (Exception e)
        {
            Log.Error("offering the electrolyzer failed", e);
        }
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
