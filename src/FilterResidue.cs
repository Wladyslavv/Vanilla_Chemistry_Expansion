// Filter residue: what a filter catches stays in it. Vanilla WaterHelper.HandleFilter only lowers the amount that
// gets through (the source is still debited in full, so the caught part vanished); here the caught amount of each
// liquid is recorded on the filter itself (Kotton fabric, water filter, advanced water filter, hydroreed filter; the
// UV filter kills microbes and catches nothing). The residue is saved with the filter as a string tag
// ("id=amount;id=amount", amounts in vanilla parts, 1000 per ml). Dragging a filter that holds residue onto an owned
// liquid container pours the residue in (as much as fits); onto the drain throws it away. Containers that hold a
// filter themselves (the pitcher) are left out, so the filter can still be put back in.
// Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;

public static class FilterResidue
{
    public const string TAG = "VCE_FILTER_RESIDUE_STRING";

    public static void Enable()
    {
        ModHook.OnCreateTooltipLate += OnCreateTooltip;
    }

    public static void Disable()
    {
        ModHook.OnCreateTooltipLate -= OnCreateTooltip;
    }

    public static bool Catches(GameItem filter)
    {
        return filter != null && filter.IsTag("WATER_FILTER") && filter.identifier != "uv_filter";
    }

    public static Dictionary<string, int> Read(GameItem filter)
    {
        Dictionary<string, int> residue = new Dictionary<string, int>();
        if (filter == null || !filter.IsTag(TAG))
        {
            return residue;
        }
        string text = filter.GetTagReadonly(TAG).GetString();
        if (string.IsNullOrEmpty(text))
        {
            return residue;
        }
        foreach (string entry in text.Split(';'))
        {
            int eq = entry.IndexOf('=');
            int amount;
            if (eq > 0 && int.TryParse(entry.Substring(eq + 1), out amount) && amount > 0)
            {
                residue[entry.Substring(0, eq)] = amount;
            }
        }
        return residue;
    }

    private static void Write(GameItem filter, Dictionary<string, int> residue)
    {
        StringBuilder text = new StringBuilder();
        foreach (KeyValuePair<string, int> entry in residue)
        {
            if (entry.Value <= 0)
            {
                continue;
            }
            if (text.Length > 0)
            {
                text.Append(';');
            }
            text.Append(entry.Key).Append('=').Append(entry.Value);
        }
        string value = text.ToString();
        if (value.Length == 0)
        {
            filter.DisableTag(TAG, false);
            return;
        }
        filter.ModifyTag(TAG, delegate(TagState state)
        {
            state.Enable().SetString(value);
        }, false);
    }

    public static void Add(GameItem filter, string liquidId, int amount)
    {
        if (amount <= 0 || !Catches(filter) || Liquid.Liquids.Find(l => l.identifier == liquidId) == null)
        {
            return;
        }
        Dictionary<string, int> residue = Read(filter);
        int old;
        residue.TryGetValue(liquidId, out old);
        residue[liquidId] = (int)Math.Min(int.MaxValue, (long)old + amount);
        Write(filter, residue);
    }

    public static int Total(GameItem filter)
    {
        long total = 0;
        foreach (int amount in Read(filter).Values)
        {
            total += amount;
        }
        return (int)Math.Min(int.MaxValue, total);
    }

    public static bool HasResidue(GameItem filter)
    {
        return Catches(filter) && Total(filter) > 0;
    }

    // ---- pouring out ----------------------------------------------------------------------------------------------

    public static bool CanPourInto(GameItem filter, GameItem target)
    {
        if (target == null || !HasResidue(filter) || !GeneralHelper.IsItemOwned(filter))
        {
            return false;
        }
        if (WaterHelper.IsDrain(target))
        {
            return true;
        }
        return target.IsTag("LIQUID_CONTAINER_TAG") && !target.IsTag("FILTER_CONTAINER_TAG")
            && WaterHelper.CanUseWaterContainer(target);
    }

