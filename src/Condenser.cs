// Condenser and still. The condenser is a 2 L liquid container (3x3): pour liquid into it, by dragging a container onto
// it, and that liquid is its coolant. Dragged onto a vanilla heating plate it joins it: both become one 6x3 machine, the
// still, built from the vanilla heating plate (MachineHeatingPlate.HeatingPlate, so heating works exactly as on the
// plate) with three more panels, left to right: a battery slot, a slot for the collecting container, and the three mode
// switches (blue = on, red = off; switching one on switches the others off). Right-click the still to take the
// condenser off again.
// What boils off the container on the still's plate (vanilla HandleBoilingReaction) goes into the condenser's vapour
// chamber: 1 L, first in first out (when it is full, the oldest vapour is pushed out and lost). The mode decides how the
// vapour is condensed into the collecting container, oldest first:
//   - coil: once a night, 1 mL for every 8 mL of coolant; the coolant is not used up;
//   - evaporative: gradually (5% of the chamber a second, at least 1 mL), using up 4 mL of coolant per mL;
//   - closed loop: gradually like the evaporative mode, 5 mL per kJ from the battery; the coolant is not used up.
// Coolant that is not pure water works less well: every mL counts as (water purity)^2 of a mL of water, the purity being
// the vanilla one (water's share of the liquid). All three modes need coolant.
// Without a collecting container, or with no room in it, the vapour waits in the chamber.
// Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;

public static class Condenser
{
    public const string ITEM_ID = "vce_condenser";
    public const string STILL_ID = "vce_still";
    public const string PLATE_ID = "heating_plate";
    public const int CAPACITY_ML = 2000;
    public const int BASE_PRICE = 60;                       // vanilla's container price field
    public const long VALUE = 600;                          // the Cartel Vendor's price
    public const long PLATE_VALUE = 250;                    // the vanilla heating plate's
    public const double CONDENSATE_TEMP = 30.0;             // what the collected liquid arrives at

    public const int VAPOUR_CAPACITY_UL = 1000000;          // the vapour chamber
    private const int VAPOUR_BUCKET_UL = 10000;             // first in first out in steps of 10 mL
    public const double COIL_COOLANT_PER_ML = 8.0;          // coil: mL of coolant per mL condensed each night
    public const double EVAPORATIVE_COOLANT_PER_ML = 4.0;   // evaporative: mL of coolant used up per mL condensed
    public const double CLOSED_ML_PER_KJ = 5.0;             // closed loop: mL condensed per kJ
    // The evaporative and closed-loop modes condense gradually, like a reaction: each second 5% of the vapour in the
    // chamber, at least 1 mL (all of it when there is less). With a container boiling 2% of its liquid a second, the
    // chamber settles at about 40% of that container's boiling liquid (500 mL of water boiling: some 200 mL of vapour).
    public const double REALTIME_RATE = 0.05;
    public const int REALTIME_MIN_UL = 1000;

    public const int MODE_OFF = 0, MODE_COIL = 1, MODE_EVAPORATIVE = 2, MODE_CLOSED = 3;
    public const string MODE_TAG = "VCE_STILL_MODE_INT";
    public const string VAPOUR_TAG = "VCE_STILL_VAPOUR_STR";     // buckets "id=uL,id=uL;id=uL", oldest first
    public const string CREDIT_TAG = "VCE_STILL_KJ_CREDIT_INT";  // uL already paid for in the closed loop
    private static readonly string[] ModeKeys = { null, "still_mode_coil", "still_mode_evaporative", "still_mode_closed" };

    // Sprites: the condenser alone, and the still with or without a container on its plate; _water while it holds any
    // coolant, _dry when empty. The beaker in the art is clear: the collected liquid is drawn over it (BeakerLayer).
    public const string CONDENSER_SPRITE = "vce_condenser";
    public const string STILL_SPRITE = "vce_still";
    public const string SWITCH_SPRITE = Electrolyzer.SWITCH_SPRITE;   // the same blue / red switch

    private class Ui
    {
        public GameSlotInventory Heat;          // the vanilla plate's container slot
        public GameSlotInventory Battery;
        public GameSlotInventory Receiver;      // the collecting container
        public MachineryButton[] Switches = new MachineryButton[4];
        public int[] SwitchShows = { -1, -1, -1, -1 };
        public MachineryText Info;
        public MachineryText Status;
        public string LastStatus;
        public int StatusTtl;                   // seconds the last condensing status stays up
        public string LastKey;
        public UnityEngine.Sprite Beaker;
        public List<Dictionary<string, int>> Vapour; // cached from VAPOUR_TAG
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameItem, Ui> stills =
        new System.Runtime.CompilerServices.ConditionalWeakTable<GameItem, Ui>();
    // The still's plate slot -> the still, to find the still from a boiling container.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameSlotInventory, GameItem> heatSlots =
        new System.Runtime.CompilerServices.ConditionalWeakTable<GameSlotInventory, GameItem>();

    // Save flag "vanilla_chem_ext:condenser_test_kit" = "1": the test kit is given once on load.
    public const string TEST_FLAG_KEY = "condenser_test_kit";

    public static void Enable()
    {
        ModHook.OnGameLoadedLate += OnGameLoaded;
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Condenser: spawn one", SpawnOne, false);
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Condenser: test kit", GiveTestKit, false);
        }
        catch (Exception e)
        {
            Log.Error("condenser debug registration failed", e);
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
            if (ModHelper.GetModData(DreamToxinMod.MOD_ID, TEST_FLAG_KEY, null) == AntibioticTestKit.FLAG_PENDING)
            {
                ModHelper.SetModData(DreamToxinMod.MOD_ID, TEST_FLAG_KEY, AntibioticTestKit.FLAG_GIVEN);
                Log.Info("this save asked for the condenser test kit");
                GiveTestKit();
            }
        }
        catch (Exception e)
        {
            Log.Error("condenser test kit on load failed", e);
        }
    }

