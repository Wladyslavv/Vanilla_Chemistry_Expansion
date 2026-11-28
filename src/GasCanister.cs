// Gas canister for the brine electrolysis line (art/salt/DESIGN.md): its own item, NOT a vanilla liquid container,
// so none of the vanilla liquid paths (pouring, machine container slots, drains, water buyers, purifiers, pipette
// mods) can put anything into it. It holds hydrogen, chlorine, hydrogen chloride, nitric oxide and methanol vapour in its own tags
// (uL, 5 L in all).
//   - The electrolyzer fills empty or pure canisters (product A hydrogen, product B chlorine; product A nitric oxide
//     when its feed slot is empty).
//   - Drag one canister onto another: its gases move into the target, as far as there is room.
//   - On a heating plate (patched below) it warms up; at 120 C+ hydrogen and chlorine burn 1:1 into twice the volume
//     of hydrogen chloride, 10% a second (at least 1000 uL), like the faster reactions of this mod.
//     At 150 C+ the hydrogen left takes carbon dioxide from the air (taken as always there): each part of hydrogen
//     makes 1 part of methanol vapour, at the same rate.
//   - Drag it onto a liquid container with water: hydrogen chloride dissolves into vanilla acid and nitric oxide into
//     nitric acid, each up to 40% of the liquid; what does not dissolve stays in the canister.
//     Methanol vapour needs no water: it condenses 1:1 into liquid methanol, as far as the container has room.
//   - Right-click "Vent" (the vanilla toggle button) empties it.
// Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using HarmonyLib;

public static class GasCanister
{
    public const string ITEM_ID = "vce_gas_canister";
    public const string SPRITE_EMPTY = "vce_gas_canister";
    public const string SPRITE_H2 = "vce_gas_canister_h2";
    public const string SPRITE_CL2 = "vce_gas_canister_cl2";
    public const string SPRITE_MIX = "vce_gas_canister_mix";
    public const string SPRITE_HCL = "vce_gas_canister_hcl";
    public const string SPRITE_NO = "vce_gas_canister_no";
    public const string SPRITE_MEOH = "vce_gas_canister_meoh";

    public const string HYDROGEN = "h2";
    public const string CHLORINE = "cl2";
    public const string HCL = "hcl";
    // Nitric oxide: the electrolyzer's arc makes it from air when its feed slot is empty (Electrolyzer.RunNoCycle).
    public const string NITRIC_OXIDE = "no";
    // Methanol vapour: hydrogen heated to 150 C+ with the carbon dioxide of the air (taken as always there).
    public const string METHANOL = "meoh";
    private static readonly string[] Gases = { HYDROGEN, CHLORINE, HCL, NITRIC_OXIDE, METHANOL };

    public const string TEMP_TAG = "CONTAINER_TEMP_TAG";  // same tag as liquid containers: the heating plate shows it

    // 5 L for now: gas compresses easily. Amounts are kept in microlitres, the unit of the liquids.
    public const int CAPACITY_ML = 5000;
    public const int CAPACITY_UL = CAPACITY_ML * 1000;
    public const long VALUE = 25;

    // Temperature: vanilla's 26 C room, but a steel bottle loses heat slowly (about 0.4% of the difference a second).
    public const double AMBIENT = 26.0;
    public const double START_TEMP = 24.0;
    public const double COOL_PER_SECOND = 0.004;

    // H2 + Cl2 -> 2 HCl.
    public const double REACT_TEMP = 120.0;
    public const double REACT_RATE = 0.1;
    public const int REACT_MIN_UL = 1000;

    // H2 (+ CO2 from the air) -> methanol vapour, 1 part of hydrogen to 1 part of vapour, at 150 C+, at the same rate.
    // The gases here are counted in liquid-equivalent millilitres, not real gas volumes, so the molar 3:1 of
    // CO2 + 3 H2 -> CH3OH + H2O does not carry over; 1:1 (3:1 up to 1.11.0) gives the HPLC enough methanol: one
    // electrolyzer night of brine (240 mL salt, 120 mL hydrogen) now makes 120 mL of methanol.
    public const double METHANOL_TEMP = 150.0;
    public const int METHANOL_H2_PER_PART = 1;

