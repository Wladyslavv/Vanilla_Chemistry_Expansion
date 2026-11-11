// Clients say why they refuse one of this mod's items, the way vanilla clients that don't buy luxury items
// (such as pure water in the Lower Level) say "dialogue_no_luxury" when it is put on the table.
// Written for C# 5 (see DreamToxinMod.cs).
using System;
using HarmonyLib;

[HarmonyPatch(typeof(StoreClient), "CompleteClientCreation")]
public static class RefusalLinesPatch
{
    private static void Postfix(StoreClient __instance)
    {
        try
        {
            StoreClient client = __instance;
            client.onItemAddedToWeighted = (Action<GameItem>)Delegate.Combine(client.onItemAddedToWeighted,
                (Action<GameItem>)delegate(GameItem item)
                {
                    Say(client, item);
                });
        }
        catch (Exception e)
        {
            Log.Error("refusal line setup failed", e);
        }
    }

    private static void Say(StoreClient client, GameItem item)
    {
        try
        {
            if (item == null || !GeneralHelper.IsItemOwned(item))
            {
                return;
            }
            string key = null;
            if (CartelBoycott.OnItemShown(client, item))
            {
                key = "cartel_boycott_line";  // the cartel's boycott, said once per client (CartelNetwork.cs)
            }
            else if (BlendedWineRefusePatch.Refuses(client, item))
            {
                key = "refuse_blended_wine";
            }
            else if (NuclearDrinkBuyerPatch.Refuses(client, item))
            {
                key = client.IsFaction("FACTION_LOWER_LEVEL") ? "refuse_nuclear_drink_lower" : "refuse_nuclear_drink";
            }
            else if (LanternBuyers.Refuses(client, item))
            {
                key = LanternBuyers.RefusalKey(client);
            }
            if (key == null)
            {
                return;
            }
            Dialogue dialogue = new Dialogue();
            dialogue.SetText(client.displayName, Text.Get(key));
            dialogue.priority = 1;
            DialogUIManager.Instance.StartDialogue(dialogue);
        }
        catch (Exception e)
        {
            Log.Error("refusal line failed", e);
        }
    }
}
