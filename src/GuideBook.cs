// The Advanced Chemistry Handbook: one multi-page paper book (the game's PaperUI) holding every note of this
// mod, plus sections other mods contribute through localization keys (see below). Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;

public static class GuideBook
{
    public const string BOOK_ID = "vce_advanced_chemistry";
    public const string ITEM_ID = "vce.advanced_chem_guide";
    public const long VALUE = 200L;
    // Notes longer than this many lines continue on the next page (none of the current notes are).
    public const int LINES_PER_PAGE = 20;

    // Other mods add a section to the handbook through their own localization, no code needed:
    //   vce_handbook_title          the section title
    //   vce_handbook_line_1 .. N    its lines (read until the first missing key)
    public const string EXTERNAL_TITLE_KEY = "vce_handbook_title";
    public const string EXTERNAL_LINE_KEY = "vce_handbook_line_";

    // Mod localization is only loaded after OnEnable, so the book is (re)built once it is available.
    public static void Register()
    {
        try
        {
            PaperBookData book = PaperStarters.NewBook("blank", BOOK_ID, Text.Get("guide_book_name"));
            book.pages.Clear();
            AddPage(book, Text.Get("guide_book_name"), Text.Get("guide_book_intro"));
            foreach (HandNote note in Notes.All)
            {
                AddNote(book, Text.Get(note.NameKey), Lines(DreamToxinMod.MOD_ID, note.LineKeys));
            }
            AddExternalSections(book);
            // No PaperItemData: the paper registry would add the item to the directory only after every
            // other mod (item spawners included) has read it. CreateItem registers it with our other items.
            book.item = null;
            ModHelper.RegisterPaperBook(DreamToxinMod.MOD_ID, book);
            Log.Info("registered the advanced chemistry handbook (" + book.pages.Count + " pages)");
        }
        catch (Exception e)
        {
            Log.Error("registering the handbook failed", e);
        }
    }

    // Sections contributed by other enabled mods, in load order.
    private static void AddExternalSections(PaperBookData book)
    {
        foreach (LoadedMod mod in ModLoader.Mods)
        {
            if (mod == null || !mod.IsEnabled || mod.Manifest == null || mod.Manifest.ID == DreamToxinMod.MOD_ID)
            {
                continue;
            }
            if (!HasKey(mod, EXTERNAL_TITLE_KEY))
            {
                continue;  // ModHelper.GetLocalized would log a "key not found" warning for every such mod
            }
            string id = mod.Manifest.ID;
            string title = ModHelper.GetLocalized(id, EXTERNAL_TITLE_KEY);
            if (title == EXTERNAL_TITLE_KEY)
            {
                continue;
            }
            List<string> lines = new List<string>();
            for (int i = 1; i <= 200; i++)
            {
                string key = EXTERNAL_LINE_KEY + i;
                string line = ModHelper.GetLocalized(id, key);
                if (line == key)
                {
                    break;
                }
                lines.Add(line);
            }
            if (lines.Count > 0)
            {
                AddNote(book, title, lines);
            }
        }
    }

    private static bool HasKey(LoadedMod mod, string key)
    {
        if (mod.Localization == null)
        {
            return false;
        }
        foreach (Dictionary<string, string> table in mod.Localization.Values)
        {
            if (table != null && table.ContainsKey(key))
            {
                return true;
            }
        }
        return false;
    }

    // Lines whose localization exists; a missing key comes back as the key itself.
    private static List<string> Lines(string modId, string[] keys)
    {
        List<string> lines = new List<string>();
        foreach (string key in keys)
        {
            string line = ModHelper.GetLocalized(modId, key);
            if (line != key)
            {
                lines.Add(line);
            }
        }
        return lines;
    }

    private static void AddNote(PaperBookData book, string title, List<string> lines)
    {
        for (int start = 0; start < lines.Count; start += LINES_PER_PAGE)
        {
            int count = Math.Min(LINES_PER_PAGE, lines.Count - start);
            string pageTitle = start == 0 ? title : Text.Get("guide_book_continued", title);
            AddPage(book, pageTitle, string.Join("\n", lines.GetRange(start, count).ToArray()));
        }
    }

