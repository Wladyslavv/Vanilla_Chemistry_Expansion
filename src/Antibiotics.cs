// Cephalosporin C and the antibiotic tablet (docs/ANTIBIOTICS_DESIGN.md).
// Blending a strain gives cephalosporin C, which carries three things of its own: the strain's genome, the
// strain's purity trait and how much of it came from each extraction target. Mixing two genomes spoils the
// whole lot into bioliquid waste. 5 mL in the Chem Finisher presses one tablet: base value 100 plus 5 a purity
// point, lab pure at 99% and pharma grade at 90% (-10%), anything below that unsellable; a batch that came
// mostly from the cell surface or from inside the hyphae gets contraband tags instead of a clean medicine.
// Written for C# 5 (see DreamToxinMod.cs).
using System;
using HarmonyLib;

// Genome, purity trait and extraction-target shares, as carried by a liquid container or a chem item.
public static class CephData
{
    public const string GENOME_TAG = "VCE_CEPH_GENOME_TAG";
    public const string PURITY_TAG = "VCE_CEPH_PURITY_TAG";
    public const string TARGET_LIQUID_TAG = "VCE_CEPH_TARGET0_TAG";
    public const string TARGET_SURFACE_TAG = "VCE_CEPH_TARGET1_TAG";
    public const string TARGET_INTRA_TAG = "VCE_CEPH_TARGET2_TAG";

    public static readonly string[] TARGET_TAGS = { TARGET_LIQUID_TAG, TARGET_SURFACE_TAG, TARGET_INTRA_TAG };

    public static string Genome(GameItem item)
    {
        if (item == null || !item.IsTag(GENOME_TAG))
        {
            return "";
        }
        string genome = item.GetTagReadonly(GENOME_TAG).GetString();
        return genome ?? "";
    }

    public static void SetGenome(GameItem item, string genome)
    {
        if (item == null)
        {
            return;
        }
        string value = genome ?? "";
        item.ModifyTag(GENOME_TAG, delegate(TagState state)
        {
            state.Enable().SetString(value);
        }, true);
        item.ValidateShapeState();
    }

    public static int Int(GameItem item, string tag)
    {
        return item != null && item.IsTag(tag) ? item.GetTagReadonly(tag).GetInt() : 0;
    }

    public static void SetInt(GameItem item, string tag, int value)
    {
        if (item == null)
        {
            return;
        }
        int stored = value;
        item.ModifyTag(tag, delegate(TagState state)
        {
            state.Enable().SetInt(stored);
        }, true);
        item.ValidateShapeState();
    }

    // The purity trait of the strains this lot came from, in tenths, volume-weighted.
    public static int PurityTenths(GameItem item)
    {
        return Int(item, PURITY_TAG);
    }

    public static int TargetShare(GameItem item, int target)
    {
        return Int(item, TARGET_TAGS[Math.Max(0, Math.Min(TARGET_TAGS.Length - 1, target))]);
    }

    public static int TargetTotal(GameItem item)
    {
        int total = 0;
        foreach (string tag in TARGET_TAGS)
        {
            total += Int(item, tag);
        }
        return total;
    }

    // Which extraction target more than half of this lot came from, or -1 when none does.
    public static int DominantTarget(GameItem item)
    {
        int total = TargetTotal(item);
        if (total <= 0)
        {
            return -1;
        }
        for (int target = 0; target < TARGET_TAGS.Length; target++)
        {
            if (Int(item, TARGET_TAGS[target]) * 2 > total)
            {
                return target;
            }
        }
        return -1;
    }

    public static void Clear(GameItem item)
    {
        if (item == null)
        {
            return;
        }
        SetGenome(item, "");
        SetInt(item, PURITY_TAG, 0);
        foreach (string tag in TARGET_TAGS)
        {
            SetInt(item, tag, 0);
        }
    }

