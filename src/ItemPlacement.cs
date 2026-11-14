// GraphUtils.TryAcceptAll returns how many units it could NOT place (amount - placed), so 0 means success and
// anything above 0 means some or all of the item is still unplaced. Vanilla reads it that way itself
// (GraphUtils.TryAddInto: "if (TryAcceptAll(node, item) > 0) return false;"). Read as a count of placed units it
// turns every check upside down - 1.8.0 did exactly that in four places - so new code asks this instead.
// Written for C# 5 (see DreamToxinMod.cs).
public static class ItemPlacement
{
    // True only when the whole item went into the node.
    public static bool TryPut(GraphNodeStorage node, GameItem item)
    {
        return node != null && item != null && GraphUtils.TryAcceptAll(node, item) == 0;
    }
}
