// Strain mutation (docs/ANTIBIOTICS_DESIGN.md section 6). Breeding rolls a natural mutation on every strain a
// fermenter harvests: a 5% chance of changing one to three letters of the genome, and a 30% chance of moving the
// numeric traits by a total of -2 to +1, so a line left to itself degrades. Dropping a liquid container on a
// strain mutates it on purpose: 4.5-5.5 mL whose largest component is a mutagen at 90% or better. Getting any of
// that wrong still costs the liquid and still mutates, just never for the better.
// Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;

public class Mutagen
{
    public string LiquidId;
    // Inclusive range for the total change of the numeric traits, in tenths of a point.
    public int MinTenths;
    public int MaxTenths;
    // Genome letters changed per application, and the chance of re-rolling the extraction target.
    public int MinGenes;
    public int MaxGenes;
    public int TargetChance;

    public static Mutagen Traits(string liquidId, int minTenths, int maxTenths)
    {
        Mutagen mutagen = new Mutagen();
        mutagen.LiquidId = liquidId;
        mutagen.MinTenths = minTenths;
        mutagen.MaxTenths = maxTenths;
        return mutagen;
    }

    public static Mutagen Genes(string liquidId, int minGenes, int maxGenes)
    {
        Mutagen mutagen = new Mutagen();
        mutagen.LiquidId = liquidId;
        mutagen.MinGenes = minGenes;
        mutagen.MaxGenes = maxGenes;
        return mutagen;
    }

    public static Mutagen Target(string liquidId, int chance)
    {
        Mutagen mutagen = new Mutagen();
        mutagen.LiquidId = liquidId;
        mutagen.TargetChance = chance;
        return mutagen;
    }
}

public static class StrainMutation
{
    // Natural mutation, rolled for every strain a fermenter harvests.
    public const int NATURAL_GENOME_CHANCE = 5;
    public const int NATURAL_GENOME_MIN = 1;
    public const int NATURAL_GENOME_MAX = 3;
    public const int NATURAL_TRAIT_CHANCE = 30;
    public const int NATURAL_MIN_TENTHS = -20;
    public const int NATURAL_MAX_TENTHS = 10;

    // Directed mutation: the dose, and how pure the active component has to be.
    public const int DOSE_MIN_UL = 4500;
    public const int DOSE_MAX_UL = 5500;
    public const double MIN_SHARE = 0.90;

    // Botched doses still mutate, never upwards.
    public const int BOTCHED_MIN_TENTHS = -20;
    public const int BOTCHED_MAX_TENTHS = 0;

    private static Mutagen[] mutagens;

    public static Mutagen[] Mutagens
    {
        get
        {
            if (mutagens == null)
            {
                mutagens = new Mutagen[]
                {
                    // Acid and base: a gentle shove, as likely to help as to hurt.
                    Mutagen.Traits("acid", -50, 50),
                    Mutagen.Traits("base", -50, 50),
                    // The strong mutagens: twice the swing, still even odds.
                    Mutagen.Traits("fanta", -100, 100),
                    Mutagen.Traits("methylol", -100, 100),
                    Mutagen.Traits(FlavorChemistry.ACETIC_ACID, -100, 100),
                    Mutagen.Traits(FlavorChemistry.ETHYL_ACETATE, -100, 100),
                    // Dream cap extract is the one that actually tends to improve a strain.
                    Mutagen.Traits("dream_cap_extract", -20, 40),
                    // Nightmare cap extract moves the extraction target instead of the numbers.
                    Mutagen.Target("nightmare_extract", 25),
                    // Dream toxin rewrites the strain's identity: the way to outrun resistance.
                    Mutagen.Genes(ToxinLiquid.ID, 1, 2),
                    // Plutonium: anything can happen, and on average it is an improvement.
                    Mutagen.Traits(NcLiquids.PLUTONIUM, -1000, 1000)
                };
            }
            return mutagens;
        }
    }

    public static Mutagen Find(string liquidId)
    {
        foreach (Mutagen mutagen in Mutagens)
        {
            if (mutagen.LiquidId == liquidId)
            {
                return mutagen;
            }
        }
        return null;
    }

    // ---- natural mutation -----------------------------------------------------------------------------------------

    public static void Natural(GameItem strain)
    {
        if (strain == null)
        {
            return;
        }
        if (RNG.Roll(NATURAL_GENOME_CHANCE))
        {
            MutateGenome(strain, RNG.GetRandomInt(NATURAL_GENOME_MIN, NATURAL_GENOME_MAX));
        }
        if (RNG.Roll(NATURAL_TRAIT_CHANCE))
        {
            MutateTraits(strain, NATURAL_MIN_TENTHS, NATURAL_MAX_TENTHS);
        }
    }

    // ---- directed mutation ----------------------------------------------------------------------------------------

    public static bool CanDose(GameItem container, GameItem strain)
    {
        return container != null && strain != null && Strain.IsStrain(strain)
            && container.IsTag("LIQUID_CONTAINER_TAG") && !WaterHelper.IsEmpty(container)
            && GeneralHelper.IsItemOwned(container) && GeneralHelper.IsItemOwned(strain);
    }

    // The liquid that makes up most of the container, and its share of the whole.
    private static string LargestLiquid(GameItem container, out double share)
    {
        share = 0.0;
        int total = WaterHelper.GetTotalVolume(container);
        if (total <= 0)
        {
            return "";
        }
        string largest = "";
        int most = 0;
        foreach (Liquid liquid in Liquid.Liquids)
        {
            int amount = IaLiquids.Get(container, liquid.identifier);
            if (amount > most)
            {
                most = amount;
                largest = liquid.identifier;
            }
        }
        share = (double)most / total;
        return largest;
    }