    public static void PourInto(GameItem filter, GameItem target)
    {
        try
        {
            if (!CanPourInto(filter, target))
            {
                return;
            }
            if (WaterHelper.IsDrain(target))
            {
                filter.DisableTag(TAG, false);
                AudioHelper.DumpWater();
                return;
            }
            Dictionary<string, int> residue = Read(filter);
            int total = Total(filter);
            int free = WaterHelper.GetFreeCapacity(target);
            if (free <= 0)
            {
                return;
            }
            int moving = Math.Min(free, total);
            // Split what fits in proportion; the rounding remainder goes to the largest part.
            Dictionary<string, int> moved = new Dictionary<string, int>();
            int sum = 0;
            string largest = null;
            foreach (KeyValuePair<string, int> entry in residue)
            {
                int part = moving == total ? entry.Value : (int)((long)entry.Value * moving / total);
                moved[entry.Key] = part;
                sum += part;
                if (largest == null || entry.Value > residue[largest])
                {
                    largest = entry.Key;
                }
            }
            if (largest != null && sum < moving)
            {
                moved[largest] = Math.Min(residue[largest], moved[largest] + moving - sum);
            }
            foreach (KeyValuePair<string, int> entry in moved)
            {
                if (entry.Value <= 0)
                {
                    continue;
                }
                string part = Liquid.GetCurrentPartFromId(entry.Key);
                int current = target.GetTagReadonly(part).GetInt();
                int add = entry.Value;
                target.ModifyTag(part, delegate(TagState state)
                {
                    state.Enable().SetInt(current + add);
                }, true);
                residue[entry.Key] -= add;
            }
            Write(filter, residue);
            AudioHelper.PourWater();
            WaterHelper.UpdateWaterBottleSound(target);
        }
        catch (Exception e)
        {
            Log.Error("pouring filter residue failed", e);
        }
    }

    public static void InitFilter(GameItem filter)
    {
        if (filter == null)
        {
            return;
        }
        filter.mayThisTargetItemFunc = (Func<GameItem, GameItem, bool>)Delegate.Combine(filter.mayThisTargetItemFunc,
            (Func<GameItem, GameItem, bool>)((GameItem s, GameItem t) => CanPourInto(s, t)));
        filter.canThisTargetItemFunc = (Func<GameItem, GameItem, bool>)Delegate.Combine(filter.canThisTargetItemFunc,
            (Func<GameItem, GameItem, bool>)((GameItem s, GameItem t) => CanPourInto(s, t)));
        filter.onThisTargetItemFunc = (Action<GameItem, GameItem>)Delegate.Combine(filter.onThisTargetItemFunc,
            (Action<GameItem, GameItem>)delegate(GameItem s, GameItem t) { PourInto(s, t); });
    }

    // ---- tooltip --------------------------------------------------------------------------------------------------

    public static void OnCreateTooltip(RichTextBuilder builder, GameItem item)
    {
        try
        {
            if (builder == null || !HasResidue(item))
            {
                return;
            }
            StringBuilder parts = new StringBuilder();
            foreach (KeyValuePair<string, int> entry in Read(item))
            {
                Liquid liquid = Liquid.Liquids.Find(l => l.identifier == entry.Key);
                if (liquid == null)
                {
                    continue;
                }
                if (parts.Length > 0)
                {
                    parts.Append(Text.Get("filter_residue_separator"));
                }
                parts.Append(Text.Get("filter_residue_part", liquid.displayName, (entry.Value / 1000.0).ToString("0.#")));
            }
            builder.AddLine(Text.Get("filter_residue_tooltip", parts.ToString()), true, RenderHandler.ColorPalette.LightCyan);
            builder.AddLine(Text.Get("filter_residue_hint"), true, RenderHandler.ColorPalette.LightCyan);
        }
        catch (Exception e)
        {
            Log.Error("filter residue tooltip failed", e);
        }
    }
}

// Record what the filter held back. The body lowers its "amount" argument, so the original is kept in the prefix.
[HarmonyPatch(typeof(WaterHelper), "HandleFilter")]
public static class FilterResidueCatchPatch
{
    private static void Prefix(int amount, out int __state)
    {
        __state = amount;
    }

    private static void Postfix(string identifier, GameItem filter, int __result, int __state)
    {
        try
        {
            // Mod setting "keep filter residue" off: filters catch nothing new (what they already hold can still be
            // poured out).
            if (__state > __result && IaSettings.KeepFilterResidue && FilterResidue.Catches(filter))
            {
                FilterResidue.Add(filter, identifier, __state - __result);
            }
        }
        catch (Exception e)
        {
            Log.Error("recording filter residue failed", e);
        }
    }
}

[HarmonyPatch(typeof(AmenitiesItemDirectory), "KottonFabric")]
public static class FilterResidueKottonPatch
{
    private static void Postfix(GameItem __result) { FilterResidue.InitFilter(__result); }
}

[HarmonyPatch(typeof(AmenitiesItemDirectory), "WaterFilter")]
public static class FilterResidueWaterFilterPatch
{
    private static void Postfix(GameItem __result) { FilterResidue.InitFilter(__result); }
}

[HarmonyPatch(typeof(AmenitiesItemDirectory), "WaterFilterAdvanced")]
public static class FilterResidueWaterFilterAdvPatch
{
    private static void Postfix(GameItem __result) { FilterResidue.InitFilter(__result); }
}

[HarmonyPatch(typeof(AmenitiesItemDirectory), "HydroreedFilter")]
public static class FilterResidueHydroreedPatch
{
    private static void Postfix(GameItem __result) { FilterResidue.InitFilter(__result); }
}
