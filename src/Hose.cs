// The flow hose (导流管): a coiled hose with a valve (2x2, art/hose/draw_hose.py) that links two liquid containers and
// moves liquid from the first to the second, a set amount every second.
//   - Right-click > Configure opens its window: a slider for the flow, 1-100 mL a second (default 5), kept on the
//     item in VCE_HOSE_RATE_INT and shown in its tooltip; and a "flow conditions" section with two sliders on the
//     receiving container's level: flow only while it holds less than X% of its capacity (VCE_HOSE_BELOW_PCT_INT,
//     0 = off, up to 100) and less than X mL (VCE_HOSE_BELOW_ML_INT, 0 = off, up to 5000); and two on the source's:
//     flow only while it holds more than X% (VCE_HOSE_ABOVE_PCT_INT) and more than X mL (VCE_HOSE_ABOVE_ML_INT), so it
//     keeps a reserve. All that are set must hold, and a second's flow stops right at the level. Last, a transfer
//     limit (VCE_HOSE_LIMIT_ML_INT, 0 = off, up to 5000 mL): the link stops once that much has left the source since
//     it was made (counted on the source in VCE_HOSE_MOVED_INT, uL). The settings go onto the link with the hose and
//     back with it; the count starts again with every new link.
//   - Dropped on a liquid container it becomes half a hose (vce_hose_half), one end on that container (its unique id
//     in VCE_HOSE_SOURCE_INT). Dropped back on the same container it is a whole hose again.
//   - The half hose dropped on a second liquid container disappears: the first container now feeds the second.
//     The source keeps the target's unique id (VCE_HOSE_OUT_INT) and the rate, the target the source's (VCE_HOSE_IN_INT).
//   - Every second (EmporiumEntry.OneSecPrefixFunction) each source pours up to its rate into its target the vanilla
//     way (WaterHelper.TransferLiquid, but limited): every liquid in proportion, through the target's filter, mixing
//     the temperature; it stops by itself when the target is full or the source empty. Both must be owned.
//   - Right-click either container > Detach (the Configure button, renamed) gives a whole hose back, with the rate it
//     had, and clears both ends. An end whose partner no longer points back (left over after the partner was
//     detached while it could not be found) clears without a hose, so no hose is ever duplicated.
// A container takes one hose at a time. Faucets and drains cannot be linked (they would be an endless water source).
// Given from the debug panel or, once, when a save carrying the mod data flag "vanilla_chem_ext:hose_test_kit" = "1"
// is loaded (two hoses, a full and an empty bottle). Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using HarmonyLib;

public static class Hose
{
    public const string ITEM_ID = "vce_hose";
    public const string SPRITE = "vce_hose";
    public const string SPRITE_HALF = "vce_hose_half";
    public const string RATE_TAG = "VCE_HOSE_RATE_INT";        // on the hose, and on a linked source
    public const string SOURCE_TAG = "VCE_HOSE_SOURCE_INT";    // on a half hose: the container its end is on
    public const string OUT_TAG = "VCE_HOSE_OUT_INT";          // on a source: the target's unique id
    public const string IN_TAG = "VCE_HOSE_IN_INT";            // on a target: the source's unique id
    public const string BELOW_PCT_TAG = "VCE_HOSE_BELOW_PCT_INT";  // on the hose, and on a linked source
    public const string BELOW_ML_TAG = "VCE_HOSE_BELOW_ML_INT";    // on the hose, and on a linked source
    public const string ABOVE_PCT_TAG = "VCE_HOSE_ABOVE_PCT_INT";  // on the hose, and on a linked source
    public const string ABOVE_ML_TAG = "VCE_HOSE_ABOVE_ML_INT";    // on the hose, and on a linked source
    public const string LIMIT_ML_TAG = "VCE_HOSE_LIMIT_ML_INT";    // on the hose, and on a linked source
    public const string MOVED_TAG = "VCE_HOSE_MOVED_INT";          // on a linked source: uL moved since linking
    public const int RATE_DEFAULT_ML = 5;
    public const int RATE_MIN_ML = 1;
    public const int RATE_MAX_ML = 100;
    public const int BELOW_PCT_MAX = 100;
    public const int BELOW_ML_MAX = 5000;
    public const long VALUE = 150;
    public const float REBUILD_SECONDS = 5f;                   // at most this often, look for an end not found

