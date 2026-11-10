// Advanced Kotton fabric: a gold Kotton fabric (pore 150, 1000 durability) that fully regains its durability
// every day, like the vanilla advanced water filter regains 50. Written for C# 5 (see DreamToxinMod.cs).
using System;
using HarmonyLib;

public static class AdvancedFabric
{
    public const string ITEM_ID = "ia.kotton_fabric_advanced";
    public const string WATER_FILTER_ID = "water_filter_adv";
    // Textures/Items/kotton_fabric_advanced.png: vanilla kotton_fabric recoloured with the gold of the
    // advanced water filter.
    public const string SPRITE_KEY = "kotton_fabric_advanced";
    public const int MAX_DURABILITY = 1000;
    public const int DAILY_REPAIR = 1000;
    public const long VALUE = 450L;

    public static GameItem Create()
    {
        GameItem item = DirectoryMaster.Item("kotton_fabric", true);
        try
        {
            item.identifier = ITEM_ID;
            item.name = IaText.Get("advanced_fabric_name");
            item.shortDescription = IaText.Get("advanced_fabric_desc");
            item.flavorText = IaText.Get("advanced_fabric_flavor");
            item.SetGameItemType("LUXURY_ITEM");
            item.unitValue = VALUE;
            DurabilityHelper.InitItemWithDurability(item, MAX_DURABILITY);
            item.onCycleEndSlotItemFunc = delegate(GameItem curItem, GameInventory inventory, SlotMarker slot)
            {
                DurabilityHelper.ChangeDurability(curItem, DAILY_REPAIR);
            };
            ApplySprite(item);
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            IaLog.Error("create " + ITEM_ID + " failed", e);
        }
        return item;
    }

    private static void ApplySprite(GameItem item)
    {
        LoadedMod mod = ModLoader.Mods.Find(m => m.Manifest != null && m.Manifest.ID == DreamToxinMod.MOD_ID);
        if (mod != null && mod.Sprites.ContainsKey(SPRITE_KEY))
        {
            item.SetSpriteAndShapeFromMod(DreamToxinMod.MOD_ID, SPRITE_KEY);
        }
    }

    // Mod sprites may not be loaded yet when items are first created; give saved fabrics the sprite again.
    public static void FixLoadedItems()
    {
        try
        {
            foreach (GameItem item in EmporiumEntry.Instance.GetAllItems())
            {
                if (item != null && item.identifier == ITEM_ID && (!item.useSpriteFromMod || item.spritePath != SPRITE_KEY))
                {
                    ApplySprite(item);
                }
            }
        }
        catch (Exception e)
        {
            IaLog.Error("updating saved fabric sprites failed", e);
        }
    }
}

// Sold wherever the advanced water filter is: every vendor and client offer goes through
// PlayerStore.TryAddDirectSellingItemToTable(GameItem, ...), so whenever an advanced water filter is
// put on the table, an advanced Kotton fabric comes with it on the same terms (owned/stolen).
[HarmonyPatch(typeof(PlayerStore), "TryAddDirectSellingItemToTable",
    new Type[] { typeof(GameItem), typeof(bool), typeof(bool), typeof(bool), typeof(int) })]
public static class AdvancedFabricSalePatch
{
    private static bool adding;

    private static void Postfix(PlayerStore __instance, GameItem gameItem, bool isOwend, bool isStolen, bool ignorePerk, int forcedHeat, bool __result)
    {
        if (adding || !__result || gameItem == null || gameItem.identifier != AdvancedFabric.WATER_FILTER_ID)
        {
            return;
        }
        adding = true;
        try
        {
            __instance.TryAddDirectSellingItemToTable(DirectoryMaster.Item(AdvancedFabric.ITEM_ID, true), isOwend, isStolen, ignorePerk, forcedHeat);
        }
        catch (Exception e)
        {
            IaLog.Error("adding advanced fabric to the table failed", e);
        }
        finally
        {
            adding = false;
        }
    }
}

// Loot crates that roll an advanced water filter also hold an advanced Kotton fabric.
[HarmonyPatch(typeof(PreBuiltItemHelper), "TryAdd")]
public static class AdvancedFabricLootPatch
{
    private static void Postfix(GameItem crate, GameItem item)
    {
        if (crate == null || item == null || item.identifier != AdvancedFabric.WATER_FILTER_ID)
        {
            return;
        }
        GameItem fabric = DirectoryMaster.Item(AdvancedFabric.ITEM_ID, true);
        if (fabric != null && GraphUtils.TryAcceptAll(crate, fabric) > 0)
        {
            fabric.Destroy();
        }
    }
}
