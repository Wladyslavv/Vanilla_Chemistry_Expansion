// Drug resistance (docs/ANTIBIOTICS_DESIGN.md section 8). Every genome the player has sold antibiotics under
// keeps a record: cumulative sales, which decay by 500 a day and 200 more for each consecutive day without a
// sale (capped at 2000), and whether that genome has gone resistant. Above 2000 cumulative, each night rolls
// (total - 2000) / 10000 for resistance; once it hits, tablets of that genome lose up to half their price, and
// genomes within three letters of it lose a share of that. The record clears, and resistance lifts, when the
// cumulative sales decay back to zero. Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;

public class ResistanceRecord
{
    public string Genome;
    public int Total;
    // Consecutive days with no sale of this genome; each one adds to the daily decay.
    public int IdleDays;
    public bool Resistant;
}

public static class Resistance
{
    public const string DATA_KEY = "antibiotic_resistance";

    public const int THRESHOLD = 2000;
    public const int PROBABILITY_SPAN = 10000;
    public const int BASE_DECAY = 500;
    public const int IDLE_DECAY_STEP = 200;
    public const int MAX_DECAY = 2000;

    // The worst a resistant genome can do to the price, and how far a genome has to differ to escape it.
    public const int MAX_PENALTY_PERCENT = 50;
    public const double IMMUNE_DIFFERENCE = 0.30;

    public const string FEATURE_CATEGORY = "vce_resistance";
    public const string FEATURE_ID = "vce_resistance_penalty";

    private static Dictionary<string, ResistanceRecord> records;

