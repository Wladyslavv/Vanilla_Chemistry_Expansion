// Separatory funnel (docs/SEP_FUNNEL_DESIGN.md): a 2x4, 5 L liquid container that also has a window. Pour into it and
// out of it like any container; double-click it to drain its lower layer into a container in its receiver slot.
//   - Oil phase (upper, stays in the funnel): fat, toluene, ethyl acetate, Lantern Ester.
//     Aqueous phase (lower, drains out): water, alcohol, methanol, acetic acid.
//     Both phases must be present for the liquid to separate.
//   - Every other liquid is a solute and is split between the phases by their volumes; particles settle and leave
//     with the first drain.
//   - Two drain modes: "stop before the interface" (out: pure aqueous phase; the funnel keeps the oil phase and
//     EXTRA of the aqueous phase) and "drain past the interface" (the funnel keeps the pure oil phase; out: the
//     aqueous phase and EXTRA of the oil phase). Solutes go with their phase in the same proportions.
//   - The receiver's free room limits a drain; everything leaving is scaled down together.
// Window, left to right: phases (volumes and solutes) | drain (two mode buttons, receiver slot, drain button, status).
// Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using TMPro;

public static class SepFunnel
{
    public const string ITEM_ID = "vce_sep_funnel";
    public const string SPRITE = "vce_sep_funnel";
    public const int CAPACITY_ML = 5000;
    public const int BASE_PRICE = 40;           // vanilla's container price field
    public const long VALUE = 200;                         // the Cartel Vendor's price (CartelVendor.cs)
    // "A little" of the other phase: what one drain leaves behind or takes along.
    public const double EXTRA = 0.05;
    public const string MODE_TAG = "VCE_SEP_FUNNEL_MODE_INT";   // 0 = stop before the interface, 1 = drain past it

    private static readonly string[] OilPhase = { "fat", FlavorChemistry.TOLUENE, FlavorChemistry.ETHYL_ACETATE, Lantern.ESTER_ID };
    private static readonly string[] AqueousPhase = { "water", "alcohol", IaLiquids.METHANOL, FlavorChemistry.ACETIC_ACID };

    private class Ui
    {
        public GameSlotInventory Receiver;
        public MachineryText Phases;
        public MachineryText Solutes;
        public MachineryButton Before;
        public MachineryButton Past;
        public MachineryText Hint;
        public MachineryText Status;
        public string LastStatus = "";
    }

    // Save flag "vanilla_chem_ext:sep_funnel_test_kit" = "1": the test kit is given once on load.
    public const string TEST_FLAG_KEY = "sep_funnel_test_kit";

