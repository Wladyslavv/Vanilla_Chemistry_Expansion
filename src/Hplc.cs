// High-performance liquid chromatograph: a 3x4 machine (art/hplc/draw_hplc_large.py) that splits a sample into
// fractions by polarity (Polarity.cs, docs/POLARITY_DESIGN.md). Its window, left to right:
//   mobile phase container | sample container | column with four collection slots off it | polarity table | manual
//
// How it runs, every second:
//   - The mobile phase must be at least 99% methanol. It is used up: METHANOL_COST x the solute separated that second
//     x (POLARITY_BASE + the highest polarity in the sample), so 0.02 x (50 + P): 1 to 3 mL of methanol a mL of
//     solute. Short of methanol, the second separates only what the methanol left pays for.
//   - The sample must hold no particles (anything a water filter would catch): it has to be filtered first.
//   - The sample's solutes are sorted by polarity. Up to four: each gets a collection slot of its own, the least polar
//     in the bottom slot, rising slot by slot. More than four: the sorted list is cut at its three widest polarity
//     gaps, so solutes of close polarity share a slot and all four slots get some.
//   - Each second up to RATE_UL of solute leaves the sample, every solute by the same fraction of what is left, so
//     they all run out together and no solute changes slot half way. Each mL of solute takes one mL of the
//     sample's solvents with it (water, alcohol, methanol, in the sample's own proportions).
//   - It pauses if a collection slot that is due a fraction has no container or no room, or the sample has no
//     solvent left to carry its solutes.
// Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public static class Hplc
{
    public const string ITEM_ID = "vce_hplc";
    public const string SPRITE = "vce_hplc";
    public const string SEGMENT_SPRITE = "vce_hplc_seg_";   // + row 0..3
    public const long VALUE = 1000;                          // the Cartel Vendor's price (CartelVendor.cs)
    public const int FRACTIONS = 4;
    public const double MIN_METHANOL_SHARE = 0.99;
    public const int RATE_UL = 5000;                         // solute per second
    // Set once the manual has been put in, so it is never added twice.
    public const string MANUAL_TAG = "VCE_HPLC_MANUAL_TAG";
    public const double METHANOL_COST = 0.02;
    public const int POLARITY_BASE = 50;

    // Polarity table: a scrolling text box under three header buttons (name, polarity, slot). Columns are lined up
    // with TextMeshPro <pos> tags, so they hold whatever the glyph widths. Widths in UI pixels.
    public const int NAME_W = 104;
    public const int POLARITY_W = 30;
    public const int SLOT_W = 30;
    public const int TABLE_H = 100;
    public const int TABLE_TEXT_SIZE = 7;
    public const int NAME_MAX = 20;          // name length in half-width characters (a CJK character counts 2)
    private const int TEXT_PAD = 2;          // ScrollableRichTextElement's left padding
    private const int COL_NAME = 0, COL_POLARITY = 1, COL_SLOT = 2;

    private static readonly string[] SampleSolvents = { "water", "alcohol", IaLiquids.METHANOL };

    private class Ui
    {
        public GameSlotInventory Solvent;
        public GameSlotInventory Sample;
        public GameSlotInventory[] Fractions = new GameSlotInventory[FRACTIONS];   // 0 = top ... 3 = bottom
        public GameSlotInventory Note;
        public HplcTableView Table;
        public MachineryButton NameHead;
        public MachineryButton PolarityHead;
        public MachineryButton SlotHead;
        public MachineryText Notes;
        public MachineryText Status;
        // Sorting, for this window only: by name or by polarity, high to low or low to high.
        public int SortColumn = COL_POLARITY;
        public bool Descending = true;
        public string Shown;
        public bool ShownSampleMode;
    }

    private class Solute
    {
        public string Id;
        public string Name;
        public int Polarity;
        public int Amount;
        public int Slot;
    }

    public static void Enable()
    {
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "HPLC: spawn one", SpawnOne, false);
        }
        catch (Exception e)
        {
            Log.Error("HPLC debug function registration failed", e);
        }
    }

    public static void AddItems(ModItemDirectory directory)
    {
        directory.Add(ITEM_ID, Create);
    }

    public static bool IsHplc(GameItem item)
    {
        return item != null && item.identifier == ITEM_ID;
    }

    public static GameItem Create()
    {
        Ui ui = new Ui();
        ui.Solvent = new GameSlotInventory();
        ui.Sample = new GameSlotInventory();
        ui.Note = new GameSlotInventory();
        ui.Table = new HplcTableView(NAME_W + POLARITY_W + SLOT_W, TABLE_H);
        ui.NameHead = new MachineryButton("", null, NAME_W);
        ui.PolarityHead = new MachineryButton("", null, POLARITY_W);
        ui.SlotHead = new MachineryButton("", null, SLOT_W);
        ui.Notes = new MachineryText("", 6, HorizontalAlignmentOptions.Left, false);
        ui.Status = new MachineryText("", 6, HorizontalAlignmentOptions.Center, false);
        MachineryStack head = new MachineryStack(false, 0).SetMainAlign(MachineryStack.Align.Start);
        head.Add(ui.NameHead);
        head.Add(ui.PolarityHead);
        head.Add(ui.SlotHead);
        MachineryStack table = new MachineryStack(true, 2).SetAlign(MachineryStack.Align.Start);
        table.Add(head);
        table.Add(ui.Table);
        table.Add(ui.Notes);
        table.Add(ui.Status);

        // The column is cut into one segment per collection row (art/hplc/draw_hplc_ui.py): each row is
        // [segment | slot] and the rows are stacked, all with no gap (MachineryStack's default is 4), so the
        // segments join into one column and every branch meets its own slot. Segments are as tall as a row (28);
        // the slot's intake (24) is stretched to it.
        MachineryStack column = new MachineryStack(true, 0);
        for (int i = 0; i < FRACTIONS; i++)
        {
            ui.Fractions[i] = new GameSlotInventory();
            MachineryStack row = new MachineryStack(false, 0);
            row.Add(new HplcColumnSegment(i));
            row.Add(MachineHelper.WrapInIntake(ui.Fractions[i], true));
            column.Add(row);
        }

        PixelWindow window = MachineHelper.CreateMachineryWindow(true,
            MachineHelper.Panel(Text.Get("hplc_label_solvent"), ui.Solvent, "solvent", true),
            MachineHelper.Panel(Text.Get("hplc_label_sample"), ui.Sample, "sample", true),
            MachineHelper.Panel(Text.Get("hplc_label_column"), column, "column", true),
            MachineHelper.Panel(Text.Get("hplc_label_table"), table, "table", true),
            MachineHelper.NotePanel(ui.Note));

        GameItem machine = ItemDirectory.CreateEmptyItem(ITEM_ID).SetContentWindow(window);
        try
        {
            machine.name = Text.Get("hplc_name");
            machine.shortDescription = Text.Get("hplc_desc");
            machine.flavorText = "";
            machine.unitValue = VALUE;
            machine.SetGameItemType("MACHINE");
            machine.EnableTag("CONTAINER_TAG", false);
            AudioHelper.InitMetalMachine(machine);
            BeerSprites.Apply(machine, SPRITE);

            Func<GameItem, GameInventory, bool> ownedContainer =
                (GameItem item, GameInventory _) => GeneralHelper.IsItemOwned(item) && item.IsTag("LIQUID_CONTAINER_TAG");
            ui.Solvent.mayInventoryAddItemFunc = ownedContainer;
            ui.Solvent.SetBackgroundFadeSprite("Items/water_container4", "xl");
            ui.Solvent.slotPriorityFunc = (GameItem _, GameInventory __, SlotMarker ___) => 4;
            ui.Sample.mayInventoryAddItemFunc = ownedContainer;
            ui.Sample.SetBackgroundFadeSprite("Items/water_container4", "xl");
            ui.Sample.slotPriorityFunc = (GameItem _, GameInventory __, SlotMarker ___) => 5;
            foreach (GameSlotInventory fraction in ui.Fractions)
            {
                fraction.mayInventoryAddItemFunc = ownedContainer;
                fraction.slotPriorityFunc = (GameItem _, GameInventory __, SlotMarker ___) => 3;
            }
            MachineHelper.SetupManualSlot(ui.Note);
            ui.NameHead.SetCallback(delegate { SortBy(ui, COL_NAME); });
            ui.PolarityHead.SetCallback(delegate { SortBy(ui, COL_POLARITY); });
            ui.SlotHead.SetCallback(delegate { SortBy(ui, COL_SLOT); });

            // However the machine came about (shop, debug, older saves), its first tick once owned puts the manual
            // in, once; DirectoryMaster.Item strips children from new items, so this cannot happen in the factory.
            // Waiting for ownership keeps an unsold machine on the vendor's counter from holding an unowned note.
            machine.onOneSecondItemFunc = delegate(GameItem item, GameInventory inventory, SlotMarker marker)
            {
                try
                {
                    if (!item.IsTag(MANUAL_TAG) && GeneralHelper.IsItemOwned(item))
                    {
                        if (ui.Note.childItem == null)
                        {
                            GraphUtils.TryAcceptAll(ui.Note, CreateManual(true));
                        }
                        item.EnableTag(MANUAL_TAG, true);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("HPLC manual failed", e);
                }
                Tick(ui);
            };
            Tick(ui);
            ModHelper.SetSource(machine, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            Log.Error("create " + ITEM_ID + " failed", e);
        }
        return machine;
    }

    // ---- reading the sample ---------------------------------------------------------------------------------------

    private static bool HasParticles(GameItem sample)
    {
        foreach (Liquid liquid in Liquid.Liquids)
        {
            if (IaLiquids.Get(sample, liquid.identifier) > 0 && Polarity.KindOf(liquid.identifier) == Polarity.Kind.Particle)
            {
                return true;
            }
        }
        return false;
    }

    // The sample's solutes, least polar first, each with its collection slot (0 = top, 3 = bottom).
    private static List<Solute> Solutes(GameItem sample)
    {
        List<Solute> list = new List<Solute>();
        foreach (Liquid liquid in Liquid.Liquids)
        {
            int amount = IaLiquids.Get(sample, liquid.identifier);
            if (amount <= 0 || Polarity.KindOf(liquid.identifier) != Polarity.Kind.Solute)
            {
                continue;
            }
            Solute s = new Solute();
            s.Id = liquid.identifier;
            s.Name = liquid.displayName;
            s.Polarity = Polarity.Of(liquid.identifier);
            s.Amount = amount;
            list.Add(s);
        }
        list.Sort((a, b) => a.Polarity != b.Polarity ? a.Polarity.CompareTo(b.Polarity) : string.CompareOrdinal(a.Id, b.Id));
        AssignSlots(list);
        return list;
    }

    // Up to four solutes: one group each. More: cut the sorted list at its three widest polarity gaps (the earlier
    // gap wins a tie). Group 0 (least polar) goes to the bottom slot, then upward.
    private static void AssignSlots(List<Solute> sorted)
    {
        int n = sorted.Count;
        int[] group = new int[n];
        if (n <= FRACTIONS)
        {
            for (int i = 0; i < n; i++)
            {
                group[i] = i;
            }
        }
        else
        {
            List<int> gaps = new List<int>();
            for (int i = 0; i < n - 1; i++)
            {
                gaps.Add(i);
            }
            gaps.Sort((a, b) =>
            {
                int da = sorted[a + 1].Polarity - sorted[a].Polarity;
                int db = sorted[b + 1].Polarity - sorted[b].Polarity;
                return da != db ? db.CompareTo(da) : a.CompareTo(b);
            });
            List<int> cuts = gaps.GetRange(0, FRACTIONS - 1);
            cuts.Sort();
            int g = 0;
            for (int i = 0; i < n; i++)
            {
                group[i] = g;
                if (g < cuts.Count && i == cuts[g])
                {
                    g++;
                }
            }
        }
        for (int i = 0; i < n; i++)
        {
            sorted[i].Slot = FRACTIONS - 1 - group[i];
        }
    }

    private static bool MethanolReady(GameItem container)
    {
        if (container == null || WaterHelper.IsEmpty(container))
        {
            return false;
        }
        int total = WaterHelper.GetTotalVolume(container);
        return total > 0 && IaLiquids.Get(container, IaLiquids.METHANOL) >= total * MIN_METHANOL_SHARE;
    }

    // ---- every second -----------------------------------------------------------------------------------------------

    private static void Tick(Ui ui)
    {
        try
        {
            GameItem sample = ui.Sample.childItem;
            List<Solute> solutes = sample != null && !WaterHelper.IsEmpty(sample) ? Solutes(sample) : new List<Solute>();
            string status = Run(ui, sample, solutes);
            // Refresh what is left for the table.
            if (sample != null && !WaterHelper.IsEmpty(sample))
            {
                solutes = Solutes(sample);
            }
            else
            {
                solutes.Clear();
            }
            ShowTable(ui, sample, solutes);
            ui.Status.SetText(Text.Get(status));
        }
        catch (Exception e)
        {
            Log.Error("HPLC tick failed", e);
        }
    }

    // One second of separation; returns the status key.
    private static string Run(Ui ui, GameItem sample, List<Solute> solutes)
    {
        if (!MethanolReady(ui.Solvent.childItem))
        {
            return "hplc_status_no_methanol";
        }
        if (sample == null || WaterHelper.IsEmpty(sample))
        {
            return "hplc_status_no_sample";
        }
        if (HasParticles(sample))
        {
            return "hplc_status_particles";
        }
        if (solutes.Count == 0)
        {
            return "hplc_status_no_solute";
        }
        int[] solvent = new int[SampleSolvents.Length];
        long solventTotal = 0;
        for (int i = 0; i < SampleSolvents.Length; i++)
        {
            solvent[i] = IaLiquids.Get(sample, SampleSolvents[i]);
            solventTotal += solvent[i];
        }
        if (solventTotal <= 0)
        {
            return "hplc_status_no_solvent";
        }
        long soluteTotal = 0;
        foreach (Solute s in solutes)
        {
            soluteTotal += s.Amount;
        }
        // Methanol a uL of solute costs, and what the mobile phase holds: it must pay for at least 1 uL of each solute.
        GameItem mobile = ui.Solvent.childItem;
        double cost = CostPerUl(solutes);
        int methanol = IaLiquids.Get(mobile, IaLiquids.METHANOL);
        if (methanol < Math.Ceiling(cost * solutes.Count))
        {
            return "hplc_status_low_methanol";
        }
        // The same fraction of every solute, limited by the rate, by the solvent there is to carry it and by the
        // methanol there is to pay for it.
        double fraction = Math.Min(1.0, Math.Min((double)RATE_UL / soluteTotal, (double)solventTotal / soluteTotal));
        fraction = Math.Min(fraction, methanol / (cost * soluteTotal));
        int[] moved = new int[solutes.Count];
        long[] perSlot = new long[FRACTIONS];
        long movedTotal = 0;
        for (int i = 0; i < solutes.Count; i++)
        {
            moved[i] = fraction >= 1.0 ? solutes[i].Amount : Math.Max(1, (int)Math.Floor(solutes[i].Amount * fraction));
            moved[i] = Math.Min(moved[i], solutes[i].Amount);
            perSlot[solutes[i].Slot] += moved[i];
            movedTotal += moved[i];
        }
        int used = (int)Math.Min(methanol, Math.Ceiling(movedTotal * cost));
        // Every slot that is due a fraction needs a container with room for the solute and the solvent it carries.
        for (int k = 0; k < FRACTIONS; k++)
        {
            if (perSlot[k] <= 0)
            {
                continue;
            }
            GameItem target = ui.Fractions[k].childItem;
            if (target == null || WaterHelper.GetFreeCapacity(target) < perSlot[k] * 2)
            {
                return "hplc_status_slot_" + (k + 1);
            }
        }
        // Move it: each solute into its slot, with as much solvent, split in the sample's solvent proportions.
        for (int i = 0; i < solutes.Count; i++)
        {
            if (moved[i] <= 0)
            {
                continue;
            }
            GameItem target = ui.Fractions[solutes[i].Slot].childItem;
            IaLiquids.Set(sample, solutes[i].Id, solutes[i].Amount - moved[i]);
            WaterHelper.AddLiquid(target, solutes[i].Id, moved[i]);
            for (int j = 0; j < SampleSolvents.Length; j++)
            {
                if (solvent[j] <= 0)
                {
                    continue;
                }
                int carried = (int)Math.Round((double)moved[i] * solvent[j] / solventTotal);
                carried = Math.Min(carried, IaLiquids.Get(sample, SampleSolvents[j]));
                if (carried > 0)
                {
                    IaLiquids.Set(sample, SampleSolvents[j], IaLiquids.Get(sample, SampleSolvents[j]) - carried);
                    WaterHelper.AddLiquid(target, SampleSolvents[j], carried);
                }
            }
        }
        IaLiquids.Set(mobile, IaLiquids.METHANOL, methanol - used);
        WaterFeatureHelper.OnLiquidContainerPourOutOf(mobile);
        SpriteHelper.UpdateWaterContainerSprite(mobile);
        WaterFeatureHelper.OnLiquidContainerPourOutOf(sample);
        SpriteHelper.UpdateWaterContainerSprite(sample);
        for (int k = 0; k < FRACTIONS; k++)
        {
            if (perSlot[k] > 0)
            {
                SpriteHelper.UpdateWaterContainerSprite(ui.Fractions[k].childItem);
            }
        }
        return "hplc_status_running";
    }

    // Methanol per uL of solute: METHANOL_COST x (POLARITY_BASE + the highest polarity in the sample). All solutes
    // run out together, so the highest polarity stays the same for the whole run.
    private static double CostPerUl(List<Solute> solutes)
    {
        int highest = 0;
        foreach (Solute s in solutes)
        {
            highest = Math.Max(highest, s.Polarity);
        }
        return METHANOL_COST * (POLARITY_BASE + highest);
    }

    // ---- the polarity table -----------------------------------------------------------------------------------------

    // Every solute of the game, two columns (name, polarity). With solutes in the sample, only those, and a third
    // column: the slot each goes to (1 = top). Below it the methanol cost and notes on solvent and particles.
    private static void ShowTable(Ui ui, GameItem sample, List<Solute> solutes)
    {
        bool hasSample = sample != null && !WaterHelper.IsEmpty(sample);
        bool sampleMode = solutes.Count > 0;
        List<Solute> rows = sampleMode ? new List<Solute>(solutes) : AllSolutes();
        rows.Sort(delegate(Solute a, Solute b) { return Compare(ui, sampleMode, a, b); });

        StringBuilder text = new StringBuilder();
        foreach (Solute row in rows)
        {
            if (text.Length > 0)
            {
                text.Append('\n');
            }
            text.Append(Fit(row.Name));
            text.Append("<pos=").Append(NAME_W + 4 - TEXT_PAD).Append('>').Append(row.Polarity);
            if (sampleMode)
            {
                text.Append("<pos=").Append(NAME_W + POLARITY_W + 4 - TEXT_PAD).Append('>').Append(row.Slot + 1);
            }
        }
        string shown = text.ToString();
        if (shown != ui.Shown)
        {
            ui.Table.SetLines(shown);
            if (sampleMode != ui.ShownSampleMode)
            {
                ui.Table.SetScroll(0f);
            }
            ui.Shown = shown;
            ui.ShownSampleMode = sampleMode;
        }

        ui.NameHead.SetText(Text.Get("hplc_head_name") + Arrow(ui, sampleMode, COL_NAME));
        ui.PolarityHead.SetText(Text.Get("hplc_head_polarity") + Arrow(ui, sampleMode, COL_POLARITY));
        ui.SlotHead.SetText(sampleMode ? Text.Get("hplc_head_slot") + Arrow(ui, sampleMode, COL_SLOT) : "");

        List<string> notes = new List<string>();
        if (sampleMode)
        {
            long soluteTotal = 0;
            foreach (Solute s in solutes)
            {
                soluteTotal += s.Amount;
            }
            notes.Add(Text.Get("hplc_table_cost", (Math.Ceiling(soluteTotal * CostPerUl(solutes)) / 1000.0).ToString("0.#")));
        }
        else
        {
            notes.Add(Text.Get("hplc_table_all"));
        }
        if (hasSample)
        {
            foreach (string id in SampleSolvents)
            {
                if (IaLiquids.Get(sample, id) > 0)
                {
                    notes.Add(Text.Get("hplc_table_solvent"));
                    break;
                }
            }
            if (HasParticles(sample))
            {
                notes.Add(Text.Get("hplc_table_particle"));
            }
        }
        ui.Notes.SetText(string.Join("\n", notes.ToArray()));
    }

    // A header was clicked: the same column again flips the order; another column sorts by it, polarity high to low
    // first, names A to Z and slots 1 to 4. The slot column only sorts while the table shows the sample's solutes.
    private static void SortBy(Ui ui, int column)
    {
        try
        {
            GameItem sample = ui.Sample.childItem;
            List<Solute> solutes = sample != null && !WaterHelper.IsEmpty(sample) ? Solutes(sample) : new List<Solute>();
            if (column == COL_SLOT && solutes.Count == 0)
            {
                return;
            }
            if (ui.SortColumn == column)
            {
                ui.Descending = !ui.Descending;
            }
            else
            {
                ui.SortColumn = column;
                ui.Descending = column == COL_POLARITY;
            }
            ShowTable(ui, sample, solutes);
        }
        catch (Exception e)
        {
            Log.Error("HPLC sorting failed", e);
        }
    }

    // The column in effect: sorting by slot falls back to polarity while the table lists every solute.
    private static int Column(Ui ui, bool sampleMode)
    {
        return ui.SortColumn == COL_SLOT && !sampleMode ? COL_POLARITY : ui.SortColumn;
    }

    private static int Compare(Ui ui, bool sampleMode, Solute a, Solute b)
    {
        int byName = string.CompareOrdinal(a.Name ?? "", b.Name ?? "");
        int byPolarity = a.Polarity.CompareTo(b.Polarity);
        int column = Column(ui, sampleMode);
        int first = column == COL_NAME ? byName : column == COL_SLOT ? a.Slot.CompareTo(b.Slot) : byPolarity;
        int second = column == COL_NAME ? byPolarity : byName;
        int result = first != 0 ? first : second != 0 ? second : string.CompareOrdinal(a.Id, b.Id);
        return ui.Descending ? -result : result;
    }

    private static string Arrow(Ui ui, bool sampleMode, int column)
    {
        if (Column(ui, sampleMode) != column)
        {
            return "";
        }
        return Text.Get(ui.Descending ? "hplc_sort_desc" : "hplc_sort_asc");
    }

    // Every solute the game knows (this mod's, vanilla and other mods' liquids that are not solvents or particles).
    private static List<Solute> AllSolutes()
    {
        List<Solute> list = new List<Solute>();
        foreach (Liquid liquid in Liquid.Liquids)
        {
            if (Polarity.KindOf(liquid.identifier) != Polarity.Kind.Solute)
            {
                continue;
            }
            Solute s = new Solute();
            s.Id = liquid.identifier;
            s.Name = liquid.displayName;
            s.Polarity = Polarity.Of(liquid.identifier);
            list.Add(s);
        }
        return list;
    }

    // Cuts a name that would run into the polarity column (a CJK character counts as two).
    private static string Fit(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "?";
        }
        name = name.Replace("<", "").Replace(">", "");
        int width = 0;
        for (int i = 0; i < name.Length; i++)
        {
            width += name[i] >= 0x2E80 ? 2 : 1;
            if (width > NAME_MAX)
            {
                return name.Substring(0, Math.Max(1, i - 1)) + "…";
            }
        }
        return name;
    }

    // The instruction note in the manual slot, like the electrolyzer's (Electrolyzer.CreateManual).
    public static GameItem CreateManual(bool owned)
    {
        GameItem note = DirectoryMaster.Item("postit", owned);
        note.name = Text.Get("hplc_manual_name");
        note.shortDescription = Text.Get("hplc_manual_short");
        note.flavorText = Text.Get("hplc_manual_flavor");
        note.SetGameItemType("DOCUMENT");
        note.EnableTag("IMPORTANT_TAG", false);
        note.EnableTag("MACHINE_MANUAL_TAG", false);
        note.unitValue = 0L;
        return note;
    }

    private static void SpawnOne()
    {
        try
        {
            GraphUtils.TryAcceptAll(EmporiumEntry.Instance.invElement, DirectoryMaster.Item(ITEM_ID, true));
        }
        catch (Exception e)
        {
            Log.Error("HPLC debug spawn failed", e);
        }
    }
}