    public static void AddItems(ModItemDirectory directory)
    {
        directory.Add(ITEM_ID, CreateCondenser);
        directory.Add(STILL_ID, CreateStill);
    }

    public static bool IsCondenser(GameItem item)
    {
        return item != null && item.identifier == ITEM_ID;
    }

    public static bool IsStill(GameItem item)
    {
        return item != null && item.identifier == STILL_ID;
    }

    // ---- the condenser on its own ----------------------------------------------------------------------------------

    public static GameItem CreateCondenser()
    {
        GameItem item = ItemDirectory.CreateEmptyItem(ITEM_ID);
        try
        {
            item.name = Text.Get("condenser_name");
            item.shortDescription = Text.Get("condenser_desc");
            item.flavorText = "";
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            item.SetGameItemType("TOOL");
            WaterHelper.InitLiquidContainerItem(item, CAPACITY_ML, BASE_PRICE, false, true);
            item.unitValue = VALUE;
            AudioHelper.InitGlass(item);
            // Dragged onto a heating plate it joins it; onto anything else it behaves as the container it is.
            item.mayThisTargetItemFunc = (Func<GameItem, GameItem, bool>)Delegate.Combine(item.mayThisTargetItemFunc,
                (Func<GameItem, GameItem, bool>)((GameItem s, GameItem t) => CanJoin(s, t)));
            item.canThisTargetItemFunc = (Func<GameItem, GameItem, bool>)Delegate.Combine(item.canThisTargetItemFunc,
                (Func<GameItem, GameItem, bool>)((GameItem s, GameItem t) => CanJoin(s, t)));
            item.onThisTargetItemFunc = (Action<GameItem, GameItem>)Delegate.Combine(item.onThisTargetItemFunc,
                (Action<GameItem, GameItem>)delegate(GameItem s, GameItem t)
                {
                    if (CanJoin(s, t))
                    {
                        Join(s, t);
                    }
                });
            Action<GameItem, GameInventory, SlotMarker> tick = item.onOneSecondItemFunc;
            item.onOneSecondItemFunc = delegate(GameItem self, GameInventory inventory, SlotMarker marker)
            {
                if (tick != null)
                {
                    tick(self, inventory, marker);
                }
                ApplyCondenserSprite(self);
            };
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
            ApplyCondenserSprite(item);
        }
        catch (Exception e)
        {
            Log.Error("create " + ITEM_ID + " failed", e);
        }
        return item;
    }

    private static void ApplyCondenserSprite(GameItem item)
    {
        BeerSprites.Apply(item, CONDENSER_SPRITE + (WaterHelper.IsEmpty(item) ? "_dry" : "_water"));
    }

    private static bool CanJoin(GameItem condenser, GameItem target)
    {
        return IsCondenser(condenser) && target != null && target.identifier == PLATE_ID
            && GeneralHelper.IsItemOwned(condenser) && GeneralHelper.IsItemOwned(target);
    }

    // ---- the still: a vanilla heating plate with the condenser's panels ---------------------------------------------