    // Folds a strain's worth of cephalosporin C into whatever the container already holds.
    // Two genomes in one container spoil the lot: all the cephalosporin C turns into bioliquid waste.
    public static void Add(GameItem container, int addedUl, string genome, int purityTenths, int target)
    {
        if (container == null || addedUl <= 0)
        {
            return;
        }
        int had = TargetTotal(container);
        string hadGenome = Genome(container);
        if (had > 0 && hadGenome.Length > 0 && hadGenome != genome)
        {
            Spoil(container);
            return;
        }
        int purity = had > 0
            ? (int)Math.Round((PurityTenths(container) * (double)had + purityTenths * (double)addedUl) / (had + addedUl))
            : purityTenths;
        SetGenome(container, genome);
        SetInt(container, PURITY_TAG, purity);
        SetInt(container, TARGET_TAGS[Math.Max(0, Math.Min(TARGET_TAGS.Length - 1, target))],
            TargetShare(container, target) + addedUl);
    }

    // Everything that was cephalosporin C becomes bioliquid waste and the carried data goes with it.
    public static void Spoil(GameItem container)
    {
        if (container == null)
        {
            return;
        }
        int ceph = IaLiquids.Get(container, Antibiotics.CEPH_ID);
        if (ceph > 0)
        {
            IaLiquids.Set(container, Antibiotics.CEPH_ID, 0);
            IaLiquids.Set(container, Antibiotics.WASTE_ID, IaLiquids.Get(container, Antibiotics.WASTE_ID) + ceph);
        }
        Clear(container);
        WaterFeatureHelper.OnLiquidContainerPourOutOf(container);
        SpriteHelper.UpdateWaterContainerSprite(container);
        Log.Info("cephalosporin C spoiled into bioliquid waste: " + ceph + " uL");
    }

    // Takes the moved share off the source, so pouring half a flask leaves half the record behind.
    public static void RemoveShare(GameItem from, double fraction)
    {
        if (from == null || fraction <= 0.0)
        {
            return;
        }
        double keep = Math.Max(0.0, 1.0 - Math.Min(1.0, fraction));
        for (int target = 0; target < TARGET_TAGS.Length; target++)
        {
            SetInt(from, TARGET_TAGS[target], (int)Math.Round(Int(from, TARGET_TAGS[target]) * keep));
        }
        if (TargetTotal(from) <= 0)
        {
            Clear(from);
        }
    }

    // The target shares are meant to add up to the cephalosporin C really in the container. Whatever took some out
    // without telling us (older versions, other mods that write liquid amounts themselves, reactions) left shares
    // behind, and those stale shares outweighed every new batch. Scale them down to what is there; clear the record
    // when nothing is. Called before the record is used. Containers only: a product's shares are set when pressed.
    public static void Reconcile(GameItem container, int actualUl)
    {
        if (container == null)
        {
            return;
        }
        int total = TargetTotal(container);
        if (actualUl <= 0)
        {
            if (total > 0 || Genome(container).Length > 0)
            {
                Clear(container);
            }
            return;
        }
        if (total <= actualUl)
        {
            return;
        }
        double factor = actualUl / (double)total;
        for (int target = 0; target < TARGET_TAGS.Length; target++)
        {
            SetInt(container, TARGET_TAGS[target], (int)Math.Round(Int(container, TARGET_TAGS[target]) * factor));
        }
    }

    public static void Reconcile(GameItem container)
    {
        if (container != null)
        {
            Reconcile(container, IaLiquids.Get(container, Antibiotics.CEPH_ID));
        }
    }

    // A product was filled with `received` uL of cephalosporin C from this container: it gets the genome, the
    // purity trait and the extraction-target split of the container's record. Based on what the product received,
    // not on what the container lost, because some batch tools fill products from a copy and write the container
    // back only at the end.
    public static void CopyToProduct(GameItem from, GameItem product, int received)
    {
        if (from == null || product == null || received <= 0)
        {
            return;
        }
        int total = TargetTotal(from);
        string genome = Genome(from);
        if (total <= 0 || genome.Length == 0)
        {
            return;
        }
        SetGenome(product, genome);
        SetInt(product, PURITY_TAG, PurityTenths(from));
        for (int target = 0; target < TARGET_TAGS.Length; target++)
        {
            SetInt(product, TARGET_TAGS[target], (int)Math.Round(Int(from, TARGET_TAGS[target]) * (double)received / total));
        }
    }

