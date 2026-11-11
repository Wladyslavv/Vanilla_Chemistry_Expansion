// Plutonium chemical battery: a vanilla energy cell (200 kJ, bandwidth 10) that starts without self-charge.
// Each plutonium ampoule dragged onto it adds 2 kJ of self-charge, up to 10 ampoules (20 kJ).
//   - Self-charge uses vanilla's recharge tag (power_source_item_recharge, via PowerHelper.InitRecharge), so the
//     vanilla tooltip shows it and vanilla's power-storage furniture recharges it every turn like the Breeder cell.
//   - Every night (onCycleEndEarlySlotItemFunc, wherever the battery sits, machine battery slots included) it
//     recharges by the tag's value. The Breeder cell does the same with a fixed 4; this one reads the tag.
//   - The sprite shows the charge level in seven steps, like vanilla batteries (UpdateEnergyCreditSprite patch).
// Sold by Dr. Jackson. Art: art/battery/DrawPuBattery.cs. Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using HarmonyLib;

public static class PuBattery
{
    public const string ITEM_ID = "vce_pu_battery";
    public const string SPRITE = "vce_pu_battery";

    public const int CAPACITY = 200;
    public const int BANDWIDTH = 10;
    public const int RECHARGE_PER_AMPOULE = 2;
    public const int MAX_AMPOULES = 10;
    public const long VALUE = 60;

    // Vanilla tags (PowerHelper).
    public const string ENERGY_TAG = "power_source_item_energy";
    public const string MAX_ENERGY_TAG = "power_source_item_max_energy";
    public const string RECHARGE_TAG = "power_source_item_recharge";

    // Ampoules taken so far.
    public const string AMPOULES_TAG = "VCE_PU_BATTERY_AMPOULES_INT";

    // Dr. Jackson: 30% per restock.
    public const int JACKSON_CHANCE = 30;

    // Wherever vanilla can hand out an ordinary battery (energy_credit), this one can show up instead:
    //   - loot tables (store sellers and other table rolls): added next to energy_credit at this share of its weight;
    //   - code-built drop tables (raids and stations): each dropped energy_credit is swapped at this chance.
    public const string VANILLA_BATTERY = "energy_credit";
    public const double LOOT_SHARE = 0.10;
    public const int DROP_SWAP_PERCENT = 10;

    private static readonly int[] Levels = { 100, 80, 60, 40, 20, 10, 0 };

