// The fermenter (docs/ANTIBIOTICS_DESIGN.md section 3): a battery-powered machine built like this mod's
// electrolyzer, with a vanilla livestock feed dispenser bolted into its input slot as the carbon source.
//   battery | modules | feed dispenser (fixed) | strain | harvest (9x6) | note
// Every night it draws 12 power and the strain's own kcal from the dispenser. A batch runs two nights at the
// standard growth speed and then turns the one strain inside into four, each of which rolls a natural mutation,
// so a line re-seeded from its own harvest drifts downhill. Sold by Dr. Jackson and the retired chemist for 400.
// Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

public static class Fermenter
{
    public const string ITEM_ID = "vce_fermenter";
    public const string SPRITE = "vce_fermenter";
    public const string SPRITE_RUNNING = "vce_fermenter_b";

    public const long VALUE = 400;
    public const int POWER_PER_NIGHT = 12;

    // Standard traits: two nights a batch, four strains out of the one that went in.
    public const int BASE_NIGHTS = 2;
    public const int BASE_HARVEST = 4;

    public const string FEED_DISPENSER_ID = "livestock_feed_dispenser";
    public const string STORED_KCAL_TAG = "STORED_KCAL_TAG";

    // Nights this batch has run.
    public const string NIGHTS_TAG = "VCE_FERMENTER_NIGHTS_TAG";
    // Set once the fixed dispenser and the manual are in, so they are never added twice.
    public const string STOCKED_TAG = "VCE_FERMENTER_STOCKED_TAG";
    public const string TURBO_ELIGIBLE_TAG = "TURBO_BOOST_ELIGIBLE_TAG";

    // Dr. Jackson: always while the player has none, otherwise 25% per restock.
    public const int RESTOCK_CHANCE_OWNED = 25;
    // The retired chemist brings one when the player has none.
    public const int CHEMIST_CHANCE_OWNED = 20;

    public const int OUTPUT_WIDTH = 9;
    public const int OUTPUT_HEIGHT = 6;

    private class Slots
    {
        public GameSlotInventory Battery;
        public GameGridInventory Modules;
        public GameSlotInventory Feed;
        public GameSlotInventory StrainInput;
        public GameGridInventory Output;
        public GameSlotInventory Note;
    }

    private static readonly ConditionalWeakTable<GameItem, Slots> slots = new ConditionalWeakTable<GameItem, Slots>();