    public static GameItem CreateStill()
    {
        GameItem still = MachineHeatingPlate.HeatingPlate();
        try
        {
            // The vanilla plate is built without an identifier (the directory sets it after the factory returns), but
            // making it a liquid container below runs vanilla code that reads it (SpriteHelper.UpdateWaterContainerSprite).
            still.identifier = STILL_ID;
            Ui ui = new Ui();
            ui.Heat = MachineHelper.GetBodyStorage(still, 1) as GameSlotInventory;
            MachineryPanelRow row = still.contentWindow != null ? still.contentWindow.childElement as MachineryPanelRow : null;
            if (row == null || ui.Heat == null)
            {
                Log.Info("still: the heating plate's window is not as expected; the condenser panels are left out");
                return still;
            }
            ui.Battery = new GameSlotInventory();
            ui.Receiver = new GameSlotInventory();
            ui.Info = new MachineryText("", 6, HorizontalAlignmentOptions.Left, false);
            ui.Status = new MachineryText("", 6, HorizontalAlignmentOptions.Center, false);
            // The three switch rows share a left edge (a vertical stack centres its rows, and the labels differ in length);
            // the block of rows is centred in the panel.
            MachineryStack switchRows = MachineryStack.Vertical().SetAlign(MachineryStack.Align.Start);
            for (int mode = MODE_COIL; mode <= MODE_CLOSED; mode++)
            {
                int m = mode;
                Sprite off = Xinke.ModSprite(SWITCH_SPRITE + "_off");
                Sprite offHover = Xinke.ModSprite(SWITCH_SPRITE + "_off_hl");
                ui.Switches[m] = off != null ? new MachineryButton(off, offHover, offHover, off, null)
                    : new MachineryButton(Text.Get(ModeKeys[m]));
                ui.Switches[m].SetCallback(delegate { Toggle(still, m); });
                switchRows.Add(MachineryStack.Horizontal(ui.Switches[m],
                    new MachineryText(Text.Get(ModeKeys[m]), 6, HorizontalAlignmentOptions.Left, false)));
            }
            List<PixelElement> modeRows = new List<PixelElement> { switchRows, ui.Info, ui.Status };

            row.Add(MachineHelper.Panel(Text.Get("still_label_battery"), ui.Battery, "battery", true));
            row.Add(MachineHelper.Panel(Text.Get("still_label_collect"), MachineHelper.WrapInIntake(ui.Receiver, true),
                "collect", true));
            row.Add(MachineHelper.Panel(Text.Get("still_label_modes"), MachineryStack.Vertical(modeRows.ToArray()), "modes", true));
            MachineHelper.SetupBatterySlot(ui.Battery, still);
            ui.Receiver.mayInventoryAddItemFunc = (GameItem other, GameInventory _) =>
                GeneralHelper.IsItemOwned(other) && other.IsTag("LIQUID_CONTAINER_TAG") && !IsCondenser(other) && !IsStill(other);
            ui.Receiver.SetBackgroundFadeSprite("Items/water_container4", "xl");
            stills.Add(still, ui);
            heatSlots.Add(ui.Heat, still);

            still.name = Text.Get("still_name");
            still.shortDescription = Text.Get("still_desc");
            // The coolant lives in the still itself: it is a liquid container too, so pouring into it fills the coolant.
            // A dropped item must not go into the plate's slot instead, so it is no longer a container to drop into.
            Action<GameItem, GameInventory, SlotMarker> plateTick = still.onOneSecondItemFunc;
            WaterHelper.InitLiquidContainerItem(still, CAPACITY_ML, BASE_PRICE, false, true);
            ChemicalHelper.InitContainerTempFunction(still);
            Action<GameItem, GameInventory, SlotMarker> coolantTick = still.onOneSecondItemFunc;
            still.onOneSecondItemFunc = delegate(GameItem self, GameInventory inventory, SlotMarker marker)
            {
                if (plateTick != null)
                {
                    plateTick(self, inventory, marker);
                }
                if (coolantTick != null)
                {
                    coolantTick(self, inventory, marker);
                }
                Condense(self, false);
                Refresh(self);
            };
            Action<GameItem, GameInventory, SlotMarker> cycleEnd = still.onCycleEndSlotItemFunc;
            still.onCycleEndSlotItemFunc = delegate(GameItem self, GameInventory inventory, SlotMarker marker)
            {
                if (cycleEnd != null)
                {
                    cycleEnd(self, inventory, marker);
                }
                Condense(self, true);
            };
            still.DisableTag("CONTAINER_TAG", false);
            still.SetGameItemType("MACHINE");
            still.unitValue = PLATE_VALUE + VALUE;
            // The vanilla plate swaps in its own 3x3 sprite when a container goes on or comes off; keep ours.
            ui.Heat.onSlotAddItemEndFunc = delegate { ApplyStillSprite(still); };
            ui.Heat.onSlotRemoveItemEndFunc = delegate { ApplyStillSprite(still); };
            ui.Receiver.onSlotAddItemEndFunc = delegate { ui.LastKey = null; Refresh(still); };
            ui.Receiver.onSlotRemoveItemEndFunc = delegate { ui.LastKey = null; Refresh(still); };
            still.getItemSpriteModifierForItemFunc = (GameItem self) => BeakerLayer(self);
            // Right-click: take the condenser off.
            still.mayToggleSlotItemFunc = (GameItem s, GameInventory _, SlotMarker __) => IsStill(s) && GeneralHelper.IsItemOwned(s);
            still.canToggleSlotItemFunc = (GameItem s, GameInventory _, SlotMarker __) => IsStill(s);
            still.onToggleSlotItemFunc = delegate(GameItem s, GameInventory _, SlotMarker __) { Detach(s); };
            ModHelper.SetSource(still, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
            ApplyStillSprite(still);
            Refresh(still);
        }
        catch (Exception e)
        {
            Log.Error("create " + STILL_ID + " failed", e);
        }
        return still;
    }

    private static void ApplyStillSprite(GameItem still)
    {
        Ui ui;
        bool onPlate = stills.TryGetValue(still, out ui) && ui.Heat != null && ui.Heat.childItem != null;
        BeerSprites.Apply(still, STILL_SPRITE + (onPlate ? "_b" : "") + (WaterHelper.IsEmpty(still) ? "_dry" : "_water"));
    }

    // ---- modes ---------------------------------------------------------------------------------------------------------

    public static int ModeOf(GameItem still)
    {
        int mode = still.IsTag(MODE_TAG) ? still.GetTagReadonly(MODE_TAG).GetInt() : MODE_OFF;
        return mode >= MODE_OFF && mode <= MODE_CLOSED ? mode : MODE_OFF;
    }

    // Switching a mode on switches the others off; switching the one that is on off leaves all three off.
    private static void Toggle(GameItem still, int mode)
    {
        try
        {
            int next = ModeOf(still) == mode ? MODE_OFF : mode;
            still.ModifyTag(MODE_TAG, delegate(TagState state) { state.Enable().SetInt(next); }, false);
            Ui ui;
            if (stills.TryGetValue(still, out ui))
            {
                ui.LastStatus = null;
                ui.StatusTtl = 0;
            }
            SyncSwitches(still);
            Refresh(still);
        }
        catch (Exception e)
        {
            Log.Error("still mode switch failed", e);
        }
    }

    private static void SyncSwitches(GameItem still)
    {
        Ui ui;
        if (!stills.TryGetValue(still, out ui))
        {
            return;
        }
        int mode = ModeOf(still);
        for (int m = MODE_COIL; m <= MODE_CLOSED; m++)
        {
            int shows = m == mode ? 1 : 0;
            if (ui.Switches[m] == null || ui.SwitchShows[m] == shows)
            {
                continue;
            }
            string key = SWITCH_SPRITE + (shows == 1 ? "_on" : "_off");
            Sprite normal = Xinke.ModSprite(key);
            Sprite hover = Xinke.ModSprite(key + "_hl");
            if (normal != null)
            {
                ui.Switches[m].SetFaceSprites(normal, hover, hover, normal);
            }
            ui.Switches[m].SetDescription(Text.Get(ModeKeys[m] + "_desc") + "\n"
                + Text.Get(shows == 1 ? "still_switch_on" : "still_switch_off"));
            ui.SwitchShows[m] = shows;
        }
    }

    // ---- the vapour chamber: buckets of vapour, oldest first ---------------------------------------------------------

    private static List<Dictionary<string, int>> Vapour(GameItem still, Ui ui)
    {
        if (ui.Vapour != null)
        {
            return ui.Vapour;
        }
        ui.Vapour = new List<Dictionary<string, int>>();
        string text = still.IsTag(VAPOUR_TAG) ? still.GetTagReadonly(VAPOUR_TAG).GetString() : null;
        if (!string.IsNullOrEmpty(text))
        {
            foreach (string bucketText in text.Split(';'))
            {
                Dictionary<string, int> bucket = new Dictionary<string, int>();
                foreach (string part in bucketText.Split(','))
                {
                    int eq = part.IndexOf('=');
                    int amount;
                    if (eq > 0 && int.TryParse(part.Substring(eq + 1), out amount) && amount > 0)
                    {
                        bucket[part.Substring(0, eq)] = amount;
                    }
                }
                if (bucket.Count > 0)
                {
                    ui.Vapour.Add(bucket);
                }
            }
        }
        return ui.Vapour;
    }

    private static void SaveVapour(GameItem still, Ui ui)
    {
        StringBuilder text = new StringBuilder();
        foreach (Dictionary<string, int> bucket in ui.Vapour)
        {
            if (text.Length > 0)
            {
                text.Append(';');
            }
            bool first = true;
            foreach (KeyValuePair<string, int> entry in bucket)
            {
                if (!first)
                {
                    text.Append(',');
                }
                text.Append(entry.Key).Append('=').Append(entry.Value);
                first = false;
            }
        }
        string value = text.ToString();
        still.ModifyTag(VAPOUR_TAG, delegate(TagState state) { state.Enable().SetString(value); }, true);
    }

    private static int Total(Dictionary<string, int> bucket)
    {
        int total = 0;
        foreach (int amount in bucket.Values)
        {
            total += amount;
        }
        return total;
    }

    public static int VapourTotal(GameItem still)
    {
        Ui ui;
        if (!stills.TryGetValue(still, out ui))
        {
            return 0;
        }
        int total = 0;
        foreach (Dictionary<string, int> bucket in Vapour(still, ui))
        {
            total += Total(bucket);
        }
        return total;
    }

    // What boiled off a container on the still's plate this tick (liquid id -> uL) goes into the chamber; whatever no longer
    // fits pushes out the oldest vapour.
    public static void AddVapour(GameItem still, Dictionary<string, int> boiled)
    {
        Ui ui;
        if (!stills.TryGetValue(still, out ui))
        {
            return;
        }
        List<Dictionary<string, int>> vapour = Vapour(still, ui);
        Dictionary<string, int> last = vapour.Count > 0 ? vapour[vapour.Count - 1] : null;
        if (last == null || Total(last) >= VAPOUR_BUCKET_UL)
        {
            last = new Dictionary<string, int>();
            vapour.Add(last);
        }
        foreach (KeyValuePair<string, int> entry in boiled)
        {
            int had;
            last.TryGetValue(entry.Key, out had);
            last[entry.Key] = had + entry.Value;
        }
        int total = 0;
        foreach (Dictionary<string, int> bucket in vapour)
        {
            total += Total(bucket);
        }
        if (total > VAPOUR_CAPACITY_UL)
        {
            Take(vapour, total - VAPOUR_CAPACITY_UL);       // lost through the relief valve
        }
        SaveVapour(still, ui);
    }

    // Removes up to `amount` uL from the oldest buckets and returns what was taken (id -> uL).
    private static Dictionary<string, int> Take(List<Dictionary<string, int>> vapour, int amount)
    {
        Dictionary<string, int> taken = new Dictionary<string, int>();
        while (amount > 0 && vapour.Count > 0)
        {
            Dictionary<string, int> bucket = vapour[0];
            int total = Total(bucket);
            if (total <= amount)
            {
                foreach (KeyValuePair<string, int> entry in bucket)
                {
                    int had;
                    taken.TryGetValue(entry.Key, out had);
                    taken[entry.Key] = had + entry.Value;
                }
                vapour.RemoveAt(0);
                amount -= total;
                continue;
            }
            double share = (double)amount / total;
            foreach (string id in new List<string>(bucket.Keys))
            {
                int part = Math.Min(bucket[id], (int)Math.Round(bucket[id] * share));
                if (part <= 0)
                {
                    continue;
                }
                int had;
                taken.TryGetValue(id, out had);
                taken[id] = had + part;
                bucket[id] -= part;
                if (bucket[id] <= 0)
                {
                    bucket.Remove(id);
                }
            }
            if (bucket.Count == 0)
            {
                vapour.RemoveAt(0);
            }
            amount = 0;
        }
        return taken;
    }

    // ---- condensing ----------------------------------------------------------------------------------------------------

    // Water's share of the coolant, squared: how much one mL of this coolant does compared with pure water.
    public static double CoolantFactor(GameItem still)
    {
        double purity = WaterHelper.GetWaterPurity(still) / 10000.0;
        return purity * purity;
    }

    // Every second (atNight false) for the evaporative and closed-loop modes, at the end of the night (atNight true) for
    // the coil.
    private static void Condense(GameItem still, bool atNight)
    {
        Ui ui;
        if (!stills.TryGetValue(still, out ui))
        {
            return;
        }
        try
        {
            int mode = ModeOf(still);
            if (mode == MODE_OFF || (mode == MODE_COIL) != atNight)
            {
                return;
            }
            List<Dictionary<string, int>> vapour = Vapour(still, ui);
            if (vapour.Count == 0)
            {
                return;
            }
            int waiting = VapourTotal(still);
            GameItem receiver = ui.Receiver.childItem;
            if (receiver == null || !receiver.IsTag("LIQUID_CONTAINER_TAG"))
            {
                SetStatus(ui, "still_status_no_receiver");
                return;
            }
            int room = WaterHelper.GetFreeCapacity(receiver);
            if (room <= 0)
            {
                SetStatus(ui, "still_status_full");
                return;
            }
            int coolant = WaterHelper.GetTotalVolume(still);
            double factor = CoolantFactor(still);
            if (coolant <= 0 || factor <= 0)
            {
                SetStatus(ui, "still_status_no_coolant");
                return;
            }
            int step = mode == MODE_COIL ? waiting
                : Math.Min(waiting, Math.Max(REALTIME_MIN_UL, (int)(waiting * REALTIME_RATE)));
            int want = Math.Min(step, room);
            int condensed;
            if (mode == MODE_COIL)
            {
                condensed = Math.Min(want, (int)(coolant / COIL_COOLANT_PER_ML * factor));
            }
            else if (mode == MODE_EVAPORATIVE)
            {
                double perUl = EVAPORATIVE_COOLANT_PER_ML / factor;
                condensed = Math.Min(want, (int)(coolant / perUl));
                if (condensed > 0)
                {
                    UseCoolant(still, (int)Math.Ceiling(condensed * perUl));
                }
            }
            else
            {
                condensed = PayClosedLoop(still, ui, want, factor);
                if (condensed <= 0)
                {
                    SetStatus(ui, "still_status_no_power");
                    return;
                }
            }
            if (condensed <= 0)
            {
                SetStatus(ui, "still_status_no_coolant");
                return;
            }
            Dictionary<string, int> taken = Take(vapour, condensed);
            SaveVapour(still, ui);
            foreach (KeyValuePair<string, int> entry in taken)
            {
                ChemicalHelper.HandleReceiveLiquidTemp(receiver, entry.Value, CONDENSATE_TEMP);
                WaterHelper.AddLiquid(receiver, entry.Key, entry.Value);
            }
            LiquidsChanged(receiver);
            SpriteHelper.UpdateWaterContainerSprite(receiver);
            SetStatus(ui, condensed < want ? (mode == MODE_CLOSED ? "still_status_low_power" : "still_status_low_coolant")
                : "still_status_condensing");
        }
        catch (Exception e)
        {
            Log.Error("still condensing failed", e);
        }
    }

    // Closed loop: draws whole kJ from the battery as needed; what a kJ condenses beyond this tick is kept as credit.
    private static int PayClosedLoop(GameItem still, Ui ui, int want, double factor)
    {
        int credit = still.IsTag(CREDIT_TAG) ? still.GetTagReadonly(CREDIT_TAG).GetInt() : 0;
        int perKj = (int)(CLOSED_ML_PER_KJ * 1000 * factor);
        GameItem battery = ui.Battery.childItem;
        while (credit < want && perKj > 0 && battery != null && PowerHelper.CanDrawPowerSource(battery, 1)
               && PowerHelper.DrawPowerSource(battery, 1))
        {
            credit += perKj;
        }
        int condensed = Math.Min(want, credit);
        int left = credit - condensed;
        still.ModifyTag(CREDIT_TAG, delegate(TagState state) { state.Enable().SetInt(left); }, true);
        return condensed;
    }

    // Evaporative cooling uses up coolant, every liquid in it in proportion.
    private static void UseCoolant(GameItem still, int amount)
    {
        int total = WaterHelper.GetTotalVolume(still);
        if (total <= 0 || amount <= 0)
        {
            return;
        }
        double share = Math.Min(1.0, (double)amount / total);
        foreach (Liquid liquid in Liquid.Liquids)
        {
            int have = IaLiquids.Get(still, liquid.identifier);
            if (have > 0)
            {
                IaLiquids.Set(still, liquid.identifier, have - Math.Min(have, (int)Math.Ceiling(have * share)));
            }
        }
        LiquidsChanged(still);
    }

    private static void SetStatus(Ui ui, string key)
    {
        ui.LastStatus = Text.Get(key);
        ui.StatusTtl = 2;
    }

    // ---- joining and taking apart ----------------------------------------------------------------------------------

    private static void Join(GameItem condenser, GameItem plate)
    {
        try
        {
            GraphNodeStorage parent = plate.parentInventory;
            GameItem still = DirectoryMaster.Item(STILL_ID, GeneralHelper.IsItemOwned(plate));
            if (!IsStill(still))
            {
                return;
            }
            CopyInt(plate, still, MachineHeatingPlate.HEATING_PLATE_STATE);
            CopyInt(plate, still, MachineHeatingPlate.CURRENT_TARGET_TEMP);
            CopyInt(condenser, still, MODE_TAG);
            CopyInt(condenser, still, CREDIT_TAG);
            CopyString(condenser, still, VAPOUR_TAG);
            MoveAll(MachineHelper.GetBodyStorage(plate, 0), MachineHelper.GetBodyStorage(still, 0));   // modules
            MoveAll(MachineHelper.GetBodyStorage(plate, 1), MachineHelper.GetBodyStorage(still, 1));   // the plate's container
            CopyLiquids(condenser, still);
            plate.Destroy();
            condenser.Destroy();
            Place(parent, still);
            SyncSwitches(still);
            ApplyStillSprite(still);
            Refresh(still);
            AudioHelper.PourWater();
            Notify(Text.Get("still_joined"));
        }
        catch (Exception e)
        {
            Log.Error("joining a condenser to a heating plate failed", e);
        }
    }

    private static void Detach(GameItem still)
    {
        try
        {
            Ui ui;
            if (!stills.TryGetValue(still, out ui))
            {
                return;
            }
            bool owned = GeneralHelper.IsItemOwned(still);
            GraphNodeStorage parent = still.parentInventory;
            GameItem plate = DirectoryMaster.Item(PLATE_ID, owned);
            GameItem condenser = DirectoryMaster.Item(ITEM_ID, owned);
            if (plate == null || !IsCondenser(condenser))
            {
                return;
            }
            CopyInt(still, plate, MachineHeatingPlate.HEATING_PLATE_STATE);
            CopyInt(still, plate, MachineHeatingPlate.CURRENT_TARGET_TEMP);
            CopyInt(still, condenser, MODE_TAG);
            CopyInt(still, condenser, CREDIT_TAG);
            CopyString(still, condenser, VAPOUR_TAG);
            MoveAll(MachineHelper.GetBodyStorage(still, 0), MachineHelper.GetBodyStorage(plate, 0));
            MoveAll(MachineHelper.GetBodyStorage(still, 1), MachineHelper.GetBodyStorage(plate, 1));
            CopyLiquids(still, condenser);
            foreach (GameSlotInventory slot in new GameSlotInventory[] { ui.Battery, ui.Receiver })
            {
                if (slot.childItem != null)
                {
                    GraphUtils.EmporiumTryAdd(slot.childItem);
                }
            }
            still.Destroy();
            Place(parent, plate);
            Place(parent, condenser);
            ApplyCondenserSprite(condenser);
            Notify(Text.Get("still_detached"));
        }
        catch (Exception e)
        {
            Log.Error("taking the condenser off a still failed", e);
        }
    }

    private static void CopyInt(GameItem from, GameItem to, string tag)
    {
        if (!from.IsTag(tag))
        {
            return;
        }
        int value = from.GetTagReadonly(tag).GetInt();
        to.ModifyTag(tag, delegate(TagState state) { state.Enable().SetInt(value); }, false);
    }

    private static void CopyString(GameItem from, GameItem to, string tag)
    {
        if (!from.IsTag(tag))
        {
            return;
        }
        string value = from.GetTagReadonly(tag).GetString();
        to.ModifyTag(tag, delegate(TagState state) { state.Enable().SetString(value); }, false);
    }

    private static void MoveAll(GraphNodeStorage from, GraphNodeStorage to)
    {
        if (from == null || to == null)
        {
            return;
        }
        List<GameItem> items = new List<GameItem>();
        GameSlotInventory slot = from as GameSlotInventory;
        GameGridInventory grid = from as GameGridInventory;
        if (slot != null && slot.childItem != null)
        {
            items.Add(slot.childItem);
        }
        else if (grid != null)
        {
            items.AddRange(grid.childItems);
        }
        foreach (GameItem item in items)
        {
            GraphUtils.TryAcceptAll(to, item);
            if (item.parentInventory == from)
            {
                GraphUtils.EmporiumTryAdd(item);      // did not fit: keep it rather than lose it with the old machine
            }
        }
    }

    private static void CopyLiquids(GameItem from, GameItem to)
    {
        foreach (Liquid liquid in Liquid.Liquids)
        {
            int amount = IaLiquids.Get(from, liquid.identifier);
            if (amount > 0)
            {
                IaLiquids.Set(to, liquid.identifier, amount);
            }
        }
        double temp = from.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
        to.ModifyTag("CONTAINER_TEMP_TAG", delegate(TagState state) { state.Enable().SetDouble(temp); }, true);
        LiquidsChanged(to);
    }

    private static void Place(GraphNodeStorage parent, GameItem item)
    {
        if (parent != null)
        {
            GraphUtils.TryAcceptAll(parent, item);
        }
        if (item.parentInventory == null)
        {
            GraphUtils.EmporiumTryAdd(item);
        }
    }

    private static void LiquidsChanged(GameItem container)
    {
        ItemFeature feature = WaterFeatureHelper.GetWaterFeature(container);
        WaterFeatureHelper.OnLiquidChanged(container, feature != null && feature.IsExposed());
    }

    // The still whose plate holds this container, or null.
    public static GameItem StillOf(GameItem container)
    {
        GameSlotInventory slot = container != null ? container.parentInventory as GameSlotInventory : null;
        GameItem still;
        return slot != null && heatSlots.TryGetValue(slot, out still) && still != null ? still : null;
    }

    // ---- window and picture ------------------------------------------------------------------------------------------

    private static void Refresh(GameItem still)
    {
        Ui ui;
        if (!stills.TryGetValue(still, out ui))
        {
            return;
        }
        try
        {
            ApplyStillSprite(still);
            GameItem receiver = ui.Receiver.childItem;
            string key = receiver == null ? "none"
                : WaterHelper.GetTotalVolume(receiver) / 1000 + "|" + WaterHelper.GetLiquidContainerColor(receiver);
            if (key != ui.LastKey)
            {
                ui.LastKey = key;
                ui.Beaker = BuildBeaker(receiver);
                GameItemElement element = still as GameItemElement;
                if (element != null)
                {
                    element.HandleInnateModSprite();
                }
            }
            if (still.contentWindow == null || !still.contentWindow.IsVisible())
            {
                return;
            }
            SyncSwitches(still);
            int coolant = WaterHelper.GetTotalVolume(still);
            double factor = CoolantFactor(still);
            int purity = (int)Math.Round(WaterHelper.GetWaterPurity(still) / 100.0);
            string text = Text.Get("still_info_vapour", Ml(VapourTotal(still)), VAPOUR_CAPACITY_UL / 1000)
                + "\n" + Text.Get("still_info_coolant", Ml(coolant), CAPACITY_ML, coolant > 0 ? purity : 0);
            int mode = ModeOf(still);
            if (mode == MODE_COIL)
            {
                text += "\n" + Text.Get("still_info_coil", Ml((long)(coolant / COIL_COOLANT_PER_ML * factor)));
            }
            else if (mode == MODE_EVAPORATIVE)
            {
                text += "\n" + (factor > 0 ? Text.Get("still_info_evaporative", (EVAPORATIVE_COOLANT_PER_ML / factor).ToString("0.#"))
                    : Text.Get("still_status_no_coolant"));
            }
            else if (mode == MODE_CLOSED)
            {
                text += "\n" + Text.Get("still_info_closed", (CLOSED_ML_PER_KJ * factor).ToString("0.#"));
            }
            else
            {
                text += "\n" + Text.Get("still_info_off");
            }
            ui.Info.SetText(text);
            if (ui.StatusTtl > 0)
            {
                ui.StatusTtl--;
            }
            else
            {
                ui.LastStatus = null;
            }
            string status = receiver == null ? Text.Get("still_status_no_receiver")
                : (ui.LastStatus ?? Text.Get("still_status_idle"));
            ui.Status.SetText(status);
        }
        catch (Exception e)
        {
            Log.Error("still refresh failed", e);
        }
    }

    private static string Ml(long ul)
    {
        return (ul / 1000.0).ToString("0.#");
    }

    // The clear beaker in the art (draw_still.py: centre x 86, half width 6.5, mouth row 29.5, bottom row 38.5, ellipses
    // half as tall as wide), filled from the bottom with the collecting container's liquid, as full as that container is.
    private const float B_CX = 86f, B_A = 6.5f, B_TOP = 29.5f, B_BOT = 38.5f, B_RATIO = 0.5f;
    private const int SPRITE_W = 96, SPRITE_H = 48;

    private static bool InEllipse(int x, int y, float cx, float cy, float a, float b)
    {
        float dx = (x + 0.5f - cx) / a, dy = (y + 0.5f - cy) / b;
        return dx * dx + dy * dy <= 1f;
    }

    private static bool InBeaker(int x, int y)
    {
        // inside the walls and the bottom ellipse, one pixel in from the outline
        float a = B_A - 1f, b = (B_A - 1f) * B_RATIO;
        if (x + 0.5f < B_CX - a || x + 0.5f > B_CX + a)
        {
            return false;
        }
        return (y + 0.5f > B_TOP + 1f && y + 0.5f <= B_BOT) || InEllipse(x, y, B_CX, B_BOT, a, b);
    }

    private static Sprite BuildBeaker(GameItem receiver)
    {
        if (receiver == null)
        {
            return null;
        }
        int volume = WaterHelper.GetTotalVolume(receiver);
        int capacity = receiver.GetTagReadonly("LIQUID_CONTAINER_CAPACITY").GetInt();
        if (volume <= 0 || capacity <= 0)
        {
            return null;
        }
        Sprite baseSprite = BaseSprite(STILL_SPRITE + "_dry");
        if (baseSprite == null)
        {
            return null;
        }
        // Rows from the bottom up, filled by area like the separatory funnel's liquid.
        List<int> rows = new List<int>();
        List<int> widths = new List<int>();
        int area = 0;
        for (int y = SPRITE_H - 1; y >= 0; y--)
        {
            int w = 0;
            for (int x = 0; x < SPRITE_W; x++)
            {
                if (InBeaker(x, y))
                {
                    w++;
                }
            }
            if (w > 0)
            {
                rows.Add(y);
                widths.Add(w);
                area += w;
            }
        }
        double target = area * Math.Min(1.0, (double)volume / (capacity * 1000.0));
        int filled = 0;
        double sum = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0 && sum + widths[i] / 2.0 > target)
            {
                break;
            }
            sum += widths[i];
            filled = i + 1;
        }
        Color colour = ColorOf(WaterHelper.GetLiquidContainerColor(receiver));
        Color32[] px = new Color32[SPRITE_W * SPRITE_H];
        for (int i = 0; i < filled; i++)
        {
            int y = rows[i];
            bool surface = i == filled - 1;
            for (int x = 0; x < SPRITE_W; x++)
            {
                if (!InBeaker(x, y))
                {
                    continue;
                }
                float shade = surface ? 1.2f : (x + 0.5f > B_CX + B_A - 3f ? 0.82f : 1f);
                px[(SPRITE_H - 1 - y) * SPRITE_W + x] = new Color(Math.Min(1f, colour.r * shade),
                    Math.Min(1f, colour.g * shade), Math.Min(1f, colour.b * shade), 0.9f);
            }
        }
        Texture2D tex = new Texture2D(SPRITE_W, SPRITE_H, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels32(px);
        tex.Apply();
        Vector2 pivot = new Vector2(baseSprite.pivot.x / baseSprite.rect.width, baseSprite.pivot.y / baseSprite.rect.height);
        return Sprite.Create(tex, new Rect(0, 0, SPRITE_W, SPRITE_H), pivot, baseSprite.pixelsPerUnit);
    }