    // Layout for the 314 x 402 "blank" notebook: the default chrome sits along the top and bottom edges.
    private static void AddPage(PaperBookData book, string title, string body)
    {
        PaperPageData page = new PaperPageData();
        page.name = title;
        PaperElementData heading = new PaperElementData();
        heading.type = "text";
        heading.id = "title";
        heading.style = "title";
        heading.text = title;
        heading.x = 30f;
        heading.y = 40f;
        heading.w = 254f;
        heading.h = 22f;
        page.elements.Add(heading);
        PaperElementData text = new PaperElementData();
        text.type = "text";
        text.id = "body";
        text.style = "body";
        text.text = body;
        text.x = 30f;
        text.y = 68f;
        text.w = 254f;
        text.h = 272f;
        text.wrap = true;
        page.elements.Add(text);
        book.pages.Add(page);
    }

    // Same as what the paper registry would build: a readable document that opens the book.
    public static GameItem CreateItem()
    {
        GameItem item = ItemDirectory.CreateEmptyItem(ITEM_ID);
        try
        {
            item.name = Text.Get("guide_book_name");
            item.shortDescription = Text.Get("guide_book_desc");
            item.SetSpriteAndShape("Items/items_misc", "book2");
            item.EnableTag("DOCUMENT", false);
            item.SetGameItemType("DOCUMENT");
            item.EnableTag("IMPORTANT_TAG", false);
            item.EnableTag("GUIDE_PAPER_TAG", false);
            BookHelper.InitPaperGuide(item);
            item.unitValue = VALUE;
            item.canActivateSlotItemFunc = (GameItem a, GameInventory b, SlotMarker c) => HealthData.CanRead();
            item.mayActivateSlotItemFunc = (GameItem a, GameInventory b, SlotMarker c) => HealthData.CanRead();
            item.onActivateSlotItemFunc = delegate(GameItem a, GameInventory b, SlotMarker c)
            {
                ModHelper.OpenPaper(DreamToxinMod.MOD_ID, BOOK_ID);
            };
            ModHelper.SetSource(item, DreamToxinMod.MOD_ID, DreamToxinMod.MOD_NAME);
        }
        catch (Exception e)
        {
            Log.Error("create " + ITEM_ID + " failed", e);
        }
        return item;
    }

    // Puts one handbook on the current seller's table (retired chemist, Dr. Jackson) unless the player has one.
    public static void OfferIfUnowned()
    {
        try
        {
            if (PlayerOwnsOne())
            {
                return;
            }
            GameItem book = DirectoryMaster.Item(ITEM_ID, true);
            if (book != null)
            {
                PlayerStore.Instance.AddDirectSellingItemToTable(book, false, false, false, 0);
            }
        }
        catch (Exception e)
        {
            Log.Error("offering the handbook failed", e);
        }
    }

    // ModHook.OnNewGameStarted fires after vanilla has placed the starting items; the chemist start
    // (StartType.DRUG_DEALER, scenario id "chemist") already gets the vanilla chemistry guide, so add ours next to it.
    public const string CHEMIST_SCENARIO = "chemist";

    public static void GiveChemistStart(string scenarioId)
    {
        try
        {
            if (scenarioId != CHEMIST_SCENARIO || PlayerOwnsOne())
            {
                return;
            }
            GameItem book = DirectoryMaster.Item(ITEM_ID, true);
            if (book != null)
            {
                GraphUtils.EmporiumTryAdd(EmporiumEntry.Instance.invElement, book);
                Log.Info("chemist start: added the advanced chemistry handbook");
            }
        }
        catch (Exception e)
        {
            Log.Error("giving the chemist start handbook failed", e);
        }
    }

    // ModHook.OnPlaceInventorInventoryItemEarly: Dr. Jackson's regular and special stock.
    public static void OnPlaceInventorInventory(List<GameItem> ownedItems)
    {
        OfferIfUnowned();
    }

    public static bool PlayerOwnsOne()
    {
        foreach (GameItem item in EmporiumEntry.Instance.GetAllOwnedItems())
        {
            if (item != null && item.identifier == ITEM_ID)
            {
                return true;
            }
        }
        return false;
    }
}