    // A pour moved cephalosporin C from one container to another: the record goes along. The target's purity
    // becomes the volume-weighted mean of what it had and what arrived; a different genome spoils the target.
    // sentFraction is the share of the source's cephalosporin C that left it, received the amount that arrived
    // (a filter may hold some back), targetHad what the target held before.
    public static void Pour(GameItem from, GameItem to, double sentFraction, int received, int targetHad)
    {
        if (from == null || to == null || sentFraction <= 0.0)
        {
            return;
        }
        string genome = Genome(from);
        int sourceTotal = TargetTotal(from);
        if (genome.Length == 0 || sourceTotal <= 0)
        {
            RemoveShare(from, sentFraction);
            return;
        }
        if (received > 0)
        {
            string hadGenome = Genome(to);
            if (targetHad > 0 && hadGenome.Length > 0 && hadGenome != genome)
            {
                RemoveShare(from, sentFraction);
                Spoil(to);
                return;
            }
            // Target liquid without a record (or none at all) takes the arriving purity.
            int purity = targetHad > 0 && hadGenome.Length > 0
                ? (int)Math.Round((PurityTenths(to) * (double)targetHad + PurityTenths(from) * (double)received)
                    / (targetHad + received))
                : PurityTenths(from);
            double arriving = Math.Min(1.0, received / (double)sourceTotal);
            SetGenome(to, genome);
            SetInt(to, PURITY_TAG, purity);
            for (int target = 0; target < TARGET_TAGS.Length; target++)
            {
                int share = (int)Math.Round(Int(from, TARGET_TAGS[target]) * arriving);
                SetInt(to, TARGET_TAGS[target], TargetShare(to, target) + share);
            }
        }
        RemoveShare(from, sentFraction);
    }
}

// Pen's pipette mod (Pen_mod_pipette) moves liquid by setting the containers' liquid tags itself, not through
// WaterHelper.TransferLiquid, so CephPourPatch never sees it. Every draw and every dispense goes through
// PenPipette.PipetteTransfer.OnTarget(pipette, target): compare the cephalosporin C on both sides before and after
// and carry the record the way the liquid went. Installed late (DeferredPatches) so the pipette mod is loaded by
// then; skipped when it is not installed.
[HarmonyPatch]
public static class CephPipettePatch
{
    private static System.Reflection.MethodBase Target()
    {
        Type type = AccessTools.TypeByName("PenPipette.PipetteTransfer");
        return type != null ? AccessTools.Method(type, "OnTarget", new Type[] { typeof(GameItem), typeof(GameItem) }) : null;
    }

    private static bool Prepare()
    {
        return DeferredPatches.Ready && Target() != null;
    }

    private static System.Reflection.MethodBase TargetMethod()
    {
        return Target();
    }

    private static void Prefix(object[] __args, out CephPourPatch.Before __state)
    {
        __state = new CephPourPatch.Before();
        try
        {
            GameItem pipette = __args[0] as GameItem;
            GameItem other = __args[1] as GameItem;
            if (pipette != null && other != null)
            {
                __state.Source = IaLiquids.Get(pipette, Antibiotics.CEPH_ID);
                __state.Target = IaLiquids.Get(other, Antibiotics.CEPH_ID);
                CephData.Reconcile(pipette, __state.Source);
                CephData.Reconcile(other, __state.Target);
            }
        }
        catch (Exception e)
        {
            Log.Error("reading cephalosporin C before a pipette move failed", e);
        }
    }

    private static void Postfix(object[] __args, CephPourPatch.Before __state)
    {
        try
        {
            GameItem pipette = __args[0] as GameItem;
            GameItem other = __args[1] as GameItem;
            if (pipette == null || other == null)
            {
                return;
            }
            int pipetteDelta = IaLiquids.Get(pipette, Antibiotics.CEPH_ID) - __state.Source;
            int otherDelta = IaLiquids.Get(other, Antibiotics.CEPH_ID) - __state.Target;
            if (pipetteDelta < 0 && __state.Source > 0)
            {
                // Dispensed: pipette -> container.
                CephData.Pour(pipette, other, (double)-pipetteDelta / __state.Source, Math.Max(0, otherDelta), __state.Target);
            }
            else if (otherDelta < 0 && __state.Target > 0)
            {
                // Drawn: container -> pipette.
                CephData.Pour(other, pipette, (double)-otherDelta / __state.Target, Math.Max(0, pipetteDelta), __state.Source);
            }
        }
        catch (Exception e)
        {
            Log.Error("carrying the cephalosporin record on a pipette move failed", e);
        }
    }
}