    public static void Enable()
    {
        ModHook.OnHandlingNightlyServicesEarly += OnNight;
        ModHook.OnGameLoadedLate += Reload;
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Antibiotics: log resistance records", LogRecords, false);
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Antibiotics: make held genomes resistant", ForceResistance, false);
        }
        catch (Exception e)
        {
            Log.Error("resistance debug function registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnHandlingNightlyServicesEarly -= OnNight;
        ModHook.OnGameLoadedLate -= Reload;
    }

    // ---- the records ----------------------------------------------------------------------------------------------

    private static Dictionary<string, ResistanceRecord> Records
    {
        get
        {
            if (records == null)
            {
                Load();
            }
            return records;
        }
    }

    public static void Reload()
    {
        records = null;
    }

    // "genome,total,idleDays,resistant;..." in this mod's save data.
    private static void Load()
    {
        records = new Dictionary<string, ResistanceRecord>();
        try
        {
            string saved = ModHelper.GetModData(DreamToxinMod.MOD_ID, DATA_KEY, null);
            if (string.IsNullOrEmpty(saved))
            {
                return;
            }
            foreach (string entry in saved.Split(';'))
            {
                if (entry.Length == 0)
                {
                    continue;
                }
                string[] parts = entry.Split(',');
                int total, idle;
                if (parts.Length < 4 || parts[0].Length == 0
                    || !int.TryParse(parts[1], out total) || !int.TryParse(parts[2], out idle))
                {
                    continue;
                }
                ResistanceRecord record = new ResistanceRecord();
                record.Genome = parts[0];
                record.Total = total;
                record.IdleDays = idle;
                record.Resistant = parts[3] == "1";
                records[record.Genome] = record;
            }
        }
        catch (Exception e)
        {
            Log.Error("reading the resistance records failed", e);
        }
    }

    private static void Save()
    {
        try
        {
            StringBuilder builder = new StringBuilder();
            foreach (ResistanceRecord record in Records.Values)
            {
                if (builder.Length > 0)
                {
                    builder.Append(';');
                }
                builder.Append(record.Genome).Append(',').Append(record.Total).Append(',')
                    .Append(record.IdleDays).Append(',').Append(record.Resistant ? '1' : '0');
            }
            ModHelper.SetModData(DreamToxinMod.MOD_ID, DATA_KEY, builder.ToString());
        }
        catch (Exception e)
        {
            Log.Error("saving the resistance records failed", e);
        }
    }

    public static ResistanceRecord Find(string genome)
    {
        ResistanceRecord record;
        return !string.IsNullOrEmpty(genome) && Records.TryGetValue(genome, out record) ? record : null;
    }

    // ---- selling --------------------------------------------------------------------------------------------------

    // Every antibiotic sold adds its price to its own genome's record and resets that genome's decay.
    public static void OnSold(string genome, int price)
    {
        if (string.IsNullOrEmpty(genome) || price <= 0)
        {
            return;
        }
        ResistanceRecord record = Find(genome);
        if (record == null)
        {
            record = new ResistanceRecord();
            record.Genome = genome;
            Records[genome] = record;
        }
        record.Total += price;
        record.IdleDays = 0;
        Save();
        Log.Info("antibiotic sold: genome " + genome + " +" + price + " -> " + record.Total);
    }

    // ---- the penalty ----------------------------------------------------------------------------------------------

    // How much of the full penalty a genome takes: the full amount when it matches a resistant genome, falling
    // away linearly up to the immunity threshold (four letters out of twelve).
    public static double PenaltyFactor(string genome)
    {
        if (string.IsNullOrEmpty(genome))
        {
            return 0.0;
        }
        double worst = 0.0;
        foreach (ResistanceRecord record in Records.Values)
        {
            if (!record.Resistant)
            {
                continue;
            }
            double difference = StrainTraits.Distance(genome, record.Genome) / (double)StrainTraits.GENOME_LENGTH;
            double factor = 1.0 - difference / IMMUNE_DIFFERENCE;
            if (factor > worst)
            {
                worst = factor;
            }
        }
        return Math.Max(0.0, Math.Min(1.0, worst));
    }

    public static int PenaltyPercent(string genome)
    {
        return (int)Math.Round(MAX_PENALTY_PERCENT * PenaltyFactor(genome));
    }

    // A market-stage feature that takes the penalty off the price, the mirror image of a contraband markup.
    public static void ApplyPenalty(GameItem tablet)
    {
        if (!Antibiotics.IsTablet(tablet))
        {
            return;
        }
        int penalty = PenaltyPercent(CephData.Genome(tablet));
        ItemFeature feature = tablet.FindItemFeatureByCategory(FEATURE_CATEGORY);
        if (penalty <= 0)
        {
            if (feature != null)
            {
                feature.isDisabled = true;
                feature.SetPreExposedValueMod(0);
            }
            return;
        }
        if (feature == null)
        {
            feature = new ItemFeature(ItemFeature.FeatureType.Special);
            feature.identifier = FEATURE_ID;
            feature.category = FEATURE_CATEGORY;
            feature.valueStage = ItemFeature.ValueStage.Market;
            feature.isFeatureMatch = true;
            tablet.AddItemFeature(feature);
        }
        feature.isDisabled = false;
        string display = Text.Get("antibiotic_resistance_feature");
        feature.SetPublicDisplay(display);
        feature.SetActualDisplay(display);
        feature.SetPreExposedValueMod(-penalty);
    }

    // Said once per client, the first time a tablet of a resistant genome goes on their counter.
    private static readonly HashSet<StoreClient> toldClients = new HashSet<StoreClient>();

    public static bool OnItemShown(StoreClient client, GameItem tablet)
    {
        if (client == null || !Antibiotics.IsTablet(tablet) || PenaltyPercent(CephData.Genome(tablet)) <= 0)
        {
            return false;
        }
        if (toldClients.Contains(client))
        {
            return false;
        }
        if (toldClients.Count > 64)
        {
            toldClients.Clear();
        }
        toldClients.Add(client);
        return true;
    }

    public static void AddTooltip(RichTextBuilder builder, string genome)
    {
        if (builder == null || string.IsNullOrEmpty(genome))
        {
            return;
        }
        int penalty = PenaltyPercent(genome);
        if (penalty > 0)
        {
            builder.AddLine(Text.Get("antibiotic_tooltip_resistance", penalty), true, RenderHandler.ColorPalette.LightRed);
        }
        ResistanceRecord record = Find(genome);
        if (record != null && record.Total > 0)
        {
            builder.AddLine(Text.Get("antibiotic_tooltip_sales", record.Total), true, RenderHandler.ColorPalette.LightYellow);
        }
    }

    // ---- nightly --------------------------------------------------------------------------------------------------

    public static int DecayFor(ResistanceRecord record)
    {
        return Math.Min(MAX_DECAY, BASE_DECAY + IDLE_DECAY_STEP * Math.Max(0, record.IdleDays));
    }

    public static int ChanceFor(ResistanceRecord record)
    {
        if (record.Total <= THRESHOLD)
        {
            return 0;
        }
        return Math.Min(100, (int)Math.Round((record.Total - THRESHOLD) * 100.0 / PROBABILITY_SPAN));
    }

    public static void OnNight()
    {
        try
        {
            List<string> gone = new List<string>();
            foreach (ResistanceRecord record in Records.Values)
            {
                // A genome that went unsold today decays faster tomorrow; selling resets that in OnSold.
                record.IdleDays++;
                record.Total = Math.Max(0, record.Total - DecayFor(record));
                if (record.Total <= 0)
                {
                    gone.Add(record.Genome);
                    continue;
                }
                if (!record.Resistant && RNG.Roll(ChanceFor(record)))
                {
                    record.Resistant = true;
                    Log.Info("resistance appeared against genome " + record.Genome + " (cumulative " + record.Total + ")");
                }
            }
            foreach (string genome in gone)
            {
                Log.Info("resistance record for genome " + genome + " decayed away");
                Records.Remove(genome);
            }
            Save();
            RefreshHeldTablets();
        }
        catch (Exception e)
        {
            Log.Error("the nightly resistance pass failed", e);
        }
    }

    public static void RefreshHeldTablets()
    {
        try
        {
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (Antibiotics.IsTablet(item))
                {
                    ApplyPenalty(item);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("refreshing antibiotic prices failed", e);
        }
    }

    // ---- debug ----------------------------------------------------------------------------------------------------

    public static void LogRecords()
    {
        foreach (ResistanceRecord record in Records.Values)
        {
            Log.Info("resistance: " + record.Genome + " total " + record.Total + ", idle " + record.IdleDays
                + " day(s), decay " + DecayFor(record) + ", nightly chance " + ChanceFor(record) + "%"
                + (record.Resistant ? ", RESISTANT" : ""));
        }
    }

    public static void ForceResistance()
    {
        try
        {
            HashSet<string> genomes = new HashSet<string>();
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                string genome = Antibiotics.IsTablet(item) ? CephData.Genome(item)
                    : (Strain.IsStrain(item) || Strain.IsFrozen(item)) ? StrainTraits.Genome(item) : "";
                if (genome.Length > 0)
                {
                    genomes.Add(genome);
                }
            }
            foreach (string genome in genomes)
            {
                ResistanceRecord record = Find(genome);
                if (record == null)
                {
                    record = new ResistanceRecord();
                    record.Genome = genome;
                    Records[genome] = record;
                }
                record.Total = Math.Max(record.Total, THRESHOLD + 1000);
                record.Resistant = true;
                Log.Info("forced resistance against genome " + genome);
            }
            Save();
            RefreshHeldTablets();
        }
        catch (Exception e)
        {
            Log.Error("forcing resistance failed", e);
        }
    }
}

// Each antibiotic the player sells is booked against its own genome.
[HarmonyPatch(typeof(SoldItem), "HandleSoldItem")]
public static class ResistanceSalePatch
{
    private static void Postfix()
    {
        try
        {
            List<SoldItem> sold = PlayerStore.Instance.soldItemsTracker;
            if (sold == null)
            {
                return;
            }
            foreach (SoldItem soldItem in sold)
            {
                if (soldItem == null || soldItem.itemId != Antibiotics.TABLET_ID)
                {
                    continue;
                }
                GameItem item = soldItem.GetGameItem();
                if (item == null)
                {
                    continue;
                }
                Resistance.OnSold(CephData.Genome(item), soldItem.soldPrice);
            }
        }
        catch (Exception e)
        {
            Log.Error("booking antibiotic sales failed", e);
        }
    }
}
