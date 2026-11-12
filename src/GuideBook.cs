// The Advanced Chemistry Handbook: one multi-page paper book (the game's PaperUI) holding every note of this
// mod, plus sections other mods contribute through localization keys (see below). Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class GuideBook
{
    public const string BOOK_ID = "vce_advanced_chemistry";
    public const string ITEM_ID = "vce.advanced_chem_guide";
    public const long VALUE = 200L;

    // Other mods add a section to the handbook through their own localization, no code needed:
    //   vce_handbook_title          the section title
    //   vce_handbook_line_1 .. N    its lines (read until the first missing key)
    public const string EXTERNAL_TITLE_KEY = "vce_handbook_title";
    public const string EXTERNAL_LINE_KEY = "vce_handbook_line_";

    private const float DESIGN_WIDTH = 314f;
    private const float DESIGN_HEIGHT = 402f;
    private const float DISPLAY_WIDTH = 628f;
    private const float DISPLAY_HEIGHT = 804f;
    private const float TITLE_LEFT = 50f;
    private const float TITLE_TOP = 19f;
    private const float TITLE_WIDTH = 225f;
    private const float TITLE_HEIGHT = 16f;
    private const float SUBTITLE_TOP = 36f;
    private const float SUBTITLE_HEIGHT = 9f;
    private const float CONTENT_TOP = 45.5f;
    private const float COVER_CONTENT_TOP = 37f;
    private const float CONTENT_BOTTOM = 360f;
    private const float BODY_LEFT = 57f;
    private const float BODY_WIDTH = 230f;
    private const float BODY_UNITS_PER_PIXEL = 54f / 200f;
    private const float FLOAT_IMAGE_LEFT = 51f;
    private const float SECTION_LEFT = 51f;
    private const float SECTION_WIDTH = 238f;
    private const float SECTION_HEIGHT = 14f;
    private const float SECTION_TO_BODY = 3f;
    private const float FIRST_SECTION_GAP = 3.5f;
    private const float SECTION_GAP = 5f;
    private const float BODY_TO_SECTION_GAP = 4f;
    private const float BODY_LINE_HEIGHT = 9f;
    private const string TITLE_INK = "#373541";
    private const string BODY_INK = "#3A3A3A";
    private const string SECTION_INK = "#D5D2B8";
    private const string SECTION_FILL = "#BB9165";
    private const string SECTION_SPRITE = "vanilla_chem_ext:notebook_manual_section_2x";
    private const string PAGE_INDICATOR_INK = "#79746E";
    private const float TABLE_DEFAULT_ROW_HEIGHT = 11f;
    private const float TABLE_DEFAULT_ROW_GAP = 1f;
    private const float TABLE_DEFAULT_CELL_GAP = 1f;
    private const float TABLE_DEFAULT_FONT_SIZE = 6f;
    private const float TABLE_DEFAULT_PADDING = 3f;
    private const string ICON_PREFIX = "[[icon:";
    private const string ICON_SUFFIX = "]]";
    private const string IMAGE_PREFIX = "[[image:";
    private const string OVERLAY_PREFIX = "[[overlay:";
    private const string TEXT_PREFIX = "[[text:";
    private const string SECTION_PREFIX = "[[section:";
    private const string GAP_PREFIX = "[[gap:";
    private const string TABLE_PREFIX = "[[table:";
    private const string ROW_PREFIX = "[[row:";
    private const string END_TABLE = "[[/table]]";
    private const string LINE_BREAK = "[[br]]";
    private const string PAGE_BREAK = "[[page]]";
    private const string OMIT = "[[omit]]";

    private static bool themeRegistered;
    private static readonly Dictionary<string, Sprite> CrispSprites =
        new Dictionary<string, Sprite>();

    // Mod localization is only loaded after OnEnable, so the book is (re)built once it is available.
    public static void Register()
    {
        try
        {
            PaperBookData book = PaperStarters.NewBook("blank", BOOK_ID, Localized("guide_book_name"));
            book.designWidth = DESIGN_WIDTH;
            book.designHeight = DESIGN_HEIGHT;
            book.width = DISPLAY_WIDTH;
            book.height = DISPLAY_HEIGHT;
            book.background = "notebook_manual";
            ConfigureChrome(book);
            book.pages.Clear();
            string bookTitle = Localized("guide_book_name");
            AddPage(book, bookTitle, null, Localized("guide_book_intro"), true);
            foreach (HandNote note in Notes.All)
            {
                AddNote(
                    book,
                    bookTitle,
                    Localized(note.NameKey),
                    Lines(DreamToxinMod.MOD_ID, note.LineKeys));
            }
            AddExternalSections(book);
            // No PaperItemData: the paper registry would add the item to the directory only after every
            // other mod (item spawners included) has read it. CreateItem registers it with our other items.
            book.item = null;
            ModHelper.RegisterPaperBook(DreamToxinMod.MOD_ID, book);
            if (!themeRegistered)
            {
                PaperBookRegistry.OnOpened(
                    PaperBookRegistry.FullId(DreamToxinMod.MOD_ID, BOOK_ID),
                    ApplyPaperTheme);
                themeRegistered = true;
            }
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
                AddNote(book, Localized("guide_book_name"), title, lines);
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
        string continuationPrefix;
        int lastNumber;
        bool allKeysNumbered;
        FindNumberedContinuation(
            keys,
            out continuationPrefix,
            out lastNumber,
            out allKeysNumbered);
        if (continuationPrefix != null && allKeysNumbered)
        {
            string introKey = continuationPrefix.Substring(
                0,
                continuationPrefix.Length - 1);
            string intro = GetLine(modId, introKey);
            if (intro != introKey)
            {
                lines.Add(intro);
            }
        }
        foreach (string key in keys)
        {
            string line = GetLine(modId, key);
            if (line != key)
            {
                lines.Add(line);
            }
        }
        if (continuationPrefix != null)
        {
            for (int number = lastNumber + 1; number <= 500; number++)
            {
                string key = continuationPrefix + number.ToString(
                    CultureInfo.InvariantCulture);
                string line = GetLine(modId, key);
                if (line == key)
                {
                    break;
                }
                lines.Add(line);
            }
        }
        return lines;
    }

    private static string GetLine(string modId, string key)
    {
        return modId == DreamToxinMod.MOD_ID
            ? Localized(key)
            : ModHelper.GetLocalized(modId, key);
    }

    private static void FindNumberedContinuation(
        string[] keys,
        out string prefix,
        out int lastNumber,
        out bool allKeysNumbered)
    {
        prefix = null;
        lastNumber = 0;
        allKeysNumbered = false;
        bool complete = true;
        for (int index = keys.Length - 1; index >= 0; index--)
        {
            string candidatePrefix;
            int number;
            if (!TryNumberedKey(keys[index], out candidatePrefix, out number))
            {
                complete = false;
                break;
            }
            if (prefix != null
                && !string.Equals(prefix, candidatePrefix, StringComparison.Ordinal))
            {
                complete = false;
                break;
            }
            prefix = candidatePrefix;
            lastNumber = Math.Max(lastNumber, number);
        }
        allKeysNumbered = complete && prefix != null && lastNumber > 0;
    }

    private static bool TryNumberedKey(
        string key,
        out string prefix,
        out int number)
    {
        prefix = null;
        number = 0;
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }
        int underscore = key.LastIndexOf('_');
        if (underscore < 0 || underscore + 1 >= key.Length)
        {
            return false;
        }
        string suffix = key.Substring(underscore + 1);
        for (int index = 0; index < suffix.Length; index++)
        {
            if (suffix[index] < '0' || suffix[index] > '9')
            {
                return false;
            }
        }
        if (!int.TryParse(
            suffix,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out number))
        {
            return false;
        }
        prefix = key.Substring(0, underscore + 1);
        return true;
    }

    // Handbook-only layout markers must not leak into the readable note item.
    public static string StripLayoutMarkup(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }
        string[] source = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        List<string> lines = new List<string>();
        for (int index = 0; index < source.Length; index++)
        {
            string trimmed = source[index].Trim();
            string sectionTitle = SectionMarkerText(trimmed);
            if (sectionTitle != null)
            {
                lines.Add(sectionTitle);
                continue;
            }
            string styledText = TextMarkerText(trimmed);
            if (styledText != null)
            {
                lines.Add(styledText);
                continue;
            }
            string tableRowText = TableRowMarkerText(trimmed);
            if (tableRowText != null)
            {
                lines.Add(tableRowText);
                continue;
            }
            if (IsLayoutMarker(trimmed))
            {
                continue;
            }
            lines.Add(source[index]);
        }
        return string.Join("\n", lines.ToArray());
    }

    private static bool IsLayoutMarker(string text)
    {
        return string.Equals(text, PAGE_BREAK, StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, LINE_BREAK, StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, OMIT, StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, END_TABLE, StringComparison.OrdinalIgnoreCase)
            || MarkerPayload(text, ICON_PREFIX) != null
            || MarkerPayload(text, IMAGE_PREFIX) != null
            || MarkerPayload(text, OVERLAY_PREFIX) != null
            || MarkerPayload(text, TEXT_PREFIX) != null
            || MarkerPayload(text, GAP_PREFIX) != null
            || MarkerPayload(text, TABLE_PREFIX) != null;
    }

    private static string SectionMarkerText(string text)
    {
        string marker = MarkerPayload(text, SECTION_PREFIX);
        if (marker == null)
        {
            return null;
        }
        string title;
        ParseMarkerOptions(marker, out title);
        return title;
    }

    private static string TextMarkerText(string text)
    {
        string marker = MarkerPayload(text, TEXT_PREFIX);
        if (marker == null)
        {
            return null;
        }
        string value;
        ParseMarkerOptions(marker, out value);
        return value;
    }

    private static string TableRowMarkerText(string text)
    {
        string marker = MarkerPayload(text, ROW_PREFIX);
        if (marker == null)
        {
            return null;
        }
        TableRow row = ParseTableRow(marker);
        return row.Cells.Count == 0
            ? string.Empty
            : string.Join(" | ", row.Cells.ToArray());
    }

    private static string Localized(string key)
    {
        string value = null;
        LoadedMod own = null;
        foreach (LoadedMod mod in ModLoader.Mods)
        {
            if (mod != null && mod.Manifest != null
                && mod.Manifest.ID == DreamToxinMod.MOD_ID)
            {
                own = mod;
                break;
            }
        }
        if (own == null || own.Localization == null)
        {
            value = ModHelper.GetLocalized(DreamToxinMod.MOD_ID, key);
            return string.IsNullOrEmpty(value) || value == key ? key : value;
        }

        string code = LocHelper.GetCurrentLocaleCode();
        value = FindLocalization(own.Localization, code, key);
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }
        int dash = string.IsNullOrEmpty(code) ? -1 : code.IndexOf('-');
        if (dash > 0)
        {
            value = FindLocalization(own.Localization, code.Substring(0, dash), key);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }
        if (IsChineseCode(code))
        {
            value = FindLocalization(own.Localization, "zh", key);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }
        else if (IsEnglishCode(code))
        {
            value = FindLocalization(own.Localization, "en", key);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        value = ModHelper.GetLocalized(DreamToxinMod.MOD_ID, key);
        if (!string.IsNullOrEmpty(value) && value != key)
        {
            return value;
        }
        value = FindLocalization(own.Localization, "en", key);
        return string.IsNullOrEmpty(value) ? key : value;
    }

    private static bool IsChineseCode(string code)
    {
        return !string.IsNullOrEmpty(code)
            && (code.IndexOf("zh", StringComparison.OrdinalIgnoreCase) >= 0
                || code.IndexOf("chinese", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool IsEnglishCode(string code)
    {
        return !string.IsNullOrEmpty(code)
            && (code.IndexOf("en", StringComparison.OrdinalIgnoreCase) >= 0
                || code.IndexOf("english", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string FindLocalization(
        Dictionary<string, Dictionary<string, string>> tables,
        string locale,
        string key)
    {
        if (string.IsNullOrEmpty(locale))
        {
            return null;
        }
        Dictionary<string, string> table;
        if (tables.TryGetValue(locale, out table) && table != null)
        {
            string value;
            if (table.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }
        }
        foreach (KeyValuePair<string, Dictionary<string, string>> pair in tables)
        {
            if (!string.Equals(pair.Key, locale, StringComparison.OrdinalIgnoreCase)
                || pair.Value == null)
            {
                continue;
            }
            string value;
            if (pair.Value.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }
        }
        return null;
    }

    private static void AddNote(
        PaperBookData book,
        string bookTitle,
        string pageTitle,
        List<string> lines)
    {
        List<PageChunk> chunks = Paginate(lines);
        for (int index = 0; index < chunks.Count; index++)
        {
            string title = chunks.Count == 1
                ? pageTitle
                : pageTitle + PaginationMarker(index);
            AddPage(book, bookTitle, title, chunks[index].Sections, false);
        }
    }

    private static void AddPage(
        PaperBookData book,
        string bookTitle,
        string pageTitle,
        string body,
        bool firstPage)
    {
        List<string> lines = new List<string>(
            body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
        AddNote(book, bookTitle, pageTitle, lines);
    }

    private static void ConfigureChrome(PaperBookData book)
    {
        if (book.chrome == null)
        {
            return;
        }
        for (int index = 0; index < book.chrome.Count; index++)
        {
            PaperElementData element = book.chrome[index];
            if (element == null)
            {
                continue;
            }
            if (element.id == "close" || element.action == "close")
            {
                element.x = 290f;
                element.y = 10f;
                element.w = 14f;
                element.h = 15f;
                element.sprite = "paper_buttons_close1";
                element.fit = "native";
                element.scale = 1f;
            }
            else if (element.id == "zoom" || element.action == "zoom")
            {
                element.x = 266.5f;
                element.y = 11f;
                element.w = 14f;
                element.h = 15f;
                element.sprite = "paper_buttons_screen_0";
                element.fit = "native";
                element.scale = 1f;
            }
            else if (element.id == "page_indicator"
                || element.action == "page_indicator")
            {
                element.style = "page_number";
                element.fontSize = 15f;
                element.color = PAGE_INDICATOR_INK;
                element.align = "center";
                element.valign = "middle";
                element.raycast = false;
            }
        }
    }

    private static void AddPage(
        PaperBookData book,
        string bookTitle,
        string pageTitle,
        List<SectionBlock> sections,
        bool firstPage)
    {
        PaperPageData page = new PaperPageData();
        page.name = pageTitle ?? bookTitle;
        page.elements.Add(TextElement(
            "book_title",
            bookTitle,
            "header",
            12.5f,
            TITLE_INK,
            TITLE_LEFT,
            TITLE_TOP,
            TITLE_WIDTH,
            TITLE_HEIGHT,
            true,
            "center",
            "middle"));
        if (!firstPage && !string.IsNullOrEmpty(pageTitle))
        {
            page.elements.Add(TextElement(
                "page_title",
                pageTitle,
                "header",
                9.5f,
                TITLE_INK,
                TITLE_LEFT,
                SUBTITLE_TOP,
                TITLE_WIDTH,
                SUBTITLE_HEIGHT,
                true,
                "center",
                "middle"));
        }

        float currentY = firstPage ? COVER_CONTENT_TOP : CONTENT_TOP;
        if (!firstPage
            && sections.Count > 0
            && !string.IsNullOrEmpty(sections[0].Heading))
        {
            currentY += FIRST_SECTION_GAP;
        }
        for (int index = 0; index < sections.Count; index++)
        {
            SectionBlock section = sections[index];
            float headingHeight = section.HeadingHeight;
            float iconGap = 0f;
            if (!string.IsNullOrEmpty(section.IconKey))
            {
                headingHeight = Math.Max(headingHeight, section.IconHeight);
                iconGap = Math.Max(0f, section.IconGap);
                float iconX = section.IconXSet
                    ? section.IconX
                    : section.HeadingX - 28f;
                float iconY = section.IconYSet
                    ? section.IconY
                    : currentY - 5f;
                page.elements.Add(ImageElement(
                    "section_icon_" + index,
                    section.IconKey,
                    section.IconColor,
                    iconX,
                    iconY,
                    section.IconWidth,
                    section.IconHeight,
                    section.IconFit));
            }
            if (!string.IsNullOrEmpty(section.Heading))
            {
                page.elements.Add(ImageElement(
                    "section_texture_" + index,
                    SECTION_SPRITE,
                    section.HeadingFill,
                    section.HeadingX,
                    currentY,
                    section.HeadingWidth,
                    section.HeadingHeight,
                    "stretch"));
                page.elements.Add(TextElement(
                    "section_" + index,
                    section.Heading,
                    "caption",
                    10f,
                    section.HeadingInk,
                    section.HeadingX + 8f,
                    currentY + 2f,
                    section.HeadingWidth - 16f,
                    10f,
                    false,
                    section.HeadingAlign,
                    "middle"));
            }
            if (headingHeight > 0f)
            {
                currentY += headingHeight + iconGap + section.HeadingToBody;
            }

            List<string> textLines = new List<string>();
            for (int nodeIndex = 0; nodeIndex < section.Nodes.Count; nodeIndex++)
            {
                ContentNode node = section.Nodes[nodeIndex];
                if (node.Kind == ContentKind.Text)
                {
                    textLines.Add(node.Text ?? string.Empty);
                    continue;
                }
                AddBodyText(
                    page,
                    "section_body_" + index + "_" + nodeIndex,
                    textLines,
                    ref currentY);
                if (node.Kind == ContentKind.Label)
                {
                    AddLabel(page, "section_label_" + index + "_" + nodeIndex, node, ref currentY);
                }
                else if (node.Kind == ContentKind.Image)
                {
                    float imageY = node.YSet ? node.Y : currentY;
                    page.elements.Add(ImageElement(
                        "section_image_" + index + "_" + nodeIndex,
                        node.Sprite,
                        node.Color,
                        ContentImageX(node),
                        imageY,
                        node.Width,
                        node.Height,
                        node.Fit));
                    if (!node.YSet)
                    {
                        currentY += node.Height + node.Gap;
                    }
                }
                else if (node.Kind == ContentKind.Float)
                {
                    AddFloatBlock(
                        page,
                        "section_float_" + index + "_" + nodeIndex,
                        node,
                        ref currentY);
                }
                else if (node.Kind == ContentKind.Table)
                {
                    AddTable(
                        page,
                        "section_table_" + index + "_" + nodeIndex,
                        node.Table,
                        ref currentY);
                }
                else if (node.Kind == ContentKind.Gap)
                {
                    currentY += node.Gap;
                }
            }
            AddBodyText(
                page,
                "section_body_" + index + "_end",
                textLines,
                ref currentY);

            if (index + 1 < sections.Count)
            {
                currentY += SectionSpacing(section, sections[index + 1]);
            }
        }

        book.pages.Add(page);
    }

    private static void AddLabel(
        PaperPageData page,
        string id,
        ContentNode node,
        ref float currentY)
    {
        float width = node.Width > 0f ? node.Width : BODY_WIDTH;
        float height = EstimateVisualLines(node.Text, width) * BODY_LINE_HEIGHT;
        page.elements.Add(TextElement(
            id,
            node.Text,
            node.Style,
            node.FontSize > 0f ? node.FontSize : SelectBodyFontSize(node.Text),
            node.Color,
            node.XSet ? node.X : BODY_LEFT,
            currentY,
            width,
            height,
            true,
            node.Align,
            "top"));
        currentY += height + node.Gap;
    }

    private static void AddFloatBlock(
        PaperPageData page,
        string id,
        ContentNode node,
        ref float currentY)
    {
        float horizontalGap = Math.Max(0f, node.Gap);
        float imageX = node.XSet ? node.X : FLOAT_IMAGE_LEFT;
        float textX = Math.Max(
            BODY_LEFT,
            imageX + node.Width + horizontalGap);
        float textWidth = Math.Max(
            1f,
            BODY_LEFT + BODY_WIDTH - textX);
        page.elements.Add(ImageElement(
            id + "_image",
            node.Sprite,
            node.Color,
            imageX,
            currentY,
            node.Width,
            node.Height,
            node.Fit));
        for (int index = 0; index < node.Overlays.Count; index++)
        {
            ImageLayer layer = node.Overlays[index];
            page.elements.Add(ImageElement(
                id + "_overlay_" + index,
                layer.Sprite,
                layer.Color,
                imageX + layer.X,
                currentY + layer.Y,
                layer.Width,
                layer.Height,
                layer.Fit));
        }

        float textHeight = 0f;
        if (!string.IsNullOrEmpty(node.Text))
        {
            textHeight =
                EstimateVisualLines(node.Text, textWidth) * BODY_LINE_HEIGHT;
            page.elements.Add(TextElement(
                id + "_text",
                node.Text,
                "title",
                SelectBodyFontSize(node.Text),
                BODY_INK,
                textX,
                currentY,
                textWidth,
                textHeight,
                true,
                "left",
                "top"));
        }
        currentY += Math.Max(node.Height, textHeight);
    }

    private static void AddTable(
        PaperPageData page,
        string id,
        TableData table,
        ref float currentY)
    {
        if (table == null || table.Rows.Count == 0)
        {
            return;
        }

        int columns = TableColumnCount(table);
        float tableX = table.XSet ? table.X : BODY_LEFT;
        float tableWidth = table.Width > 0f ? table.Width : BODY_WIDTH;
        float cellWidth = Math.Max(
            1f,
            (tableWidth - table.CellGap * Math.Max(0, columns - 1)) / columns);
        float rowY = currentY;
        for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            TableRow row = table.Rows[rowIndex];
            float rowHeight = TableRowHeight(table, row);
            for (int columnIndex = 0; columnIndex < columns; columnIndex++)
            {
                float cellX = tableX
                    + columnIndex * (cellWidth + table.CellGap);
                string cellId = id + "_r" + rowIndex + "_c" + columnIndex;
                page.elements.Add(ImageElement(
                    cellId + "_fill",
                    SECTION_SPRITE,
                    TableCellFill(table, row, rowIndex, columnIndex),
                    cellX,
                    rowY,
                    cellWidth,
                    rowHeight,
                    "stretch"));
                if (columnIndex >= row.Cells.Count)
                {
                    continue;
                }
                string cell = row.Cells[columnIndex] ?? string.Empty;
                if (row.Header
                    && cell.IndexOf("<b>", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    cell = "<b>" + cell + "</b>";
                }
                if (cell.Length == 0)
                {
                    continue;
                }
                page.elements.Add(TextElement(
                    cellId + "_text",
                    cell,
                    "title",
                    table.FontSize,
                    TableCellColor(table, row, rowIndex, columnIndex),
                    cellX + table.Padding,
                    rowY + 0.5f,
                    Math.Max(1f, cellWidth - table.Padding * 2f),
                    Math.Max(1f, rowHeight - 1f),
                    true,
                    TableCellAlign(table, row, rowIndex, columnIndex),
                    "middle"));
            }
            rowY += rowHeight + table.RowGap;
        }
        currentY = rowY - table.RowGap;
    }

    private static int TableColumnCount(TableData table)
    {
        int columns = 1;
        for (int index = 0; index < table.Rows.Count; index++)
        {
            columns = Math.Max(columns, table.Rows[index].Cells.Count);
        }
        return columns;
    }

    private static float TableCost(TableData table)
    {
        if (table == null || table.Rows.Count == 0)
        {
            return 0f;
        }
        float height = 0f;
        for (int index = 0; index < table.Rows.Count; index++)
        {
            height += TableRowHeight(table, table.Rows[index]);
        }
        return height + Math.Max(0, table.Rows.Count - 1) * table.RowGap;
    }

    private static float TableRowHeight(TableData table, TableRow row)
    {
        return row.HeightSet ? row.Height : table.RowHeight;
    }

    private static string TableCellFill(
        TableData table,
        TableRow row,
        int rowIndex,
        int columnIndex)
    {
        string value = ValueAt(row.Fills, columnIndex);
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }
        if (!string.IsNullOrEmpty(row.Fill))
        {
            return row.Fill;
        }
        value = ValueAt(table.ColumnFills, columnIndex);
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }
        return row.Header ? "#6E5A45" : "#D9C7A3";
    }

    private static string TableCellColor(
        TableData table,
        TableRow row,
        int rowIndex,
        int columnIndex)
    {
        string value = ValueAt(row.Colors, columnIndex);
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }
        if (!string.IsNullOrEmpty(row.Color))
        {
            return row.Color;
        }
        value = ValueAt(table.ColumnColors, columnIndex);
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }
        return row.Header ? "#F6EED7" : BODY_INK;
    }

    private static string TableCellAlign(
        TableData table,
        TableRow row,
        int rowIndex,
        int columnIndex)
    {
        string value = ValueAt(row.Aligns, columnIndex);
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }
        value = ValueAt(table.Aligns, columnIndex);
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }
        return columnIndex == 0 ? "left" : "center";
    }

    private static void AddBodyText(
        PaperPageData page,
        string id,
        List<string> lines,
        ref float currentY)
    {
        if (lines.Count == 0)
        {
            return;
        }
        string text = string.Join("\n", lines.ToArray());
        lines.Clear();
        if (text.Length == 0)
        {
            return;
        }
        float height = EstimateVisualLines(text) * BODY_LINE_HEIGHT;
        page.elements.Add(TextElement(
            id,
            text,
            "title",
            SelectBodyFontSize(text),
            BODY_INK,
            BODY_LEFT,
            currentY,
            BODY_WIDTH,
            height,
            true,
            "left",
            "top"));
        currentY += height;
    }

    private static float ContentImageX(ContentNode image)
    {
        if (image.XSet)
        {
            return image.X;
        }
        if (string.Equals(image.Align, "left", StringComparison.OrdinalIgnoreCase))
        {
            return BODY_LEFT;
        }
        if (string.Equals(image.Align, "right", StringComparison.OrdinalIgnoreCase))
        {
            return BODY_LEFT + BODY_WIDTH - image.Width;
        }
        return BODY_LEFT + (BODY_WIDTH - image.Width) * 0.5f;
    }

    private static PaperElementData TextElement(
        string id,
        string text,
        string style,
        float fontSize,
        string color,
        float x,
        float y,
        float width,
        float height,
        bool wrap,
        string align,
        string valign)
    {
        PaperElementData element = new PaperElementData();
        element.type = "text";
        element.id = id;
        element.text = text;
        element.style = style;
        element.fontSize = fontSize;
        element.color = color;
        element.x = x;
        element.y = y;
        element.w = width;
        element.h = height;
        element.wrap = wrap;
        element.align = align;
        element.valign = valign;
        return element;
    }

    private static PaperElementData ImageElement(
        string id,
        string sprite,
        string color,
        float x,
        float y,
        float width,
        float height,
        string fit)
    {
        PaperElementData element = new PaperElementData();
        element.type = "image";
        element.id = id;
        element.sprite = sprite;
        element.color = color;
        element.fit = string.IsNullOrEmpty(fit) ? "fit" : fit;
        element.x = x;
        element.y = y;
        element.w = width;
        element.h = height;
        return element;
    }

    private static List<PageChunk> Paginate(List<string> lines)
    {
        List<SectionBlock> source = SplitSections(lines);
        float capacity = CONTENT_BOTTOM - CONTENT_TOP;
        List<PageChunk> pages = new List<PageChunk>();
        PageChunk page = new PageChunk();
        float used = 0f;

        for (int sectionIndex = 0; sectionIndex < source.Count; sectionIndex++)
        {
            SectionBlock section = source[sectionIndex];
            if (section.ForcePageBreakBefore && page.Sections.Count > 0)
            {
                FlushPage(pages, ref page, ref used);
            }

            SectionBlock open = null;
            if (section.HasHeading)
            {
                float firstCost = section.Nodes.Count > 0
                    ? ContentCost(section.Nodes[0])
                    : 0f;
                float spacing = page.Sections.Count > 0
                    ? SectionSpacing(
                        page.Sections[page.Sections.Count - 1],
                        section)
                    : 0f;
                float headingCost = section.HeadingCost;
                if (page.Sections.Count > 0
                    && used + spacing + headingCost + firstCost > capacity)
                {
                    FlushPage(pages, ref page, ref used);
                    spacing = 0f;
                }
                open = section.CreateFrame();
                page.Sections.Add(open);
                used += spacing + headingCost;
            }

            for (int nodeIndex = 0; nodeIndex < section.Nodes.Count; nodeIndex++)
            {
                ContentNode node = section.Nodes[nodeIndex];
                float cost = ContentCost(node);
                if (open == null)
                {
                    float spacing = page.Sections.Count > 0
                        ? SectionSpacing(
                            page.Sections[page.Sections.Count - 1],
                            section)
                        : 0f;
                    if (page.Sections.Count > 0
                        && used + spacing + cost > capacity)
                    {
                        FlushPage(pages, ref page, ref used);
                        spacing = 0f;
                    }
                    open = section.CreateBodyFrame();
                    page.Sections.Add(open);
                    used += spacing;
                }
                else if (open.Nodes.Count > 0 && used + cost > capacity)
                {
                    FlushPage(pages, ref page, ref used);
                    open = null;
                    nodeIndex--;
                    continue;
                }

                open.Nodes.Add(node);
                used += cost;
                if (used >= capacity && nodeIndex + 1 < section.Nodes.Count)
                {
                    FlushPage(pages, ref page, ref used);
                    open = null;
                }
            }
        }

        if (page.Sections.Count > 0)
        {
            pages.Add(page);
        }
        if (pages.Count == 0)
        {
            pages.Add(new PageChunk());
        }
        return pages;
    }

    private static void FlushPage(
        List<PageChunk> pages,
        ref PageChunk page,
        ref float used)
    {
        if (page.Sections.Count > 0)
        {
            pages.Add(page);
        }
        page = new PageChunk();
        used = 0f;
    }

    private static float ContentCost(ContentNode node)
    {
        if (node.Kind == ContentKind.Text)
        {
            return EstimateVisualLines(node.Text) * BODY_LINE_HEIGHT;
        }
        if (node.Kind == ContentKind.Label)
        {
            float width = node.Width > 0f ? node.Width : BODY_WIDTH;
            return EstimateVisualLines(node.Text, width) * BODY_LINE_HEIGHT
                + node.Gap;
        }
        if (node.Kind == ContentKind.Gap)
        {
            return node.Gap;
        }
        if (node.Kind == ContentKind.Float)
        {
            float imageX = node.XSet ? node.X : FLOAT_IMAGE_LEFT;
            float textX = Math.Max(
                BODY_LEFT,
                imageX + node.Width + Math.Max(0f, node.Gap));
            float textWidth = Math.Max(1f, BODY_LEFT + BODY_WIDTH - textX);
            float textHeight =
                EstimateVisualLines(node.Text, textWidth) * BODY_LINE_HEIGHT;
            return Math.Max(node.Height, textHeight);
        }
        if (node.Kind == ContentKind.Table)
        {
            return TableCost(node.Table);
        }
        return node.YSet ? 0f : node.Height + node.Gap;
    }

    private static float SectionSpacing(
        SectionBlock previous,
        SectionBlock next)
    {
        float spacing = SECTION_GAP;
        if (HasVisibleBody(previous)
            && next != null
            && next.HasHeading)
        {
            spacing += BODY_TO_SECTION_GAP;
        }
        return spacing;
    }

    private static bool HasVisibleBody(SectionBlock section)
    {
        if (section == null || section.Nodes.Count == 0)
        {
            return false;
        }
        for (int index = 0; index < section.Nodes.Count; index++)
        {
            if (section.Nodes[index].Kind != ContentKind.Gap)
            {
                return true;
            }
        }
        return false;
    }

    private static List<SectionBlock> SplitSections(List<string> lines)
    {
        List<SectionBlock> sections = new List<SectionBlock>();
        SectionBlock current = new SectionBlock();
        string pendingIcon = null;
        ImageOptions pendingIconOptions = null;
        ContentNode floatNode = null;
        ContentNode tableNode = null;
        List<string> physicalLines = new List<string>();
        for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            string value = lines[lineIndex] ?? string.Empty;
            physicalLines.AddRange(
                value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
        }

        for (int index = 0; index < physicalLines.Count; index++)
        {
            string line = physicalLines[index];
            string trimmed = line.Trim();
            if (string.Equals(trimmed, OMIT, StringComparison.OrdinalIgnoreCase))
            {
                floatNode = null;
                tableNode = null;
                continue;
            }
            if (string.Equals(trimmed, PAGE_BREAK, StringComparison.OrdinalIgnoreCase))
            {
                floatNode = null;
                tableNode = null;
                if (current.HasContent)
                {
                    sections.Add(current);
                }
                current = new SectionBlock();
                current.ForcePageBreakBefore = true;
                continue;
            }
            if (string.Equals(trimmed, LINE_BREAK, StringComparison.OrdinalIgnoreCase))
            {
                floatNode = null;
                tableNode = null;
                AttachIcon(current, ref pendingIcon, ref pendingIconOptions);
                ContentNode breakNode = new ContentNode();
                breakNode.Kind = ContentKind.Gap;
                breakNode.Gap = BODY_LINE_HEIGHT;
                current.Nodes.Add(breakNode);
                continue;
            }
            if (string.Equals(trimmed, END_TABLE, StringComparison.OrdinalIgnoreCase))
            {
                floatNode = null;
                tableNode = null;
                continue;
            }

            string marker = MarkerPayload(trimmed, ICON_PREFIX);
            if (marker != null)
            {
                floatNode = null;
                tableNode = null;
                ImageOptions icon = ParseImageOptions(marker, 24f, 24f);
                if (icon != null && !string.IsNullOrEmpty(icon.Sprite))
                {
                    pendingIcon = icon.Sprite;
                    pendingIconOptions = icon;
                }
                continue;
            }

            marker = MarkerPayload(trimmed, SECTION_PREFIX);
            if (marker != null)
            {
                floatNode = null;
                tableNode = null;
                if (current.HasContent)
                {
                    sections.Add(current);
                    current = new SectionBlock();
                }
                ApplySectionMarker(current, marker);
                AttachIcon(current, ref pendingIcon, ref pendingIconOptions);
                continue;
            }

            marker = MarkerPayload(trimmed, TABLE_PREFIX);
            if (marker != null)
            {
                floatNode = null;
                AttachIcon(current, ref pendingIcon, ref pendingIconOptions);
                tableNode = new ContentNode();
                tableNode.Kind = ContentKind.Table;
                tableNode.Table = new TableData();
                ApplyTableMarker(tableNode.Table, marker);
                current.Nodes.Add(tableNode);
                continue;
            }

            marker = MarkerPayload(trimmed, ROW_PREFIX);
            if (marker != null)
            {
                floatNode = null;
                AttachIcon(current, ref pendingIcon, ref pendingIconOptions);
                if (tableNode == null)
                {
                    tableNode = new ContentNode();
                    tableNode.Kind = ContentKind.Table;
                    tableNode.Table = new TableData();
                    current.Nodes.Add(tableNode);
                }
                tableNode.Table.Rows.Add(ParseTableRow(marker));
                continue;
            }

            marker = MarkerPayload(trimmed, OVERLAY_PREFIX);
            if (marker != null)
            {
                tableNode = null;
                if (floatNode != null)
                {
                    ImageOptions overlay = ParseImageOptions(marker, 16f, 16f);
                    if (overlay != null)
                    {
                        ImageLayer layer = new ImageLayer();
                        layer.Sprite = overlay.Sprite;
                        layer.Color = overlay.Color;
                        layer.Fit = overlay.Fit;
                        layer.X = overlay.X;
                        layer.Y = overlay.Y;
                        layer.Width = overlay.Width;
                        layer.Height = overlay.Height;
                        floatNode.Overlays.Add(layer);
                    }
                }
                continue;
            }

            marker = MarkerPayload(trimmed, TEXT_PREFIX);
            if (marker != null)
            {
                floatNode = null;
                tableNode = null;
                AttachIcon(current, ref pendingIcon, ref pendingIconOptions);
                ContentNode labelNode = new ContentNode();
                labelNode.Kind = ContentKind.Label;
                ApplyTextMarker(labelNode, marker);
                if (!string.IsNullOrEmpty(labelNode.Text))
                {
                    current.Nodes.Add(labelNode);
                }
                continue;
            }

            marker = MarkerPayload(trimmed, IMAGE_PREFIX);
            if (marker != null)
            {
                ImageOptions image = ParseImageOptions(marker, 48f, 48f);
                if (image == null)
                {
                    continue;
                }
                floatNode = null;
                tableNode = null;
                AttachIcon(current, ref pendingIcon, ref pendingIconOptions);
                ContentNode imageNode = new ContentNode();
                imageNode.Kind = image.FloatLeft
                    ? ContentKind.Float
                    : ContentKind.Image;
                imageNode.Sprite = image.Sprite;
                imageNode.Color = image.Color;
                imageNode.Fit = image.Fit;
                imageNode.Align = image.Align;
                imageNode.X = image.X;
                imageNode.XSet = image.XSet;
                imageNode.Y = image.Y;
                imageNode.YSet = image.YSet;
                imageNode.Width = image.Width;
                imageNode.Height = image.Height;
                imageNode.Gap = image.Gap;
                imageNode.FloatLeft = image.FloatLeft;
                current.Nodes.Add(imageNode);
                if (image.FloatLeft)
                {
                    floatNode = imageNode;
                }
                continue;
            }

            marker = MarkerPayload(trimmed, GAP_PREFIX);
            if (marker != null)
            {
                floatNode = null;
                tableNode = null;
                AttachIcon(current, ref pendingIcon, ref pendingIconOptions);
                ContentNode gapNode = new ContentNode();
                gapNode.Kind = ContentKind.Gap;
                gapNode.Gap = ParseFloat(marker, 6f);
                current.Nodes.Add(gapNode);
                continue;
            }

            tableNode = null;
            if (floatNode != null)
            {
                if (floatNode.Text == null)
                {
                    floatNode.Text = line;
                }
                else
                {
                    floatNode.Text += "\n" + line;
                }
                continue;
            }
            AttachIcon(current, ref pendingIcon, ref pendingIconOptions);
            ContentNode textNode = new ContentNode();
            textNode.Kind = ContentKind.Text;
            textNode.Text = line;
            current.Nodes.Add(textNode);
        }
        if (pendingIcon != null)
        {
            if (current.HasContent)
            {
                sections.Add(current);
                current = new SectionBlock();
            }
            AttachIcon(current, ref pendingIcon, ref pendingIconOptions);
        }
        if (current.HasContent)
        {
            sections.Add(current);
        }
        if (sections.Count == 0)
        {
            sections.Add(new SectionBlock());
        }
        return sections;
    }

    private static void AttachIcon(
        SectionBlock section,
        ref string iconKey,
        ref ImageOptions options)
    {
        if (iconKey == null || section.IconKey != null)
        {
            return;
        }
        section.IconKey = iconKey;
        if (options != null)
        {
            section.IconX = options.X;
            section.IconXSet = options.XSet;
            section.IconY = options.Y;
            section.IconYSet = options.YSet;
            section.IconWidth = options.Width;
            section.IconHeight = options.Height;
            section.IconGap = options.Gap;
            section.IconColor = options.Color;
            section.IconFit = options.Fit;
        }
        iconKey = null;
        options = null;
    }

    private static void ApplySectionMarker(SectionBlock section, string payload)
    {
        string title;
        Dictionary<string, string> options = ParseMarkerOptions(payload, out title);
        section.Heading = title;
        section.HeadingX = ParseFloat(GetOption(options, "x"), section.HeadingX);
        section.HeadingWidth = ParseFloat(
            GetOption(options, "w"),
            section.HeadingWidth);
        section.HeadingHeight = ParseFloat(
            GetOption(options, "h"),
            section.HeadingHeight);
        section.HeadingToBody = ParseFloat(
            GetOption(options, "gap"),
            section.HeadingToBody);
        section.HeadingAlign = GetOption(options, "align") ?? section.HeadingAlign;
        section.HeadingInk = GetOption(options, "color") ?? section.HeadingInk;
        section.HeadingFill = GetOption(options, "fill") ?? section.HeadingFill;
    }

    private static void ApplyTextMarker(ContentNode node, string payload)
    {
        string text;
        Dictionary<string, string> options = ParseMarkerOptions(payload, out text);
        node.Text = text;
        node.Style = GetOption(options, "style") ?? "body";
        node.Color = GetOption(options, "color") ?? BODY_INK;
        node.Align = GetOption(options, "align") ?? "left";
        node.FontSize = ParseFloat(GetOption(options, "size"), 0f);
        node.Width = ParseFloat(GetOption(options, "w"), BODY_WIDTH);
        node.Gap = ParseFloat(GetOption(options, "gap"), 0f);
        string x = GetOption(options, "x");
        if (x != null)
        {
            node.X = ParseFloat(x, BODY_LEFT);
            node.XSet = true;
        }
    }

    private static void ApplyTableMarker(TableData table, string payload)
    {
        Dictionary<string, string> options = ParseOptionParts(payload);
        string x = GetOption(options, "x");
        if (x != null)
        {
            table.X = ParseFloat(x, BODY_LEFT);
            table.XSet = true;
        }
        table.Width = ParseFloat(GetOption(options, "w"), BODY_WIDTH);
        table.RowHeight = ParseFloat(
            GetOption(options, "rowh"),
            TABLE_DEFAULT_ROW_HEIGHT);
        table.RowGap = ParseFloat(
            GetOption(options, "rowgap"),
            TABLE_DEFAULT_ROW_GAP);
        table.CellGap = ParseFloat(
            GetOption(options, "cellgap"),
            TABLE_DEFAULT_CELL_GAP);
        table.FontSize = ParseFloat(
            GetOption(options, "size"),
            TABLE_DEFAULT_FONT_SIZE);
        table.Padding = ParseFloat(
            GetOption(options, "padding"),
            TABLE_DEFAULT_PADDING);
        ParseListInto(
            GetOption(options, "fills"),
            table.ColumnFills);
        ParseListInto(
            GetOption(options, "colors"),
            table.ColumnColors);
        ParseListInto(
            GetOption(options, "align"),
            table.Aligns);
    }

    private static TableRow ParseTableRow(string payload)
    {
        TableRow row = new TableRow();
        string[] parts = (payload ?? string.Empty).Split('|');
        for (int index = 0; index < parts.Length; index++)
        {
            string part = parts[index].Trim();
            int equals = part.IndexOf('=');
            if (equals <= 0)
            {
                row.Cells.Add(part);
                continue;
            }
            string key = part.Substring(0, equals).Trim();
            string value = part.Substring(equals + 1).Trim();
            if (string.Equals(key, "fill", StringComparison.OrdinalIgnoreCase))
            {
                row.Fill = value;
            }
            else if (string.Equals(
                key,
                "fills",
                StringComparison.OrdinalIgnoreCase))
            {
                ParseListInto(value, row.Fills);
            }
            else if (string.Equals(
                key,
                "color",
                StringComparison.OrdinalIgnoreCase))
            {
                row.Color = value;
            }
            else if (string.Equals(
                key,
                "colors",
                StringComparison.OrdinalIgnoreCase))
            {
                ParseListInto(value, row.Colors);
            }
            else if (string.Equals(
                key,
                "align",
                StringComparison.OrdinalIgnoreCase))
            {
                ParseListInto(value, row.Aligns);
            }
            else if (string.Equals(
                key,
                "height",
                StringComparison.OrdinalIgnoreCase))
            {
                row.Height = ParseFloat(value, TABLE_DEFAULT_ROW_HEIGHT);
                row.HeightSet = true;
            }
            else if (string.Equals(
                key,
                "head",
                StringComparison.OrdinalIgnoreCase))
            {
                row.Header = string.Equals(value, "1", StringComparison.Ordinal)
                    || string.Equals(
                        value,
                        "true",
                        StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                row.Cells.Add(part);
            }
        }
        return row;
    }

    private static Dictionary<string, string> ParseOptionParts(string payload)
    {
        string[] parts = (payload ?? string.Empty).Split('|');
        Dictionary<string, string> options =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < parts.Length; index++)
        {
            int equals = parts[index].IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }
            string key = parts[index].Substring(0, equals).Trim();
            string value = parts[index].Substring(equals + 1).Trim();
            if (key.Length > 0)
            {
                options[key] = value;
            }
        }
        return options;
    }

    private static void ParseListInto(string value, List<string> target)
    {
        target.Clear();
        if (string.IsNullOrEmpty(value))
        {
            return;
        }
        string[] parts = value.Split(',');
        for (int index = 0; index < parts.Length; index++)
        {
            target.Add(parts[index].Trim());
        }
    }

    private static string ValueAt(List<string> values, int index)
    {
        return values != null && index >= 0 && index < values.Count
            ? values[index]
            : null;
    }

    private static ImageOptions ParseImageOptions(
        string payload,
        float defaultWidth,
        float defaultHeight)
    {
        string sprite;
        Dictionary<string, string> options = ParseMarkerOptions(payload, out sprite);
        if (string.IsNullOrEmpty(sprite))
        {
            return null;
        }
        ImageOptions result = new ImageOptions();
        result.Sprite = sprite;
        result.Width = defaultWidth;
        result.Height = defaultHeight;
        result.Color = GetOption(options, "color") ?? "#FFFFFF";
        result.Fit = GetOption(options, "fit") ?? "fit";
        result.Align = GetOption(options, "align") ?? "center";
        result.FloatLeft = string.Equals(
            GetOption(options, "float"),
            "left",
            StringComparison.OrdinalIgnoreCase);
        result.Gap = ParseFloat(GetOption(options, "gap"), 0f);
        string x = GetOption(options, "x");
        if (x != null)
        {
            result.X = ParseFloat(x, 0f);
            result.XSet = true;
        }
        string y = GetOption(options, "y");
        if (y != null)
        {
            result.Y = ParseFloat(y, 0f);
            result.YSet = true;
        }
        result.Width = ParseFloat(
            GetOption(options, "w"),
            result.Width);
        result.Height = ParseFloat(
            GetOption(options, "h"),
            result.Height);
        return result;
    }

    private static Dictionary<string, string> ParseMarkerOptions(
        string payload,
        out string value)
    {
        string[] parts = (payload ?? string.Empty).Split('|');
        value = parts.Length > 0 ? parts[0].Trim() : string.Empty;
        Dictionary<string, string> options =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 1; index < parts.Length; index++)
        {
            int equals = parts[index].IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }
            string key = parts[index].Substring(0, equals).Trim();
            string optionValue = parts[index].Substring(equals + 1).Trim();
            if (key.Length > 0)
            {
                options[key] = optionValue;
            }
        }
        return options;
    }

    private static string GetOption(
        Dictionary<string, string> options,
        string key)
    {
        string value;
        return options.TryGetValue(key, out value) && value.Length > 0
            ? value
            : null;
    }

    private static float ParseFloat(string value, float fallback)
    {
        float number;
        return !string.IsNullOrEmpty(value)
            && float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out number)
            ? number
            : fallback;
    }

    private static string MarkerPayload(string text, string prefix)
    {
        if (text == null
            || text.Length <= prefix.Length + ICON_SUFFIX.Length
            || !text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !text.EndsWith(ICON_SUFFIX, StringComparison.Ordinal))
        {
            return null;
        }
        return text.Substring(
            prefix.Length,
            text.Length - prefix.Length - ICON_SUFFIX.Length).Trim();
    }

    private static int EstimateVisualLines(string text)
    {
        return EstimateVisualLines(text, BODY_WIDTH);
    }

    private static int EstimateVisualLines(string text, float width)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int count = 0;
        for (int index = 0; index < lines.Length; index++)
        {
            count += EstimateLineCost(lines[index], width);
        }
        return count;
    }

    private static int EstimateLineCost(string line)
    {
        return EstimateLineCost(line, BODY_WIDTH);
    }

    private static int EstimateLineCost(string line, float width)
    {
        if (string.IsNullOrEmpty(line))
        {
            return 1;
        }
        float units = 0f;
        for (int index = 0; index < line.Length; index++)
        {
            units += line[index] >= '\u0080' ? 2f : 1f;
        }
        float unitsPerLine = Math.Max(
            6f,
            Math.Max(1f, width) * BODY_UNITS_PER_PIXEL);
        return Math.Max(1, (int)Math.Ceiling(units / unitsPerLine));
    }

    private static float SelectBodyFontSize(string body)
    {
        if (body != null
            && body.IndexOf("<pos=", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return 6f;
        }
        if (body != null && body.Length >= 1600)
        {
            return 6.4f;
        }
        if (body != null && body.Length >= 1200)
        {
            return 6.7f;
        }
        return 7f;
    }

    private static string PaginationMarker(int index)
    {
        return index >= 0 && index < 20
            ? ((char)(0x2460 + index)).ToString()
            : "(" + (index + 1) + ")";
    }

    private static void ApplyPaperTheme(PaperUI paper)
    {
        if (paper == null)
        {
            return;
        }

        CustomUIElement indicator = paper.Get("page_indicator");
        if (indicator != null && indicator.text != null)
        {
            ApplyPageIndicatorStyle(indicator);
        }

        paper.PageShown -= UpdateCompactPageIndicator;
        paper.PageShown += UpdateCompactPageIndicator;
        UpdateCompactPageIndicator(paper, paper.CurrentPage);

        ApplyGameButton(paper, "close", "paper_buttons_close1");
        ApplyGameButton(paper, "zoom", "paper_buttons_screen_0");
        ApplyCrispImages(paper);
    }

    private static void UpdateCompactPageIndicator(PaperUI paper, int pageIndex)
    {
        if (paper == null)
        {
            return;
        }
        CustomUIElement indicator = paper.Get("page_indicator");
        if (indicator != null)
        {
            int number = Math.Max(0, pageIndex) + 1;
            int count = Math.Max(1, paper.PageCount);
            string code = LocHelper.GetCurrentLocaleCode();
            ApplyPageIndicatorStyle(indicator);
            indicator.SetText(
                IsEnglishCode(code)
                    ? "Page\n" + number + "/" + count
                    : number + "/" + count);
        }
    }

    private static void ApplyPageIndicatorStyle(CustomUIElement indicator)
    {
        if (indicator == null || indicator.text == null)
        {
            return;
        }
        FontLoader loader = FontLoader.Instance;
        if (loader != null && loader.caveat != null)
        {
            indicator.text.font = loader.caveat;
        }
        indicator.text.fontSize = 15f;
        indicator.text.fontStyle = FontStyles.Bold;
        indicator.SetTextColor(new Color32(121, 116, 110, 255));

        // PaperUI adds a locale font switcher to every text element. The vanilla
        // chemistry guide's PageTMP uses Caveat directly in every locale.
        if (indicator.root != null)
        {
            TMPLocalizer localizer = indicator.root.GetComponent<TMPLocalizer>();
            if (localizer != null)
            {
                localizer.enabled = false;
            }
        }
    }

    private static void ApplyGameButton(PaperUI paper, string elementId, string spriteKey)
    {
        CustomUIElement element = paper.Get(elementId);
        if (element == null || element.image == null)
        {
            return;
        }

        CustomUIManager manager = CustomUIManager.Instance;
        if (manager == null)
        {
            return;
        }
        Sprite source = manager.GetSprite(spriteKey, true);
        if (source == null)
        {
            return;
        }
        element.image.sprite = GetCrispSprite(spriteKey, source);
        element.image.color = Color.white;
        element.image.preserveAspect = false;
        element.image.pixelsPerUnitMultiplier = 1f;
        element.image.rectTransform.localScale = Vector3.one;
        if (element.image.canvas != null)
        {
            element.image.canvas.pixelPerfect = true;
        }
        element.image.SetAllDirty();
    }

    private static void ApplyCrispImages(PaperUI paper)
    {
        if (paper.Root == null)
        {
            return;
        }
        CustomUIManager manager = CustomUIManager.Instance;
        if (manager == null)
        {
            return;
        }
        Image[] images = paper.Root.GetComponentsInChildren<Image>(true);
        for (int index = 0; index < images.Length; index++)
        {
            Image image = images[index];
            Sprite current = image == null ? null : image.sprite;
            if (current == null || current.texture == null)
            {
                continue;
            }
            string name = current.name ?? string.Empty;

            Sprite original = manager.GetSprite(name, true);
            if (original == null)
            {
                original = current;
            }
            if (name == "notebook_manual" || name.StartsWith("paper_buttons", StringComparison.Ordinal))
            {
                image.sprite = GetCrispSprite(name, original);
            }
            else
            {
                original.texture.filterMode = FilterMode.Point;
                original.texture.anisoLevel = 0;
                image.sprite = original;
            }
            image.pixelsPerUnitMultiplier = 1f;
            image.preserveAspect =
                !name.StartsWith("notebook_manual", StringComparison.Ordinal)
                && !name.StartsWith("paper_buttons", StringComparison.Ordinal);
            image.SetAllDirty();
        }
    }

    private static Sprite GetCrispSprite(string key, Sprite source)
    {
        Sprite cached;
        if (CrispSprites.TryGetValue(key, out cached) && cached != null)
        {
            return cached;
        }
        Texture2D sourceTexture = source.texture;
        if (sourceTexture == null)
        {
            return source;
        }

        int width = Mathf.Max(1, Mathf.RoundToInt(source.rect.width));
        int height = Mathf.Max(1, Mathf.RoundToInt(source.rect.height));
        int scaledWidth = width * 2;
        int scaledHeight = height * 2;
        Texture2D texture = new Texture2D(
            scaledWidth,
            scaledHeight,
            TextureFormat.RGBA32,
            false,
            true);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.anisoLevel = 0;

        RenderTexture renderTexture = RenderTexture.GetTemporary(
            scaledWidth,
            scaledHeight,
            0,
            RenderTextureFormat.ARGB32);
        renderTexture.filterMode = FilterMode.Point;
        renderTexture.wrapMode = TextureWrapMode.Clamp;
        RenderTexture previous = RenderTexture.active;
        FilterMode previousFilter = sourceTexture.filterMode;
        try
        {
            sourceTexture.filterMode = FilterMode.Point;
            Graphics.Blit(
                sourceTexture,
                renderTexture,
                new Vector2(
                    source.rect.width / sourceTexture.width,
                    source.rect.height / sourceTexture.height),
                new Vector2(
                    source.rect.x / sourceTexture.width,
                    source.rect.y / sourceTexture.height));
            RenderTexture.active = renderTexture;
            texture.ReadPixels(
                new Rect(0f, 0f, scaledWidth, scaledHeight),
                0,
                0,
                false);
            texture.Apply(false, true);
        }
        finally
        {
            RenderTexture.active = previous;
            sourceTexture.filterMode = previousFilter;
            RenderTexture.ReleaseTemporary(renderTexture);
        }

        Vector2 pivot = new Vector2(
            source.pivot.x / source.rect.width,
            source.pivot.y / source.rect.height);
        Sprite crisp = Sprite.Create(
            texture,
            new Rect(0f, 0f, scaledWidth, scaledHeight),
            pivot,
            source.pixelsPerUnit * 2f,
            0,
            SpriteMeshType.FullRect);
        crisp.name = source.name + "_crisp2x";
        CrispSprites[key] = crisp;
        return crisp;
    }

    private sealed class PageChunk
    {
        internal readonly List<SectionBlock> Sections = new List<SectionBlock>();
    }

    private sealed class SectionBlock
    {
        internal string Heading;
        internal string IconKey;
        internal bool ForcePageBreakBefore;
        internal float HeadingX = SECTION_LEFT;
        internal float HeadingWidth = SECTION_WIDTH;
        internal float HeadingHeight = SECTION_HEIGHT;
        internal float HeadingToBody = SECTION_TO_BODY;
        internal string HeadingAlign = "center";
        internal string HeadingInk = SECTION_INK;
        internal string HeadingFill = SECTION_FILL;
        internal float IconX;
        internal bool IconXSet;
        internal float IconY;
        internal bool IconYSet;
        internal float IconWidth = 24f;
        internal float IconHeight = 24f;
        internal float IconGap;
        internal string IconColor = "#FFFFFF";
        internal string IconFit = "fit";
        internal readonly List<ContentNode> Nodes = new List<ContentNode>();

        internal bool HasHeading
        {
            get
            {
                return !string.IsNullOrEmpty(Heading)
                    || !string.IsNullOrEmpty(IconKey);
            }
        }

        internal float HeadingCost
        {
            get
            {
                float height = string.IsNullOrEmpty(Heading)
                    ? 0f
                    : HeadingHeight;
                if (!string.IsNullOrEmpty(IconKey))
                {
                    height = Math.Max(height, IconHeight)
                        + Math.Max(0f, IconGap);
                }
                return height + HeadingToBody;
            }
        }

        internal bool HasContent
        {
            get
            {
                return HasHeading || Nodes.Count > 0;
            }
        }

        internal SectionBlock CreateFrame()
        {
            SectionBlock frame = new SectionBlock();
            frame.Heading = Heading;
            frame.IconKey = IconKey;
            frame.HeadingX = HeadingX;
            frame.HeadingWidth = HeadingWidth;
            frame.HeadingHeight = HeadingHeight;
            frame.HeadingToBody = HeadingToBody;
            frame.HeadingAlign = HeadingAlign;
            frame.HeadingInk = HeadingInk;
            frame.HeadingFill = HeadingFill;
            frame.IconX = IconX;
            frame.IconXSet = IconXSet;
            frame.IconY = IconY;
            frame.IconYSet = IconYSet;
            frame.IconWidth = IconWidth;
            frame.IconHeight = IconHeight;
            frame.IconGap = IconGap;
            frame.IconColor = IconColor;
            frame.IconFit = IconFit;
            return frame;
        }

        internal SectionBlock CreateBodyFrame()
        {
            return new SectionBlock();
        }
    }

    private enum ContentKind
    {
        Text,
        Label,
        Image,
        Float,
        Table,
        Gap
    }

    private sealed class ContentNode
    {
        internal ContentKind Kind;
        internal string Text;
        internal string Sprite;
        internal string Color = "#FFFFFF";
        internal string Fit = "fit";
        internal string Align = "center";
        internal string Style = "body";
        internal float FontSize;
        internal float X;
        internal bool XSet;
        internal float Y;
        internal bool YSet;
        internal float Width = 48f;
        internal float Height = 48f;
        internal float Gap;
        internal bool FloatLeft;
        internal TableData Table;
        internal readonly List<ImageLayer> Overlays = new List<ImageLayer>();
    }

    private sealed class TableData
    {
        internal float X = BODY_LEFT;
        internal bool XSet;
        internal float Width = BODY_WIDTH;
        internal float RowHeight = TABLE_DEFAULT_ROW_HEIGHT;
        internal float RowGap = TABLE_DEFAULT_ROW_GAP;
        internal float CellGap = TABLE_DEFAULT_CELL_GAP;
        internal float FontSize = TABLE_DEFAULT_FONT_SIZE;
        internal float Padding = TABLE_DEFAULT_PADDING;
        internal readonly List<string> ColumnFills = new List<string>();
        internal readonly List<string> ColumnColors = new List<string>();
        internal readonly List<string> Aligns = new List<string>();
        internal readonly List<TableRow> Rows = new List<TableRow>();
    }

    private sealed class TableRow
    {
        internal readonly List<string> Cells = new List<string>();
        internal readonly List<string> Fills = new List<string>();
        internal readonly List<string> Colors = new List<string>();
        internal readonly List<string> Aligns = new List<string>();
        internal string Fill;
        internal string Color;
        internal float Height = TABLE_DEFAULT_ROW_HEIGHT;
        internal bool HeightSet;
        internal bool Header;
    }

    private sealed class ImageLayer
    {
        internal string Sprite;
        internal string Color = "#FFFFFF";
        internal string Fit = "fit";
        internal float X;
        internal float Y;
        internal float Width = 16f;
        internal float Height = 16f;
    }

    private sealed class ImageOptions
    {
        internal string Sprite;
        internal string Color = "#FFFFFF";
        internal string Fit = "fit";
        internal string Align = "center";
        internal float X;
        internal bool XSet;
        internal float Y;
        internal bool YSet;
        internal float Width;
        internal float Height;
        internal float Gap;
        internal bool FloatLeft;
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