// Pouring cephalosporin C between containers carries its genome, purity trait and extraction target along
// (without this the record stayed behind and tablets pressed from a poured lot came out at purity 0, no genome).
[HarmonyPatch(typeof(WaterHelper), "TransferLiquid")]
public static class CephPourPatch
{
    public struct Before
    {
        public int Source;
        public int Target;
    }

    private static void Prefix(GameItem sourceContainer, GameItem targetContainer, out Before __state)
    {
        __state = new Before();
        try
        {
            if (sourceContainer != null && targetContainer != null)
            {
                __state.Source = IaLiquids.Get(sourceContainer, Antibiotics.CEPH_ID);
                __state.Target = IaLiquids.Get(targetContainer, Antibiotics.CEPH_ID);
                CephData.Reconcile(sourceContainer, __state.Source);
                CephData.Reconcile(targetContainer, __state.Target);
            }
        }
        catch (Exception e)
        {
            Log.Error("reading cephalosporin C before a pour failed", e);
        }
    }

    private static void Postfix(GameItem sourceContainer, GameItem targetContainer, Before __state)
    {
        try
        {
            if (__state.Source <= 0 || sourceContainer == null || targetContainer == null)
            {
                return;
            }
            int sent = __state.Source - IaLiquids.Get(sourceContainer, Antibiotics.CEPH_ID);
            if (sent <= 0)
            {
                return;
            }
            int received = Math.Max(0, IaLiquids.Get(targetContainer, Antibiotics.CEPH_ID) - __state.Target);
            CephData.Pour(sourceContainer, targetContainer, (double)sent / __state.Source, received, __state.Target);
        }
        catch (Exception e)
        {
            Log.Error("carrying the cephalosporin record on a pour failed", e);
        }
    }
}

public static class Antibiotics
{
    public const string CEPH_ID = "vce_cephalosporin";
    public const string WASTE_ID = "vce_bioliquid_waste";

    public const string TABLET_ID = "vce_antibiotic";
    public const string TABLET_SPRITE = "vce_antibiotic";

    // 5 mL of cephalosporin C a tablet; base 100 at the standard purity trait, 5 more a point.
    public const int TABLET_VOLUME = 5000;
    public const long TABLET_VALUE = 60;
    public const double VALUE_PER_PURITY_POINT = 5.0;

    public const double LAB_PURE_PERCENT = 99.0;
    public const double PHARMA_PERCENT = 90.0;
    // Lab pure sells at the base value, pharma grade 10% under it, anything below 90% cannot be sold at all.
    public const int LAB_PURE_MOD = 0;
    public const int PHARMA_MOD = -10;
    public const int REJECT_MOD = -90;

    public const int SURFACE_CONTRABAND_LEVEL = 2;
    public const int INTRA_CONTRABAND_LEVEL = 1;
    public const int NARCOTIC_VALUE = 2;

    public static void Register()
    {
        // Cephalosporin C dissolves like any fine chemical and does not boil off.
        Liquid ceph = IaLiquids.AddLiquid(CEPH_ID, "liquid_cephalosporin", 5, "efe4c0", 1);
        if (ceph != null)
        {
            ceph.isEvaporative = false;
            ceph.canBeRemovedByPurifier = false;
            ceph.basePurifierRemoveAmount = 0;
        }
        // Bioliquid waste is as coarse as microbes, so the water filters catch it.
        Liquid waste = IaLiquids.AddLiquid(WASTE_ID, "liquid_bioliquid_waste", 50, "7d7a52", 3);
        if (waste != null)
        {
            waste.isEvaporative = false;
        }
        AddRecipe();
    }

    private static void AddRecipe()
    {
        if (FinisherRecipes.Find(TABLET_ID) != null)
        {
            return;
        }
        ChemFinisherRecipe recipe = new ChemFinisherRecipe();
        recipe.identifier = TABLET_ID;
        recipe.displayName = Text.Get("antibiotic_name");
        recipe.requiredVolume = TABLET_VOLUME;
        recipe.isRequireDry = false;
        recipe.activeIngredientId = CEPH_ID;
        ChemFinisherRecipe.chemFinisherRecipes.Add(recipe);
    }