    // Hydrogen chloride dissolves until acid makes up 40% of the liquid: acid <= other liquids x 2/3.
    public const double SOLUBILITY = 0.4;

    public static void Enable()
    {
        ModHook.OnCreateTooltipLate += OnCreateTooltip;
        ModHook.OnGameLoadedLate += FixLoadedItems;
        try
        {
            string id = DreamToxinMod.MOD_ID;
            ModHelper.AddDebugFunction(id, "Modding", "Gas: empty canister", delegate { Give(null, null); }, false);
            ModHelper.AddDebugFunction(id, "Modding", "Gas: full hydrogen canister", delegate { Give(HYDROGEN, null); }, false);
            ModHelper.AddDebugFunction(id, "Modding", "Gas: full chlorine canister", delegate { Give(CHLORINE, null); }, false);
            ModHelper.AddDebugFunction(id, "Modding", "Gas: hydrogen + chlorine canister", delegate { Give(HYDROGEN, CHLORINE); }, false);
            ModHelper.AddDebugFunction(id, "Modding", "Gas: full nitric oxide canister", delegate { Give(NITRIC_OXIDE, null); }, false);
            ModHelper.AddDebugFunction(id, "Modding", "Gas: full methanol vapour canister", delegate { Give(METHANOL, null); }, false);
        }
        catch (Exception e)
        {
            Log.Error("gas canister debug function registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnCreateTooltipLate -= OnCreateTooltip;
        ModHook.OnGameLoadedLate -= FixLoadedItems;
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
            item.shortDescription = Text.Get("gas_canister_desc");
            item.unitValue = VALUE;
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            item.SetGameItemType("TOOL");
            AudioHelper.InitSolidMetal(item);
            item.ModifyTag(TEMP_TAG, delegate(TagState state)
            {
                state.Enable().SetDouble(START_TEMP);
            }, true);

            // Drag onto another canister (merge) or onto a liquid container (dissolve hydrogen chloride).
            item.mayThisTargetItemFunc = (GameItem s, GameItem t) => CanTarget(s, t);
            item.canThisTargetItemFunc = (GameItem s, GameItem t) => CanTarget(s, t);
            item.onThisTargetItemFunc = delegate(GameItem s, GameItem t) { OnTarget(s, t); };

            // Right-click: the vanilla toggle button, relabelled "Vent".
            item.mayToggleSlotItemFunc = (GameItem s, GameInventory _, SlotMarker __) => IsCanister(s) && GeneralHelper.IsItemOwned(s);
            item.canToggleSlotItemFunc = (GameItem s, GameInventory _, SlotMarker __) => Total(s) > 0;
            item.onToggleSlotItemFunc = delegate(GameItem s, GameInventory _, SlotMarker __) { Vent(s); };

            item.onOneSecondItemFunc = delegate(GameItem s, GameInventory _, SlotMarker __) { Tick(s); };

            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
            Apply(item);
        }
        catch (Exception e)
        {
            Log.Error("create " + ITEM_ID + " failed", e);
        }
        return item;
    }

    public static bool IsCanister(GameItem item)
    {
        return item != null && item.identifier == ITEM_ID;
    }

    // ---- contents -------------------------------------------------------------------------------------------------

    private static string TagOf(string gas)
    {
        return "VCE_GAS_" + gas.ToUpperInvariant() + "_INT";
    }

    // Gas amounts and temperature are written to the item's base state without revalidating it (that would cost a
    // full shape validation every second), so they are read from the base state too. IsTag / GetTagReadonly read the
    // validated state, which only catches up when the item is next validated (moved, for one): reading that made a
    // freshly filled canister look empty and lost gas when it was filled or reacted again before being moved.
    private static bool HasBaseTag(GameItem item, string tag)
    {
        return item != null && item.state != null && item.state.IsTag(tag);
    }

    public static int Amount(GameItem item, string gas)
    {
        if (!IsCanister(item) || gas == null || !HasBaseTag(item, TagOf(gas)))
        {
            return 0;
        }
        return Math.Max(0, item.state.GetTag(TagOf(gas)).GetInt());
    }

    public static int Total(GameItem item)
    {
        int total = 0;
        foreach (string gas in Gases)
        {
            total += Amount(item, gas);
        }
        return total;
    }

    public static int FreeSpace(GameItem item)
    {
        return IsCanister(item) ? Math.Max(0, CAPACITY_UL - Total(item)) : 0;
    }

    // Empty, or holding nothing but `gas`.
    public static bool IsPure(GameItem item, string gas)
    {
        return IsCanister(item) && Total(item) == Amount(item, gas);
    }

    // Room for `gas` in a canister that must stay pure (the electrolyzer's product slots).
    public static int FreeFor(GameItem item, string gas)
    {
        return IsPure(item, gas) ? FreeSpace(item) : 0;
    }

    // Adds up to `amount` uL of `gas`; returns what fitted.
    public static int Fill(GameItem item, string gas, int amount)
    {
        if (!IsCanister(item) || amount <= 0)
        {
            return 0;
        }
        int stored = Math.Min(amount, FreeSpace(item));
        if (stored > 0)
        {
            SetAmount(item, gas, Amount(item, gas) + stored);
            Apply(item);
        }
        return stored;
    }

    // Removes up to `amount` uL of `gas`; returns what was taken.
    public static int Take(GameItem item, string gas, int amount)
    {
        int taken = Math.Min(Math.Max(0, amount), Amount(item, gas));
        if (taken > 0)
        {
            SetAmount(item, gas, Amount(item, gas) - taken);
            Apply(item);
        }
        return taken;
    }

    private static void SetAmount(GameItem item, string gas, int amount)
    {
        int value = Math.Max(0, amount);
        item.ModifyTag(TagOf(gas), delegate(TagState state)
        {
            state.Enable().SetInt(value);
        }, true);
    }

    public static void Vent(GameItem item)
    {
        if (!IsCanister(item))
        {
            return;
        }
        foreach (string gas in Gases)
        {
            SetAmount(item, gas, 0);
        }
        Apply(item);
        Notify(Text.Get("gas_canister_vented"));
    }

    public static double TempOf(GameItem item)
    {
        return HasBaseTag(item, TEMP_TAG) ? item.state.GetTag(TEMP_TAG).GetDouble() : START_TEMP;
    }

    public static void SetTemp(GameItem item, double temp)
    {
        item.ModifyTag(TEMP_TAG, delegate(TagState state)
        {
            state.Enable().SetDouble(temp);
        }, true);
    }

    // ---- name, sprite, tooltip ----------------------------------------------------------------------------------

    // Nitric oxide or methanol vapour mixed with anything is a plain mixed canister; otherwise hydrogen chloride takes priority, then the
    // hydrogen / chlorine mix, then a single gas.
    public static void Apply(GameItem item)
    {
        int h2 = Amount(item, HYDROGEN), cl2 = Amount(item, CHLORINE), hcl = Amount(item, HCL), no = Amount(item, NITRIC_OXIDE);
        int meoh = Amount(item, METHANOL);
        string name, sprite;
        if ((no > 0 && h2 + cl2 + hcl + meoh > 0) || (meoh > 0 && h2 + cl2 + hcl > 0))
        {
            name = "gas_canister_mixed_name"; sprite = SPRITE_MIX;
        }
        else if (meoh > 0)
        {
            name = "gas_canister_meoh_name"; sprite = SPRITE_MEOH;
        }
        else if (no > 0)
        {
            name = "gas_canister_no_name"; sprite = SPRITE_NO;
        }
        else if (hcl > 0)
        {
            name = "gas_canister_hcl_name"; sprite = SPRITE_HCL;
        }
        else if (h2 > 0 && cl2 > 0)
        {
            name = "gas_canister_mix_name"; sprite = SPRITE_MIX;
        }
        else if (h2 > 0)
        {
            name = "gas_canister_h2_name"; sprite = SPRITE_H2;
        }
        else if (cl2 > 0)
        {
            name = "gas_canister_cl2_name"; sprite = SPRITE_CL2;
        }
        else
        {
            name = "gas_canister_name"; sprite = SPRITE_EMPTY;
        }
        item.name = Text.Get(name);
        BeerSprites.Apply(item, sprite);
        // Bring the validated state up to date, so vanilla code reading the item (tooltips, saving) sees the gas too.
        item.ValidateShapeState();
    }

    public static void OnCreateTooltip(RichTextBuilder builder, GameItem item)
    {
        try
        {
            if (builder == null || !IsCanister(item))
            {
                return;
            }
            bool any = false;
            foreach (string gas in Gases)
            {
                int amount = Amount(item, gas);
                if (amount > 0)
                {
                    builder.AddLine(Text.Get("gas_canister_tooltip_gas", Text.Get("gas_" + gas), Ml(amount)), true, RenderHandler.ColorPalette.LightCyan);
                    any = true;
                }
            }
            if (!any)
            {
                builder.AddLine(Text.Get("gas_canister_tooltip_empty"), true, RenderHandler.ColorPalette.LightCyan);
            }
            builder.AddLine(Text.Get("gas_canister_tooltip_total", Ml(Total(item)), CAPACITY_ML, TempOf(item).ToString("0")), true, RenderHandler.ColorPalette.LightCyan);
        }
        catch (Exception e)
        {
            Log.Error("gas canister tooltip failed", e);
        }
    }

    private static string Ml(int ul)
    {
        return (ul / 1000.0).ToString("0.#");
    }

    // ---- every second: cool down, and burn hydrogen in chlorine when hot enough ---------------------------------

    public static void Tick(GameItem item)
    {
        try
        {
            if (!IsCanister(item))
            {
                return;
            }
            double temp = TempOf(item);
            if (Math.Abs(temp - AMBIENT) > 0.01)
            {
                SetTemp(item, AMBIENT + (temp - AMBIENT) * Math.Exp(-COOL_PER_SECOND));
            }
            bool changed = false;
            int h2 = Amount(item, HYDROGEN), cl2 = Amount(item, CHLORINE);
            if (temp >= REACT_TEMP && h2 > 0 && cl2 > 0)
            {
                int limiting = Math.Min(h2, cl2);
                int pair = Math.Min(limiting, Math.Max(REACT_MIN_UL, (int)Math.Round(limiting * REACT_RATE)));
                h2 -= pair;
                SetAmount(item, HYDROGEN, h2);
                SetAmount(item, CHLORINE, cl2 - pair);
                SetAmount(item, HCL, Amount(item, HCL) + 2 * pair);
                changed = true;
            }
            // Hydrogen left over at 150 C+ takes carbon dioxide from the air: 1 part makes 1 part of methanol vapour.
            if (temp >= METHANOL_TEMP && h2 > 0)
            {
                int used = Math.Min(h2, Math.Max(REACT_MIN_UL, (int)Math.Round(h2 * REACT_RATE)));
                SetAmount(item, HYDROGEN, h2 - used);
                SetAmount(item, METHANOL, Amount(item, METHANOL) + Math.Max(1, used / METHANOL_H2_PER_PART));
                changed = true;
            }
            if (changed)
            {
                Apply(item);
            }
        }
        catch (Exception e)
        {
            Log.Error("gas canister tick failed", e);
        }
    }

    // Heated by a heating plate (see HeatingPlateCanisterPatch): vanilla's stove formula, without its volume check.
    public static void Heat(GameItem item, GameItem plate)
    {
        int state = plate.GetTagReadonly("HEATING_PLATE_STATE").GetInt();
        if (state != 1 && state != 2)
        {
            return;
        }
        double factor = state == 2 ? 30.0 : 10.0;
        int upgrades = MachineBlender.GetUpgradeCount(plate);
        if (upgrades == 3)
        {
            factor *= 4.0;
        }
        else if (upgrades >= 4)
        {
            factor *= 16.0;
        }
        double target = plate.GetTagReadonly("CURRENT_TARGET_TEMP").GetInt();
        double temp = TempOf(item);
        if (temp < target)
        {
            SetTemp(item, Math.Min(temp + (target - temp) * (factor / 350.0) + 1.0, target));
        }
        plate.EnableTag("HEATING_TAG", true);
    }

    // ---- dragging onto things -------------------------------------------------------------------------------------

    private static bool CanTarget(GameItem s, GameItem t)
    {
        if (!IsCanister(s) || t == null || t == s || !GeneralHelper.IsItemOwned(s) || !GeneralHelper.IsItemOwned(t))
        {
            return false;
        }
        if (IsCanister(t))
        {
            return Total(s) > 0;
        }
        return t.IsTag("LIQUID_CONTAINER_TAG") && (Amount(s, HCL) > 0 || Amount(s, NITRIC_OXIDE) > 0 || Amount(s, METHANOL) > 0);
    }

    private static void OnTarget(GameItem s, GameItem t)
    {
        try
        {
            if (IsCanister(t))
            {
                Merge(s, t);
            }
            else if (t.IsTag("LIQUID_CONTAINER_TAG"))
            {
                Dissolve(s, t);
            }
        }
        catch (Exception e)
        {
            Log.Error("gas canister drag failed", e);
        }
    }

    // Moves the source's gases into the target, in proportion when the target cannot take them all.
    public static void Merge(GameItem source, GameItem target)
    {
        int total = Total(source), room = FreeSpace(target);
        if (total <= 0)
        {
            return;
        }
        if (room <= 0)
        {
            Notify(Text.Get("gas_canister_full"));
            return;
        }
        double share = Math.Min(1.0, (double)room / total);
        int moved = 0;
        foreach (string gas in Gases)
        {
            int amount = (int)Math.Floor(Amount(source, gas) * share);
            if (amount > 0)
            {
                SetAmount(source, gas, Amount(source, gas) - amount);
                SetAmount(target, gas, Amount(target, gas) + amount);
                moved += amount;
            }
        }
        if (moved <= 0)
        {
            return;
        }
        int before = Total(target) - moved;
        SetTemp(target, (TempOf(target) * before + TempOf(source) * moved) / Math.Max(1, before + moved));
        Apply(source);
        Apply(target);
        AudioHelper.PourWater();
    }

    // Soluble gases into water, each 1:1 into its acid (hydrogen chloride -> vanilla acid, nitric oxide -> nitric
    // acid), each acid up to 40% of the liquid; what does not dissolve stays in the canister.
    // Methanol vapour needs no water: it condenses 1:1 into liquid methanol, as far as the container has room.
    public static void Dissolve(GameItem canister, GameItem container)
    {
        bool acids = Amount(canister, HCL) > 0 || Amount(canister, NITRIC_OXIDE) > 0;
        if (!acids && Amount(canister, METHANOL) <= 0)
        {
            return;
        }
        string failure = null;
        bool any = false;
        Condense(canister, container, ref any, ref failure);
        if (acids)
        {
            if (IaLiquids.Get(container, "water") <= 0)
            {
                failure = "gas_canister_needs_water";
            }
            else
            {
                DissolveGas(canister, container, HCL, "acid", ref any, ref failure);
                DissolveGas(canister, container, NITRIC_OXIDE, FlavorChemistry.NITRIC_ACID, ref any, ref failure);
            }
        }
        if (!any)
        {
            if (failure != null)
            {
                Notify(Text.Get(failure));
            }
            return;
        }
        Apply(canister);
        ItemFeature waterFeature = WaterFeatureHelper.GetWaterFeature(container);
        WaterFeatureHelper.OnLiquidChanged(container, waterFeature != null && waterFeature.IsExposed());
        SpriteHelper.UpdateWaterContainerSprite(container);
        AudioHelper.PourWater();
    }

    // One gas into its acid: until that acid makes up 40% of the liquid (acid <= other liquids x 2/3), and as far
    // as the container has room.
    private static void DissolveGas(GameItem canister, GameItem container, string gas, string liquid, ref bool any, ref string failure)
    {
        int held = Amount(canister, gas);
        if (held <= 0)
        {
            return;
        }
        int total = WaterHelper.GetTotalVolume(container);
        int acid = IaLiquids.Get(container, liquid);
        long other = Math.Max(0, total - acid);
        long limit = (long)Math.Floor(other * SOLUBILITY / (1.0 - SOLUBILITY)) - acid;
        long amount = Math.Min(Math.Min(limit, held), WaterHelper.GetFreeCapacity(container));
        if (amount <= 0)
        {
            failure = limit <= 0 ? "gas_canister_saturated" : "gas_canister_container_full";
            return;
        }
        int dissolved = (int)amount;
        SetAmount(canister, gas, held - dissolved);
        ChemicalHelper.HandleReceiveLiquidTemp(container, dissolved, TempOf(canister));
        IaLiquids.Set(container, liquid, acid + dissolved);
        any = true;
    }

    private static void Condense(GameItem canister, GameItem container, ref bool any, ref string failure)
    {
        int held = Amount(canister, METHANOL);
        if (held <= 0)
        {
            return;
        }
        int amount = Math.Min(held, WaterHelper.GetFreeCapacity(container));
        if (amount <= 0)
        {
            failure = "gas_canister_container_full";
            return;
        }
        SetAmount(canister, METHANOL, held - amount);
        ChemicalHelper.HandleReceiveLiquidTemp(container, amount, TempOf(canister));
        IaLiquids.Set(container, IaLiquids.METHANOL, IaLiquids.Get(container, IaLiquids.METHANOL) + amount);
        any = true;
    }

    private static void Notify(string text)
    {
        if (StoreUIManager.Instance != null)
        {
            StoreUIManager.Instance.Notify(text, "default");
        }
    }

    // ---- loading, shops, debug ----------------------------------------------------------------------------------

    // Saved canisters are rebuilt by the factory and get their tags back from the save: redo name and sprite.
    // Test builds before the mixed-gas canister kept one gas in VCE_GAS_TYPE_STRING / VCE_GAS_AMOUNT_INT.
    public static void FixLoadedItems()
    {
        try
        {
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (!IsCanister(item))
                {
                    continue;
                }
                if (item.IsTag("VCE_GAS_AMOUNT_INT"))
                {
                    string gas = item.IsTag("VCE_GAS_TYPE_STRING") ? item.GetTagReadonly("VCE_GAS_TYPE_STRING").GetString() : "";
                    int amount = item.GetTagReadonly("VCE_GAS_AMOUNT_INT").GetInt();
                    if ((gas == HYDROGEN || gas == CHLORINE) && amount > 0)
                    {
                        SetAmount(item, gas, Amount(item, gas) + Math.Min(amount, CAPACITY_UL));
                    }
                    item.DisableTag("VCE_GAS_AMOUNT_INT", true);
                    item.DisableTag("VCE_GAS_TYPE_STRING", true);
                }
                Apply(item);
            }
        }
        catch (Exception e)
        {
            Log.Error("fixing loaded gas canisters failed", e);
        }
    }

