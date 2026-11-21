// What it takes to try the HPLC (Hplc.cs), for testing: the machine, six bottles of pure methanol, four empty bottles
// for the collection slots, and six water samples holding one to six solutes (20 mL each in 200 mL of water):
//   1  caffeine 61
//   2  fat 0, caffeine 61
//   3  fat 0, caffeine 61, sugar 98
//   4  fat 0, toluene 26, caffeine 61, sugar 98                    (one slot each)
//   5  fat 0, toluene 26, TNT 40, caffeine 61, sugar 98            (toluene and TNT share a slot)
//   6  fat 0, toluene 26, TNT 40, caffeine 61, sugar 98, acid 99   (also sugar and acid)
// Given from the debug panel or, once, when a save carrying the mod data flag "vanilla_chem_ext:hplc_test_kit" = "1"
// is loaded (the flag turns to "given" before anything is built, like AntibioticTestKit). Anything that fits nowhere
// is logged. Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;

public static class HplcTestKit
{
    public const string FLAG_KEY = "hplc_test_kit";
    public const int SOLUTE_UL = 20000;
    public const int WATER_UL = 200000;
    public const int METHANOL_UL = 700000;

    private static readonly string[] Solutes =
    {
        "caffeine", "fat", "sugar", FlavorChemistry.TOLUENE, FlavorChemistry.TNT, "acid",
    };

    // Which of Solutes each sample holds.
    private static readonly int[][] Samples =
    {
        new int[] { 0 },
        new int[] { 1, 0 },
        new int[] { 1, 0, 2 },
        new int[] { 1, 3, 0, 2 },
        new int[] { 1, 3, 4, 0, 2 },
        new int[] { 1, 3, 4, 0, 2, 5 },
    };