    public static void Unregister()
    {
        ChemFinisherRecipe.chemFinisherRecipes.RemoveAll(r => r.identifier == TABLET_ID);
    }

    public static void RefreshNames()
    {
        Liquid liquid;
        if (Liquid.LiquidDict.TryGetValue(CEPH_ID, out liquid))
        {
            liquid.displayName = Text.Get("liquid_cephalosporin");
        }
        if (Liquid.LiquidDict.TryGetValue(WASTE_ID, out liquid))
        {
            liquid.displayName = Text.Get("liquid_bioliquid_waste");
        }
        ChemFinisherRecipe recipe = FinisherRecipes.Find(TABLET_ID);
        if (recipe != null)
        {
            recipe.displayName = Text.Get("antibiotic_name");
        }
    }

    public static void Enable()
    {
        ModHook.OnCreateTooltipLate += OnCreateTooltip;
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Antibiotics: 5 pharma tablets", SpawnTablets, false);
        }
        catch (Exception e)
        {
            Log.Error("antibiotic debug function registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnCreateTooltipLate -= OnCreateTooltip;
    }

    public static void AddItems(ModItemDirectory directory)
    {
        directory.Add(TABLET_ID, CreateTablet);
    }

    public static bool IsTablet(GameItem item)
    {
        return item != null && item.identifier == TABLET_ID;
    }

    public static GameItem CreateTablet()
    {
        GameItem item = ItemDirectory.CreateEmptyItem(TABLET_ID);
        try
        {
            item.name = Text.Get("antibiotic_name");
            item.shortDescription = Text.Get("antibiotic_desc");
            item.flavorText = Text.Get("antibiotic_flavor");
            item.unitValue = TABLET_VALUE;
            item.SetGameItemType("MEDICAL");
            ChemicalHelper.InitChemItem(item, true, true);
            // Default contents for tablets not pressed in the Chem Finisher (debug spawns): pharma grade.
            ChemFinisherRecipe.SetCompositionByPurity(item, 90, 95, TABLET_VOLUME, CEPH_ID);
            ChemicalProductHelper.InitChemicalProduct(item, CEPH_ID);
            // A medical product as far as vanilla's chemical feedback goes: a purity label set higher than the real
            // grade is now caught like any medicine's (buyer complaints, fraud), not sold for free. Tablets the
            // extraction target makes contraband are left out by vanilla, as all contraband is.
            ChemicalFeedbackHelper.InitChemicalIntentItem(item, ChemicalFeedbackHelper.ChemicalIntent.MEDICAL);
            AudioHelper.InitTinyItem(item);
            BeerSprites.Apply(item, TABLET_SPRITE);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            Log.Error("create " + TABLET_ID + " failed", e);
        }
        return item;
    }

    // ---- grading ------------------------------------------------------------------------------------------------

    private static ItemCondition Condition(string key, string identifier, int modValue)
    {
        ItemCondition condition = new ItemCondition();
        condition.category = "CATEGORY_CHEMICAL_PURITY";
        condition.display = Text.Get(key);
        condition.modValue = modValue;
        condition.identifier = identifier;
        return condition;
    }

    // Replaces vanilla's seven purity tiers with three, sets the base value from the purity trait, and applies
    // whatever the extraction target did to this batch. Called after vanilla's GradeProduct.
    public static void Grade(GameItem tablet)
    {
        if (!IsTablet(tablet))
        {
            return;
        }
        double purity = ChemicalProductHelper.GetPurity(tablet);
        bool sellable = purity >= PHARMA_PERCENT;
        if (purity >= LAB_PURE_PERCENT)
        {
            ChemicalProductHelper.SetPurityFeature(tablet, Condition("antibiotic_grade_lab", "vce_antibiotic_lab", LAB_PURE_MOD));
        }
        else if (sellable)
        {
            ChemicalProductHelper.SetPurityFeature(tablet, Condition("antibiotic_grade_pharma", "vce_antibiotic_pharma", PHARMA_MOD));
        }
        else
        {
            ChemicalProductHelper.SetPurityFeature(tablet, Condition("antibiotic_grade_reject", "vce_antibiotic_reject", REJECT_MOD));
        }

        long value = TABLET_VALUE + (long)Math.Round(VALUE_PER_PURITY_POINT * CephData.PurityTenths(tablet) / 10.0);
        tablet.unitValue = Math.Max(1L, value);

        ApplyTarget(tablet);
        // Whatever resistance stands against this genome right now (Resistance.cs).
        Resistance.ApplyPenalty(tablet);
        if (!sellable)
        {
            // Below pharma grade nobody will take it: not a medicine any more, just waste to be thrown out.
            tablet.RemoveGameItemType("MEDICAL");
            tablet.SetGameItemType("JUNK");
        }
    }

    // The extraction target the batch mostly came from decides what else the tablet counts as.
    private static void ApplyTarget(GameItem tablet)
    {
        int target = CephData.DominantTarget(tablet);
        if (target == StrainTraits.TARGET_SURFACE)
        {
            tablet.SetGameItemType("POISON");
            ContrabandHelper.InitContrabandItem(tablet, SURFACE_CONTRABAND_LEVEL);
        }
        else if (target == StrainTraits.TARGET_INTRACELLULAR)
        {
            tablet.SetGameItemType("NARCOTIC");
            tablet.SetGameItemType("SUBSTANCE");
            ConsumableHelper.InitNarcotic(tablet, NARCOTIC_VALUE);
            ContrabandHelper.InitContrabandItem(tablet, INTRA_CONTRABAND_LEVEL);
        }
    }

    // ---- tooltip ------------------------------------------------------------------------------------------------

    public static void OnCreateTooltip(RichTextBuilder builder, GameItem item)
    {
        try
        {
            if (builder == null)
            {
                return;
            }
            bool tablet = IsTablet(item);
            bool container = !tablet && item != null && item.IsTag("LIQUID_CONTAINER_TAG")
                && IaLiquids.Get(item, CEPH_ID) > 0;
            if (!tablet && !container)
            {
                return;
            }
            string genome = CephData.Genome(item);
            if (genome.Length > 0)
            {
                builder.AddLine(Text.Get("antibiotic_tooltip_genome", genome), true, RenderHandler.ColorPalette.LightCyan);
            }
            else if (tablet)
            {
                // A tablet always says which genome it is; one that lost its record says so instead of nothing.
                builder.AddLine(Text.Get("antibiotic_tooltip_genome", Text.Get("antibiotic_genome_unknown")), true,
                    RenderHandler.ColorPalette.LightCyan);
            }
            int target = CephData.DominantTarget(item);
            if (target > StrainTraits.TARGET_LIQUID)
            {
                builder.AddLine(Text.Get("antibiotic_tooltip_target", StrainTraits.TargetName(target)), true,
                    RenderHandler.ColorPalette.LightRed);
            }
            if (tablet)
            {
                Resistance.AddTooltip(builder, genome);
            }
        }
        catch (Exception e)
        {
            Log.Error("antibiotic tooltip failed", e);
        }
    }

    public static void SpawnTablets()
    {
        try
        {
            string genome = StrainTraits.RandomGenome();
            for (int i = 0; i < 5; i++)
            {
                GameItem tablet = DirectoryMaster.Item(TABLET_ID, true);
                if (tablet == null)
                {
                    continue;
                }
                CephData.SetGenome(tablet, genome);
                CephData.SetInt(tablet, CephData.TARGET_LIQUID_TAG, TABLET_VOLUME);
                ChemicalProductHelper.GradeProduct(tablet);
                ModHelper.MoveItemToInventory(tablet);
            }
        }
        catch (Exception e)
        {
            Log.Error("antibiotic debug kit failed", e);
        }
    }
}

// Blending a strain: vanilla moves the contents, we fold the strain's genome, purity trait and extraction
// target into the container's record (and spoil the lot when two genomes meet).
[HarmonyPatch(typeof(ChemicalHelper), "BlendInto")]
public static class StrainBlendPatch
{
    // A strain straight out of the item spawner may not have had its first tick yet: give it its identity now,
    // before vanilla empties it into the container.
    // Also reads what the strain really holds (strains saved before a yield change hold the old amount), worked out
    // the way vanilla does: rounded after the blender's loss.
    private static void Prefix(GameItem chemicalItem, int loss, out int __state)
    {
        __state = 0;
        try
        {
            if (Strain.IsStrain(chemicalItem))
            {
                Strain.EnsureIdentity(chemicalItem);
                double kept = 1.0 - Math.Max(0, Math.Min(100, loss)) / 100.0;
                __state = (int)Math.Round(IaLiquids.Get(chemicalItem, Antibiotics.CEPH_ID) * kept, MidpointRounding.AwayFromZero);
            }
        }
        catch (Exception e)
        {
            Log.Error("preparing a strain for the blender failed", e);
        }
    }