    // Pouring a dose over a strain. The liquid is always used up and the strain always changes; only a dose that
    // meets all three conditions gets the mutagen's own effect.
    public static void Dose(GameItem container, GameItem strain)
    {
        try
        {
            if (!CanDose(container, strain))
            {
                return;
            }
            int total = WaterHelper.GetTotalVolume(container);
            double share;
            string largest = LargestLiquid(container, out share);
            Mutagen mutagen = Find(largest);
            bool right = total >= DOSE_MIN_UL && total <= DOSE_MAX_UL && mutagen != null && share >= MIN_SHARE;

            WaterHelper.EmptyContainer(container);
            WaterFeatureHelper.OnLiquidContainerPourOutOf(container);
            SpriteHelper.UpdateWaterContainerSprite(container);

            if (!right)
            {
                MutateTraits(strain, BOTCHED_MIN_TENTHS, BOTCHED_MAX_TENTHS);
                Log.Info("botched mutagen dose on a strain: " + total + " uL, largest " + largest
                    + " at " + (share * 100).ToString("0") + "%");
                return;
            }
            Apply(strain, mutagen);
        }
        catch (Exception e)
        {
            Log.Error("dosing a strain with a mutagen failed", e);
        }
    }

    public static void Apply(GameItem strain, Mutagen mutagen)
    {
        if (strain == null || mutagen == null)
        {
            return;
        }
        if (mutagen.MaxGenes > 0)
        {
            MutateGenome(strain, RNG.GetRandomInt(mutagen.MinGenes, mutagen.MaxGenes));
            Log.Info("mutagen " + mutagen.LiquidId + " rewrote part of a genome: " + StrainTraits.Genome(strain));
            return;
        }
        if (mutagen.TargetChance > 0)
        {
            if (RNG.Roll(mutagen.TargetChance))
            {
                StrainTraits.SetTarget(strain, RNG.GetRandomInt(StrainTraits.TARGET_LIQUID, StrainTraits.TARGET_INTRACELLULAR));
                Log.Info("mutagen " + mutagen.LiquidId + " moved the extraction target to "
                    + StrainTraits.Target(strain));
            }
            return;
        }
        MutateTraits(strain, mutagen.MinTenths, mutagen.MaxTenths);
        Log.Info("mutagen " + mutagen.LiquidId + " shifted the traits of a strain");
    }

    // ---- the mechanics --------------------------------------------------------------------------------------------

    public static void MutateGenome(GameItem strain, int letters)
    {
        string genome = StrainTraits.Genome(strain);
        if (genome.Length < StrainTraits.GENOME_LENGTH)
        {
            genome = StrainTraits.RandomGenome();
        }
        char[] chars = genome.ToCharArray();
        List<int> positions = new List<int>();
        for (int i = 0; i < chars.Length; i++)
        {
            positions.Add(i);
        }
        int changes = Math.Min(Math.Max(1, letters), chars.Length);
        for (int change = 0; change < changes; change++)
        {
            int pick = RNG.GetRandomIntExculsive(0, positions.Count);
            int position = positions[pick];
            positions.RemoveAt(pick);
            char was = chars[position];
            // A mutation that changes nothing is not a mutation: pick one of the other three letters.
            char now = was;
            while (now == was)
            {
                now = StrainTraits.GENOME_LETTERS[RNG.GetRandomIntExculsive(0, StrainTraits.GENOME_LETTERS.Length)];
            }
            chars[position] = now;
        }
        StrainTraits.SetGenome(strain, new string(chars));
    }

    // Draws a total somewhere in [minTenths, maxTenths] and spreads it at random over the four numeric traits.
    // No single trait moves further than the total's own range allows.
    public static void MutateTraits(GameItem strain, int minTenths, int maxTenths)
    {
        if (strain == null || minTenths > maxTenths)
        {
            return;
        }
        int total = RNG.GetRandomInt(minTenths, maxTenths);
        int[] deltas = Spread(total, minTenths, maxTenths, StrainTraits.NUMERIC_TAGS.Length);
        for (int i = 0; i < StrainTraits.NUMERIC_TAGS.Length; i++)
        {
            if (deltas[i] != 0)
            {
                StrainTraits.AddTenths(strain, StrainTraits.NUMERIC_TAGS[i], deltas[i]);
            }
        }
        // Carbon-source efficiency may reach the point where a batch costs nothing, but never less than nothing;
        // the night's draw is clamped where it is used (StrainTraits.KcalPerNight), so nothing to do here.
    }

    // Random values in [lo, hi] that add up to total, nudged into place one tenth at a time.
    public static int[] Spread(int total, int lo, int hi, int parts)
    {
        int[] deltas = new int[parts];
        for (int i = 0; i < parts; i++)
        {
            deltas[i] = RNG.GetRandomInt(lo, hi);
        }
        int sum = 0;
        foreach (int delta in deltas)
        {
            sum += delta;
        }
        // At most one nudge per unit of error, plus slack for picks that are already at their limit.
        int guard = Math.Abs(sum - total) * 8 + 64;
        while (sum != total && guard-- > 0)
        {
            int step = sum > total ? -1 : 1;
            int pick = RNG.GetRandomIntExculsive(0, parts);
            int moved = deltas[pick] + step;
            if (moved < lo || moved > hi)
            {
                continue;
            }
            deltas[pick] = moved;
            sum += step;
        }
        return deltas;
    }
}
