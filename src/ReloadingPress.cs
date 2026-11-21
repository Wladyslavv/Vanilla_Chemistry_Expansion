// Reloading press: a 2x3 machine (art/explosives/draw_reloading_press.py) that presses vanilla pistol rounds out of
// nitrocellulose, and TNT for armour-piercing ones. Its window, laid out like the vanilla Chem Finisher's:
//   nitrocellulose grid (6x6) | liquid container slot | output grid (8x6) | control (calibre, type, status, press)
// Nitrocellulose put in the grid is tipped into the hopper (up to HOPPER_CAPACITY of propellant); a piece gives its
// value in propellant (Nitrocellulose.PowderOf: 54 / 69 / 90). A round takes its vanilla base price in propellant, so
// pressing pays exactly the contraband markup: non-lethal (no markup) breaks even, normal +35%, armour-piercing +70%.
// Armour-piercing rounds also take TNT from the container: 3 mL a .22, 4 mL a heavy pistol round, which leaves them a
// little ahead of pressing normal rounds and making dynamite from the TNT (2.52 a mL after markup).
// Written for C# 5 (see DreamToxinMod.cs).
using System;
using TMPro;

public static class ReloadingPress
{
    public const string ITEM_ID = "vce_reloading_press";
    public const string SPRITE = "vce_reloading_press";
    public const long VALUE = 300;
    // Output 8x6; the nitrocellulose grid (6x6) holds nine large pieces.
    public const int OUTPUT_WIDTH = 8;
    public const int OUTPUT_HEIGHT = 6;
    public const int POWDER_WIDTH = 6;
    public const int POWDER_HEIGHT = 6;
    public const int HOPPER_CAPACITY = 3000;

    // Machine state, kept in its base tags (saved with the machine).
    public const string CALIBER_TAG = "VCE_PRESS_CALIBER_INT";  // 0 = .22, 1 = heavy pistol
    public const string TYPE_TAG = "VCE_PRESS_TYPE_INT";        // 0 = non-lethal, 1 = normal, 2 = armour-piercing
    public const string POWDER_TAG = "VCE_PRESS_POWDER_INT";    // propellant in the hopper

    private class Round
    {
        public string ItemId;
        public string NameKey;   // vanilla item name key
        public int Powder;       // = vanilla base price
        public int TntUl;

        public Round(string itemId, string nameKey, int powder, int tntUl)
        {
            ItemId = itemId;
            NameKey = nameKey;
            Powder = powder;
            TntUl = tntUl;
        }
    }

    // [calibre, type]
    private static readonly Round[,] Rounds = {
        {
            new Round("small_pistol_ammo_nl", "item_small_pistol_ammo_nl_name", 14, 0),
            new Round("small_pistol_ammo", "item_small_pistol_ammo_name", 20, 0),
            new Round("small_pistol_ammo_p", "item_small_pistol_ammo_p_name", 26, 3000),
        },
        {
            new Round("heavy_pistol_ammo_nl", "item_heavy_pistol_ammo_nl_name", 24, 0),
            new Round("heavy_pistol_ammo", "item_heavy_pistol_ammo_name", 30, 0),
            new Round("heavy_pistol_ammo_p", "item_heavy_pistol_ammo_p_name", 36, 4000),
        },
    };

    private class Ui
    {
        public GameGridInventory Powder;
        public GameSlotInventory Container;
        public GameGridInventory Output;
        public MachineryButton Caliber;
        public MachineryButton Type;
        public MachineryText Target;
        public MachineryText Status;
    }

    // Set while the press puts a round in the output, which refuses anything put in by hand.
    private static bool pressing;