    public static void Enable()
    {
        ModHook.OnCreateTooltipLate += OnCreateTooltip;
        ModHook.OnGameLoadedLate += FixLoadedItems;
        ModHook.OnPlaceInventorInventoryItemEarly += OnPlaceInventorInventory;
        ModHook.OnLootTablesLoaded += AddToLootTables;
        AddToLootTables();
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Nuclear: plutonium battery", delegate { Give(0); }, false);
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Nuclear: plutonium battery (fully fuelled)", delegate { Give(MAX_AMPOULES); }, false);
        }
        catch (Exception e)
        {
            Log.Error("plutonium battery debug function registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnCreateTooltipLate -= OnCreateTooltip;
        ModHook.OnGameLoadedLate -= FixLoadedItems;
        ModHook.OnPlaceInventorInventoryItemEarly -= OnPlaceInventorInventory;
        ModHook.OnLootTablesLoaded -= AddToLootTables;
    }

    // Every loot table that can roll an ordinary battery can also roll this one. The loot registry replays mod edits
    // whenever the vanilla tables reload, so a table that already lists this battery is skipped.
    public static void AddToLootTables()
    {
        try
        {
            int added = 0;
            foreach (string tableId in new List<string>(LootRegistry.TableIds))
            {
                List<LootEntry> entries = ModHelper.GetLootEntries(tableId);
                if (entries == null || entries.Exists(e => e.id == ITEM_ID))
                {
                    continue;
                }
                LootEntry vanilla = entries.Find(e => e.id == VANILLA_BATTERY);
                if (vanilla == null || vanilla.weight <= 0f)
                {
                    continue;
                }
                ModHelper.AddLootEntry(DreamToxinMod.MOD_ID, tableId, ITEM_ID, (float)(vanilla.weight * LOOT_SHARE));
                added++;
            }
            if (added > 0)
            {
                Log.Info("plutonium battery added to " + added + " loot table(s) next to the ordinary battery");
            }
        }
        catch (Exception e)
        {
            Log.Error("adding the plutonium battery to loot tables failed", e);
        }
    }

    // A code-built drop table (raids, stations) just dropped an ordinary battery: sometimes it is this one instead.
    public static GameItem MaybeSwap(GameItem dropped)
    {
        if (dropped == null || dropped.identifier != VANILLA_BATTERY || !RNG.Roll(DROP_SWAP_PERCENT))
        {
            return dropped;
        }
        GameItem battery = DirectoryMaster.Item(ITEM_ID, GeneralHelper.IsItemOwned(dropped));
        if (battery == null)
        {
            return dropped;
        }
        dropped.Destroy();
        return battery;
    }

    public static void AddItems(ModItemDirectory directory)
    {
        directory.Add(ITEM_ID, Create);
    }

    public static GameItem Create()
    {
        GameItem item = ItemDirectory.CreateEmptyItem(ITEM_ID);
        try
        {
            item.name = Text.Get("pu_battery_name");
            item.shortDescription = Text.Get("pu_battery_desc");
            item.flavorText = Text.Get("pu_battery_flavor");
            item.EnableManufacturer("eminence");
            PowerHelper.InitPowerSourceItem(item, CAPACITY, BANDWIDTH);
            PowerHelper.InitRecharge(item, 0);
            item.unitBaseValue = VALUE;
            item.onCycleEndEarlySlotItemFunc = delegate(GameItem s, GameInventory _, SlotMarker __) { NightlyCharge(s); };
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
            ApplySprite(item);
        }
        catch (Exception e)
        {
            Log.Error("create " + ITEM_ID + " failed", e);
        }
        return item;
    }

    public static bool IsBattery(GameItem item)
    {
        return item != null && item.identifier == ITEM_ID;
    }

    public static int Ampoules(GameItem item)
    {
        return IsBattery(item) && item.IsTag(AMPOULES_TAG) ? Math.Max(0, item.GetTagReadonly(AMPOULES_TAG).GetInt()) : 0;
    }

    public static int Recharge(GameItem item)
    {
        return IsBattery(item) ? Math.Max(0, item.GetTagReadonly(RECHARGE_TAG).GetInt()) : 0;
    }

    private static void SetAmpoules(GameItem item, int count)
    {
        int ampoules = Math.Max(0, Math.Min(MAX_AMPOULES, count));
        int recharge = ampoules * RECHARGE_PER_AMPOULE;
        item.ModifyTag(AMPOULES_TAG, delegate(TagState state)
        {
            state.Enable().SetInt(ampoules);
        }, true);
        item.ModifyTag(RECHARGE_TAG, delegate(TagState state)
        {
            state.Enable().SetInt(recharge);
        }, true);
        // Tag reads (IsTag, GetTagReadonly) see the validated state, which a skip-validate write leaves stale. Without
        // this, every ampoule of one multi-select drop read the count from before the drop: all were used up for one.
        item.ValidateShapeState();
    }

    // ---- fuelling with plutonium ampoules (called from Ampoule in NuclearWaste.cs) ------------------------------

    public static bool CanFuel(GameItem ampoule, GameItem battery)
    {
        return IsBattery(battery) && Ampoules(battery) < MAX_AMPOULES
            && GeneralHelper.IsItemOwned(ampoule) && GeneralHelper.IsItemOwned(battery);
    }

    public static void Fuel(GameItem ampoule, GameItem battery)
    {
        if (!CanFuel(ampoule, battery))
        {
            return;
        }
        SetAmpoules(battery, Ampoules(battery) + 1);
        // Battery value is energy + unitBaseValue (PowerHelper.GetPowerSourceItemValue).
        battery.unitBaseValue += ampoule.unitValue;
        ampoule.Destroy();
        battery.Validate();
        AudioHelper.DropperDrop();
        Notify(Text.Get("pu_battery_fuelled", Recharge(battery), Ampoules(battery), MAX_AMPOULES));
    }

    // ---- nightly self-charge ------------------------------------------------------------------------------------

    public static void NightlyCharge(GameItem item)
    {
        try
        {
            int recharge = Recharge(item);
            if (recharge <= 0)
            {
                return;
            }
            int energy = item.GetTagReadonly(ENERGY_TAG).GetInt();
            int max = item.GetTagReadonly(MAX_ENERGY_TAG).GetInt();
            int charged = Math.Min(max, energy + recharge);
            if (charged != energy)
            {
                item.ModifyTag(ENERGY_TAG, delegate(TagState state)
                {
                    state.SetInt(charged);
                }, true);
                // A turbo boost runs the night's charge again right away: it must read this charge, not the old one.
                item.ValidateShapeState();
                PowerHelper.UpdateEnergyCreditSprite(item);
            }
        }
        catch (Exception e)
        {
            Log.Error("plutonium battery nightly charge failed", e);
        }
    }

    // ---- sprite, tooltip ----------------------------------------------------------------------------------------

    // Vanilla's thresholds: 90+ full, 70+, 50+, 30+, 10+, 1+, empty.
    public static void ApplySprite(GameItem item)
    {
        int max = item.GetTagReadonly(MAX_ENERGY_TAG).GetInt();
        int percent = max > 0 ? item.GetTagReadonly(ENERGY_TAG).GetInt() * 100 / max : 0;
        int level = percent >= 90 ? Levels[0] : percent >= 70 ? Levels[1] : percent >= 50 ? Levels[2]
            : percent >= 30 ? Levels[3] : percent >= 10 ? Levels[4] : percent >= 1 ? Levels[5] : Levels[6];
        BeerSprites.Apply(item, SPRITE + level);
    }

    public static void OnCreateTooltip(RichTextBuilder builder, GameItem item)
    {
        try
        {
            if (builder == null || !IsBattery(item))
            {
                return;
            }
            int ampoules = Ampoules(item);
            builder.AddLine(Text.Get("pu_battery_tooltip_fuel", ampoules, MAX_AMPOULES, RECHARGE_PER_AMPOULE), true, RenderHandler.ColorPalette.LightCyan);
            if (ampoules == 0)
            {
                builder.AddLine(Text.Get("pu_battery_tooltip_hint"), true, RenderHandler.ColorPalette.LightOrange);
            }
        }
        catch (Exception e)
        {
            Log.Error("plutonium battery tooltip failed", e);
        }
    }

    private static void Notify(string text)
    {
        if (StoreUIManager.Instance != null)
        {
            StoreUIManager.Instance.Notify(text, "default");
        }
    }

    // ---- loading, shop, debug ------------------------------------------------------------------------------------

    // Saved batteries are rebuilt by the factory and get their tags back from the save: redo the sprite.
    public static void FixLoadedItems()
    {
        try
        {
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (IsBattery(item))
                {
                    ApplySprite(item);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("fixing loaded plutonium batteries failed", e);
        }
    }

    public static void OnPlaceInventorInventory(List<GameItem> ownedItems)
    {
        try
        {
            if (RNG.Roll(JACKSON_CHANCE))
            {
                PlayerStore.Instance.AddDirectSellingItemToTable(DirectoryMaster.Item(ITEM_ID, false), false, false, false, 0);
            }
        }
        catch (Exception e)
        {
            Log.Error("offering the plutonium battery failed", e);
        }
    }

    private static void Give(int ampoules)
    {
        try
        {
            GameItem item = DirectoryMaster.Item(ITEM_ID, true);
            if (ampoules > 0)
            {
                SetAmpoules(item, ampoules);
            }
            GraphUtils.TryAcceptAll(EmporiumEntry.Instance.invElement, item);
        }
        catch (Exception e)
        {
            Log.Error("plutonium battery debug kit failed", e);
        }
    }
}

// Raid and station loot comes from code-built drop tables (DropDirectory / DropTableDirectory), not the loot registry.
[HarmonyPatch(typeof(DropTable), "GetDropped")]
public static class PuBatteryDropPatch
{
    private static void Postfix(List<GameItem> __result)
    {
        try
        {
            if (__result == null)
            {
                return;
            }
            for (int i = 0; i < __result.Count; i++)
            {
                __result[i] = PuBattery.MaybeSwap(__result[i]);
            }
        }
        catch (Exception e)
        {
            Log.Error("plutonium battery drop swap failed", e);
        }
    }
}

// Vanilla only swaps sprites for its own battery IDs.
[HarmonyPatch(typeof(PowerHelper), "UpdateEnergyCreditSprite")]
public static class PuBatterySpritePatch
{
    private static void Postfix(GameItem item)
    {
        try
        {
            if (PuBattery.IsBattery(item))
            {
                PuBattery.ApplySprite(item);
            }
        }
        catch (Exception e)
        {
            Log.Error("plutonium battery sprite failed", e);
        }
    }
}