    private static List<ItemSpriteModifier> BeakerLayer(GameItem still)
    {
        Ui ui;
        if (!stills.TryGetValue(still, out ui) || ui.Beaker == null)
        {
            return null;
        }
        return new List<ItemSpriteModifier>
        {
            new ItemSpriteModifier(ui.Beaker, new Vector2(0f, 0f), 0, false, 0, false, Color.white)
        };
    }

    private static Sprite BaseSprite(string key)
    {
        LoadedMod mod = ModLoader.Mods.Find(m => m.Manifest != null && m.Manifest.ID == DreamToxinMod.MOD_ID);
        Sprite sprite;
        return mod != null && mod.Sprites.TryGetValue(key, out sprite) ? sprite : null;
    }

    // Vanilla gives water pure white (its bottles turn it blue); show near-white liquid as pale water blue.
    private static Color ColorOf(string hex)
    {
        Color colour;
        if (string.IsNullOrEmpty(hex) || !ColorUtility.TryParseHtmlString("#" + hex, out colour))
        {
            return new Color(0.56f, 0.76f, 0.82f);
        }
        if (Math.Min(colour.r, Math.Min(colour.g, colour.b)) > 0.85f)
        {
            return new Color(0.56f, 0.76f, 0.82f);
        }
        return colour;
    }