    public static void Enable()
    {
        ModHook.OnGameLoadedLate += OnGameLoaded;
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "HPLC: full test kit", Give, false);
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Methanol: test kit", GiveMethanol, false);
        }
        catch (Exception e)
        {
            Log.Error("HPLC test kit debug function registration failed", e);
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
                Log.Info("this save asked for the HPLC test kit");
                Give();
            }
            if (ModHelper.GetModData(DreamToxinMod.MOD_ID, METHANOL_FLAG_KEY, null) == AntibioticTestKit.FLAG_PENDING)
            {
                ModHelper.SetModData(DreamToxinMod.MOD_ID, METHANOL_FLAG_KEY, AntibioticTestKit.FLAG_GIVEN);
                Log.Info("this save asked for the methanol test kit");
                GiveMethanol();
            }
        }
        catch (Exception e)
        {
            Log.Error("giving the HPLC test kit on load failed", e);
        }
    }

    // ---- methanol (GasCanister.cs): flag "vanilla_chem_ext:methanol_test_kit" = "1" -----------------------------
    // A heating plate with chem modules (a plain plate stops at 99 C; one module lifts it to 160 C, three heat 4x
    // faster, four 16x), two full hydrogen canisters (5 L -> about 1.67 L of methanol vapour each), one half hydrogen
    // half chlorine (both reactions at 150 C+), and two empty bottles to condense the vapour into.
    public const string METHANOL_FLAG_KEY = "methanol_test_kit";
    public const int PLATE_MODULES = 4;

    public static void GiveMethanol()
    {
        List<string> missing = new List<string>();
        int placed = 0;

        GameItem plate = Owned("heating_plate");
        List<GameItem> loose = new List<GameItem>();
        if (plate != null)
        {
            GameInventory grid = MachineHelper.GetBodyStorage(plate, 0) as GameInventory;
            for (int i = 0; i < PLATE_MODULES; i++)
            {
                GameItem module = Owned("chem_module");
                if (module == null)
                {
                    continue;
                }
                if (grid != null)
                {
                    GraphUtils.TryAcceptAll(grid, module);
                }
                if (module.parentInventory == null)
                {
                    loose.Add(module);
                }
            }
        }
        placed += Place(plate, "heating plate", missing);
        foreach (GameItem module in loose)
        {
            placed += Place(module, "chem module (did not fit the plate)", missing);
        }
        for (int i = 0; i < 2; i++)
        {
            GameItem canister = Owned(GasCanister.ITEM_ID);
            if (canister != null)
            {
                GasCanister.Fill(canister, GasCanister.HYDROGEN, GasCanister.CAPACITY_UL);
            }
            placed += Place(canister, "hydrogen canister", missing);
        }
        GameItem mixed = Owned(GasCanister.ITEM_ID);
        if (mixed != null)
        {
            GasCanister.Fill(mixed, GasCanister.HYDROGEN, GasCanister.CAPACITY_UL / 2);
            GasCanister.Fill(mixed, GasCanister.CHLORINE, GasCanister.CAPACITY_UL / 2);
        }
        placed += Place(mixed, "hydrogen + chlorine canister", missing);
        for (int i = 0; i < 2; i++)
        {
            placed += Place(Bottle(new string[0], new int[0]), "empty bottle for methanol", missing);
        }

        if (missing.Count == 0)
        {
            Log.Info("methanol test kit: all " + placed + " item(s) placed");
        }
        else
        {
            Log.Info("methanol test kit: " + placed + " item(s) placed; no room for: " + string.Join(", ", missing.ToArray()));
        }
    }

    public static void Give()
    {
        List<string> missing = new List<string>();
        int placed = 0;

        placed += Place(Owned(Hplc.ITEM_ID), "HPLC", missing);
        for (int i = 0; i < 6; i++)
        {
            placed += Place(Bottle(new string[] { IaLiquids.METHANOL }, new int[] { METHANOL_UL }), "methanol " + (i + 1), missing);
        }
        for (int i = 0; i < Hplc.FRACTIONS; i++)
        {
            placed += Place(Bottle(new string[0], new int[0]), "empty bottle " + (i + 1), missing);
        }
        for (int s = 0; s < Samples.Length; s++)
        {
            int[] picks = Samples[s];
            string[] liquids = new string[picks.Length + 1];
            int[] amounts = new int[picks.Length + 1];
            liquids[0] = "water";
            amounts[0] = WATER_UL;
            for (int i = 0; i < picks.Length; i++)
            {
                liquids[i + 1] = Solutes[picks[i]];
                amounts[i + 1] = SOLUTE_UL;
            }
            placed += Place(Bottle(liquids, amounts), "sample with " + picks.Length + " solute(s)", missing);
        }

        if (missing.Count == 0)
        {
            Log.Info("HPLC test kit: all " + placed + " item(s) placed");
        }
        else
        {
            Log.Info("HPLC test kit: " + placed + " item(s) placed; no room for: " + string.Join(", ", missing.ToArray()));
        }
    }

    private static GameItem Owned(string id)
    {
        GameItem item = DirectoryMaster.Item(id, true);
        if (item == null)
        {
            Log.Info("HPLC test kit: no item called " + id);
        }
        return item;
    }

    // An emptied vanilla 750 mL bottle with the given liquids.
    private static GameItem Bottle(string[] liquids, int[] amounts)
    {
        GameItem bottle = Owned("bottled_water");
        if (bottle == null)
        {
            return null;
        }
        WaterHelper.EmptyContainer(bottle);
        for (int i = 0; i < liquids.Length; i++)
        {
            WaterHelper.AddLiquid(bottle, liquids[i], amounts[i]);
        }
        SpriteHelper.UpdateWaterContainerSprite(bottle);
        return bottle;
    }

    // Anywhere in the shop that takes it. EmporiumTryAdd destroys what fits nowhere, so check afterwards.
    private static int Place(GameItem item, string what, List<string> missing)
    {
        if (item == null)
        {
            missing.Add(what);
            return 0;
        }
        try
        {
            GraphUtils.EmporiumTryAdd(item);
            if (item.parentInventory == null)
            {
                missing.Add(what);
                return 0;
            }
            return 1;
        }
        catch (Exception e)
        {
            Log.Error("placing " + what + " from the HPLC test kit failed", e);
            missing.Add(what);
            return 0;
        }
    }
}
