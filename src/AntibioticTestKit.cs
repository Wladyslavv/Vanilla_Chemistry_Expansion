// Everything needed to walk the antibiotics line end to end, for testing. Given either from the debug panel or,
// once, when a save carrying the mod data flag "vanilla_chem_ext:antibiotic_test_kit" = "1" is loaded; the flag
// is set to "given" before anything is built, so a save is never stocked twice. Items are built by the mod's own
// factories and placed wherever the shop has room; anything that fits nowhere is logged, never silently lost.
// Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;

public static class AntibioticTestKit
{
    public const string FLAG_KEY = "antibiotic_test_kit";
    public const string FLAG_PENDING = "1";
    public const string FLAG_GIVEN = "given";

    // A mutagen dose sits in the middle of the 4.5-5.5 mL window.
    public const int DOSE_UL = 5000;
    public const int FEED_KCAL = 20000;

    public static void Enable()
    {
        ModHook.OnGameLoadedLate += OnGameLoaded;
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Antibiotics: full test kit", Give, false);
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Antibiotics: enlarge fridges for freezing", EnlargeFridges, false);
        }
        catch (Exception e)
        {
            Log.Error("antibiotic test kit debug function registration failed", e);
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
            if (ModHelper.GetModData(DreamToxinMod.MOD_ID, FLAG_KEY, null) != FLAG_PENDING)
            {
                return;
            }
            // Marked first: if building the kit fails half way, a reload does not hand out a second one.
            ModHelper.SetModData(DreamToxinMod.MOD_ID, FLAG_KEY, FLAG_GIVEN);
            Log.Info("this save asked for the antibiotic test kit");
            Give();
        }
        catch (Exception e)
        {
            Log.Error("giving the antibiotic test kit on load failed", e);
        }
    }

    public static void Give()
    {
        List<string> missing = new List<string>();
        int placed = 0;

        // Big things first, while there is still room for them.
        placed += Place(BuildFermenter(), "fermenter", missing);
        placed += Place(Enlarge(Owned("fridge")), "fridge", missing);
        placed += Place(Owned("blender"), "blender", missing);
        placed += Place(Owned("chem_finisher"), "chem finisher", missing);

        GameItem frozen = DirectoryMaster.Item(Strain.FROZEN_ID, true);
        if (frozen != null)
        {
            StrainTraits.Randomize(frozen);
        }
        placed += Place(frozen, "frozen strain", missing);

        for (int i = 0; i < 4; i++)
        {
            placed += Place(Strain.Spawn(true, null, false), "strain", missing);
        }

        placed += Place(Owned("100ml_cylinder"), "empty cylinder", missing);
        placed += Place(Dose("med_bottle_red", "acid"), "acid dose", missing);
        placed += Place(Dose("med_bottle_orange", "dream_cap_extract"), "dream cap extract dose", missing);
        placed += Place(Dose("med_bottle_violet", ToxinLiquid.ID), "dream toxin dose", missing);
        placed += Place(Dose("med_bottle_blue", NcLiquids.PLUTONIUM), "plutonium dose", missing);
        placed += Place(PreBuiltItemHelper.EnergyCredit(100), "battery for the fridge", missing);
        placed += Place(Owned(GuideBook.ITEM_ID), "handbook", missing);

        if (missing.Count == 0)
        {
            Log.Info("antibiotic test kit: all " + placed + " item(s) placed");
        }
        else
        {
            Log.Info("antibiotic test kit: " + placed + " item(s) placed; no room for: " + string.Join(", ", missing.ToArray()));
        }
    }

    private static GameItem Owned(string id)
    {
        GameItem item = DirectoryMaster.Item(id, true);
        if (item == null)
        {
            Log.Info("antibiotic test kit: no item called " + id);
        }
        return item;
    }

    // A stock freezer zone is 3x2 and a frozen strain 3x3, so a fridge needs storage upgrades before it can freeze
    // anything. Two of them (3x4) leave room around the dewar. Same effect as vanilla's MachineFridge.UpgradeStorage,
    // which also wants an upgrade item to consume; the count is a saved tag and vanilla reapplies the size on load.
    public const int FRIDGE_STORAGE_UPGRADES = 2;
    public const string FRIDGE_STORAGE_TAG = "FRIDGE_STORAGE_UPGRADE_COUNT_TAG";

    private static GameItem Enlarge(GameItem fridge)
    {
        if (fridge == null || !fridge.IsTag("FRIDGE_TAG"))
        {
            return fridge;
        }
        int have = fridge.GetTagReadonly(FRIDGE_STORAGE_TAG).GetInt();
        if (have >= FRIDGE_STORAGE_UPGRADES)
        {
            return fridge;
        }
        fridge.ModifyTag(FRIDGE_STORAGE_TAG, delegate(TagState state)
        {
            state.Enable().SetInt(FRIDGE_STORAGE_UPGRADES);
        }, true);
        fridge.ValidateShapeState();
        MachineFridge.ApplyStorageSize(fridge);
        MachineFridge.UpdateFridgePowerUsage(fridge);
        return fridge;
    }

    public static void EnlargeFridges()
    {
        try
        {
            int count = 0;
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (item != null && item.IsTag("FRIDGE_TAG") && GeneralHelper.IsItemOwned(item)
                    && item.GetTagReadonly(FRIDGE_STORAGE_TAG).GetInt() < FRIDGE_STORAGE_UPGRADES)
                {
                    Enlarge(item);
                    count++;
                }
            }
            Log.Info("enlarged " + count + " fridge(s) to a 3x" + (2 + FRIDGE_STORAGE_UPGRADES) + " freezer zone");
        }
        catch (Exception e)
        {
            Log.Error("enlarging fridges failed", e);
        }
    }

    // A charged battery, a fed dispenser and one strain already inside: it starts on the first night.
    private static GameItem BuildFermenter()
    {
        GameItem machine = DirectoryMaster.Item(Fermenter.ITEM_ID, true);
        if (machine == null)
        {
            return null;
        }
        Fermenter.StockForTesting(machine, FEED_KCAL);
        return machine;
    }

    private static GameItem Dose(string bottleId, string liquidId)
    {
        GameItem bottle = DirectoryMaster.Item(bottleId, true);
        if (bottle == null)
        {
            return null;
        }
        WaterHelper.AddLiquid(bottle, liquidId, DOSE_UL);
        AudioHelper.InitPlasticContainer(bottle);
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
            Log.Error("placing " + what + " from the antibiotic test kit failed", e);
            missing.Add(what);
            return 0;
        }
    }
}