    private static void Notify(string text)
    {
        if (StoreUIManager.Instance != null)
        {
            StoreUIManager.Instance.Notify(text, "default");
        }
    }

    // ---- debug ---------------------------------------------------------------------------------------------------------

    private static void SpawnOne()
    {
        Give(DirectoryMaster.Item(ITEM_ID, true));
    }

    // A condenser with 2 L of pure water, a heating plate, a 100 kJ battery (closed loop), a bottle of beer-strength
    // wash (water + 10% alcohol) to distil, a bottle of half water, half alcohol (impure coolant: purity 50%, so a
    // quarter of the effect) and two empty bottles to collect in.
    public static void GiveTestKit()
    {
        try
        {
            GameItem condenser = DirectoryMaster.Item(ITEM_ID, true);
            if (condenser != null)
            {
                WaterHelper.AddLiquid(condenser, "water", CAPACITY_ML * 1000);
                LiquidsChanged(condenser);
                ApplyCondenserSprite(condenser);
            }
            Give(condenser);
            Give(DirectoryMaster.Item(PLATE_ID, true));
            Give(DirectoryMaster.Item("energy_credit", true));
            Give(Bottle(450000, 50000));
            Give(Bottle(250000, 250000));
            Give(Bottle(0, 0));
            Give(Bottle(0, 0));
        }
        catch (Exception e)
        {
            Log.Error("condenser test kit failed", e);
        }
    }