    // An empty canister for sale (the Cartel Vendor, CartelVendor.cs).
    public static GameItem CreateForSale()
    {
        return DirectoryMaster.Item(ITEM_ID, false);
    }

    // gasA alone fills the canister; gasA + gasB fill half each.
    private static void Give(string gasA, string gasB)
    {
        try
        {
            GameItem item = DirectoryMaster.Item(ITEM_ID, true);
            if (gasA != null)
            {
                int each = gasB != null ? CAPACITY_UL / 2 : CAPACITY_UL;
                Fill(item, gasA, each);
                if (gasB != null)
                {
                    Fill(item, gasB, each);
                }
            }
            GraphUtils.TryAcceptAll(EmporiumEntry.Instance.invElement, item);
        }
        catch (Exception e)
        {
            Log.Error("gas canister debug kit failed", e);
        }
    }
}

// The heating plate also takes a canister and warms it (vanilla only heats liquid, by volume, so a canister would
// be skipped).
[HarmonyPatch(typeof(MachineHeatingPlate), "HeatingPlate")]
public static class HeatingPlateCanisterPatch
{
    private static void Postfix(GameItem __result)
    {
        try
        {
            GameItem plate = __result;
            GameSlotInventory slot = null;
            for (int i = 0; i < 6 && slot == null; i++)
            {
                slot = MachineHelper.GetBodyStorage(plate, i) as GameSlotInventory;
            }
            if (slot == null)
            {
                Log.Info("heating plate container slot not found; gas canisters cannot be heated");
                return;
            }
            Func<GameItem, GameInventory, bool> mayAdd = slot.mayInventoryAddItemFunc;
            slot.mayInventoryAddItemFunc = (GameItem item, GameInventory inventory) =>
                (mayAdd != null && mayAdd(item, inventory)) || (GasCanister.IsCanister(item) && GeneralHelper.IsItemOwned(item));
            Action<GameItem, GameInventory, SlotMarker> vanilla = plate.onOneSecondItemFunc;
            GameSlotInventory containerSlot = slot;
            plate.onOneSecondItemFunc = delegate(GameItem item, GameInventory inventory, SlotMarker marker)
            {
                if (vanilla != null)
                {
                    vanilla(item, inventory, marker);
                }
                try
                {
                    if (GasCanister.IsCanister(containerSlot.childItem) && GasCanister.Total(containerSlot.childItem) > 0)
                    {
                        GasCanister.Heat(containerSlot.childItem, plate);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("heating a gas canister failed", e);
                }
            };
        }
        catch (Exception e)
        {
            Log.Error("heating plate canister patch failed", e);
        }
    }
}

// The fridge takes a canister in either zone (vanilla only lets in liquid containers, drinks and ice cream).
[HarmonyPatch(typeof(MachineFridge), "MayZoneAcceptItem")]
public static class CanisterFridgeAcceptPatch
{
    private static void Postfix(GameItem item, ref bool __result)
    {
        try
        {
            if (!__result && GasCanister.IsCanister(item) && GeneralHelper.IsItemOwned(item))
            {
                __result = true;
            }
        }
        catch (Exception e)
        {
            Log.Error("letting a gas canister into the fridge failed", e);
        }
    }
}

// A powered fridge cools canisters like liquid containers: towards 3 C at 0.5 C a second in the chill zone, -18 C at
// 1 C a second in the freezer. Vanilla only runs this while the fridge has power.
[HarmonyPatch(typeof(MachineFridge), "CoolZoneContainers")]
public static class CanisterFridgeCoolPatch
{
    private static void Postfix(GameGridInventory zone, double targetTemp, double degreesPerSecond)
    {
        try
        {
            if (zone == null)
            {
                return;
            }
            foreach (GameItem item in zone.childItems)
            {
                if (!GasCanister.IsCanister(item))
                {
                    continue;
                }
                double temp = GasCanister.TempOf(item);
                double next = temp > targetTemp ? Math.Max(temp - degreesPerSecond, targetTemp) : Math.Min(temp + degreesPerSecond, targetTemp);
                if (next != temp)
                {
                    GasCanister.SetTemp(item, next);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("cooling a gas canister in the fridge failed", e);
        }
    }
}

// The right-click toggle button reads "Vent" on a canister.
[HarmonyPatch(typeof(ItemContextHandler), "SetButtonColor")]
public static class CanisterVentLabelPatch
{
    private static void Postfix(RichTextElement button, RichTextElement selectedButton, bool isEnabled, GameItem ___currentItem,
        RichTextElement ___buttonToggle, RenderHandler.ColorPalette ___textColor, RenderHandler.ColorPalette ___textDisabledColor,
        RenderHandler.ColorPalette ___textSelectColor, RenderHandler.ColorPalette ___textSelectDisabledColor)
    {
        try
        {
            if (button == null || button != ___buttonToggle || !GasCanister.IsCanister(___currentItem))
            {
                return;
            }
            bool selected = button == selectedButton;
            RenderHandler.ColorPalette color = !isEnabled ? (selected ? ___textSelectDisabledColor : ___textDisabledColor)
                : (selected ? ___textSelectColor : ___textColor);
            button.SetText(Text.Get("gas_canister_vent"), 10000, color);
        }
        catch (Exception e)
        {
            Log.Error("vent label failed", e);
        }
    }
}