    public static void Enable()
    {
        ModHook.OnPlaceInventorInventoryItemEarly += OnPlaceInventorInventory;
        ModHook.OnGameLoadedLate += FixLoadedItems;
        ModHook.OnCreateTooltipLate += OnCreateTooltip;
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Antibiotics: fermenter kit", SpawnKit, false);
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Antibiotics: run one fermenter night", RunNight, false);
        }
        catch (Exception e)
        {
            Log.Error("fermenter debug function registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnPlaceInventorInventoryItemEarly -= OnPlaceInventorInventory;
        ModHook.OnGameLoadedLate -= FixLoadedItems;
        ModHook.OnCreateTooltipLate -= OnCreateTooltip;
    }

    public static void AddItems(ModItemDirectory directory)
    {
        directory.Add(ITEM_ID, Create);
    }

    public static bool IsFermenter(GameItem item)
    {
        return item != null && item.identifier == ITEM_ID;
    }

    // ---- the machine ----------------------------------------------------------------------------------------------

    public static GameItem Create()
    {
        Slots s = new Slots();
        s.Battery = new GameSlotInventory();
        s.Modules = new GameGridInventory(Electrolyzer.MODULE_BAY_SHAPE, Electrolyzer.MODULE_BAY_WIDTH);
        s.Feed = new GameSlotInventory();
        s.StrainInput = new GameSlotInventory();
        s.Output = new GameGridInventory(OUTPUT_WIDTH, OUTPUT_HEIGHT);
        s.Note = new GameSlotInventory();
        GameSlotInventory note = s.Note;

        PixelWindow window = MachineHelper.CreateMachineryWindow(true,
            MachineHelper.PowerPanel(s.Battery),
            MachineHelper.Panel(Text.Get("fermenter_label_module"), s.Modules, "module", true),
            MachineHelper.Panel(Text.Get("fermenter_label_feed"), s.Feed, "feed", true),
            MachineHelper.Panel(Text.Get("fermenter_label_strain"), s.StrainInput, "strain", true),
            MachineHelper.Panel(Text.Get("fermenter_label_output"), s.Output, "output", true),
            MachineHelper.NotePanel(note));

        GameItem machine = ItemDirectory.CreateEmptyItem(ITEM_ID).SetContentWindow(window);
        slots.Remove(machine);
        slots.Add(machine, s);
        try
        {
            machine.name = Text.Get("fermenter_name");
            machine.shortDescription = Text.Get("fermenter_desc");
            machine.flavorText = Text.Get("fermenter_flavor");
            machine.unitValue = VALUE;
            machine.SetGameItemType("MACHINE");
            machine.EnableTag("CONTAINER_TAG", false);
            AudioHelper.InitMetalMachine(machine);
            ApplySprite(machine);

            // The feed dispenser is bolted in: nothing else goes in, and it never comes out.
            s.Feed.mayInventoryAddItemFunc = (GameItem item, GameInventory _) => IsFeedDispenser(item);
            s.Feed.mayInventoryRemoveItemFunc = (GameItem _, GameInventory __) => false;
            s.Feed.slotPriorityFunc = (GameItem _, GameInventory __, SlotMarker ___) => 3;

            s.StrainInput.mayInventoryAddItemFunc = (GameItem item, GameInventory _) =>
                Strain.IsStrain(item) && GeneralHelper.IsItemOwned(item);
            s.StrainInput.slotPriorityFunc = (GameItem _, GameInventory __, SlotMarker ___) => 2;
            // The harvest comes out of the machine; nothing is put back in by hand.
            s.Output.mayInventoryAddItemFunc = (GameItem _, GameInventory __) => false;

            foreach (GameSlotInventory watched in new GameSlotInventory[] { s.StrainInput })
            {
                watched.onSlotAddItemEndFunc = delegate(GameItem _, GameInventory __, SlotMarker ___)
                {
                    ResetBatch(machine);
                    ApplySprite(machine);
                };
                watched.onSlotRemoveItemEndFunc = delegate(GameItem _, GameInventory __, SlotMarker ___)
                {
                    ResetBatch(machine);
                    ApplySprite(machine);
                };
            }

            MachineHelper.SetupBatterySlot(s.Battery, machine);
            MachineHelper.SetupModuleBay(s.Modules, machine, null, new string[] { "MODULE_TYPE_UNIVERSAL" });
            MachineHelper.SetupManualSlot(note);

            machine.onOneSecondItemFunc = delegate(GameItem item, GameInventory inventory, SlotMarker marker)
            {
                try
                {
                    if (!item.IsTag(STOCKED_TAG))
                    {
                        AddFixtures(item, GeneralHelper.IsItemOwned(item));
                    }
                }
                catch (Exception e)
                {
                    Log.Error("stocking the fermenter failed", e);
                }
            };

            MachineryHelper.InitMachinery(machine, POWER_PER_NIGHT, false);
            machine.EnableTag(TURBO_ELIGIBLE_TAG, false);
            machine.EnableTag(MachineHelper.STANDARD_MACHINE_TAG, false);
            machine.onCycleEndSlotItemFunc = delegate(GameItem item, GameInventory inventory, SlotMarker marker)
            {
                try
                {
                    RunCycle(item);
                }
                catch (Exception e)
                {
                    Log.Error("fermenter cycle failed", e);
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

    public static bool IsFeedDispenser(GameItem item)
    {
        return item != null && item.identifier == FEED_DISPENSER_ID;
    }

    public static void ApplySprite(GameItem machine)
    {
        Slots s = SlotsOf(machine);
        bool running = s != null && s.StrainInput.childItem != null;
        BeerSprites.Apply(machine, running ? SPRITE_RUNNING : SPRITE);
    }

    public static void FixLoadedItems()
    {
        try
        {
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (IsFermenter(item))
                {
                    item.EnableTag(TURBO_ELIGIBLE_TAG, false);
                    item.EnableTag(MachineHelper.STANDARD_MACHINE_TAG, false);
                    ApplySprite(item);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("fixing loaded fermenters failed", e);
        }
    }

    // ---- power and feed -------------------------------------------------------------------------------------------

    // Efficiency, performance and quality all do the same thing here: they take percent off the power draw,
    // down to nothing at all, never below it.
    public static int PowerUsage(GameItem machine)
    {
        int sum = machine.GetTagReadonly("TOTAL_PERCENTAGE_POWER_BONUS_INT").GetInt()
            + MachineryHelper.GetCurrentPerformanceBonus(machine)
            + MachineryHelper.GetCurrentQualityBonus(machine);
        double usage = POWER_PER_NIGHT * (1.0 - sum / 100.0);
        return Math.Max(0, (int)Math.Round(usage, MidpointRounding.AwayFromZero));
    }

    public static int StoredKcal(GameItem machine)
    {
        Slots s = SlotsOf(machine);
        GameItem dispenser = s != null ? s.Feed.childItem : null;
        return dispenser != null && dispenser.IsTag(STORED_KCAL_TAG)
            ? Math.Max(0, dispenser.GetTagReadonly(STORED_KCAL_TAG).GetInt()) : 0;
    }

    private static bool DrawKcal(GameItem machine, int kcal)
    {
        Slots s = SlotsOf(machine);
        GameItem dispenser = s != null ? s.Feed.childItem : null;
        if (dispenser == null)
        {
            return false;
        }
        if (kcal <= 0)
        {
            return true;
        }
        int stored = StoredKcal(machine);
        if (stored < kcal)
        {
            return false;
        }
        int left = stored - kcal;
        dispenser.ModifyTag(STORED_KCAL_TAG, delegate(TagState state)
        {
            state.Enable().SetInt(left);
        }, true);
        dispenser.ValidateShapeState();
        return true;
    }

    // ---- the batch ------------------------------------------------------------------------------------------------

    public static int Nights(GameItem machine)
    {
        return machine != null && machine.IsTag(NIGHTS_TAG) ? Math.Max(0, machine.GetTagReadonly(NIGHTS_TAG).GetInt()) : 0;
    }

    private static void SetNights(GameItem machine, int nights)
    {
        if (machine == null)
        {
            return;
        }
        int value = Math.Max(0, nights);
        machine.ModifyTag(NIGHTS_TAG, delegate(TagState state)
        {
            state.Enable().SetInt(value);
        }, true);
        machine.ValidateShapeState();
    }

    public static void ResetBatch(GameItem machine)
    {
        SetNights(machine, 0);
    }

    // Nights this batch needs, from the strain in the machine.
    public static int NightsNeeded(GameItem machine)
    {
        Slots s = SlotsOf(machine);
        GameItem strain = s != null ? s.StrainInput.childItem : null;
        return strain != null ? StrainTraits.BatchNights(strain, BASE_NIGHTS) : BASE_NIGHTS;
    }

    public static void RunCycle(GameItem machine)
    {
        Slots s = SlotsOf(machine);
        if (s == null || s.Battery.childItem == null)
        {
            return;
        }
        GameItem strain = s.StrainInput.childItem;
        if (strain == null)
        {
            return;
        }
        // Same as the blender: a strain put in before its first tick still gets a genome before it breeds.
        Strain.EnsureIdentity(strain);
        int kcal = StrainTraits.KcalPerNight(strain);
        if (StoredKcal(machine) < kcal)
        {
            return;
        }
        if (!PowerHelper.DrawPowerSource(s.Battery.childItem, PowerUsage(machine)))
        {
            return;
        }
        if (!DrawKcal(machine, kcal))
        {
            return;
        }
        int nights = Nights(machine) + 1;
        int needed = StrainTraits.BatchNights(strain, BASE_NIGHTS);
        Log.Info("fermenter night " + nights + " of " + needed + ": power " + PowerUsage(machine)
            + ", kcal " + kcal + " (" + StoredKcal(machine) + " left), strain speed "
            + StrainTraits.Format(StrainTraits.Speed(strain)) + ", yield "
            + StrainTraits.Format(StrainTraits.Yield(strain)));
        if (nights < needed)
        {
            SetNights(machine, nights);
            MachineHelper.OnMachineActioned(machine, s.Modules);
            return;
        }
        Harvest(machine, s, strain);
        MachineHelper.OnMachineActioned(machine, s.Modules);
    }

    // The batch is done: the strain inside is used up and its offspring come out, each with a natural mutation.
    private static void Harvest(GameItem machine, Slots s, GameItem strain)
    {
        int count = StrainTraits.HarvestCount(strain, BASE_HARVEST);
        bool owned = GeneralHelper.IsItemOwned(strain);
        int made = 0;
        // The harvest grid refuses everything by hand, so like vanilla's hydroponic machine
        // (HydroponicHelper, around FinishGrowing) it is opened only while the machine fills it.
        s.Output.mayInventoryAddItemFunc = (GameItem _, GameInventory __) => true;
        try
        {
            for (int i = 0; i < count; i++)
            {
                GameItem child = Strain.Spawn(owned, strain, false);
                if (child == null)
                {
                    break;
                }
                StrainMutation.Natural(child);
                // The mutation may have moved the purity trait, which decides how the blend grades.
                Strain.ApplyContents(child);
                Log.Info("  harvested strain: genome " + StrainTraits.Genome(child)
                    + ", purity " + StrainTraits.Format(StrainTraits.Purity(child))
                    + ", yield " + StrainTraits.Format(StrainTraits.Yield(child))
                    + ", speed " + StrainTraits.Format(StrainTraits.Speed(child))
                    + ", carbon " + StrainTraits.Format(StrainTraits.Carbon(child))
                    + ", target " + StrainTraits.Target(child)
                    + ", seed " + StrainTraits.IsSeedStock(child)
                    + ", ceph " + IaLiquids.Get(child, Antibiotics.CEPH_ID) + " uL");
                if (!ItemPlacement.TryPut(s.Output, child))
                {
                    // The harvest grid is full: the rest of the batch is lost rather than spilled over the shop.
                    child.Destroy();
                    break;
                }
                made++;
            }
        }
        finally
        {
            s.Output.mayInventoryAddItemFunc = (GameItem _, GameInventory __) => false;
        }
        SetNights(machine, 0);
        s.StrainInput.Expel(strain);
        strain.Destroy();
        ApplySprite(machine);
        Log.Info("fermenter harvested " + made + " of " + count + " strain(s)");
    }

    // ---- fixtures and shops ---------------------------------------------------------------------------------------

    private static void AddFixtures(GameItem machine, bool owned)
    {
        Slots s = SlotsOf(machine);
        if (s == null)
        {
            return;
        }
        if (s.Feed.childItem == null)
        {
            GameItem dispenser = DirectoryMaster.Item(FEED_DISPENSER_ID, owned);
            if (dispenser != null)
            {
                GeneralHelper.LockItem(dispenser);
                GraphUtils.TryAcceptAll(s.Feed, dispenser);
            }
        }
        if (s.Note.childItem == null)
        {
            GraphUtils.TryAcceptAll(s.Note, CreateManual(owned));
        }
        machine.EnableTag(STOCKED_TAG, true);
        ApplySprite(machine);
    }

    public static GameItem CreateManual(bool owned)
    {
        GameItem note = DirectoryMaster.Item("postit", owned);
        note.name = Text.Get("fermenter_manual_name");
        note.shortDescription = Text.Get("fermenter_manual_short");
        note.flavorText = Text.Get("fermenter_manual_flavor");
        note.SetGameItemType("DOCUMENT");
        note.EnableTag("IMPORTANT_TAG", false);
        note.EnableTag("MACHINE_MANUAL_TAG", false);
        note.unitValue = 0L;
        return note;
    }

    public static void OnPlaceInventorInventory(List<GameItem> ownedItems)
    {
        try
        {
            bool owned = ownedItems != null && ownedItems.Exists(i => IsFermenter(i));
            if (owned && !RNG.Roll(RESTOCK_CHANCE_OWNED))
            {
                return;
            }
            Offer(false);
        }
        catch (Exception e)
        {
            Log.Error("offering the fermenter failed", e);
        }
    }

    // The retired chemist's stock (Strain.cs hooks him for strains, this for the machine).
    public static void OfferToChemist()
    {
        try
        {
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (IsFermenter(item) && GeneralHelper.IsItemOwned(item) && !RNG.Roll(CHEMIST_CHANCE_OWNED))
                {
                    return;
                }
            }
            Offer(false);
        }
        catch (Exception e)
        {
            Log.Error("offering the fermenter to the retired chemist failed", e);
        }
    }

    private static void Offer(bool owned)
    {
        GameItem machine = DirectoryMaster.Item(ITEM_ID, owned);
        // DirectoryMaster.Item strips child items from new items, so the fixtures go in afterwards.
        AddFixtures(machine, owned);
        PlayerStore.Instance.AddDirectSellingItemToTable(machine, false, false, false, 0);
    }

    // Vanilla marks only the bought machine as owned; pass it on to the fixtures inside.
    public static void SyncOwnership(GameItem machine, bool own)
    {
        Slots s = SlotsOf(machine);
        if (s == null)
        {
            return;
        }
        foreach (GameSlotInventory slot in new GameSlotInventory[] { s.Feed, s.Note })
        {
            if (slot.childItem != null && GeneralHelper.IsItemOwned(slot.childItem) != own)
            {
                GeneralHelper.SetItemOwned(slot.childItem, own);
            }
        }
    }

    // ---- tooltip and debug ----------------------------------------------------------------------------------------

    public static void OnCreateTooltip(RichTextBuilder builder, GameItem item)
    {
        try
        {
            if (builder == null || !IsFermenter(item))
            {
                return;
            }
            Slots s = SlotsOf(item);
            GameItem strain = s != null ? s.StrainInput.childItem : null;
            if (strain == null)
            {
                builder.AddLine(Text.Get("fermenter_tooltip_idle"), true, RenderHandler.ColorPalette.LightYellow);
            }
            else
            {
                builder.AddLine(Text.Get("fermenter_tooltip_batch", Nights(item), NightsNeeded(item),
                    StrainTraits.HarvestCount(strain, BASE_HARVEST)), true, RenderHandler.ColorPalette.LightCyan);
                builder.AddLine(Text.Get("fermenter_tooltip_draw", PowerUsage(item),
                    StrainTraits.KcalPerNight(strain)), true, RenderHandler.ColorPalette.LightCyan);
            }
            builder.AddLine(Text.Get("fermenter_tooltip_feed", StoredKcal(item)), true, RenderHandler.ColorPalette.LightCyan);
        }
        catch (Exception e)
        {
            Log.Error("fermenter tooltip failed", e);
        }
    }

    // An owned fermenter with a charged battery, a fed dispenser and a strain in it.
    public static void SpawnKit()
    {
        try
        {
            GameItem machine = DirectoryMaster.Item(ITEM_ID, true);
            StockForTesting(machine, 20000);
            ModHelper.MoveItemToInventory(machine);
        }
        catch (Exception e)
        {
            Log.Error("fermenter debug kit failed", e);
        }
    }

    // Fixtures, a charged battery, kcal in the dispenser and a random strain inside (also AntibioticTestKit.cs).
    public static void StockForTesting(GameItem machine, int kcal)
    {
        AddFixtures(machine, true);
        Slots s = SlotsOf(machine);
        if (s == null)
        {
            return;
        }
        GraphUtils.TryAcceptAll(s.Battery, PreBuiltItemHelper.EnergyCredit(100));
        GameItem dispenser = s.Feed.childItem;
        if (dispenser != null)
        {
            int stored = kcal;
            dispenser.ModifyTag(STORED_KCAL_TAG, delegate(TagState state)
            {
                state.Enable().SetInt(stored);
            }, true);
            dispenser.ValidateShapeState();
        }
        GameItem strain = Strain.Spawn(true, null, false);
        if (strain != null)
        {
            GraphUtils.TryAcceptAll(s.StrainInput, strain);
        }
    }

    public static void RunNight()
    {
        try
        {
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (IsFermenter(item))
                {
                    RunCycle(item);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("fermenter debug night failed", e);
        }
    }
}

[HarmonyPatch(typeof(GeneralHelper), "SetItemOwned", new Type[] { typeof(GameItem), typeof(bool) })]
public static class FermenterOwnershipPatch
{
    private static void Postfix(GameItem item, bool own)
    {
        try
        {
            if (Fermenter.IsFermenter(item))
            {
                Fermenter.SyncOwnership(item, own);
            }
        }
        catch (Exception e)
        {
            Log.Error("fermenter fixture ownership failed", e);
        }
    }
}

// The retired chemist sells the fermenter and, now and then, a strain; the retired farmer sells strains too.
[HarmonyPatch(typeof(StoreClientListSpec), "CreateRetiredChemist")]
public static class FermenterChemistPatch
{
    private static void Postfix(StoreClient __result)
    {
        XinkeRetiredFarmerPatch.AppendToStock(__result, OfferStock, "retired chemist (antibiotics)");
    }

    private static void OfferStock()
    {
        Fermenter.OfferToChemist();
        StrainShop.OfferStrain();
    }
}

[HarmonyPatch(typeof(StoreClientListSpec), "CreateRetiredFarmer")]
public static class StrainFarmerPatch
{
    private static void Postfix(StoreClient __result)
    {
        XinkeRetiredFarmerPatch.AppendToStock(__result, StrainShop.OfferStrain, "retired farmer (antibiotics)");
    }
}

// Both retired specialists have a one-in-five chance of bringing a strain along.
public static class StrainShop
{
    public const int CHANCE = 20;

    public static void OfferStrain()
    {
        try
        {
            if (!RNG.Roll(CHANCE))
            {
                return;
            }
            GameItem strain = Strain.Spawn(false, null, false);
            if (strain != null)
            {
                PlayerStore.Instance.AddDirectSellingItemToTable(strain, false, false, false, 0);
                Log.Info("a strain is on offer: genome " + StrainTraits.Genome(strain));
            }
        }
        catch (Exception e)
        {
            Log.Error("offering a strain failed", e);
        }
    }
}