    // The parameter names have to match vanilla's exactly; Harmony binds them by name.
    private static void Postfix(GameItem chemicalItem, GameItem liquidContainer, int loss, int __state)
    {
        try
        {
            if (liquidContainer == null || !Strain.IsStrain(chemicalItem))
            {
                return;
            }
            int added = __state;
            if (StrainTraits.IsSeedStock(chemicalItem) || added <= 0)
            {
                return;
            }
            // What the container held before this strain went in: the record must not count more than that.
            CephData.Reconcile(liquidContainer, Math.Max(0, IaLiquids.Get(liquidContainer, Antibiotics.CEPH_ID) - added));
            CephData.Add(liquidContainer, added, StrainTraits.Genome(chemicalItem),
                StrainTraits.Purity(chemicalItem), StrainTraits.Target(chemicalItem));
            Log.Info("blended a strain: genome " + StrainTraits.Genome(chemicalItem) + ", " + added
                + " uL cephalosporin C, container now " + CephData.Genome(liquidContainer));
        }
        catch (Exception e)
        {
            Log.Error("blending a strain failed", e);
        }
    }
}

// The Chem Finisher moves liquid out of the container and into the product: the carried record moves with it.
// The product's share is what it received, not what the container lost: Pen's QoL batch output skips vanilla's
// transfer, fills each product from a copy of the container's liquid and writes the container back only at the
// end, so during the batch the container never changes. The container's record is trimmed to what is left
// before and after each press.
[HarmonyPatch(typeof(ChemicalHelper), "TransferLiquidToChem")]
public static class CephTransferPatch
{
    private static void Prefix(GameItem container)
    {
        try
        {
            CephData.Reconcile(container);
        }
        catch (Exception e)
        {
            Log.Error("trimming the cephalosporin record before pressing failed", e);
        }
    }