    public const string TEST_FLAG_KEY = "hose_test_kit";
    private const string WINDOW_ID = "vce_hose";
    private const string RATE_LABEL_ID = "rate";
    private const string PCT_LABEL_ID = "below_pct";
    private const string ML_LABEL_ID = "below_ml";
    private const string SRC_PCT_LABEL_ID = "above_pct";
    private const string SRC_ML_LABEL_ID = "above_ml";
    private const string LIMIT_LABEL_ID = "limit";
    private const float WINDOW_WIDTH = 460f;
    private const float WINDOW_HEIGHT = 170f;

    private static GameItem openHose;

    // Linked containers by unique id, and the sources among them; rebuilt on load and when an end goes missing.
    private static readonly Dictionary<int, GameItem> linked = new Dictionary<int, GameItem>();
    private static readonly List<GameItem> sources = new List<GameItem>();
    private static bool dirty = true;
    private static float lastRebuild = -100f;

    public static void Enable()
    {
        ModHook.OnCreateTooltipLate += OnCreateTooltip;
        ModHook.OnGameLoadedLate += OnGameLoaded;
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Hose: two flow hoses and two bottles", GiveTestKit, false);
        }
        catch (Exception e)
        {
            Log.Error("hose debug function registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnCreateTooltipLate -= OnCreateTooltip;
        ModHook.OnGameLoadedLate -= OnGameLoaded;
        CloseWindow();
        linked.Clear();
        sources.Clear();
        dirty = true;
    }

    public static void AddItems(ModItemDirectory directory)
    {
        directory.Add(ITEM_ID, Create);
    }

    public static bool IsHose(GameItem item)
    {
        return item != null && item.identifier == ITEM_ID;
    }

    public static bool IsHalf(GameItem hose)
    {
        return IsHose(hose) && hose.IsTag(SOURCE_TAG);
    }

    public static bool IsLinked(GameItem item)
    {
        return item != null && (item.IsTag(OUT_TAG) || item.IsTag(IN_TAG));
    }

    public static GameItem Create()
    {
        GameItem item = ItemDirectory.CreateEmptyItem(ITEM_ID);
        try
        {
            item.name = Text.Get("hose_name");
            item.shortDescription = Text.Get("hose_desc");
            item.flavorText = "";
            item.unitValue = VALUE;
            item.SetGameItemType("TOOL");
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            AudioHelper.InitSmallMachine(item);
            item.ModifyTag(RATE_TAG, delegate(TagState state) { state.Enable().SetInt(RATE_DEFAULT_ML); }, false);
            // Right-click > Configure opens the window (vanilla offers it while CONFIGURE_TAG is on and the function set).
            item.EnableTag(GeneralHelper.CONFIGURE_TAG, true);
            item.onConfigureSlotItemFunc = delegate(GameItem self, GameInventory inventory, SlotMarker marker)
            {
                OpenWindow(self);
            };
            item.onSlotAddItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(item.onSlotAddItemFunc,
                (Action<GameItem, GameInventory, SlotMarker>)delegate(GameItem self, GameInventory inventory, SlotMarker marker)
                {
                    self.EnableTag(GeneralHelper.CONFIGURE_TAG, true);
                });
            // Dropped on a liquid container: put an end on it, or finish the link.
            item.mayThisTargetItemFunc = (Func<GameItem, GameItem, bool>)Delegate.Combine(item.mayThisTargetItemFunc,
                (Func<GameItem, GameItem, bool>)((GameItem s, GameItem t) => CanDrop(s, t)));
            item.canThisTargetItemFunc = (Func<GameItem, GameItem, bool>)Delegate.Combine(item.canThisTargetItemFunc,
                (Func<GameItem, GameItem, bool>)((GameItem s, GameItem t) => CanDrop(s, t)));
            item.onThisTargetItemFunc = (Action<GameItem, GameItem>)Delegate.Combine(item.onThisTargetItemFunc,
                (Action<GameItem, GameItem>)delegate(GameItem s, GameItem t)
                {
                    if (CanDrop(s, t))
                    {
                        Drop(s, t);
                    }
                });
            item.onLoaded = (Action<GameItem>)Delegate.Combine(item.onLoaded, (Action<GameItem>)ApplySprite);
            ApplySprite(item);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            Log.Error("create " + ITEM_ID + " failed", e);
        }
        return item;
    }

    private static void ApplySprite(GameItem hose)
    {
        BeerSprites.Apply(hose, IsHalf(hose) ? SPRITE_HALF : SPRITE);
    }

    // mL a second, kept to RATE_MIN_ML..RATE_MAX_ML (an item saved without the tag gets the default).
    public static int Rate(GameItem item)
    {
        if (item == null || !item.IsTag(RATE_TAG))
        {
            return RATE_DEFAULT_ML;
        }
        return Math.Max(RATE_MIN_ML, Math.Min(RATE_MAX_ML, item.GetTagReadonly(RATE_TAG).GetInt()));
    }

    public static void SetRate(GameItem item, int ml)
    {
        int rate = Math.Max(RATE_MIN_ML, Math.Min(RATE_MAX_ML, ml));
        item.ModifyTag(RATE_TAG, delegate(TagState state) { state.Enable().SetInt(rate); }, false);
    }

    // The flow conditions on the receiving container's level, 0 = off.
    public static int BelowPct(GameItem item)
    {
        return Math.Max(0, Math.Min(BELOW_PCT_MAX, GetInt(item, BELOW_PCT_TAG)));
    }

    public static int BelowMl(GameItem item)
    {
        return Math.Max(0, Math.Min(BELOW_ML_MAX, GetInt(item, BELOW_ML_TAG)));
    }

    // The flow conditions on the source's level (a reserve it keeps), 0 = off.
    public static int AbovePct(GameItem item)
    {
        return Math.Max(0, Math.Min(BELOW_PCT_MAX, GetInt(item, ABOVE_PCT_TAG)));
    }

    public static int AboveMl(GameItem item)
    {
        return Math.Max(0, Math.Min(BELOW_ML_MAX, GetInt(item, ABOVE_ML_TAG)));
    }

    // The transfer limit in mL, 0 = off; and how much a linked source has given since the link was made, in uL.
    public static int LimitMl(GameItem item)
    {
        return Math.Max(0, Math.Min(BELOW_ML_MAX, GetInt(item, LIMIT_ML_TAG)));
    }

    public static int MovedUl(GameItem source)
    {
        return Math.Max(0, GetInt(source, MOVED_TAG));
    }

    // Rate and conditions from one item (hose or source) to another.
    private static void CopySettings(GameItem from, GameItem to)
    {
        SetRate(to, Rate(from));
        SetInt(to, BELOW_PCT_TAG, BelowPct(from));
        SetInt(to, BELOW_ML_TAG, BelowMl(from));
        SetInt(to, ABOVE_PCT_TAG, AbovePct(from));
        SetInt(to, ABOVE_ML_TAG, AboveMl(from));
        SetInt(to, LIMIT_ML_TAG, LimitMl(from));
    }

    private static void SetInt(GameItem item, string tag, int value)
    {
        item.ModifyTag(tag, delegate(TagState state) { state.Enable().SetInt(value); }, false);
    }

    private static int GetInt(GameItem item, string tag)
    {
        return item != null && item.IsTag(tag) ? item.GetTagReadonly(tag).GetInt() : 0;
    }

    // ---- linking -----------------------------------------------------------------------------------------------

    private static bool IsLinkable(GameItem container)
    {
        return container != null && container.IsTag("LIQUID_CONTAINER_TAG") && !IsHose(container)
            && !WaterHelper.IsFaucet(container) && !WaterHelper.IsDrain(container);
    }

    private static bool CanDrop(GameItem hose, GameItem target)
    {
        if (!IsHose(hose) || !IsLinkable(target) || target == hose || !GeneralHelper.IsItemOwned(hose, target))
        {
            return false;
        }
        if (IsHalf(hose) && target.GetUniqueID() == GetInt(hose, SOURCE_TAG))
        {
            return true;                                    // back on its own end: a whole hose again
        }
        return !IsLinked(target);
    }

    private static void Drop(GameItem hose, GameItem target)
    {
        try
        {
            int targetId = target.GetUniqueID();
            if (IsHalf(hose))
            {
                int sourceId = GetInt(hose, SOURCE_TAG);
                if (sourceId == targetId)
                {
                    hose.DisableTag(SOURCE_TAG, true);
                    ApplySprite(hose);
                    PlaySound();
                    return;
                }
                GameItem source = Find(sourceId, true);
                if (source != null && IsLinkable(source) && !IsLinked(source) && GeneralHelper.IsItemOwned(source))
                {
                    Link(source, target, hose);
                    if (openHose == hose)
                    {
                        CloseWindow();
                    }
                    hose.Destroy();
                    PlaySound();
                    return;
                }
                // The first end is gone (sold, used up, linked elsewhere): start again from this container.
            }
            SetInt(hose, SOURCE_TAG, targetId);
            ApplySprite(hose);
            PlaySound();
        }
        catch (Exception e)
        {
            Log.Error("dropping a hose on a container failed", e);
        }
    }

    private static void Link(GameItem source, GameItem target, GameItem hose)
    {
        int rate = Rate(hose);
        SetInt(source, OUT_TAG, target.GetUniqueID());
        CopySettings(hose, source);
        SetInt(source, MOVED_TAG, 0);
        SetInt(target, IN_TAG, source.GetUniqueID());
        linked[source.GetUniqueID()] = source;
        linked[target.GetUniqueID()] = target;
        if (!sources.Contains(source))
        {
            sources.Add(source);
        }
        Log.Info("hose: " + source.identifier + " now feeds " + target.identifier + " at " + rate + " mL/s");
    }

    // Right-click > Detach on either end: a whole hose back (with the link's rate), both ends cleared.
    public static void Detach(GameItem container)
    {
        try
        {
            if (!IsLinked(container))
            {
                return;
            }
            bool isSource = container.IsTag(OUT_TAG);
            GameItem partner = Find(GetInt(container, isSource ? OUT_TAG : IN_TAG), true);
            GameItem source = isSource ? container : partner;
            // The partner still points back, or is gone altogether: this link has a hose to give back. A partner that
            // points elsewhere (or nowhere) means the hose already came back from the other end.
            bool partnerPointsBack = partner != null && GetInt(partner, isSource ? IN_TAG : OUT_TAG) == container.GetUniqueID();
            bool giveHose = partner == null || partnerPointsBack;
            // The hose comes back with the link's settings (kept on the source); read them before clearing.
            GameItem hose = giveHose ? DirectoryMaster.Item(ITEM_ID, true) : null;
            if (hose != null && source != null)
            {
                CopySettings(source, hose);
            }
            Clear(container);
            if (partnerPointsBack)
            {
                Clear(partner);
            }
            if (hose != null)
            {
                GraphUtils.EmporiumTryAdd(container.parentInventory != null ? (GraphNodeStorage)container.parentInventory
                    : EmporiumEntry.Instance.invElement, hose);
            }
            PlaySound();
        }
        catch (Exception e)
        {
            Log.Error("detaching a hose failed", e);
        }
    }

    private static void Clear(GameItem container)
    {
        container.DisableTag(OUT_TAG, true);
        container.DisableTag(IN_TAG, true);
        container.DisableTag(RATE_TAG, true);
        container.DisableTag(BELOW_PCT_TAG, true);
        container.DisableTag(BELOW_ML_TAG, true);
        container.DisableTag(ABOVE_PCT_TAG, true);
        container.DisableTag(ABOVE_ML_TAG, true);
        container.DisableTag(LIMIT_ML_TAG, true);
        container.DisableTag(MOVED_TAG, true);
        sources.Remove(container);
        linked.Remove(container.GetUniqueID());
    }

    private static void PlaySound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play3("scanner_activate");
        }
    }

    // ---- finding the ends --------------------------------------------------------------------------------------

    private static void OnGameLoaded()
    {
        dirty = true;
        lastRebuild = -100f;
        Rebuild();
        try
        {
            if (ModHelper.GetModData(DreamToxinMod.MOD_ID, TEST_FLAG_KEY, null) == AntibioticTestKit.FLAG_PENDING)
            {
                ModHelper.SetModData(DreamToxinMod.MOD_ID, TEST_FLAG_KEY, AntibioticTestKit.FLAG_GIVEN);
                Log.Info("this save asked for the hose test kit");
                GiveTestKit();
            }
        }
        catch (Exception e)
        {
            Log.Error("giving the hose test kit on load failed", e);
        }
    }

    // Every linked container in the shop (and the item any half hose points at is found the same way).
    private static void Rebuild()
    {
        if (PlayerStore.Instance == null || EmporiumEntry.Instance == null)
        {
            return;
        }
        lastRebuild = UnityEngine.Time.realtimeSinceStartup;
        dirty = false;
        linked.Clear();
        sources.Clear();
        foreach (GameItem item in PlayerStore.Instance.FindAllItem(true))
        {
            if (item == null || item.uniqueId == 0)
            {
                continue;
            }
            if (IsLinked(item))
            {
                linked[item.uniqueId] = item;
                if (item.IsTag(OUT_TAG))
                {
                    sources.Add(item);
                }
            }
        }
    }

    // A linked container (or, with scan, any owned item in the shop) by unique id; null if it is not in the shop.
    private static GameItem Find(int uniqueId, bool scan)
    {
        if (uniqueId == 0)
        {
            return null;
        }
        GameItem item;
        if (linked.TryGetValue(uniqueId, out item) && item != null && item.uniqueId == uniqueId && item.parentInventory != null)
        {
            return item;
        }
        if (!scan || PlayerStore.Instance == null || EmporiumEntry.Instance == null)
        {
            return null;
        }
        foreach (GameItem candidate in PlayerStore.Instance.FindAllItem(true))
        {
            if (candidate != null && candidate.uniqueId == uniqueId)
            {
                return candidate;
            }
        }
        return null;
    }

    // ---- the flow ----------------------------------------------------------------------------------------------

    public static void Tick()
    {
        try
        {
            if (dirty && UnityEngine.Time.realtimeSinceStartup - lastRebuild >= REBUILD_SECONDS)
            {
                Rebuild();
            }
            if (sources.Count == 0)
            {
                return;
            }
            foreach (GameItem source in sources.ToArray())
            {
                if (source == null || !source.IsTag(OUT_TAG))
                {
                    sources.Remove(source);
                    continue;
                }
                if (source.parentInventory == null)
                {
                    dirty = true;                           // being dragged, or gone: look again shortly
                    continue;
                }
                GameItem target = Find(GetInt(source, OUT_TAG), false);
                if (target == null)
                {
                    dirty = true;
                    continue;
                }
                if (!GeneralHelper.IsItemOwned(source, target))
                {
                    continue;
                }
                int allowed = Math.Min(Rate(source) * 1000, Headroom(source, target));
                int limit = LimitMl(source);
                if (limit > 0)
                {
                    allowed = Math.Min(allowed, limit * 1000 - MovedUl(source));
                }
                if (allowed > 0)
                {
                    int moved = Flow(source, target, allowed);
                    if (moved > 0)
                    {
                        SetInt(source, MOVED_TAG, (int)Math.Min(int.MaxValue, (long)MovedUl(source) + moved));
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("hose flow failed", e);
        }
    }

    // How much the flow conditions still let through, in uL (int.MaxValue with none set): the room left in the target
    // under X% of its capacity and under X mL, and what the source holds over its reserve of X% and X mL, whichever is
    // smallest.
    private static int Headroom(GameItem source, GameItem target)
    {
        long room = int.MaxValue;
        long inTarget = WaterHelper.GetCurrentCapacityUL(target);
        int pct = BelowPct(source);
        if (pct > 0)
        {
            room = Math.Min(room, (long)WaterHelper.GetTotalCapacity(target) * pct / 100 - inTarget);
        }
        int ml = BelowMl(source);
        if (ml > 0)
        {
            room = Math.Min(room, (long)ml * 1000 - inTarget);
        }
        long inSource = WaterHelper.GetCurrentCapacityUL(source);
        int keepPct = AbovePct(source);
        if (keepPct > 0)
        {
            room = Math.Min(room, inSource - (long)WaterHelper.GetTotalCapacity(source) * keepPct / 100);
        }
        int keepMl = AboveMl(source);
        if (keepMl > 0)
        {
            room = Math.Min(room, inSource - (long)keepMl * 1000);
        }
        return (int)Math.Max(0, room);
    }

    // Vanilla's pour (WaterHelper.TransferLiquid) limited to maxUl: every liquid in proportion, through the target's
    // filter, the temperature mixed, the water features updated. Returns how much left the source, in uL.
    private static int Flow(GameItem source, GameItem target, int maxUl)
    {
        int free = WaterHelper.GetFreeCapacity(target);
        if (free <= 0)
        {
            return 0;
        }
        int[] parts = WaterHelper.GetLiquidParts(source);
        int total = 0;
        foreach (int part in parts)
        {
            total += part;
        }
        int amount = Math.Min(maxUl, Math.Min(free, total));
        if (amount <= 0)
        {
            return 0;
        }
        bool wasEmpty = WaterHelper.IsEmpty(target);
        GameItem filter = WaterHelper.GetFilterItem(target);
        int[] split = WaterHelper.SplitProportionally(parts, amount);
        for (int i = 0; i < Liquid.Liquids.Count && i < split.Length; i++)
        {
            int take = split[i];
            if (take <= 0)
            {
                continue;
            }
            string tag = Liquid.Liquids[i].currentPart;
            int give = WaterHelper.HandleFilter(Liquid.Liquids[i].identifier, take, source, filter);
            int have = target.GetTagReadonly(tag).GetInt();
            int left = source.GetTagReadonly(tag).GetInt() - take;
            ChemicalHelper.HandleReceiveLiquidTemp(target, give, source);
            target.ModifyTag(tag, delegate(TagState state) { state.Enable().SetInt(have + give); }, true);
            source.ModifyTag(tag, delegate(TagState state) { state.Enable().SetInt(left); }, true);
        }
        WaterFeatureHelper.OnLiquidContainerPourInto(target, source, wasEmpty);
        WaterFeatureHelper.OnLiquidContainerPourOutOf(source);
        return amount;
    }

    // ---- tooltips ----------------------------------------------------------------------------------------------

    private static string RateText(GameItem item)
    {
        return Text.Get("hose_rate", Rate(item));
    }

    private static string PctText(GameItem item)
    {
        int pct = BelowPct(item);
        return pct > 0 ? Text.Get("hose_below_pct", pct) : Text.Get("hose_below_pct_off");
    }

    private static string MlText(GameItem item)
    {
        int ml = BelowMl(item);
        return ml > 0 ? Text.Get("hose_below_ml", ml) : Text.Get("hose_below_ml_off");
    }

    private static string SrcPctText(GameItem item)
    {
        int pct = AbovePct(item);
        return pct > 0 ? Text.Get("hose_above_pct", pct) : Text.Get("hose_above_pct_off");
    }

    private static string SrcMlText(GameItem item)
    {
        int ml = AboveMl(item);
        return ml > 0 ? Text.Get("hose_above_ml", ml) : Text.Get("hose_above_ml_off");
    }

    // The conditions that are set, one line each (none when all are off).
    private static void AddConditionLines(RichTextBuilder builder, GameItem item)
    {
        if (BelowPct(item) > 0)
        {
            builder.AddLine(PctText(item), true, RenderHandler.ColorPalette.LightCyan);
        }
        if (BelowMl(item) > 0)
        {
            builder.AddLine(MlText(item), true, RenderHandler.ColorPalette.LightCyan);
        }
        if (AbovePct(item) > 0)
        {
            builder.AddLine(SrcPctText(item), true, RenderHandler.ColorPalette.LightCyan);
        }
        if (AboveMl(item) > 0)
        {
            builder.AddLine(SrcMlText(item), true, RenderHandler.ColorPalette.LightCyan);
        }
        int limit = LimitMl(item);
        if (limit > 0)
        {
            // On a linked source, how far along it is; on a hose, just the limit.
            string line = item.IsTag(OUT_TAG) ? Text.Get("hose_moved", MovedUl(item) / 1000, limit) : LimitText(item);
            builder.AddLine(line, true, RenderHandler.ColorPalette.LightCyan);
        }
    }

    private static string LimitText(GameItem item)
    {
        int limit = LimitMl(item);
        return limit > 0 ? Text.Get("hose_limit", limit) : Text.Get("hose_limit_off");
    }

    private static string NameOf(GameItem item)
    {
        return item != null ? item.GetDisplayName(true) : Text.Get("hose_end_missing");
    }

    public static void OnCreateTooltip(RichTextBuilder builder, GameItem item)
    {
        if (item == null)
        {
            return;
        }
        if (IsHose(item))
        {
            builder.AddLine(" ");
            builder.AddLine(RateText(item), true, RenderHandler.ColorPalette.LightCyan);
            AddConditionLines(builder, item);
            if (IsHalf(item))
            {
                builder.AddLine(Text.Get("hose_half_hint", NameOf(Find(GetInt(item, SOURCE_TAG), true))), true,
                    RenderHandler.ColorPalette.LightCyan);
            }
            return;
        }
        if (item.IsTag(OUT_TAG))
        {
            builder.AddLine(" ");
            builder.AddLine(Text.Get("hose_feeds", NameOf(Find(GetInt(item, OUT_TAG), true)), Rate(item)), true,
                RenderHandler.ColorPalette.LightCyan);
            AddConditionLines(builder, item);
        }
        else if (item.IsTag(IN_TAG))
        {
            builder.AddLine(" ");
            builder.AddLine(Text.Get("hose_fed_by", NameOf(Find(GetInt(item, IN_TAG), true))), true,
                RenderHandler.ColorPalette.LightCyan);
        }
    }

    // ---- the window --------------------------------------------------------------------------------------------

    private static CustomUIWindow Window()
    {
        CustomUIWindow window = CustomUIManager.Instance != null ? CustomUIManager.Instance.GetWindow(WINDOW_ID) : null;
        return window != null && window.IsAlive ? window : null;
    }

    private static void CloseWindow()
    {
        try
        {
            CustomUIWindow window = Window();
            if (window != null)
            {
                window.Close();
            }
        }
        catch
        {
        }
        openHose = null;
    }

    // Opens the window for this hose; for the hose already open it closes it again (like vanilla's hopper windows).
    public static void OpenWindow(GameItem hose)
    {
        try
        {
            if (CustomUIManager.Instance == null || hose == null)
            {
                return;
            }
            CustomUIWindow old = Window();
            if (old != null && openHose == hose)
            {
                old.Close();
                return;
            }
            UnityEngine.Vector2? position = null;
            if (old != null)
            {
                position = old.Rect.anchoredPosition;
                old.Close();
            }
            openHose = hose;
            CustomUIBuilder builder = CustomUIManager.Instance.CreateWindow(WINDOW_ID, hose.name, "paper", null, CustomUIManager.Style.Machinery)
                .SetSize(WINDOW_WIDTH, WINDOW_HEIGHT)
                .SetDraggable(true)
                .SetCloseOnEscape(true)
                .OnClose(delegate
                {
                    if (openHose != null && AudioManager.Instance != null)
                    {
                        AudioManager.Instance.TryPlayOne(openHose.soundContainerClose);
                    }
                    openHose = null;
                });
            builder.AddLabel(RateText(hose), RATE_LABEL_ID);
            builder.AddSlider(RATE_MIN_ML, RATE_MAX_ML, Rate(hose), delegate(float value)
            {
                SetRate(hose, (int)Math.Round(value));
                CustomUIWindow window = Window();
                CustomUIElement label = window != null ? window.Get(RATE_LABEL_ID) : null;
                if (label != null)
                {
                    label.SetText(RateText(hose));
                }
            }, true, null);
            // Flow conditions on the receiving container's level; 0 (far left) is off.
            builder.AddSeparator();
            builder.AddHeader(Text.Get("hose_conditions"));
            builder.AddLabel(PctText(hose), PCT_LABEL_ID);
            builder.AddSlider(0, BELOW_PCT_MAX, BelowPct(hose), delegate(float value)
            {
                SetInt(hose, BELOW_PCT_TAG, Math.Max(0, Math.Min(BELOW_PCT_MAX, (int)Math.Round(value))));
                SetLabel(PCT_LABEL_ID, PctText(hose));
            }, true, null);
            builder.AddLabel(MlText(hose), ML_LABEL_ID);
            builder.AddSlider(0, BELOW_ML_MAX, BelowMl(hose), delegate(float value)
            {
                SetInt(hose, BELOW_ML_TAG, Math.Max(0, Math.Min(BELOW_ML_MAX, (int)Math.Round(value))));
                SetLabel(ML_LABEL_ID, MlText(hose));
            }, true, null);
            // ... and on the source's level: the reserve it keeps.
            builder.AddLabel(SrcPctText(hose), SRC_PCT_LABEL_ID);
            builder.AddSlider(0, BELOW_PCT_MAX, AbovePct(hose), delegate(float value)
            {
                SetInt(hose, ABOVE_PCT_TAG, Math.Max(0, Math.Min(BELOW_PCT_MAX, (int)Math.Round(value))));
                SetLabel(SRC_PCT_LABEL_ID, SrcPctText(hose));
            }, true, null);
            builder.AddLabel(SrcMlText(hose), SRC_ML_LABEL_ID);
            builder.AddSlider(0, BELOW_ML_MAX, AboveMl(hose), delegate(float value)
            {
                SetInt(hose, ABOVE_ML_TAG, Math.Max(0, Math.Min(BELOW_ML_MAX, (int)Math.Round(value))));
                SetLabel(SRC_ML_LABEL_ID, SrcMlText(hose));
            }, true, null);
            // ... and the most one link may move.
            builder.AddLabel(LimitText(hose), LIMIT_LABEL_ID);
            builder.AddSlider(0, BELOW_ML_MAX, LimitMl(hose), delegate(float value)
            {
                SetInt(hose, LIMIT_ML_TAG, Math.Max(0, Math.Min(BELOW_ML_MAX, (int)Math.Round(value))));
                SetLabel(LIMIT_LABEL_ID, LimitText(hose));
            }, true, null);
            if (position.HasValue)
            {
                builder.SetPosition(position.Value);
            }
            else
            {
                builder.Center();
            }
            CustomUIWindow shown = builder.Show();
            FitHeight(shown);
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.TryPlayOne(hose.soundContainerOpen);
            }
        }
        catch (Exception e)
        {
            Log.Error("opening the hose window failed", e);
        }
    }

    private static void SetLabel(string id, string text)
    {
        CustomUIWindow window = Window();
        CustomUIElement label = window != null ? window.Get(id) : null;
        if (label != null)
        {
            label.SetText(text);
        }
    }

    // Grow or shrink the window to its content, keeping its top edge where it is (vanilla HopperHelper.FitWindowHeight).
    private static void FitHeight(CustomUIWindow window)
    {
        if (window == null || window.Rect == null || window.frame == null || window.frame.contentRoot == null)
        {
            return;
        }
        UnityEngine.RectTransform content = window.frame.contentRoot;
        float height = window.Rect.rect.height;
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        float chrome = height - content.rect.height;
        float wanted = UnityEngine.Mathf.Ceil(UnityEngine.UI.LayoutUtility.GetPreferredHeight(content) + chrome);
        if (UnityEngine.Mathf.Approximately(wanted, height))
        {
            return;
        }
        window.SetSize(WINDOW_WIDTH, wanted);
        UnityEngine.Vector2 position = window.Rect.anchoredPosition;
        position.y += (1f - window.Rect.pivot.y) * (height - wanted);
        window.Rect.anchoredPosition = position;
    }

    // ---- testing -----------------------------------------------------------------------------------------------

    // Two hoses, a full bottle of water and an empty one.
    public static void GiveTestKit()
    {
        for (int i = 0; i < 2; i++)
        {
            Give(DirectoryMaster.Item(ITEM_ID, true), "hose");
        }
        Give(DirectoryMaster.Item("bottled_water", true), "full bottle");
        GameItem empty = DirectoryMaster.Item("bottled_water", true);
        if (empty != null)
        {
            WaterHelper.EmptyContainer(empty);
        }
        Give(empty, "empty bottle");
    }

    private static void Give(GameItem item, string what)
    {
        if (item == null)
        {
            return;
        }
        GraphUtils.EmporiumTryAdd(item);
        if (item.parentInventory == null)
        {
            Log.Info("hose test kit: no room for the " + what);
        }
    }
}

// The flow runs once a second, just before the shop's items tick.
[HarmonyPatch(typeof(EmporiumEntry), "OneSecPrefixFunction")]
public static class HoseTickPatch
{
    private static void Postfix()
    {
        Hose.Tick();
    }
}

// A linked container offers the Configure button, renamed Detach, which takes the hose off.
[HarmonyPatch(typeof(GameItem), "MayConfigureSlotItem")]
public static class HoseMayDetachPatch
{
    private static bool Prefix(GameItem __instance, ref bool __result)
    {
        if (Hose.IsLinked(__instance) && __instance.parentInventory != null)
        {
            __result = true;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(GameItem), "ConfigureSlotItem")]
public static class HoseDetachPatch
{
    private static bool Prefix(GameItem __instance)
    {
        if (Hose.IsLinked(__instance))
        {
            Hose.Detach(__instance);
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(ItemContextHandler), "SetButtonColor")]
public static class HoseDetachLabelPatch
{
    private static void Postfix(RichTextElement button, RichTextElement selectedButton, bool isEnabled,
        RichTextElement ___buttonConfigure, GameItem ___currentItem,
        RenderHandler.ColorPalette ___textColor, RenderHandler.ColorPalette ___textDisabledColor,
        RenderHandler.ColorPalette ___textSelectColor, RenderHandler.ColorPalette ___textSelectDisabledColor)
    {
        if (button == null || button != ___buttonConfigure || !Hose.IsLinked(___currentItem))
        {
            return;
        }
        bool selected = button == selectedButton;
        RenderHandler.ColorPalette color = !isEnabled
            ? (selected ? ___textSelectDisabledColor : ___textDisabledColor)
            : (selected ? ___textSelectColor : ___textColor);
        button.SetText(Text.Get("hose_detach"), 10000, color);
    }
}