    public static void Enable()
    {
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Reloading press: spawn one", SpawnOne, false);
        }
        catch (Exception e)
        {
            Log.Error("reloading press debug function registration failed", e);
        }
    }

    public static bool IsReloadingPress(GameItem item)
    {
        return item != null && item.identifier == ITEM_ID;
    }

    public static void AddItems(ModItemDirectory directory)
    {
        directory.Add(ITEM_ID, Create);
    }

    public static GameItem Create()
    {
        Ui ui = new Ui();
        ui.Powder = new GameGridInventory(POWDER_WIDTH, POWDER_HEIGHT);
        ui.Container = new GameSlotInventory();
        ui.Output = new GameGridInventory(OUTPUT_WIDTH, OUTPUT_HEIGHT);
        ui.Caliber = new MachineryButton("");
        ui.Type = new MachineryButton("");
        ui.Target = new MachineryText("", 6, HorizontalAlignmentOptions.Center, false);
        ui.Status = new MachineryText("", 6, HorizontalAlignmentOptions.Center, false);
        MachineryButton press = new MachineryButton(Text.Get("reloading_press_btn_press"));
        ui.Caliber.SetSize(75);
        ui.Type.SetSize(75);
        MachineryStack control = MachineryStack.Vertical(ui.Caliber, ui.Type, ui.Target, ui.Status, press);
        PixelWindow window = MachineHelper.CreateMachineryWindow(true,
            MachineHelper.Panel(Text.Get("reloading_press_label_powder"), ui.Powder, "powder", true),
            MachineHelper.Panel(Text.Get("reloading_press_label_container"), ui.Container, "container", true),
            MachineHelper.Panel(Text.Get("reloading_press_label_output"), ui.Output, "output", true),
            MachineHelper.Panel(Text.Get("reloading_press_label_control"), control, "control", true));

        GameItem machine = ItemDirectory.CreateEmptyItem(ITEM_ID).SetContentWindow(window);
        try
        {
            machine.name = Text.Get("reloading_press_name");
            machine.shortDescription = Text.Get("reloading_press_desc");
            machine.flavorText = "";
            machine.unitValue = VALUE;
            machine.SetGameItemType("MACHINE");
            machine.EnableTag("CONTAINER_TAG", false);
            AudioHelper.InitMetalMachine(machine);
            BeerSprites.Apply(machine, SPRITE);

            ui.Powder.mayInventoryAddItemFunc = (GameItem item, GameInventory _) => GeneralHelper.IsItemOwned(item) && Nitrocellulose.IsNitrocellulose(item);
            ui.Container.mayInventoryAddItemFunc = (GameItem item, GameInventory _) => GeneralHelper.IsItemOwned(item) && item.IsTag("LIQUID_CONTAINER_TAG");
            ui.Container.SetBackgroundFadeSprite("Items/water_container4", "xl");
            ui.Container.slotPriorityFunc = (GameItem _, GameInventory __, SlotMarker ___) => 4;
            ui.Output.mayInventoryAddItemFunc = (GameItem item, GameInventory _) => pressing;

            ui.Caliber.SetCallback(delegate
            {
                SetInt(machine, CALIBER_TAG, 1 - Clamp(GetInt(machine, CALIBER_TAG), 1));
                Refresh(machine, ui);
            });
            ui.Type.SetCallback(delegate
            {
                SetInt(machine, TYPE_TAG, (Clamp(GetInt(machine, TYPE_TAG), 2) + 1) % 3);
                Refresh(machine, ui);
            });
            press.SetCallback(delegate
            {
                Press(machine, ui);
                Refresh(machine, ui);
            });
            // Every second: tip waiting nitrocellulose into the hopper, then bring the labels up to date.
            machine.onOneSecondItemFunc = delegate(GameItem item, GameInventory inventory, SlotMarker marker)
            {
                try
                {
                    LoadHopper(machine, ui);
                    Refresh(machine, ui);
                }
                catch (Exception e)
                {
                    Log.Error("reloading press tick failed", e);
                }
            };
            SetIntIfMissing(machine, CALIBER_TAG, 1);
            SetIntIfMissing(machine, TYPE_TAG, 1);
            Refresh(machine, ui);
            ModHelper.SetSource(machine, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            Log.Error("create " + ITEM_ID + " failed", e);
        }
        return machine;
    }

    // ---- state ----------------------------------------------------------------------------------------------------------

    // Read and written on the base state, like the gas canister: written every second without revalidating.
    private static int GetInt(GameItem item, string tag)
    {
        return item.state != null && item.state.IsTag(tag) ? item.state.GetTag(tag).GetInt() : 0;
    }

    private static void SetInt(GameItem item, string tag, int value)
    {
        item.ModifyTag(tag, delegate(TagState state) { state.Enable().SetInt(value); }, true);
    }

    private static void SetIntIfMissing(GameItem item, string tag, int value)
    {
        if (item.state == null || !item.state.IsTag(tag))
        {
            SetInt(item, tag, value);
        }
    }

    private static int Clamp(int value, int max)
    {
        return Math.Max(0, Math.Min(max, value));
    }

    private static Round Selected(GameItem machine)
    {
        return Rounds[Clamp(GetInt(machine, CALIBER_TAG), 1), Clamp(GetInt(machine, TYPE_TAG), 2)];
    }

    // ---- hopper and pressing --------------------------------------------------------------------------------------------

    // Every piece in the grid that still fits in the hopper goes in; the rest wait for room.
    private static void LoadHopper(GameItem machine, Ui ui)
    {
        int powder = GetInt(machine, POWDER_TAG);
        bool changed = false;
        foreach (GameItem piece in new System.Collections.Generic.List<GameItem>(ui.Powder.childItems))
        {
            if (piece == null || !Nitrocellulose.IsNitrocellulose(piece))
            {
                continue;
            }
            int add = Nitrocellulose.PowderOf(piece);
            if (add <= 0 || powder + add > HOPPER_CAPACITY)
            {
                continue;
            }
            powder += add;
            piece.Destroy();
            changed = true;
        }
        if (changed)
        {
            SetInt(machine, POWDER_TAG, powder);
        }
    }

    // Why the selected round cannot be pressed now, or null.
    private static string Blocker(GameItem machine, Ui ui, Round round)
    {
        if (GetInt(machine, POWDER_TAG) < round.Powder)
        {
            return "reloading_press_status_no_powder";
        }
        if (round.TntUl > 0)
        {
            GameItem container = ui.Container.childItem;
            if (container == null || IaLiquids.Get(container, FlavorChemistry.TNT) < round.TntUl)
            {
                return "reloading_press_status_no_tnt";
            }
        }
        return null;
    }

    private static void Press(GameItem machine, Ui ui)
    {
        try
        {
            Round round = Selected(machine);
            if (Blocker(machine, ui, round) != null)
            {
                return;
            }
            GameItem made = DirectoryMaster.Item(round.ItemId, true);
            if (made == null)
            {
                return;
            }
            ModHelper.SetItemOwnerShip(made, true);
            pressing = true;
            bool placed;
            try
            {
                placed = ItemPlacement.TryPut(ui.Output, made);
            }
            finally
            {
                pressing = false;
            }
            if (!placed)
            {
                made.Destroy();
                Notify(Text.Get("reloading_press_status_full"));
                return;
            }
            SetInt(machine, POWDER_TAG, GetInt(machine, POWDER_TAG) - round.Powder);
            if (round.TntUl > 0)
            {
                GameItem container = ui.Container.childItem;
                IaLiquids.Set(container, FlavorChemistry.TNT, IaLiquids.Get(container, FlavorChemistry.TNT) - round.TntUl);
                WaterFeatureHelper.OnLiquidContainerPourOutOf(container);
                SpriteHelper.UpdateWaterContainerSprite(container);
            }
            AudioHelper.UseWaterFilter();
        }
        catch (Exception e)
        {
            Log.Error("reloading press failed", e);
        }
    }

    private static void Refresh(GameItem machine, Ui ui)
    {
        try
        {
            Round round = Selected(machine);
            ui.Caliber.SetText(Text.Get(Clamp(GetInt(machine, CALIBER_TAG), 1) == 0 ? "reloading_press_caliber_small" : "reloading_press_caliber_heavy"));
            ui.Type.SetText(Text.Get("reloading_press_type_" + Clamp(GetInt(machine, TYPE_TAG), 2)));
            string cost = round.TntUl > 0
                ? Text.Get("reloading_press_cost_tnt", round.Powder, round.TntUl / 1000)
                : Text.Get("reloading_press_cost", round.Powder);
            ui.Target.SetText(LocHelper.GetLocalizedItem(round.NameKey) + "\n" + cost);
            string blocker = Blocker(machine, ui, round);
            ui.Status.SetText(Text.Get("reloading_press_hopper", GetInt(machine, POWDER_TAG), HOPPER_CAPACITY)
                + "\n" + Text.Get(blocker ?? "reloading_press_status_ready"));
        }
        catch (Exception e)
        {
            Log.Error("reloading press refresh failed", e);
        }
    }

    private static void Notify(string text)
    {
        if (StoreUIManager.Instance != null)
        {
            StoreUIManager.Instance.Notify(text, "default");
        }
    }

    private static void SpawnOne()
    {
        try
        {
            GraphUtils.TryAcceptAll(EmporiumEntry.Instance.invElement, DirectoryMaster.Item(ITEM_ID, true));
        }
        catch (Exception e)
        {
            Log.Error("reloading press debug spawn failed", e);
        }
    }
}