    private static void Postfix(GameItem container, GameItem chemicalItem)
    {
        try
        {
            if (container == null || chemicalItem == null)
            {
                return;
            }
            int received = IaLiquids.Get(chemicalItem, Antibiotics.CEPH_ID);
            if (received <= 0)
            {
                return;
            }
            CephData.CopyToProduct(container, chemicalItem, received);
            CephData.Reconcile(container);
            Log.Info("pressed from cephalosporin C: genome " + CephData.Genome(chemicalItem) + ", " + received
                + " uL, purity trait " + StrainTraits.Format(CephData.PurityTenths(chemicalItem))
                + ", target split " + CephData.Int(chemicalItem, CephData.TARGET_LIQUID_TAG) + "/"
                + CephData.Int(chemicalItem, CephData.TARGET_SURFACE_TAG) + "/"
                + CephData.Int(chemicalItem, CephData.TARGET_INTRA_TAG));
        }
        catch (Exception e)
        {
            Log.Error("carrying the cephalosporin record to a product failed", e);
        }
    }
}

// Our tablets get three purity tiers instead of vanilla's seven.
[HarmonyPatch(typeof(ChemicalProductHelper), "GradeProduct")]
public static class AntibioticGradePatch
{
    private static void Postfix(GameItem item)
    {
        try
        {
            if (Antibiotics.IsTablet(item))
            {
                Antibiotics.Grade(item);
            }
        }
        catch (Exception e)
        {
            Log.Error("grading an antibiotic tablet failed", e);
        }
    }
}