    private static GameItem Bottle(int waterUl, int alcoholUl)
    {
        GameItem bottle = DirectoryMaster.Item("bottled_water", true);
        if (bottle == null)
        {
            return null;
        }
        WaterHelper.EmptyContainer(bottle);
        if (waterUl > 0)
        {
            WaterHelper.AddLiquid(bottle, "water", waterUl);
        }
        if (alcoholUl > 0)
        {
            WaterHelper.AddLiquid(bottle, "alcohol", alcoholUl);
        }
        LiquidsChanged(bottle);
        SpriteHelper.UpdateWaterContainerSprite(bottle);
        return bottle;
    }

    private static void Give(GameItem item)
    {
        if (item == null)
        {
            return;
        }
        GraphUtils.EmporiumTryAdd(item);
        if (item.parentInventory == null)
        {
            Log.Info("condenser: no room for " + item.identifier);
        }
    }
}

// Catches what boils off a container standing on a still's plate. Snapshot before the vanilla boiling (and before the
// boil speed setting changes it), compare after both; the difference goes into the still's vapour chamber.
[HarmonyPatch(typeof(ChemicalHelper), "HandleBoilingReaction")]
public static class StillBoilingPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(GameItem container, out Dictionary<string, int> __state)
    {
        __state = null;
        try
        {
            if (Condenser.StillOf(container) == null)
            {
                return;
            }
            __state = new Dictionary<string, int>();
            foreach (Liquid liquid in Liquid.Liquids)
            {
                if (liquid.isEvaporative)
                {
                    int amount = IaLiquids.Get(container, liquid.identifier);
                    if (amount > 0)
                    {
                        __state[liquid.identifier] = amount;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("still boiling snapshot failed", e);
        }
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(GameItem container, Dictionary<string, int> __state)
    {
        if (__state == null || __state.Count == 0)
        {
            return;
        }
        try
        {
            GameItem still = Condenser.StillOf(container);
            if (still == null)
            {
                return;
            }
            Dictionary<string, int> boiled = new Dictionary<string, int>();
            foreach (KeyValuePair<string, int> entry in __state)
            {
                int gone = entry.Value - IaLiquids.Get(container, entry.Key);
                if (gone > 0)
                {
                    boiled[entry.Key] = gone;
                }
            }
            if (boiled.Count > 0)
            {
                Condenser.AddVapour(still, boiled);
            }
        }
        catch (Exception e)
        {
            Log.Error("still vapour capture failed", e);
        }
    }
}

// A heating plate's container slot does not take a condenser (dragging one onto a plate joins them instead) or a still.
[HarmonyPatch(typeof(MachineHeatingPlate), "HeatingPlate")]
public static class HeatingPlateRefuseCondenserPatch
{
    private static void Postfix(GameItem __result)
    {
        try
        {
            GameSlotInventory slot = MachineHelper.GetBodyStorage(__result, 1) as GameSlotInventory;
            if (slot == null)
            {
                return;
            }
            Func<GameItem, GameInventory, bool> mayAdd = slot.mayInventoryAddItemFunc;
            slot.mayInventoryAddItemFunc = (GameItem item, GameInventory inventory) =>
                !Condenser.IsCondenser(item) && !Condenser.IsStill(item) && mayAdd != null && mayAdd(item, inventory);
        }
        catch (Exception e)
        {
            Log.Error("heating plate condenser refusal failed", e);
        }
    }
}

// The right-click toggle button reads "Take the condenser off" on a still.
[HarmonyPatch(typeof(ItemContextHandler), "SetButtonColor")]
public static class StillDetachLabelPatch
{
    private static void Postfix(RichTextElement button, RichTextElement selectedButton, bool isEnabled, GameItem ___currentItem,
        RichTextElement ___buttonToggle, RenderHandler.ColorPalette ___textColor, RenderHandler.ColorPalette ___textDisabledColor,
        RenderHandler.ColorPalette ___textSelectColor, RenderHandler.ColorPalette ___textSelectDisabledColor)
    {
        try
        {
            if (button == null || button != ___buttonToggle || !Condenser.IsStill(___currentItem))
            {
                return;
            }
            bool selected = button == selectedButton;
            RenderHandler.ColorPalette color = !isEnabled ? (selected ? ___textSelectDisabledColor : ___textDisabledColor)
                : (selected ? ___textSelectColor : ___textColor);
            button.SetText(Text.Get("still_detach"), 10000, color);
        }
        catch (Exception e)
        {
            Log.Error("still detach label failed", e);
        }
    }
}
