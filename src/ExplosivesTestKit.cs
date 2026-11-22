// Everything needed to walk the explosives line end to end, for testing: the machines, a full nitric oxide canister,
// bottles of each intermediate (fat, caffeine, water, nitric acid, a nitration mix ready to heat, dry TNT), Kotton
// fiber and nitrocellulose of each size, and one finished bundle of dynamite. Given from the debug panel or, once,
// when a save carrying the mod data flag "vanilla_chem_ext:explosives_test_kit" = "1" is loaded (the flag turns to
// "given" before anything is built, like AntibioticTestKit). Anything that fits nowhere is logged.
// Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;

public static class ExplosivesTestKit
{
    public const string FLAG_KEY = "explosives_test_kit";

    public static void Enable()
    {
        ModHook.OnGameLoadedLate += OnGameLoaded;
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Explosives: full test kit", Give, false);
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Reloading press: test kit", GivePress, false);
        }
        catch (Exception e)
        {
            Log.Error("explosives test kit debug function registration failed", e);
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
                Log.Info("this save asked for the explosives test kit");
                Give();
            }
            if (ModHelper.GetModData(DreamToxinMod.MOD_ID, PRESS_FLAG_KEY, null) == AntibioticTestKit.FLAG_PENDING)
            {
                ModHelper.SetModData(DreamToxinMod.MOD_ID, PRESS_FLAG_KEY, AntibioticTestKit.FLAG_GIVEN);
                Log.Info("this save asked for the reloading press test kit");
                GivePress();
            }
        }
        catch (Exception e)
        {
            Log.Error("giving the explosives test kit on load failed", e);
        }
    }

    // ---- reloading press only: flag "vanilla_chem_ext:press_test_kit" = "1" -----------------------------------
    // The press, nine large nitrocellulose (810 propellant: about forty .22 rounds, enough for a few x10 batches and a
    // short one at the end) and 100 mL of TNT for armour-piercing rounds (33 .22 or 25 heavy).
    public const string PRESS_FLAG_KEY = "press_test_kit";

    public static void GivePress()
    {
        List<string> missing = new List<string>();
        int placed = 0;
        placed += Place(Owned(ReloadingPress.ITEM_ID), "reloading press", missing);
        for (int i = 0; i < 9; i++)
        {
            placed += Place(Owned("vce_nitrocellulose_l"), "large nitrocellulose", missing);
        }
        placed += Place(Bottle("bottled_water", new string[] { FlavorChemistry.TNT }, new int[] { 100000 }), "TNT 100 mL", missing);
        Log.Info("reloading press test kit: " + placed + " item(s) placed"
            + (missing.Count > 0 ? "; no room for: " + string.Join(", ", missing.ToArray()) : ""));
    }

    public static void Give()
    {
        List<string> missing = new List<string>();
        int placed = 0;

        // Machines first, while there is room.
        placed += Place(Owned(Electrolyzer.ITEM_ID), "electrolyzer", missing);
        placed += Place(Owned(ReloadingPress.ITEM_ID), "reloading press", missing);
        placed += Place(Owned("chem_finisher"), "chem finisher", missing);
        placed += Place(Owned("heating_plate"), "heating plate", missing);
        placed += Place(PreBuiltItemHelper.EnergyCredit(100), "battery", missing);
        placed += Place(PreBuiltItemHelper.EnergyCredit(100), "battery", missing);

        // Gas: a full canister of nitric oxide (dissolve it in the water bottle for nitric acid).
        GameItem canister = Owned(GasCanister.ITEM_ID);
        if (canister != null)
        {
            GasCanister.Fill(canister, GasCanister.NITRIC_OXIDE, GasCanister.CAPACITY_UL);
        }
        placed += Place(canister, "nitric oxide canister", missing);

        // Bottles (750 mL), one per step of the line.
        placed += Place(Bottle("bottled_water", new string[] { "fat" }, new int[] { 300000 }), "fat 300 mL (electrolysis)", missing);
        placed += Place(Bottle("bottled_water", new string[0], new int[0]), "empty bottle (toluene out of product A)", missing);
        placed += Place(Bottle("small_bottled_water", new string[] { "caffeine" }, new int[] { 50000 }), "caffeine 50 mL (150 C -> fat)", missing);
        placed += Place(Bottle("bottled_water", new string[] { "water" }, new int[] { 500000 }), "water 500 mL (for nitric oxide)", missing);
        placed += Place(Bottle("bottled_water", new string[] { FlavorChemistry.NITRIC_ACID }, new int[] { 200000 }), "nitric acid 200 mL (Kotton)", missing);
        // 100 mL toluene + 300 mL nitric acid + 60 mL acid (13%): heat to 50 C+ for 100 mL TNT.
        placed += Place(Bottle("bottled_water", new string[] { FlavorChemistry.TOLUENE, FlavorChemistry.NITRIC_ACID, "acid" },
            new int[] { 100000, 300000, 60000 }), "nitration mix", missing);
        placed += Place(Bottle("bottled_water", new string[] { FlavorChemistry.TNT }, new int[] { Dynamite.REQUIRED_VOLUME }), "dry TNT for one dynamite", missing);
        placed += Place(Bottle("bottled_water", new string[] { FlavorChemistry.TNT }, new int[] { 60000 }), "TNT 60 mL (armour-piercing rounds)", missing);

        // Kotton fiber to nitrate, and nitrocellulose ready for the press.
        placed += Place(OwnedPrebuilt(PreBuiltItemHelper.CreateSmallKotton()), "small Kotton fiber", missing);
        placed += Place(OwnedPrebuilt(PreBuiltItemHelper.CreateKotton()), "Kotton fiber", missing);
        placed += Place(OwnedPrebuilt(PreBuiltItemHelper.CreateLargeKotton()), "large Kotton fiber", missing);
        placed += Place(Owned("vce_nitrocellulose_s"), "small nitrocellulose", missing);
        placed += Place(Owned("vce_nitrocellulose"), "nitrocellulose", missing);
        placed += Place(Owned("vce_nitrocellulose_l"), "large nitrocellulose", missing);

        // A finished product to compare against.
        GameItem dynamite = Owned(Dynamite.ITEM_ID);
        if (dynamite != null)
        {
            ChemicalProductHelper.GradeProduct(dynamite);
        }
        placed += Place(dynamite, "dynamite", missing);

        if (missing.Count == 0)
        {
            Log.Info("explosives test kit: all " + placed + " item(s) placed");
        }
        else
        {
            Log.Info("explosives test kit: " + placed + " item(s) placed; no room for: " + string.Join(", ", missing.ToArray()));
        }
    }

    private static GameItem Owned(string id)
    {
        GameItem item = DirectoryMaster.Item(id, true);
        if (item == null)
        {
            Log.Info("explosives test kit: no item called " + id);
        }
        return item;
    }

    private static GameItem OwnedPrebuilt(GameItem item)
    {
        if (item != null)
        {
            ModHelper.SetItemOwnerShip(item, true);
        }
        return item;
    }

    // An emptied vanilla bottle with the given liquids.
    private static GameItem Bottle(string id, string[] liquids, int[] amounts)
    {
        GameItem bottle = Owned(id);
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
            Log.Error("placing " + what + " from the explosives test kit failed", e);
            missing.Add(what);
            return 0;
        }
    }
}
