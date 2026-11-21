// Polarity of every liquid, for the HPLC (Hplc.cs). Values and their real-world counterparts: docs/POLARITY_DESIGN.md.
//   P 0-100: dissolved molecules P = 60 - 12.5 x logP of the real counterpart (0..98); ions, strong acids and bases,
//   salts 95-100; large molecules and made-up substances set by hand.
//   Solvents (water, alcohol, methanol) have no P: they leave with the solvent front. Methanol is the only mobile phase.
//   Particles (particle size > 22, what a water filter catches) have no P: they must be filtered out before injection.
// Liquids of other mods are not in the table: a particle if their particle size says so, otherwise P 50.
// Written for C# 5 (see DreamToxinMod.cs).
using System.Collections.Generic;

public static class Polarity
{
    public enum Kind { Solute, Solvent, Particle }

    public const int PARTICLE_SIZE = 22;      // a water filter's pore: anything coarser is a particle
    public const int UNKNOWN_POLARITY = 50;

    private static readonly Dictionary<string, int> Values = new Dictionary<string, int>
    {
        // vanilla
        { "heavy_metal", 96 }, { "chemical_contaminant", 42 }, { "dream_cap_extract", 76 }, { "caffeine", 61 },
        { "protein", 58 }, { "fat", 0 }, { "starch", 95 }, { "sugar", 98 }, { "contraceptive", 17 },
        { "zyanide", 97 }, { "nightmare_extract", 84 }, { "chlorine", 71 }, { "synaprest", 25 }, { "fanta", 9 },
        { "methylol", 34 }, { "reducing_agent", 95 }, { "acid", 99 }, { "base", 99 }, { "stabilizing_agent", 82 },
        { "lipobond_agent", 35 }, { "livestock_steroid", 26 }, { "livestock_immunity", 60 }, { "wine_extract", 54 },
        { "nomore", 36 },
        // this mod
        { FlavorChemistry.ACETIC_ACID, 62 }, { FlavorChemistry.ETHYL_ACETATE, 51 }, { FlavorChemistry.TOLUENE, 26 },
        { FlavorChemistry.NITRIC_ACID, 98 }, { FlavorChemistry.TNT, 40 }, { ToxinLiquid.ID, 55 },
        { Xinke.ALKALOID_ID, 31 }, { Crackine.SALT_ID, 85 }, { Lantern.CAPSAICIN_ID, 22 }, { Lantern.ESTER_ID, 4 },
        { SaltOre.SALT_ID, 100 }, { Antibiotics.CEPH_ID, 98 }, { NcLiquids.PU_DISSOLVED, 96 },
        { NcLiquids.FISSION, 97 }, { NcLiquids.PU_EXTRACT, 10 }, { NcLiquids.PLUTONIUM, 96 },
        { OreLiquids.WATER_GLASS, 95 },
    };

    private static readonly HashSet<string> Solvents = new HashSet<string> { "water", "alcohol", IaLiquids.METHANOL };

    public const string MOBILE_PHASE = IaLiquids.METHANOL;

    public static Kind KindOf(string liquidId)
    {
        if (liquidId == null)
        {
            return Kind.Particle;
        }
        if (Solvents.Contains(liquidId))
        {
            return Kind.Solvent;
        }
        if (Values.ContainsKey(liquidId))
        {
            return Kind.Solute;
        }
        Liquid liquid;
        if (Liquid.LiquidDict.TryGetValue(liquidId, out liquid) && liquid.particleSize > PARTICLE_SIZE)
        {
            return Kind.Particle;
        }
        return Kind.Solute;
    }

    // P of a solute; -1 for solvents and particles.
    public static int Of(string liquidId)
    {
        if (KindOf(liquidId) != Kind.Solute)
        {
            return -1;
        }
        int value;
        return Values.TryGetValue(liquidId, out value) ? value : UNKNOWN_POLARITY;
    }
}