    public static void Enable()
    {
        ModHook.OnGameLoadedLate += OnGameLoaded;
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Separatory funnel: spawn one", SpawnOne, false);
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Separatory funnel: test kit", GiveTestKit, false);
        }
        catch (Exception e)
        {
            Log.Error("separatory funnel debug registration failed", e);
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
            if (ModHelper.GetModData(DreamToxinMod.MOD_ID, TEST_FLAG_KEY, null) == AntibioticTestKit.FLAG_PENDING)
            {
                ModHelper.SetModData(DreamToxinMod.MOD_ID, TEST_FLAG_KEY, AntibioticTestKit.FLAG_GIVEN);
                Log.Info("this save asked for the separatory funnel test kit");
                GiveTestKit();
            }
        }
        catch (Exception e)
        {
            Log.Error("separatory funnel test kit on load failed", e);
        }
    }

    public static void AddItems(ModItemDirectory directory)
    {
        directory.Add(ITEM_ID, Create);
    }

    public static bool IsFunnel(GameItem item)
    {
        return item != null && item.identifier == ITEM_ID;
    }

    public static GameItem Create()
    {
        Ui ui = new Ui();
        ui.Receiver = new GameSlotInventory();
        ui.Phases = new MachineryText("", 6, HorizontalAlignmentOptions.Left, false);
        ui.Solutes = new MachineryText("", 6, HorizontalAlignmentOptions.Left, false);
        ui.Before = new MachineryButton("");
        ui.Past = new MachineryButton("");
        ui.Hint = new MachineryText("", 6, HorizontalAlignmentOptions.Center, false);
        ui.Status = new MachineryText("", 6, HorizontalAlignmentOptions.Center, false);
        MachineryButton drain = new MachineryButton(Text.Get("sep_funnel_btn_drain"));
        ui.Before.SetSize(130);
        ui.Past.SetSize(130);
        drain.SetSize(130);

        PixelWindow window = MachineHelper.CreateMachineryWindow(true,
            MachineHelper.Panel(Text.Get("sep_funnel_label_phases"), MachineryStack.Vertical(ui.Phases, ui.Solutes), "phases", true),
            MachineHelper.Panel(Text.Get("sep_funnel_label_drain"),
                MachineryStack.Vertical(ui.Before, ui.Past, ui.Hint, MachineHelper.WrapInIntake(ui.Receiver, true), drain, ui.Status),
                "drain", true));

        GameItem item = ItemDirectory.CreateEmptyItem(ITEM_ID).SetContentWindow(window);
        try
        {
            item.name = Text.Get("sep_funnel_name");
            item.shortDescription = Text.Get("sep_funnel_desc");
            item.flavorText = "";
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            item.SetGameItemType("TOOL");
            WaterHelper.InitLiquidContainerItem(item, CAPACITY_ML, BASE_PRICE, false, true);
            item.unitValue = VALUE;
            AudioHelper.InitGlass(item);
            BeerSprites.Apply(item, SPRITE);
            item.getItemSpriteModifierForItemFunc = (GameItem self) => SpriteLayers(self);

            ui.Receiver.mayInventoryAddItemFunc = (GameItem other, GameInventory _) =>
                GeneralHelper.IsItemOwned(other) && other.IsTag("LIQUID_CONTAINER_TAG") && !IsFunnel(other);
            ui.Receiver.SetBackgroundFadeSprite("Items/water_container4", "xl");
            ui.Receiver.slotPriorityFunc = (GameItem _, GameInventory __, SlotMarker ___) => 4;

            ui.Before.SetCallback(delegate { SetMode(item, 0); Refresh(item, ui); });
            ui.Past.SetCallback(delegate { SetMode(item, 1); Refresh(item, ui); });
            drain.SetCallback(delegate
            {
                ui.LastStatus = Drain(item, ui.Receiver.childItem);
                UpdateLook(item, true);
                Refresh(item, ui);
            });

            // Keep the container's own temperature tick and refresh the window after it.
            Action<GameItem, GameInventory, SlotMarker> containerTick = item.onOneSecondItemFunc;
            item.onOneSecondItemFunc = delegate(GameItem self, GameInventory inventory, SlotMarker marker)
            {
                if (containerTick != null)
                {
                    containerTick(self, inventory, marker);
                }
                UpdateLook(self, false);
                Refresh(self, ui);
            };
            Refresh(item, ui);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            Log.Error("create " + ITEM_ID + " failed", e);
        }
        return item;
    }

    // ---- state ----------------------------------------------------------------------------------------------------

    private static int Mode(GameItem item)
    {
        return item.state != null && item.state.IsTag(MODE_TAG) && item.state.GetTag(MODE_TAG).GetInt() == 1 ? 1 : 0;
    }

    private static void SetMode(GameItem item, int mode)
    {
        item.ModifyTag(MODE_TAG, delegate(TagState state) { state.Enable().SetInt(mode); }, true);
    }

    private enum Phase { Oil, Aqueous, Particle, Solute }

    private static Phase PhaseOf(string id)
    {
        if (Array.IndexOf(OilPhase, id) >= 0)
        {
            return Phase.Oil;
        }
        if (Array.IndexOf(AqueousPhase, id) >= 0)
        {
            return Phase.Aqueous;
        }
        return Polarity.KindOf(id) == Polarity.Kind.Particle ? Phase.Particle : Phase.Solute;
    }

    private static void Totals(GameItem item, out long oil, out long aqueous)
    {
        oil = 0;
        aqueous = 0;
        foreach (Liquid liquid in Liquid.Liquids)
        {
            int amount = IaLiquids.Get(item, liquid.identifier);
            if (amount <= 0)
            {
                continue;
            }
            Phase phase = PhaseOf(liquid.identifier);
            if (phase == Phase.Oil)
            {
                oil += amount;
            }
            else if (phase == Phase.Aqueous)
            {
                aqueous += amount;
            }
        }
    }

    // ---- draining -------------------------------------------------------------------------------------------------

    // Drains the lower layer into the receiver; returns the status text key's text.
    private static string Drain(GameItem funnel, GameItem receiver)
    {
        try
        {
            if (receiver == null)
            {
                return Text.Get("sep_funnel_status_no_receiver");
            }
            long oil, aqueous;
            Totals(funnel, out oil, out aqueous);
            if (oil <= 0 || aqueous <= 0)
            {
                return Text.Get("sep_funnel_status_one_phase");
            }
            bool past = Mode(funnel) == 1;
            double oilShare = (double)oil / (oil + aqueous);
            // Fraction of each phase that leaves: before the interface 95% of the aqueous phase and none of the oil;
            // past it all of the aqueous phase and 5% of the oil.
            double aqueousOut = past ? 1.0 : 1.0 - EXTRA;
            double oilOut = past ? EXTRA : 0.0;

            Dictionary<string, int> plan = new Dictionary<string, int>();
            long planned = 0;
            foreach (Liquid liquid in Liquid.Liquids)
            {
                int amount = IaLiquids.Get(funnel, liquid.identifier);
                if (amount <= 0)
                {
                    continue;
                }
                double fraction;
                switch (PhaseOf(liquid.identifier))
                {
                    case Phase.Oil: fraction = oilOut; break;
                    case Phase.Aqueous: fraction = aqueousOut; break;
                    case Phase.Particle: fraction = 1.0; break;   // settled at the bottom, out with the first drain
                    default: fraction = oilShare * oilOut + (1.0 - oilShare) * aqueousOut; break;
                }
                int take = (int)Math.Round(amount * fraction);
                if (take > 0)
                {
                    plan[liquid.identifier] = take;
                    planned += take;
                }
            }
            if (planned <= 0)
            {
                return Text.Get("sep_funnel_status_nothing");
            }
            int room = WaterHelper.GetFreeCapacity(receiver);
            if (room <= 0)
            {
                return Text.Get("sep_funnel_status_full");
            }
            double scale = Math.Min(1.0, (double)room / planned);
            double temp = funnel.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
            int moved = 0;
            foreach (KeyValuePair<string, int> entry in plan)
            {
                int take = (int)Math.Floor(entry.Value * scale);
                if (take <= 0)
                {
                    continue;
                }
                IaLiquids.Set(funnel, entry.Key, IaLiquids.Get(funnel, entry.Key) - take);
                ChemicalHelper.HandleReceiveLiquidTemp(receiver, take, temp);
                WaterHelper.AddLiquid(receiver, entry.Key, take);
                moved += take;
            }
            WaterFeatureHelper.OnLiquidContainerPourOutOf(funnel);
            ItemFeature feature = WaterFeatureHelper.GetWaterFeature(receiver);
            WaterFeatureHelper.OnLiquidChanged(receiver, feature != null && feature.IsExposed());
            SpriteHelper.UpdateWaterContainerSprite(receiver);
            AudioHelper.PourWater();
            string text = Text.Get("sep_funnel_status_drained", Ml(moved));
            return scale < 1.0 ? text + "\n" + Text.Get("sep_funnel_status_receiver_full") : text;
        }
        catch (Exception e)
        {
            Log.Error("separatory funnel drain failed", e);
            return "";
        }
    }

    // ---- window ---------------------------------------------------------------------------------------------------

    private static void Refresh(GameItem item, Ui ui)
    {
        try
        {
            long oil, aqueous;
            Totals(item, out oil, out aqueous);
            long total = WaterHelper.GetTotalVolume(item);
            // Each phase with the liquids it is made of, then the total.
            List<string> phaseLines = new List<string>();
            phaseLines.Add(Text.Get("sep_funnel_phase_oil", Ml(oil)));
            phaseLines.AddRange(PhaseMembers(item, OilPhase));
            phaseLines.Add(Text.Get("sep_funnel_phase_aqueous", Ml(aqueous)));
            phaseLines.AddRange(PhaseMembers(item, AqueousPhase));
            phaseLines.Add(Text.Get("sep_funnel_phase_total", Ml(total), CAPACITY_ML));
            ui.Phases.SetText(string.Join("\n", phaseLines.ToArray()));

            List<string> lines = new List<string>();
            double oilShare = oil + aqueous > 0 ? (double)oil / (oil + aqueous) : 0.0;
            foreach (Liquid liquid in Liquid.Liquids)
            {
                int amount = IaLiquids.Get(item, liquid.identifier);
                Phase phase = PhaseOf(liquid.identifier);
                if (amount <= 0 || phase == Phase.Oil || phase == Phase.Aqueous)
                {
                    continue;
                }
                if (lines.Count >= 6)
                {
                    lines.Add(Text.Get("sep_funnel_solute_more"));
                    break;
                }
                lines.Add(phase == Phase.Particle
                    ? Text.Get("sep_funnel_particle_row", liquid.displayName, Ml(amount))
                    : Text.Get("sep_funnel_solute_row", liquid.displayName, Ml((long)Math.Round(amount * oilShare)), Ml((long)Math.Round(amount * (1 - oilShare)))));
            }
            ui.Solutes.SetText(lines.Count > 0
                ? Text.Get("sep_funnel_solute_head") + "\n" + string.Join("\n", lines.ToArray())
                : Text.Get("sep_funnel_solute_none"));

            int mode = Mode(item);
            ui.Before.SetText((mode == 0 ? "> " : "") + Text.Get("sep_funnel_mode_before"));
            ui.Past.SetText((mode == 1 ? "> " : "") + Text.Get("sep_funnel_mode_past"));
            ui.Hint.SetText(Text.Get(mode == 0 ? "sep_funnel_hint_before" : "sep_funnel_hint_past"));
            string status = ui.LastStatus;
            if (string.IsNullOrEmpty(status))
            {
                status = oil > 0 && aqueous > 0 ? Text.Get("sep_funnel_status_ready") : Text.Get("sep_funnel_status_one_phase");
            }
            ui.Status.SetText(status);
        }
        catch (Exception e)
        {
            Log.Error("separatory funnel refresh failed", e);
        }
    }

    // "  Toluene: 600 mL" for each liquid of the phase that is in the funnel; "  (none)" if none is.
    private static List<string> PhaseMembers(GameItem item, string[] phase)
    {
        List<string> lines = new List<string>();
        foreach (string id in phase)
        {
            int amount = IaLiquids.Get(item, id);
            Liquid liquid = Liquid.GetLiquidFromId(id);
            if (amount > 0 && liquid != null)
            {
                lines.Add(Text.Get("sep_funnel_phase_item", liquid.displayName, Ml(amount)));
            }
        }
        if (lines.Count == 0)
        {
            lines.Add(Text.Get("sep_funnel_phase_none"));
        }
        return lines;
    }

    private static string Ml(long ul)
    {
        return (ul / 1000.0).ToString("0.#");
    }

    // ---- the liquid in the picture --------------------------------------------------------------------------------
    // The sprite's glass is empty; the liquid is an innate sprite layer drawn over it, built here from the same bulb
    // shape as art/glassware/draw_sep_funnel.py (radius per row). Fresh liquid shows one mixed colour; when it holds
    // both phases it settles into an upper oil layer and a lower aqueous layer over SETTLE_SECONDS.
    public const int SETTLE_SECONDS = 5;
    private const int SPRITE_W = 32, SPRITE_H = 64;
    private const float AXIS = 19.5f;
    private const int LIQ_TOP = 10, LIQ_BOT = 36;           // bulb and cone rows that show liquid
    private const int CHANGE_UL = 1000;                     // a change bigger than this stirs the funnel up again

    private class Look
    {
        public long Oil = -1, Aqueous = -1, Total = -1;
        public int Settle = SETTLE_SECONDS;                 // seconds settled, up to SETTLE_SECONDS
        public string Key;
        public UnityEngine.Sprite Sprite;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameItem, Look> looks =
        new System.Runtime.CompilerServices.ConditionalWeakTable<GameItem, Look>();
    private static readonly Dictionary<string, UnityEngine.Sprite> spriteCache = new Dictionary<string, UnityEngine.Sprite>();

    private static float Radius(int y)
    {
        if (y <= 9) return 2.5f;
        if (y <= 14) return 2.5f + 7.5f * (float)Math.Sin((y - 9) / 5.0 * Math.PI / 2);
        if (y <= 22) return 10f;
        return 10f - 8.5f * (y - 22) / 14f;
    }

    // Liquid columns of a row: inside the outline, leaving the left rim highlight.
    private static void Columns(int y, out int from, out int to)
    {
        float r = Radius(y);
        int l = (int)Math.Round(AXIS - r);
        int rr = (int)Math.Round(AXIS + r - 1);
        from = l + 2;
        to = rr - 1;
    }

    // Round things are seen from slightly above, like vanilla items: a liquid surface is an ellipse this many times
    // as tall as it is wide (draw_sep_funnel.py: TILT).
    private const float TILT = 0.3f;
    private const float RING_CY = 28f, RING_A = 8f, RING_B = 2.6f;  // the clamp ring (draw_sep_funnel.py)

    private static bool InEllipse(int x, int y, float cx, float cy, float a, float b)
    {
        float dx = (x + 0.5f - cx) / a, dy = (y + 0.5f - cy) / b;
        return dx * dx + dy * dy <= 1f;
    }

    // The front half of the clamp ring: the outline of its ellipse, nearer than the centre line.
    private static bool OnRing(int x, int y)
    {
        if (y + 0.5f < RING_CY || !InEllipse(x, y, AXIS, RING_CY, RING_A, RING_B))
        {
            return false;
        }
        return !InEllipse(x + 1, y, AXIS, RING_CY, RING_A, RING_B) || !InEllipse(x - 1, y, AXIS, RING_CY, RING_A, RING_B)
            || !InEllipse(x, y + 1, AXIS, RING_CY, RING_A, RING_B) || !InEllipse(x, y - 1, AXIS, RING_CY, RING_A, RING_B);
    }

    private static bool InFrontRing(int x, int y)
    {
        return OnRing(x, y) || OnRing(x, y - 1);
    }

    // Called every second and after a drain: work out the settling and redraw when the picture changed.
    private static void UpdateLook(GameItem item, bool drained)
    {
        try
        {
            Look look = looks.GetValue(item, k => new Look());
            long oil, aqueous;
            Totals(item, out oil, out aqueous);
            long total = WaterHelper.GetTotalVolume(item);
            bool stirred = look.Total >= 0 && !drained
                && (Math.Abs(total - look.Total) > CHANGE_UL || Math.Abs(oil - look.Oil) > CHANGE_UL);
            if (stirred)
            {
                look.Settle = 0;
            }
            else if (look.Settle < SETTLE_SECONDS)
            {
                look.Settle++;
            }
            look.Oil = oil;
            look.Aqueous = aqueous;
            look.Total = total;

            string mixed = Fix(WaterHelper.GetLiquidContainerColor(item));
            bool layered = oil > 0 && aqueous > 0;
            double oilShare = oil + aqueous > 0 ? (double)oil / (oil + aqueous) : 0.0;
            string upper = layered ? PhaseColor(item, true, oilShare) : mixed;
            string lower = layered ? PhaseColor(item, false, oilShare) : mixed;
            int settle = layered ? look.Settle : SETTLE_SECONDS;
            // Liquid area of the picture: volume share of the funnel, and the lower layer's share of that.
            double fill = Math.Min(1.0, (double)total / (CAPACITY_ML * 1000L));
            double lowerShare = total > 0 ? Math.Min(1.0, (aqueous + (total - oil - aqueous) * (1.0 - oilShare)) / (double)total) : 0.0;
            string key = (int)Math.Round(fill * 200) + "|" + (int)Math.Round(lowerShare * 100) + "|" + mixed + "|" + upper + "|" + lower + "|" + settle;
            if (key == look.Key)
            {
                return;
            }
            look.Key = key;
            look.Sprite = total > 0 ? BuildSprite(key, fill, lowerShare, mixed, upper, lower, settle / (double)SETTLE_SECONDS) : null;
            GameItemElement element = item as GameItemElement;
            if (element != null)
            {
                element.HandleInnateModSprite();
            }
        }
        catch (Exception e)
        {
            Log.Error("separatory funnel picture failed", e);
        }
    }

    private static List<ItemSpriteModifier> SpriteLayers(GameItem item)
    {
        Look look;
        if (!looks.TryGetValue(item, out look) || look.Sprite == null)
        {
            return null;
        }
        return new List<ItemSpriteModifier>
        {
            new ItemSpriteModifier(look.Sprite, new UnityEngine.Vector2(0f, 0f), 0, false, 0, false, UnityEngine.Color.white)
        };
    }

    private static UnityEngine.Sprite BuildSprite(string key, double fill, double lowerShare, string mixedHex, string upperHex, string lowerHex, double settled)
    {
        UnityEngine.Sprite cached;
        if (spriteCache.TryGetValue(key, out cached))
        {
            return cached;
        }
        UnityEngine.Sprite baseSprite = BaseSprite();
        if (baseSprite == null)
        {
            return null;
        }
        // Rows that can show liquid, from the bottom up, and how many pixels each holds. The liquid fills by area:
        // the picture's liquid pixels are the funnel's volume share of all of them, and each layer gets as many
        // pixels as its share of the liquid (equal volumes, equal areas, whatever the bulb's shape).
        List<int> rows = new List<int>();
        List<int> widths = new List<int>();
        int area = 0;
        for (int y = LIQ_BOT; y >= LIQ_TOP; y--)
        {
            int from, to;
            Columns(y, out from, out to);
            if (to >= from)
            {
                rows.Add(y);
                widths.Add(to - from + 1);
                area += to - from + 1;
            }
        }
        double filledArea = area * fill;
        double lowerArea = filledArea * lowerShare;
        // Whole rows: fill while the next row's middle stays under the target, at least one row.
        int filledRows = 0;
        int lowerRows = 0;
        double sum = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0 && sum + widths[i] / 2.0 > filledArea)
            {
                break;
            }
            if (sum + widths[i] / 2.0 <= lowerArea)
            {
                lowerRows = i + 1;
            }
            sum += widths[i];
            filledRows = i + 1;
        }
        if (filledRows >= 2 && lowerShare > 0.0 && lowerShare < 1.0)
        {
            lowerRows = Math.Max(1, Math.Min(filledRows - 1, lowerRows));    // both layers stay visible
        }
        UnityEngine.Color mixed = ColorOf(mixedHex), upper = ColorOf(upperHex), lower = ColorOf(lowerHex);
        UnityEngine.Color upperColour = UnityEngine.Color.Lerp(mixed, upper, (float)settled);
        UnityEngine.Color lowerColour = UnityEngine.Color.Lerp(mixed, lower, (float)settled);
        UnityEngine.Color32[] px = new UnityEngine.Color32[SPRITE_W * SPRITE_H];
        if (filledRows > 0)
        {
            // Seen from slightly above: the top of the liquid is a lit ellipse across the glass at the top row, and
            // the interface between the layers is the front arc of an ellipse, bowed down in the middle.
            Surface top = new Surface(rows[filledRows - 1]);
            Surface between = lowerRows > 0 && lowerRows < filledRows ? new Surface(rows[lowerRows - 1]) : null;
            for (int y = LIQ_TOP; y <= LIQ_BOT; y++)
            {
                int from, to;
                Columns(y, out from, out to);
                for (int x = from; x <= to; x++)
                {
                    bool surface;
                    if (!IsLiquid(top, x, y, out surface) || InFrontRing(x, y))
                    {
                        continue;                       // no liquid here, or the clamp ring is in front of the glass
                    }
                    bool low = IsLower(lowerRows, between, x, y);
                    UnityEngine.Color colour = low ? lowerColour : upperColour;
                    float shade = 1f, light = 0f;
                    if (surface)
                    {
                        light = 45f / 255f;
                    }
                    else
                    {
                        if (x >= to - 1)
                        {
                            shade = 0.82f;
                        }
                        bool aboveSurface;
                        if (settled >= 0.6 && between != null && low && IsLiquid(top, x, y - 1, out aboveSurface)
                            && !aboveSurface && !IsLower(lowerRows, between, x, y - 1))
                        {
                            light = 45f / 255f;          // a light line along the settled interface
                        }
                    }
                    px[(SPRITE_H - 1 - y) * SPRITE_W + x] = new UnityEngine.Color(Math.Min(1f, colour.r * shade + light),
                        Math.Min(1f, colour.g * shade + light), Math.Min(1f, colour.b * shade + light), 0.9f);
                }
            }
        }
        UnityEngine.Texture2D tex = new UnityEngine.Texture2D(SPRITE_W, SPRITE_H, UnityEngine.TextureFormat.RGBA32, false);
        tex.filterMode = UnityEngine.FilterMode.Point;
        tex.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        tex.SetPixels32(px);
        tex.Apply();
        UnityEngine.Vector2 pivot = new UnityEngine.Vector2(baseSprite.pivot.x / baseSprite.rect.width, baseSprite.pivot.y / baseSprite.rect.height);
        UnityEngine.Sprite sprite = UnityEngine.Sprite.Create(tex, new UnityEngine.Rect(0, 0, SPRITE_W, SPRITE_H), pivot, baseSprite.pixelsPerUnit);
        if (spriteCache.Count > 256)
        {
            spriteCache.Clear();
        }
        spriteCache[key] = sprite;
        return sprite;
    }

    // A level in the funnel seen from above: an ellipse as wide as the glass at that row, its centre on the row.
    private class Surface
    {
        public readonly int Row;
        public readonly float Cx, A, B;

        public Surface(int row)
        {
            int from, to;
            Columns(row, out from, out to);
            Row = row;
            Cx = (from + to + 1) / 2f;
            A = Math.Max(0.5f, (to - from + 1) / 2f);
            B = Math.Max(1f, A * TILT);
        }
    }

    // Whether the liquid reaches (x, y): below the top surface's centre row, or on the surface ellipse itself.
    private static bool IsLiquid(Surface top, int x, int y, out bool surface)
    {
        surface = false;
        if (y < LIQ_TOP || y > LIQ_BOT)
        {
            return false;
        }
        int from, to;
        Columns(y, out from, out to);
        if (x < from || x > to)
        {
            return false;
        }
        surface = InEllipse(x, y, top.Cx, top.Row + 0.5f, top.A, top.B);
        return surface || y > top.Row;
    }

    // Whether (x, y) is in the lower layer: under the interface's front arc (all of it with no interface showing).
    private static bool IsLower(int lowerRows, Surface between, int x, int y)
    {
        if (lowerRows == 0)
        {
            return false;
        }
        if (between == null)
        {
            return true;
        }
        float dx = (x + 0.5f - between.Cx) / between.A;
        float bow = between.B * (float)Math.Sqrt(Math.Max(0f, 1f - dx * dx));
        return y + 0.5f > between.Row - between.B * 0.5f + bow;
    }

    private static UnityEngine.Sprite BaseSprite()
    {
        LoadedMod mod = ModLoader.Mods.Find(m => m.Manifest != null && m.Manifest.ID == DreamToxinMod.MOD_ID);
        UnityEngine.Sprite sprite;
        return mod != null && mod.Sprites.TryGetValue(SPRITE, out sprite) ? sprite : null;
    }

    // One phase's colour: its own liquids plus its share of the solutes, weighted like vanilla's container colour.
    private static string PhaseColor(GameItem item, bool oilPhase, double oilShare)
    {
        float r = 0, g = 0, b = 0, w = 0;
        foreach (Liquid liquid in Liquid.Liquids)
        {
            int amount = IaLiquids.Get(item, liquid.identifier);
            if (amount <= 0)
            {
                continue;
            }
            Phase phase = PhaseOf(liquid.identifier);
            double part;
            if (phase == Phase.Oil)
            {
                part = oilPhase ? 1.0 : 0.0;
            }
            else if (phase == Phase.Aqueous || phase == Phase.Particle)
            {
                part = oilPhase ? 0.0 : 1.0;
            }
            else
            {
                part = oilPhase ? oilShare : 1.0 - oilShare;
            }
            UnityEngine.Color colour;
            if (part <= 0 || !UnityEngine.ColorUtility.TryParseHtmlString("#" + liquid.color, out colour))
            {
                continue;
            }
            float weight = (float)(amount * part) * Math.Max(1, liquid.colorMultiplier);
            r += colour.r * weight;
            g += colour.g * weight;
            b += colour.b * weight;
            w += weight;
        }
        if (w <= 0)
        {
            return "000000";
        }
        return Fix(UnityEngine.ColorUtility.ToHtmlStringRGB(new UnityEngine.Color(r / w, g / w, b / w)));
    }

    // Vanilla gives water pure white (its bottles turn it blue); show near-white liquid as pale water blue.
    private static string Fix(string hex)
    {
        UnityEngine.Color colour;
        if (string.IsNullOrEmpty(hex) || !UnityEngine.ColorUtility.TryParseHtmlString("#" + hex, out colour))
        {
            return "8fc3d1";
        }
        if (Math.Min(colour.r, Math.Min(colour.g, colour.b)) > 0.85f)
        {
            return "8fc3d1";
        }
        return hex.ToLowerInvariant();
    }

    private static UnityEngine.Color ColorOf(string hex)
    {
        UnityEngine.Color colour;
        return UnityEngine.ColorUtility.TryParseHtmlString("#" + hex, out colour) ? colour : UnityEngine.Color.gray;
    }

    // ---- debug ----------------------------------------------------------------------------------------------------

    private static void SpawnOne()
    {
        Give(DirectoryMaster.Item(ITEM_ID, true));
    }

    // A funnel holding 600 mL toluene and 900 mL water with some caffeine, sugar and debris, and two empty bottles.
    public static void GiveTestKit()
    {
        try
        {
            GameItem funnel = DirectoryMaster.Item(ITEM_ID, true);
            if (funnel != null)
            {
                WaterHelper.AddLiquid(funnel, FlavorChemistry.TOLUENE, 600000);
                WaterHelper.AddLiquid(funnel, "water", 900000);
                WaterHelper.AddLiquid(funnel, "caffeine", 30000);
                WaterHelper.AddLiquid(funnel, "sugar", 15000);
                WaterHelper.AddLiquid(funnel, "debris", 5000);
            }
            Give(funnel);
            for (int i = 0; i < 2; i++)
            {
                GameItem bottle = DirectoryMaster.Item("bottled_water", true);
                if (bottle != null)
                {
                    WaterHelper.EmptyContainer(bottle);
                    SpriteHelper.UpdateWaterContainerSprite(bottle);
                }
                Give(bottle);
            }
        }
        catch (Exception e)
        {
            Log.Error("separatory funnel test kit failed", e);
        }
    }

    private static void Give(GameItem item)
    {
        if (item == null)
        {
            return;
        }
        GraphUtils.EmporiumTryAdd(item);
        if (item.parentInventory == null)
        {
            Log.Info("separatory funnel: no room for " + item.identifier);
        }
    }
}