// One row's piece of the column art (vce_hplc_seg_0..3); hovering any of them explains what the column does.
public class HplcColumnSegment : SpritePixelElement, TooltipListener
{
    public HplcColumnSegment(int row) : base("Items/items_tool", "postit")
    {
        Sprite sprite = Xinke.ModSprite(Hplc.SEGMENT_SPRITE + row);
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
        builder.AddLine(Text.Get("hplc_column_title"), true, RenderHandler.ColorPalette.White);
        builder.AddLine(Text.Get("hplc_column_line_1"), true, RenderHandler.ColorPalette.LightCyan);
        builder.AddLine(Text.Get("hplc_column_line_2"), true, RenderHandler.ColorPalette.LightCyan);
        return builder.Build(-1);
    }

    public RichText GetTooltipAdvanced()
    {
        return GetTooltip();
    }
}

// The polarity table's scrolling text box: vanilla's ScrollableRichTextElement (mouse wheel, drag, scroll bar) at the
// machinery window's text size, holding raw TextMeshPro text so <pos> tags can line up its columns.
public class HplcTableView : ScrollableRichTextElement
{
    public HplcTableView(int width, int height) : base(true, false)
    {
        textNode.text.fontSize = Hplc.TABLE_TEXT_SIZE;
        textNode.text.richText = true;
        ResizePixels(width, height);
    }

    public void SetLines(string text)
    {
        textNode.text.text = text ?? "";
        Validate();
    }
}
