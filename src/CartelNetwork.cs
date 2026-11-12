// Cartel Network: a new faction that works with the player as a partner, separate from vanilla's cartel (which owns
// the chemist start and is removed when the player is authorised to leave it).
//
// Joining, chemist start: when the vanilla cartel faction is removed after the chemist's authorised exit
// (StoreReputation.RemoveCartelFaction with PlayerStore.isCartelExited), the player joins at 50 reputation: a new
// entry in the vanilla reputation list, so the reputation panel, notifications and the save (ES3 saves the whole
// list) handle it like any vanilla faction. Vanilla calls RemoveCartelFaction again on every load once the exit
// happened (PlayerStore.AlwayCall), so saves that left the cartel before this mod version join on their next load.
//
// Other starts, first step: the network watches. Every contraband sale with the Medical or Narcotic type adds its
// sale price to a running total; once it reaches 500, the next morning (on waking up, before the shutter opens)
// a sealed envelope lies on the inner counter, or the outer one if the inner is full: "We see" (CartelNote). The
// player has to open the shutter and face the counters to start the day, so the envelope is always seen; vanilla's
// counter clearing is left alone.
// Then, from the day after the note at the earliest (so one huge sale still brings the note first), once the total
// reaches 1000 the Cartel Contact (CartelContact) comes in every day until the player answers: pay 100 to join at 0
// reputation, ask him to come back in two days (once), or refuse, which sets the "refused" label for what comes next.
// No perks yet. Written for C# 5 (see DreamToxinMod.cs).
using System;
using System.Collections.Generic;
using HarmonyLib;

public static class CartelNetwork
{
    public const string FACTION_ID = "FACTION_VCE_CARTEL_NETWORK";
    public const double CHEMIST_START_REPUTATION = 50.0;

    // Vanilla cartel colours, shifted toward the mod's chemistry teal.
    public const string COLOR_1 = "4f7a78";
    public const string COLOR_2 = "3a4446";
    public const string COLOR_3 = "7f8c7a";

    // The watching path (non-chemist starts).
    public const int SEEN_THRESHOLD = 500;
    public const string SEEN_VALUE_KEY = "cartel_network_seen_value";
    public const string NOTE_GIVEN_KEY = "cartel_network_note_given";            // day the note was left (0: not yet)

    // The contact: from 1000 watched sales, the day after the note at the earliest.
    public const int CONTACT_THRESHOLD = 1000;
    public const int JOIN_FEE = 100;
    public const int POSTPONE_DAYS = 2;
    public const string POSTPONED_UNTIL_KEY = "cartel_network_contact_postponed_until";  // 0: never postponed
    // The label for what follows a refusal: the day the player refused the cartel (0: never).
    public const string REFUSED_KEY = "cartel_network_refused";

    public static void Enable()
    {
        ModHook.OnGameLoadedLate += RefreshName;
        ModHook.OnWakingUpLate += OnWakingUp;
        ModHook.OnGenerateCustomerNormal += OnGenerateCustomers;
        try
        {
            string id = DreamToxinMod.MOD_ID;
            ModHelper.AddDebugFunction(id, "Modding", "Cartel Network: join now (chemist exit)", delegate { Join("debug", CHEMIST_START_REPUTATION); }, false);
            ModHelper.AddDebugFunction(id, "Modding", "Cartel Network: +25 reputation",
                delegate { StoreReputation.ModReputation(FACTION_ID, 25, true); }, false);
            ModHelper.AddDebugFunction(id, "Modding", "Cartel Network: \"we see\" note", delegate { CartelNote.Give(false); }, false);
            ModHelper.AddDebugFunction(id, "Modding", "Cartel Network: +500 watched sales", delegate { AddSeenValue(SEEN_THRESHOLD); }, false);
            ModHelper.AddDebugFunction(id, "Modding", "Cartel Network: contact visits now",
                delegate { PlayerStore.Instance.storeClientManager.AddClient(CartelContact.Create()); }, false);
            ModHelper.AddDebugFunction(id, "Modding", "Cartel Network: reset the watching path", ResetWatching, false);
            ModHelper.AddDebugFunction(id, "Modding", "Cartel Network: refuse now (boycott on)", Refuse, false);
            ModHelper.AddDebugFunction(id, "Modding", "Cartel Network: +9 boycotts",
                delegate { SetData(CartelBoycott.COUNT_KEY, Data(CartelBoycott.COUNT_KEY) + 9); }, false);
            ModHelper.AddDebugFunction(id, "Modding", "Cartel Network: contact returns now",
                delegate { PlayerStore.Instance.storeClientManager.AddClient(CartelContact.CreateSecond()); }, false);
        }
        catch (Exception e)
        {
            Log.Error("cartel network debug function registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnGameLoadedLate -= RefreshName;
        ModHook.OnWakingUpLate -= OnWakingUp;
        ModHook.OnGenerateCustomerNormal -= OnGenerateCustomers;
    }

    public static void AddItems(ModItemDirectory directory)
    {
        directory.Add(CartelNote.ITEM_ID, CartelNote.CreateItem);
    }

    public static StoreReputation Find()
    {
        List<StoreReputation> list = PlayerStore.Instance != null ? PlayerStore.Instance.storeReputations : null;
        return list != null ? list.Find(r => r != null && r.factionId == FACTION_ID) : null;
    }

    public static bool IsMember()
    {
        if (!IaSettings.CartelEnabled)
        {
            return false;
        }
        return Find() != null;
    }

    // Adds the faction once; later calls do nothing.
    public static void Join(string reason, double reputation)
    {
        Join(reason, reputation, "cartel_network_joined");
    }

    public static void Join(string reason, double reputation, string messageKey)
    {
        if (!IaSettings.CartelEnabled)
        {
            return;
        }
        try
        {
            if (PlayerStore.Instance == null || PlayerStore.Instance.storeReputations == null || IsMember())
            {
                return;
            }
            StoreReputation network = new StoreReputation();
            network.factionDisplay = Text.Get("cartel_network_name");
            network.factionId = FACTION_ID;
            network.factionColor1 = COLOR_1;
            network.factionColor2 = COLOR_2;
            network.factionColor3 = COLOR_3;
            network.perks = CartelNetworkPerks.Create();
            network.SetReputation(reputation);  // the amount field is private; this also re-checks perks
            PlayerStore.Instance.storeReputations.Add(network);
            StoreReputation.UpdateReputation();
            Notify(Text.Get(messageKey));
            Log.Info("joined the Cartel Network (" + reason + ") at " + reputation + " reputation");
        }
        catch (Exception e)
        {
            Log.Error("joining the Cartel Network failed", e);
        }
    }

    // The display name is saved as text; follow the current game language.
    public static void RefreshName()
    {
        try
        {
            StoreReputation network = Find();
            if (network != null)
            {
                network.factionDisplay = Text.Get("cartel_network_name");
            }
        }
        catch (Exception e)
        {
            Log.Error("refreshing the Cartel Network name failed", e);
        }
    }

    // ---- the watching path ----------------------------------------------------------------------------------------

    // Players the network is still sizing up: not the chemist start (vanilla's cartel already owns them, and they
    // join through the exit), not members, and not those who refused.
    public static bool IsWatching()
    {
        if (!IaSettings.CartelEnabled)
        {
            return false;
        }
        return PlayerStore.Instance != null && !PlayerStore.Instance.IsChemistStart() && !IsMember() && !HasRefused();
    }

    public static int Data(string key)
    {
        return ModHelper.GetModDataInt(DreamToxinMod.MOD_ID, key, 0);
    }

    public static void SetData(string key, int value)
    {
        ModHelper.SetModDataInt(DreamToxinMod.MOD_ID, key, value);
    }

    public static bool HasRefused()
    {
        return Data(REFUSED_KEY) > 0;
    }

    public static bool HasPostponed()
    {
        return Data(POSTPONED_UNTIL_KEY) > 0;
    }

    // The contact comes in on a day when: the note was left on an earlier day, the watched total is at least 1000,
    // and any "come back in two days" has run out. He keeps coming until the player pays or refuses.
    public static bool IsContactDue(int today)
    {
        int noteDay = Data(NOTE_GIVEN_KEY);
        int postponedUntil = Data(POSTPONED_UNTIL_KEY);
        return IsWatching() && noteDay > 0 && noteDay < today && SeenValue() >= CONTACT_THRESHOLD
            && (postponedUntil == 0 || today >= postponedUntil);
    }

    public static void OnGenerateCustomers(StoreClientManager manager)
    {
        if (!IaSettings.CartelEnabled)
        {
            return;
        }
        try
        {
            int today = StoreStation.Instance.dayCounter;
            if (manager != null && IsContactDue(today))
            {
                manager.AddClient(CartelContact.Create());
                Log.Info("cartel network: the contact visits (day " + today + ", watched sales " + SeenValue() + ")");
            }
            else if (manager != null && CartelBoycott.IsSecondContactDue(today))
            {
                manager.AddClient(CartelContact.CreateSecond());
                Log.Info("cartel network: the contact comes back after " + Data(CartelBoycott.COUNT_KEY) + " boycotts (day " + today + ")");
            }
        }
        catch (Exception e)
        {
            Log.Error("cartel network contact failed", e);
        }
    }

    // ---- the contact's answers ------------------------------------------------------------------------------------

    public static void PayAndJoin()
    {
        PlayerStore.Instance.ModCash(-JOIN_FEE);
        Join("paid the contact", 0.0, "cartel_network_joined_paid");
    }

    public static void Postpone()
    {
        SetData(POSTPONED_UNTIL_KEY, StoreStation.Instance.dayCounter + POSTPONE_DAYS);
        Log.Info("cartel network: contact postponed until day " + Data(POSTPONED_UNTIL_KEY));
    }

    public static void Refuse()
    {
        SetData(REFUSED_KEY, Math.Max(1, StoreStation.Instance.dayCounter));
        Log.Info("cartel network: the player refused the cartel (label " + REFUSED_KEY + ")");
    }

    private static void ResetWatching()
    {
        SetData(SEEN_VALUE_KEY, 0);
        SetData(NOTE_GIVEN_KEY, 0);
        SetData(POSTPONED_UNTIL_KEY, 0);
        SetData(REFUSED_KEY, 0);
        SetData(CartelBoycott.COUNT_KEY, 0);
        SetData(CartelBoycott.COUNT_REACHED_KEY, 0);
        SetData(CartelBoycott.SECOND_REFUSED_KEY, 0);
        SetData(CartelRaid.DUE_KEY, 0);
        SetData(CartelRaid.ANSWER_KEY, 0);
        SetData(CartelRaid.SECOND_DUE_KEY, 0);
        SetData(CartelRaid.SECOND_ANSWER_KEY, 0);
        SetData(CartelRaid.AGAINST_CARTEL_KEY, 0);
        foreach (string k in new string[] { CartelSweep.MEDICAL_SPENT_KEY, CartelSweep.MILITARY_SPENT_KEY, CartelSweep.MEDICAL_DONE_KEY, CartelSweep.MILITARY_DONE_KEY, CartelSweep.DESTROYED_KEY, CartelSweep.THANKED_KEY })
        {
            SetData(k, 0);
        }
    }

    public static bool IsWatchedSale(GameItem item)
    {
        return item != null && ContrabandHelper.IsContraband(item)
            && (item.IsGameItemType("MEDICAL") || item.IsGameItemType("NARCOTIC"));
    }

    public static int SeenValue()
    {
        return ModHelper.GetModDataInt(DreamToxinMod.MOD_ID, SEEN_VALUE_KEY, 0);
    }

    public static void AddSeenValue(int value)
    {
        if (value <= 0)
        {
            return;
        }
        ModHelper.SetModDataInt(DreamToxinMod.MOD_ID, SEEN_VALUE_KEY, (int)Math.Min(int.MaxValue, (long)SeenValue() + value));
    }

    // On waking up, before the shutter opens: deliver the note once the total has reached the threshold (sales happen
    // during the day, so this is always the morning after). Vanilla clears the outer counter at the end of the day,
    // before this, so the envelope sits on a counter for the whole day.
    public static void OnWakingUp()
    {
        try
        {
            if (IsWatching() && Data(NOTE_GIVEN_KEY) == 0 && SeenValue() >= SEEN_THRESHOLD)
            {
                SetData(NOTE_GIVEN_KEY, Math.Max(1, StoreStation.Instance.dayCounter));
                CartelNote.Give(true);
                Log.Info("cartel network: \"we see\" note delivered (watched sales " + SeenValue() + ")");
            }
        }
        catch (Exception e)
        {
            Log.Error("cartel network note delivery failed", e);
        }
    }

    public static void Notify(string text)
    {
        if (StoreUIManager.Instance != null)
        {
            StoreUIManager.Instance.Notify(text, "default");
        }
    }
}

// "We see": a one-page paper document on aged parchment with the cartel's ink eye (Textures/Paper/vce_cartel_note.png,
// drawn by art/cartel/DrawCartelNote.cs) and the words in the game's handwriting style.
public static class CartelNote
{
    public const string BOOK_ID = "vce_cartel_we_see";
    public const string ITEM_ID = "vce.cartel_note";
    // Textures/Paper/vce_cartel_note_<lang>.png (art/cartel/DrawCartelNote.cs); chosen by cartel_note_background.
    public const string BACKGROUND_FALLBACK = "vce_cartel_note_en";
    public const string SPRITE = "vce_cartel_envelope";

    public static bool IsNote(GameItem item)
    {
        return item != null && item.identifier == ITEM_ID;
    }

    // Registered with the handbook, once mod localization is available (Content.EnsureRegistered, OnModAssetsLoaded).
    public static void Register()
    {
        try
        {
            PaperBookData book = PaperStarters.NewBook("blank", BOOK_ID, Text.Get("cartel_note_name"));
            // The words are inked into the image itself (brush handwriting with drips), one image per language.
            string background = Text.Get("cartel_note_background");
            book.background = background == "cartel_note_background" ? BACKGROUND_FALLBACK : background;
            book.chrome = PaperStarters.DefaultChrome(new UnityEngine.Vector2(314f, 402f), 2f, true, false, false, false);
            book.pages.Clear();
            PaperPageData page = new PaperPageData();
            page.name = Text.Get("cartel_note_name");
            book.pages.Add(page);
            book.item = null;
            ModHelper.RegisterPaperBook(DreamToxinMod.MOD_ID, book);
        }
        catch (Exception e)
        {
            Log.Error("registering the cartel note failed", e);
        }
    }

    public static GameItem CreateItem()
    {
        GameItem item = ItemDirectory.CreateEmptyItem(ITEM_ID);
        try
        {
            item.name = Text.Get("cartel_note_name");
            item.shortDescription = Text.Get("cartel_note_desc");
            // A folded, sealed envelope (art/cartel/DrawCartelEnvelope.cs), brighter than vanilla notes on the counter.
            item.SetSpriteAndShape("Items/items_misc", "handnote");
            BeerSprites.Apply(item, SPRITE);
            item.EnableTag("DOCUMENT", false);
            item.SetGameItemType("DOCUMENT");
            item.EnableTag("IMPORTANT_TAG", false);
            item.unitValue = 0L;
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

    // Left in the middle of the inner counter, where it cannot be missed; the outer counter if the inner one is full;
    // the main inventory as a last resort.
    public static void Give(bool announce)
    {
        GameItem note = DirectoryMaster.Item(ITEM_ID, true);
        if (note == null)
        {
            return;
        }
        EmporiumEntry shop = EmporiumEntry.Instance;
        if (GraphUtils.TryAcceptAllMid(shop.backInvinvElement, note) != 0 && GraphUtils.TryAcceptAllMid(shop.frontInvinvElement, note) != 0)
        {
            GraphUtils.EmporiumTryAdd(shop.invElement, note);
        }
        if (announce)
        {
            CartelNetwork.Notify(Text.Get("cartel_note_found"));
        }
    }
}

// The chemist's authorised exit removes the vanilla cartel; the network takes the player in.
[HarmonyPatch(typeof(StoreReputation), "RemoveCartelFaction")]
public static class CartelNetworkJoinPatch
{
    private static void Postfix()
    {
        if (PlayerStore.Instance != null && PlayerStore.Instance.isCartelExited)
        {
            CartelNetwork.Join("left the cartel", CartelNetwork.CHEMIST_START_REPUTATION);
        }
    }
}

// Every sale goes through SoldItem.TrackItem with its final price.
[HarmonyPatch(typeof(SoldItem), "TrackItem")]
public static class CartelNetworkSalePatch
{
    private static void Postfix(GameItem gameItem, StoreClient storeClient, int soldPrice)
    {
        try
        {
            if (soldPrice > 0 && CartelNetwork.IsWatching() && CartelNetwork.IsWatchedSale(gameItem))
            {
                CartelNetwork.AddSeenValue(soldPrice);
            }
        }
        catch (Exception e)
        {
            Log.Error("cartel network sale tracking failed", e);
        }
    }
}

// Left on the inner counter during a deal, the note would go with the goods: every buyer refuses it.
[HarmonyPatch(typeof(StoreClient), "IsClientRefusingItem")]
public static class CartelNoteRefusalPatch
{
    private static void Postfix(GameItem gameItem, ref bool __result)
    {
        if (!__result && CartelNote.IsNote(gameItem))
        {
            __result = true;
        }
    }
}

// The Cartel Contact: a dialogue visitor with vanilla's cartel handler face. Five lines, then three answers; the
// "come back in two days" answer is only offered the first time.
public static class CartelContact
{
    public const string CLIENT_ID = "vce_cartel_contact";

    public static StoreClient Create()
    {
        StoreClient client = new StoreClient();
        client.identifier = CLIENT_ID;
        client.displayName = Text.Get("cartel_contact_name");
        client.spriteName = SpriteDict.GetCartelHandlerSpriteName();
        StoreClientFactionSetup.InitBlackMarket(client);
        client.clientIntent = StoreClient.ClientIntent.DIALOGUE;
        client.dismissable = false;
        string name = client.displayName;

        Dialogue last = client.mainDialogue.SetText(name, Text.Get("cartel_contact_1"))
            .NextDialogue().SetText(name, Text.Get("cartel_contact_2"))
            .NextDialogue().SetText(name, Text.Get("cartel_contact_3"))
            .NextDialogue().SetText(name, Text.Get("cartel_contact_4"))
            .NextDialogue().SetText(name, Text.Get("cartel_contact_5"));

        // Answers with no reply from him act in the choice action and end the visit there, as vanilla does (the
        // nuclear waste client): an end action on the player's own line is never run, and the talk would hang.

        // Pay: needs the fee in hand.
        Dialogue pay = new Dialogue().SetText("PLAYER", Text.Get("cartel_contact_choice_pay", CartelNetwork.JOIN_FEE));
        last.AddChoice(DirectoryMaster.Item("$", false), "", "", pay, delegate
        {
            CartelNetwork.PayAndJoin();
            Leave();
        }, () => PlayerStore.Instance.playerCash >= CartelNetwork.JOIN_FEE);

        // Come back in two days: once.
        if (!CartelNetwork.HasPostponed())
        {
            Dialogue later = new Dialogue().SetText("PLAYER", Text.Get("cartel_contact_choice_later"));
            last.AddChoice(DirectoryMaster.Item("$", false), "", "", later, delegate
            {
                CartelNetwork.Postpone();
                Leave();
            });
        }

        // Refuse: he laughs, and the label is set.
        Dialogue refuse = new Dialogue().SetText("PLAYER", Text.Get("cartel_contact_choice_refuse"));
        refuse.NextDialogue().SetText(name, Text.Get("cartel_contact_refused_1"))
            .NextDialogue().SetText(name, Text.Get("cartel_contact_refused_2"))
            .SetEndAction(delegate
            {
                CartelNetwork.Refuse();
                Leave();
            });
        last.AddChoice(DirectoryMaster.Item("$", false), "", "", refuse, null);

        client.CompleteClientCreation();
        return client;
    }

    // After 10 boycotts: two lines, then pay 200 to join, or refuse again.
    public static StoreClient CreateSecond()
    {
        StoreClient client = new StoreClient();
        client.identifier = CLIENT_ID + "_return";
        client.displayName = Text.Get("cartel_contact_name");
        client.spriteName = SpriteDict.GetCartelHandlerSpriteName();
        StoreClientFactionSetup.InitBlackMarket(client);
        client.clientIntent = StoreClient.ClientIntent.DIALOGUE;
        client.dismissable = false;
        string name = client.displayName;

        Dialogue last = client.mainDialogue.SetText(name, Text.Get("cartel_contact_return_1"))
            .NextDialogue().SetText(name, Text.Get("cartel_contact_return_2"));

        Dialogue pay = new Dialogue().SetText("PLAYER", Text.Get("cartel_contact_return_choice_pay", CartelBoycott.SECOND_FEE));
        pay.NextDialogue().SetText(name, Text.Get("cartel_contact_return_welcome"))
            .SetEndAction(delegate
            {
                CartelBoycott.PayAndJoin();
                Leave();
            });
        last.AddChoice(DirectoryMaster.Item("$", false), "", "", pay, null, () => PlayerStore.Instance.playerCash >= CartelBoycott.SECOND_FEE);

        // No reply: act in the choice action (see Create).
        Dialogue refuse = new Dialogue().SetText("PLAYER", Text.Get("cartel_contact_choice_refuse"));
        last.AddChoice(DirectoryMaster.Item("$", false), "", "", refuse, delegate
        {
            CartelBoycott.RefuseAgain();
            Leave();
        });

        client.CompleteClientCreation();
        return client;
    }

    // Vanilla's way out for a talk-only client.
    public static void Leave()
    {
        DialogUIManager.Instance.CurrentClientLeave();
    }
}

// Refusing the cartel has a price: word gets around. While the player has refused and is not a member, every
// client decides once, the first time the player puts contraband or stolen goods on the counter in front of them:
// one in three refuses all such goods for the rest of the visit, and says so. Each such boycott is counted; at
// 10 the Cartel Contact comes back the next day with a second, dearer offer (CartelContact.CreateSecond).
public static class CartelBoycott
{
    public const int CHANCE = 33;
    public const int RETURN_AFTER = 10;
    public const int SECOND_FEE = 200;
    public const string COUNT_KEY = "cartel_network_boycotts";
    public const string COUNT_REACHED_KEY = "cartel_network_boycotts_reached";    // day the count reached 10
    public const string SECOND_REFUSED_KEY = "cartel_network_refused_twice";       // label: day of the second refusal

    private class Decision
    {
        public bool Boycott;
    }

    // One decision per client visit.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<StoreClient, Decision> decisions =
        new System.Runtime.CompilerServices.ConditionalWeakTable<StoreClient, Decision>();

    public static bool IsActive()
    {
        if (!IaSettings.CartelEnabled)
        {
            return false;
        }
        return CartelNetwork.HasRefused() && !CartelNetwork.IsMember() && !CartelSweep.IsCartelDestroyed();
    }

    public static bool IsShadyGoods(GameItem item)
    {
        return item != null && (ContrabandHelper.IsContraband(item) || StolenHelper.IsStolenItem(item));
    }

    // Clients the cartel has no say over: Security, and anyone who already refuses contraband on their own (vanilla's
    // noAcceptingContraband: every Security client, some scavengers and Lower Level citizens). They never roll, so
    // their refusals are not counted as boycotts.
    public static bool IsOutOfReach(StoreClient client)
    {
        return client.isSecurity || client.clientFaction == "FACTION_SECURITY" || client.noAcceptingContraband;
    }

    public static bool Refuses(StoreClient client, GameItem item)
    {
        Decision decision;
        return client != null && IsActive() && !IsOutOfReach(client) && IsShadyGoods(item)
            && decisions.TryGetValue(client, out decision) && decision.Boycott;
    }

    // From the refusal lines (an owned item put on the counter in front of the client). Returns true when this item
    // made the client decide to boycott, so the line is said once.
    public static bool OnItemShown(StoreClient client, GameItem item)
    {
        Decision decision;
        if (client == null || !IsActive() || IsOutOfReach(client) || !IsShadyGoods(item) || decisions.TryGetValue(client, out decision))
        {
            return false;
        }
        decision = new Decision();
        decision.Boycott = RNG.Roll(CHANCE);
        decisions.Add(client, decision);
        if (!decision.Boycott)
        {
            return false;
        }
        int count = CartelNetwork.Data(COUNT_KEY) + 1;
        CartelNetwork.SetData(COUNT_KEY, count);
        if (count >= RETURN_AFTER && CartelNetwork.Data(COUNT_REACHED_KEY) == 0)
        {
            CartelNetwork.SetData(COUNT_REACHED_KEY, Math.Max(1, StoreStation.Instance.dayCounter));
        }
        Log.Info("cartel network: " + client.displayName + " refuses contraband and stolen goods (boycott " + count + ")");
        return true;
    }

    // The second visit: the day after the count reached 10 at the earliest, until the player answers. Not once the
    // player has reported the cartel to Security: there is no deal left to offer.
    public static bool IsSecondContactDue(int today)
    {
        int reached = CartelNetwork.Data(COUNT_REACHED_KEY);
        return IsActive() && reached > 0 && reached < today && CartelNetwork.Data(SECOND_REFUSED_KEY) == 0
            && !CartelRaid.IsAgainstCartel();
    }

    public static void PayAndJoin()
    {
        PlayerStore.Instance.ModCash(-SECOND_FEE);
        CartelNetwork.Join("paid the contact after the boycott", 0.0, "cartel_network_joined_paid");
    }

    public static void RefuseAgain()
    {
        CartelNetwork.SetData(SECOND_REFUSED_KEY, Math.Max(1, StoreStation.Instance.dayCounter));
        Log.Info("cartel network: the player refused the cartel a second time (label " + SECOND_REFUSED_KEY + ")");
        CartelRaid.OnSecondRefusal();
    }
}

[HarmonyPatch(typeof(StoreClient), "IsClientRefusingItem")]
public static class CartelBoycottRefusalPatch
{
    private static void Postfix(StoreClient __instance, GameItem gameItem, ref bool __result)
    {
        if (!__result && CartelBoycott.Refuses(__instance, gameItem))
        {
            __result = true;
        }
    }
}

// The route against the cartel. Checked every night; once all of these hold, a Security officer comes in the next day:
//   1. at least one client has boycotted the player's contraband (CartelBoycott);
//   2. Security reputation 80 or more;
//   3. the player has sold incapacitant to Security or the Upper Level, or (Upper Level + Security) - (Lower Level +
//      Revolution + Black Market) >= 50, or any of Lower Level, Revolution, Black Market is negative;
//   4. not a member of the Cartel Network (the cartel's visit has happened: a boycott needs a refusal).
// The officer asks the player to report cartel activity. Reporting sets the "against the cartel" label; declining
// just ends the talk, and is not offered to a player who has refused the cartel twice.
// He can come twice: once when the conditions are first met, and once more three days after the player refuses the
// cartel a second time, if the conditions hold then and the player has not reported the cartel yet.
public static class CartelRaid
{
    public const int SECURITY_MIN = 80;
    public const int BALANCE_MIN = 50;
    public const int SECOND_VISIT_DELAY = 3;
    public const string INCAPACITANT_SOLD_KEY = "cartel_raid_incapacitant_sold";   // 1 once sold to the authorities
    public const string DUE_KEY = "cartel_raid_officer_due";                       // night the conditions held
    public const string ANSWER_KEY = "cartel_raid_officer_answer";                 // 1 reported, 2 declined
    public const string SECOND_DUE_KEY = "cartel_raid_officer_second_due";         // day of the second visit
    public const string SECOND_ANSWER_KEY = "cartel_raid_officer_second_answer";   // 1 reported, 2 declined
    public const string AGAINST_CARTEL_KEY = "cartel_network_against_cartel";      // label: day the player reported
    public const string CLIENT_ID = "vce_cartel_raid_officer";

    public static void Enable()
    {
        ModHook.OnGoingSleepEarly += OnGoingSleep;
        ModHook.OnGenerateCustomerNormal += OnGenerateCustomers;
        try
        {
            string id = DreamToxinMod.MOD_ID;
            ModHelper.AddDebugFunction(id, "Modding", "Cartel raid: log conditions", LogConditions, false);
            ModHelper.AddDebugFunction(id, "Modding", "Cartel raid: officer visits now",
                delegate { PlayerStore.Instance.storeClientManager.AddClient(CreateOfficer(ANSWER_KEY)); }, false);
        }
        catch (Exception e)
        {
            Log.Error("cartel raid debug function registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnGoingSleepEarly -= OnGoingSleep;
        ModHook.OnGenerateCustomerNormal -= OnGenerateCustomers;
    }

    public static void MarkIncapacitantSold()
    {
        CartelNetwork.SetData(INCAPACITANT_SOLD_KEY, 1);
    }

    public static bool IsAgainstCartel()
    {
        return CartelNetwork.Data(AGAINST_CARTEL_KEY) > 0;
    }

    public static int Balance()
    {
        return StoreReputation.GetULReputation() + StoreReputation.GetSecReputation()
            - (StoreReputation.GetLLReputation() + StoreReputation.GetRevReputation() + StoreReputation.GetBMReputation());
    }

    public static bool SidesWithTheLaw()
    {
        return CartelNetwork.Data(INCAPACITANT_SOLD_KEY) > 0 || Balance() >= BALANCE_MIN
            || StoreReputation.GetLLReputation() < 0 || StoreReputation.GetRevReputation() < 0 || StoreReputation.GetBMReputation() < 0;
    }

    public static bool ConditionsHold()
    {
        if (!IaSettings.CartelEnabled)
        {
            return false;
        }
        return CartelNetwork.Data(CartelBoycott.COUNT_KEY) >= 1
            && StoreReputation.GetSecReputation() >= SECURITY_MIN
            && SidesWithTheLaw()
            && !CartelNetwork.IsMember() && CartelNetwork.HasRefused();
    }

    // Every night, until the conditions are first met.
    public static void OnGoingSleep()
    {
        if (!IaSettings.CartelEnabled)
        {
            return;
        }
        try
        {
            if (CartelNetwork.Data(DUE_KEY) == 0 && CartelNetwork.Data(ANSWER_KEY) == 0 && ConditionsHold())
            {
                CartelNetwork.SetData(DUE_KEY, Math.Max(1, StoreStation.Instance.dayCounter));
                Log.Info("cartel raid: conditions met on day " + StoreStation.Instance.dayCounter + "; the officer comes tomorrow");
            }
        }
        catch (Exception e)
        {
            Log.Error("cartel raid night check failed", e);
        }
    }

    // The second refusal of the cartel: if the conditions hold and the player has not reported the cartel yet, the
    // officer comes back in three days.
    public static void OnSecondRefusal()
    {
        if (!IaSettings.CartelEnabled)
        {
            return;
        }
        try
        {
            if (CartelNetwork.Data(SECOND_DUE_KEY) == 0 && !IsAgainstCartel() && ConditionsHold())
            {
                CartelNetwork.SetData(SECOND_DUE_KEY, StoreStation.Instance.dayCounter + SECOND_VISIT_DELAY);
                Log.Info("cartel raid: the officer comes back on day " + CartelNetwork.Data(SECOND_DUE_KEY));
            }
        }
        catch (Exception e)
        {
            Log.Error("cartel raid second visit scheduling failed", e);
        }
    }

    // First visit: from the day after the conditions were met. Second visit: from its scheduled day. Each until the
    // player has answered it; never both on one day.
    public static void OnGenerateCustomers(StoreClientManager manager)
    {
        if (!IaSettings.CartelEnabled)
        {
            return;
        }
        try
        {
            if (manager == null || CartelNetwork.IsMember())
            {
                return;
            }
            int today = StoreStation.Instance.dayCounter;
            int due = CartelNetwork.Data(DUE_KEY);
            int secondDue = CartelNetwork.Data(SECOND_DUE_KEY);
            if (due > 0 && due < today && CartelNetwork.Data(ANSWER_KEY) == 0)
            {
                manager.AddClient(CreateOfficer(ANSWER_KEY));
                Log.Info("cartel raid: the Security officer visits (day " + today + ")");
            }
            else if (secondDue > 0 && today >= secondDue && CartelNetwork.Data(SECOND_ANSWER_KEY) == 0 && !IsAgainstCartel())
            {
                manager.AddClient(CreateOfficer(SECOND_ANSWER_KEY));
                Log.Info("cartel raid: the Security officer comes back (day " + today + ")");
            }
        }
        catch (Exception e)
        {
            Log.Error("cartel raid officer failed", e);
        }
    }

    // answerKey: where this visit's answer is kept (ANSWER_KEY or SECOND_ANSWER_KEY).
    public static StoreClient CreateOfficer(string answerKey)
    {
        StoreClient client = new StoreClient();
        client.identifier = CLIENT_ID;
        client.displayName = Text.Get("cartel_raid_officer_name");
        client.spriteName = SpriteDict.GetRandomSecSpriteName();
        StoreClientFactionSetup.InitSecurity(client);
        client.clientIntent = StoreClient.ClientIntent.DIALOGUE;
        client.dismissable = false;
        string name = client.displayName;

        Dialogue ask = client.mainDialogue.SetText(name, Text.Get("cartel_raid_officer_1"));

        Dialogue report = new Dialogue().SetText("PLAYER", Text.Get("cartel_raid_choice_report"));
        report.NextDialogue().SetText(name, Text.Get("cartel_raid_officer_report_1"))
            .NextDialogue().SetText(name, Text.Get("cartel_raid_officer_report_2"))
            .SetEndAction(delegate
            {
                CartelNetwork.SetData(answerKey, 1);
                CartelNetwork.SetData(AGAINST_CARTEL_KEY, Math.Max(1, StoreStation.Instance.dayCounter));
                Log.Info("cartel raid: the player reported the cartel (label " + AGAINST_CARTEL_KEY + ")");
                CartelContact.Leave();
            });
        ask.AddChoice(DirectoryMaster.Item("$", false), "", "", report, null);

        // Someone who has refused the cartel twice has nothing left to protect. No reply from him: act in the choice
        // action (an end action on the player's own line is never run).
        if (CartelNetwork.Data(CartelBoycott.SECOND_REFUSED_KEY) == 0)
        {
            Dialogue decline = new Dialogue().SetText("PLAYER", Text.Get("cartel_raid_choice_decline"));
            ask.AddChoice(DirectoryMaster.Item("$", false), "", "", decline, delegate
            {
                CartelNetwork.SetData(answerKey, 2);
                CartelContact.Leave();
            });
        }

        client.CompleteClientCreation();
        return client;
    }

    private static void LogConditions()
    {
        Log.Info("cartel raid conditions: boycotts " + CartelNetwork.Data(CartelBoycott.COUNT_KEY)
            + ", security " + StoreReputation.GetSecReputation() + " (need " + SECURITY_MIN + ")"
            + ", incapacitant sold " + (CartelNetwork.Data(INCAPACITANT_SOLD_KEY) > 0)
            + ", balance " + Balance() + " (need " + BALANCE_MIN + ")"
            + ", LL/Rev/BM " + StoreReputation.GetLLReputation() + "/" + StoreReputation.GetRevReputation() + "/" + StoreReputation.GetBMReputation()
            + ", member " + CartelNetwork.IsMember() + ", refused " + CartelNetwork.HasRefused()
            + " => " + (ConditionsHold() ? "met" : "not met"));
    }
}

// The sweep: once the player has reported the cartel (the "against the cartel" label), Security buys supplies for it
// in a three-day cycle, starting the day after the report:
//   day 1  a Security buyer for medical goods ("MEDICAL"),
//   day 2  a Security buyer for military supplies ("FOOD" or "WEAPON"),
//   day 3  nobody.
// Both pay 50% less for anything (a selling price feature, like vanilla's Upper Level tips but negative), and each
// has a fixed 500 budget that carries over between visits. When a buyer's budget reaches 0 (vanilla lets him take a
// dearer item and pays only what is left), he thanks the player and stops coming. Once both are done the
// cartel is destroyed (label; the cartel's boycott ends), and a week later a Security logistics officer comes to
// thank the shop: Security +50, Black Market -20, and Security gains a perk at 120, "Logistics' Gratitude": a 15%
// tip on anything Security buys.
public static class CartelSweep
{
    public const int BUDGET = 500;
    public const int EXHAUSTED_BELOW = 1;  // spent means 0 left: vanilla lets a buyer take a dearer item and pays only what is left
    public const int DISCOUNT_PERCENT = -50;
    public const int CYCLE = 3;
    public const int THANKS_AFTER_DAYS = 7;
    public const int THANKS_SECURITY_REP = 50;
    public const int THANKS_BLACK_MARKET_REP = -20;

    public const string MEDICAL_ID = "vce_sweep_medical_buyer";
    public const string MILITARY_ID = "vce_sweep_military_buyer";
    public const string THANKS_ID = "vce_sweep_logistics_officer";

    // Spent budget per buyer, and whether he is done.
    public const string MEDICAL_SPENT_KEY = "cartel_sweep_medical_spent";
    public const string MILITARY_SPENT_KEY = "cartel_sweep_military_spent";
    public const string MEDICAL_DONE_KEY = "cartel_sweep_medical_done";
    public const string MILITARY_DONE_KEY = "cartel_sweep_military_done";
    // Labels: the day the cartel was destroyed, and the day Security came to thank the shop.
    public const string DESTROYED_KEY = "cartel_network_cartel_destroyed";
    public const string THANKED_KEY = "cartel_sweep_thanked";

    public const string PERK_ID = "VCE_SEC_LOGISTICS";
    public const int PERK_THRESHOLD = 120;
    public const string DISCOUNT_FEATURE_ID = "vceSecurityRequisition";

    public static void Enable()
    {
        ModHook.OnGenerateCustomerNormal += OnGenerateCustomers;
        try
        {
            string id = DreamToxinMod.MOD_ID;
            ModHelper.AddDebugFunction(id, "Modding", "Cartel sweep: report the cartel now",
                delegate { CartelNetwork.SetData(CartelRaid.AGAINST_CARTEL_KEY, Math.Max(1, StoreStation.Instance.dayCounter - 1)); }, false);
            ModHelper.AddDebugFunction(id, "Modding", "Cartel sweep: medical buyer now",
                delegate { PlayerStore.Instance.storeClientManager.AddClient(CreateBuyer(true)); }, false);
            ModHelper.AddDebugFunction(id, "Modding", "Cartel sweep: military buyer now",
                delegate { PlayerStore.Instance.storeClientManager.AddClient(CreateBuyer(false)); }, false);
            ModHelper.AddDebugFunction(id, "Modding", "Cartel sweep: thanks officer now",
                delegate { PlayerStore.Instance.storeClientManager.AddClient(CreateThanks()); }, false);
        }
        catch (Exception e)
        {
            Log.Error("cartel sweep debug function registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnGenerateCustomerNormal -= OnGenerateCustomers;
    }

    public static bool IsCartelDestroyed()
    {
        return CartelNetwork.Data(DESTROYED_KEY) > 0;
    }

    public static bool IsBuyer(StoreClient client)
    {
        return client != null && (client.identifier == MEDICAL_ID || client.identifier == MILITARY_ID);
    }

    private static string SpentKey(bool medical)
    {
        return medical ? MEDICAL_SPENT_KEY : MILITARY_SPENT_KEY;
    }

    private static string DoneKey(bool medical)
    {
        return medical ? MEDICAL_DONE_KEY : MILITARY_DONE_KEY;
    }

    public static int Remaining(bool medical)
    {
        return Math.Max(0, BUDGET - CartelNetwork.Data(SpentKey(medical)));
    }

    public static bool IsDone(bool medical)
    {
        return CartelNetwork.Data(DoneKey(medical)) > 0;
    }

    public static void OnGenerateCustomers(StoreClientManager manager)
    {
        if (!IaSettings.CartelEnabled)
        {
            return;
        }
        try
        {
            if (manager == null)
            {
                return;
            }
            int today = StoreStation.Instance.dayCounter;
            int destroyed = CartelNetwork.Data(DESTROYED_KEY);
            if (destroyed > 0)
            {
                if (CartelNetwork.Data(THANKED_KEY) == 0 && today >= destroyed + THANKS_AFTER_DAYS)
                {
                    manager.AddClient(CreateThanks());
                    Log.Info("cartel sweep: the logistics officer visits (day " + today + ")");
                }
                return;
            }
            int reported = CartelNetwork.Data(CartelRaid.AGAINST_CARTEL_KEY);
            if (reported == 0 || today <= reported)
            {
                return;
            }
            int phase = (today - reported - 1) % CYCLE;
            if (phase == 0 && !IsDone(true))
            {
                manager.AddClient(CreateBuyer(true));
                Log.Info("cartel sweep: medical buyer visits, budget " + Remaining(true));
            }
            else if (phase == 1 && !IsDone(false))
            {
                manager.AddClient(CreateBuyer(false));
                Log.Info("cartel sweep: military buyer visits, budget " + Remaining(false));
            }
        }
        catch (Exception e)
        {
            Log.Error("cartel sweep visitors failed", e);
        }
    }

    public static StoreClient CreateBuyer(bool medical)
    {
        StoreClient client = new StoreClient();
        client.identifier = medical ? MEDICAL_ID : MILITARY_ID;
        client.displayName = Text.Get(medical ? "cartel_sweep_medical_name" : "cartel_sweep_military_name");
        client.spriteName = SpriteDict.GetRandomSecSpriteName();
        StoreClientFactionSetup.InitSecurity(client);
        client.clientIntent = StoreClient.ClientIntent.BUY;
        // A fixed budget: SetBudget would add vanilla's perk and endorsement bonuses, so force the exact amount.
        client.SetBudget(Remaining(medical));
        client.OverrideBudget(Remaining(medical));
        client.mainDialogue.SetText(client.displayName, Text.Get(medical ? "cartel_sweep_medical_1" : "cartel_sweep_military_1"))
            .SetEndAction(delegate
            {
            });
        if (medical)
        {
            client.clientBuyingTagList.Add("MEDICAL");
        }
        else
        {
            client.clientBuyingTagList.Add("FOOD");
            client.clientBuyingTagList.Add("WEAPON");
        }
        StoreClient self = client;
        client.OnDismiss = (Action)Delegate.Combine(client.OnDismiss, (Action)delegate { SaveBudget(self); });
        client.AddBasicDialog();
        // Vanilla plays allDoneDialogue once a buyer's budget is at 0; that is his thank-you line, so it is said in
        // vanilla's own place instead of competing with vanilla's "all done" line.
        client.allDoneDialogue = new Dialogue().SetText(client.displayName, Text.Get("cartel_sweep_spent")).SetEndAction(delegate
        {
            PlayerStore.Instance.DismissCurrentClient(false);
        });
        client.CompleteClientCreation();
        return client;
    }

    // After every sale to one of the buyers, and when he leaves: keep what is left of his budget. Running out ends
    // his visits (his allDoneDialogue says the thank-you line).
    public static void SaveBudget(StoreClient client)
    {
        if (!IsBuyer(client))
        {
            return;
        }
        bool medical = client.identifier == MEDICAL_ID;
        int left = Math.Max(0, client.GetBudget());
        CartelNetwork.SetData(SpentKey(medical), BUDGET - Math.Min(BUDGET, left));
        if (left >= EXHAUSTED_BELOW || IsDone(medical))
        {
            return;
        }
        CartelNetwork.SetData(DoneKey(medical), Math.Max(1, StoreStation.Instance.dayCounter));
        Log.Info("cartel sweep: " + (medical ? "medical" : "military") + " buyer's budget spent");
        if (IsDone(true) && IsDone(false) && !IsCartelDestroyed())
        {
            CartelNetwork.SetData(DESTROYED_KEY, Math.Max(1, StoreStation.Instance.dayCounter));
            Log.Info("cartel sweep: the cartel is destroyed (label " + DESTROYED_KEY + ")");
        }
    }

    public static StoreClient CreateThanks()
    {
        StoreClient client = new StoreClient();
        client.identifier = THANKS_ID;
        client.displayName = Text.Get("cartel_sweep_thanks_name");
        client.spriteName = SpriteDict.GetRandomSecCorpoSpriteName();
        StoreClientFactionSetup.InitSecurity(client);
        client.clientIntent = StoreClient.ClientIntent.DIALOGUE;
        client.dismissable = false;
        string name = client.displayName;
        client.mainDialogue.SetText(name, Text.Get("cartel_sweep_thanks_1"))
            .NextDialogue().SetText(name, Text.Get("cartel_sweep_thanks_2"))
            .NextDialogue().SetText(name, Text.Get("cartel_sweep_thanks_3"))
            .NextDialogue().SetText(name, Text.Get("cartel_sweep_thanks_4"))
            .SetEndAction(delegate
            {
                Reward();
                PlayerStore.Instance.DismissCurrentClient(false);
            });
        client.CompleteClientCreation();
        return client;
    }

    public static void Reward()
    {
        if (CartelNetwork.Data(THANKED_KEY) > 0)
        {
            return;
        }
        CartelNetwork.SetData(THANKED_KEY, Math.Max(1, StoreStation.Instance.dayCounter));
        StoreReputation.ModReputation("FACTION_SECURITY", THANKS_SECURITY_REP, true);
        StoreReputation.ModReputation("FACTION_BLACK_MARKET", THANKS_BLACK_MARKET_REP, true);
        AddLogisticsPerk();
        Log.Info("cartel sweep: Security thanked the shop; perk " + PERK_ID + " added");
    }

    // A perk on the Security faction, saved with the reputation list like vanilla's own; it shows up only now.
    public static void AddLogisticsPerk()
    {
        StoreReputation security = StoreReputation.GetStoreReputation("FACTION_SECURITY");
        if (security == null)
        {
            return;
        }
        if (security.perks == null)
        {
            security.perks = new List<StoreReputationPerk>();
        }
        if (!security.perks.Exists(p => p != null && p.perkId == PERK_ID))
        {
            security.perks.Add(new StoreReputationPerk
            {
                perkId = PERK_ID,
                perkThreshold = PERK_THRESHOLD,
                perkType = 1,
                iconName = "Referal"
            });
        }
        StoreReputation.UpdateReputation();
    }

    public static ItemFeature CreateDiscount()
    {
        ItemFeature feature = new ItemFeature(ItemFeature.FeatureType.TemporarySelling);
        feature.identifier = DISCOUNT_FEATURE_ID;
        feature.category = DISCOUNT_FEATURE_ID;
        feature.valueStage = ItemFeature.ValueStage.Market;
        string display = Text.Get("cartel_sweep_discount");
        feature.SetPublicDisplay(display);
        feature.SetActualDisplay(display);
        feature.isFeatureMatch = true;
        feature.SetPreExposedValueMod(DISCOUNT_PERCENT);
        return feature;
    }
}

// Selling to Security: the sweep buyers pay half; with "Logistics' Gratitude", every Security buyer tips 15%.
[HarmonyPatch(typeof(GameItem), "OnAddedToWeightedArea")]
public static class CartelSweepPricePatch
{
    private static void Postfix(GameItem __instance)
    {
        try
        {
            if (PlayerStore.Instance.currentClientInstance == null || !GeneralHelper.IsItemOwned(__instance))
            {
                return;
            }
            StoreClient client = PlayerStore.Instance.currentClientInstance.GetClientBlueprint();
            if (client.clientIntent != StoreClient.ClientIntent.BUY && client.clientIntent != StoreClient.ClientIntent.SELLNBUY)
            {
                return;
            }
            if (CartelSweep.IsBuyer(client))
            {
                __instance.RemoveItemFeatureByID(CartelSweep.DISCOUNT_FEATURE_ID);
                __instance.AddItemFeature(CartelSweep.CreateDiscount());
            }
            if (IaSettings.CartelEnabled && client.clientFaction == "FACTION_SECURITY" && StoreReputation.IsPerkUnlocked(CartelSweep.PERK_ID))
            {
                __instance.RemoveItemFeatureByID("tips");
                __instance.AddItemFeature(ItemFeatureList.Pourboire());
            }
        }
        catch (Exception e)
        {
            Log.Error("cartel sweep price feature failed", e);
        }
    }
}

[HarmonyPatch(typeof(PlayerStore), "SellItem")]
public static class CartelSweepSalePatch
{
    private static void Postfix()
    {
        try
        {
            if (PlayerStore.Instance.currentClientInstance != null)
            {
                CartelSweep.SaveBudget(PlayerStore.Instance.currentClientInstance.GetClientBlueprint());
            }
        }
        catch (Exception e)
        {
            Log.Error("cartel sweep budget tracking failed", e);
        }
    }
}

// The perk's name and texts (vanilla looks them up in its own table by perk ID).
[HarmonyPatch(typeof(StoreReputationPerk), "GetLocalizedPerkName")]
public static class CartelSweepPerkNamePatch
{
    private static void Postfix(StoreReputationPerk __instance, ref string __result)
    {
        if (__instance != null && __instance.perkId == CartelSweep.PERK_ID)
        {
            __result = Text.Get("rep_perk_logistics_name");
        }
    }
}

[HarmonyPatch(typeof(StoreReputationPerk), "GetLocalizedDescription")]
public static class CartelSweepPerkDescPatch
{
    private static void Postfix(StoreReputationPerk __instance, ref string __result)
    {
        if (__instance != null && __instance.perkId == CartelSweep.PERK_ID)
        {
            __result = Text.Get("rep_perk_logistics_desc");
        }
    }
}

[HarmonyPatch(typeof(StoreReputationPerk), "GetLocalizedFlavorText")]
public static class CartelSweepPerkFlavorPatch
{
    private static void Postfix(StoreReputationPerk __instance, ref string __result)
    {
        if (__instance != null && __instance.perkId == CartelSweep.PERK_ID)
        {
            __result = Text.Get("rep_perk_logistics_flavor");
        }
    }
}

// The Cartel Network's own -100 perk. Vanilla's cartel has CARTEL_RETIRED; this one has its own ID, so its texts are
// ours and it does nothing once the mod is gone. Like any vanilla perk with a negative threshold it is active while
// the reputation is at or below -100 (StoreReputation.UpdateReputation). While it is active, the morning the shutter
// opens vanilla's cartel hitman is waiting (PlayerStore.SetupWaitingClient("cartel")), and the run ends with vanilla's
// "retired by the cartel" ending.
public static class CartelNetworkPerks
{
    public const string RETIRED_ID = "VCE_NET_RETIRED";
    public const int RETIRED_THRESHOLD = -100;
    public const string BOYCOTT_ID = "VCE_NET_BOYCOTT";      // -60: the black market stops coming (CartelNetworkBoycott)
    public const int BOYCOTT_THRESHOLD = -60;
    public const string DISTRUST_ID = "VCE_NET_DISTRUST";    // -30: contraband markup halved (CartelNetworkDistrust)
    public const int DISTRUST_THRESHOLD = -30;
    public const string OPEN_TRADE_ID = "VCE_NET_OPEN_TRADE";  // 0: two Cartel traders a week (CartelTrader)
    public const int OPEN_TRADE_THRESHOLD = 0;
    public const string TIGHT_LIPS_ID = "VCE_NET_TIGHT_LIPS";  // 25: half the bad-sale complaints (CartelTightLips)
    public const int TIGHT_LIPS_THRESHOLD = 25;
    public const string NO_STRANGERS_ID = "VCE_NET_NO_STRANGERS";  // 50: free Cartel Protection instead of insurance
    public const int NO_STRANGERS_THRESHOLD = 50;
    public const string SUPPLY_ID = "VCE_NET_SUPPLY";  // 75: Cartel Network phone supplier (CartelSupply)
    public const int SUPPLY_THRESHOLD = 75;
    public const string FRIENDS_ID = "VCE_NET_FRIENDS";  // 100: every sentence is a fine, at most 1000 (CartelFriends)
    public const int FRIENDS_THRESHOLD = 100;

    public static void Enable()
    {
        ModHook.OnGameLoadedLate += EnsurePerks;
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Cartel Network: -50 reputation",
                delegate { StoreReputation.ModReputation(CartelNetwork.FACTION_ID, -50, true); }, false);
        }
        catch (Exception e)
        {
            Log.Error("cartel network perk debug registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnGameLoadedLate -= EnsurePerks;
    }

    // Lowest threshold first, like vanilla's lists.
    public static List<StoreReputationPerk> Create()
    {
        return new List<StoreReputationPerk>
        {
            Perk(RETIRED_ID, RETIRED_THRESHOLD),
            Perk(BOYCOTT_ID, BOYCOTT_THRESHOLD),
            Perk(DISTRUST_ID, DISTRUST_THRESHOLD),
            Perk(OPEN_TRADE_ID, OPEN_TRADE_THRESHOLD),
            Perk(TIGHT_LIPS_ID, TIGHT_LIPS_THRESHOLD),
            Perk(NO_STRANGERS_ID, NO_STRANGERS_THRESHOLD),
            Perk(SUPPLY_ID, SUPPLY_THRESHOLD),
            Perk(FRIENDS_ID, FRIENDS_THRESHOLD),
        };
    }

    private static StoreReputationPerk Perk(string id, int threshold)
    {
        return new StoreReputationPerk
        {
            perkId = id,
            perkThreshold = threshold,
            perkType = threshold < 0 ? -1 : 1,
            iconName = "Referal"
        };
    }

    // Members who joined before a perk existed get it on load (in the same order Create lists them).
    public static void EnsurePerks()
    {
        if (!IaSettings.CartelEnabled)
        {
            return;
        }
        try
        {
            StoreReputation network = CartelNetwork.Find();
            if (network == null)
            {
                return;
            }
            if (network.perks == null)
            {
                network.perks = CartelNetworkPerks.Create();
            }
            List<StoreReputationPerk> wanted = Create();
            for (int i = 0; i < wanted.Count; i++)
            {
                StoreReputationPerk perk = wanted[i];
                if (!network.perks.Exists(p => p != null && p.perkId == perk.perkId))
                {
                    network.perks.Insert(Math.Min(i, network.perks.Count), perk);
                }
            }
            StoreReputation.UpdateReputation();
        }
        catch (Exception e)
        {
            Log.Error("adding the Cartel Network perks failed", e);
        }
    }

    public static string TextKey(string perkId)
    {
        switch (perkId)
        {
            case RETIRED_ID: return "rep_perk_net_retired";
            case BOYCOTT_ID: return "rep_perk_net_boycott";
            case DISTRUST_ID: return "rep_perk_net_distrust";
            case OPEN_TRADE_ID: return "rep_perk_net_open_trade";
            case TIGHT_LIPS_ID: return "rep_perk_net_tight_lips";
            case NO_STRANGERS_ID: return "rep_perk_net_no_strangers";
            case SUPPLY_ID: return "rep_perk_net_supply";
            case FRIENDS_ID: return "rep_perk_net_friends";
            default: return null;
        }
    }
}

// The morning after the reputation falls to -100: vanilla's cartel hitman, unless vanilla already sent its own.
[HarmonyPatch(typeof(PlayerStore), "OnLateShutterOpen")]
public static class CartelNetworkRetiredPatch
{
    private static void Postfix(PlayerStore __instance)
    {
        try
        {
            if (IaSettings.CartelEnabled && StoreReputation.IsPerkUnlocked(CartelNetworkPerks.RETIRED_ID) && !StoreReputation.IsPerkUnlocked("CARTEL_RETIRED"))
            {
                Log.Info("cartel network: reputation at -100 or lower, the cartel's hitman is waiting");
                __instance.SetupWaitingClient("cartel");
            }
        }
        catch (Exception e)
        {
            Log.Error("cartel network retirement failed", e);
        }
    }
}

[HarmonyPatch(typeof(StoreReputationPerk), "GetLocalizedPerkName")]
public static class CartelNetworkPerkNamePatch
{
    private static void Postfix(StoreReputationPerk __instance, ref string __result)
    {
        string key = __instance != null ? CartelNetworkPerks.TextKey(__instance.perkId) : null;
        if (key != null)
        {
            __result = Text.Get(key + "_name");
        }
    }
}

[HarmonyPatch(typeof(StoreReputationPerk), "GetLocalizedDescription")]
public static class CartelNetworkPerkDescPatch
{
    private static void Postfix(StoreReputationPerk __instance, ref string __result)
    {
        string key = __instance != null ? CartelNetworkPerks.TextKey(__instance.perkId) : null;
        if (key != null)
        {
            __result = Text.Get(key + "_desc");
        }
    }
}

[HarmonyPatch(typeof(StoreReputationPerk), "GetLocalizedFlavorText")]
public static class CartelNetworkPerkFlavorPatch
{
    private static void Postfix(StoreReputationPerk __instance, ref string __result)
    {
        string key = __instance != null ? CartelNetworkPerks.TextKey(__instance.perkId) : null;
        if (key != null)
        {
            __result = Text.Get(key + "_flavor");
        }
    }
}

// Cartel Network reputation -60, "Boycott": the black market stops coming. Blocked when they would join the queue
// (StoreClientManager.AddClient / AddNextClient): clients who trade (buy, sell, buy-and-sell, barter) and are either
// Black Market or buy contraband as such ("CONTRABAND" buying tag). Talk-only and story clients (loan shark,
// janitor, Lou, the cartel's hitman, this mod's cartel visitors) are not traders and still come; so do the
// phone-ordered chemical couriers and a few one-off event clients listed in PROTECTED.
public static class CartelNetworkBoycott
{
    public static readonly HashSet<string> PROTECTED = new HashSet<string>
    {
        "courrierChem", "courrierChemFree", "hypertech_smuggler", "woundedMan", CartelSupply.COURIER_ID, CartelTrader.CLIENT_ID, CartelPeer.CLIENT_ID,
    };

    public static bool IsActive()
    {
        if (!IaSettings.CartelEnabled)
        {
            return false;
        }
        return StoreReputation.IsPerkUnlocked(CartelNetworkPerks.BOYCOTT_ID);
    }

    public static bool IsBlocked(StoreClient client)
    {
        if (client == null || !IsActive() || (client.identifier != null && PROTECTED.Contains(client.identifier)))
        {
            return false;
        }
        StoreClient.ClientIntent intent = client.clientIntent;
        bool trades = intent == StoreClient.ClientIntent.BUY || intent == StoreClient.ClientIntent.SELL
            || intent == StoreClient.ClientIntent.SELLNBUY || intent == StoreClient.ClientIntent.BARTER;
        if (!trades)
        {
            return false;
        }
        return client.clientFaction == "FACTION_BLACK_MARKET"
            || (client.clientBuyingTagList != null && client.clientBuyingTagList.Contains("CONTRABAND"));
    }
}

[HarmonyPatch(typeof(StoreClientManager), "AddClient")]
public static class CartelNetworkBoycottAddPatch
{
    private static bool Prefix(StoreClient storeClient)
    {
        if (CartelNetworkBoycott.IsBlocked(storeClient))
        {
            Log.Info("cartel network boycott: " + storeClient.identifier + " stays away");
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(StoreClientManager), "AddNextClient")]
public static class CartelNetworkBoycottAddNextPatch
{
    private static bool Prefix(StoreClient storeClient)
    {
        if (CartelNetworkBoycott.IsBlocked(storeClient))
        {
            Log.Info("cartel network boycott: " + storeClient.identifier + " stays away");
            return false;
        }
        return true;
    }
}

// Cartel Network reputation -30, "Distrust": the contraband markup is halved, the way a gunsmith's permit halves it
// for covered guns (vanilla's restricted-class markups are contrabandMarkup / 2).
public static class CartelNetworkDistrust
{
    public static bool IsActive()
    {
        if (!IaSettings.CartelEnabled)
        {
            return false;
        }
        return StoreReputation.IsPerkUnlocked(CartelNetworkPerks.DISTRUST_ID);
    }

    public static void Halve(ItemFeature feature, int markup, string vanillaKey)
    {
        if (feature == null || !IsActive())
        {
            return;
        }
        feature.SetPreExposedValueMod(markup / 2);
        string display = LocHelper.GetLocalizedMechanic(vanillaKey) + Text.Get("cartel_distrust_suffix");
        feature.SetPublicDisplay(display);
        feature.SetActualDisplay(display);
    }
}

// Installed late (DeferredPatches, DreamToxinMod.cs): Prepare keeps the loader from installing it at load.
[HarmonyPatch(typeof(ItemFeatureList), "ContrabandLowMarkUp")]
public static class CartelDistrustLowPatch
{
    private static bool Prepare()
    {
        return DeferredPatches.Ready;
    }

    private static void Postfix(ItemFeature __result)
    {
        CartelNetworkDistrust.Halve(__result, PlayerStore.Instance.contrabandMarkupLow, "item_feature_contraband_low");
    }
}

// Installed late (DeferredPatches, DreamToxinMod.cs): Prepare keeps the loader from installing it at load.
[HarmonyPatch(typeof(ItemFeatureList), "ContrabandMidMarkUp")]
public static class CartelDistrustMidPatch
{
    private static bool Prepare()
    {
        return DeferredPatches.Ready;
    }

    private static void Postfix(ItemFeature __result)
    {
        CartelNetworkDistrust.Halve(__result, PlayerStore.Instance.contrabandMarkupMid, "item_feature_contraband_mid");
    }
}

// Installed late (DeferredPatches, DreamToxinMod.cs): Prepare keeps the loader from installing it at load.
[HarmonyPatch(typeof(ItemFeatureList), "ContrabandHighMarkUp")]
public static class CartelDistrustHighPatch
{
    private static bool Prepare()
    {
        return DeferredPatches.Ready;
    }

    private static void Postfix(ItemFeature __result)
    {
        CartelNetworkDistrust.Halve(__result, PlayerStore.Instance.contrabandMarkupHigh, "item_feature_contraband_high");
    }
}

// Installed late (DeferredPatches, DreamToxinMod.cs): Prepare keeps the loader from installing it at load.
[HarmonyPatch(typeof(ItemFeatureList), "ContrabandCriticalMarkUp")]
public static class CartelDistrustCriticalPatch
{
    private static bool Prepare()
    {
        return DeferredPatches.Ready;
    }

    private static void Postfix(ItemFeature __result)
    {
        CartelNetworkDistrust.Halve(__result, PlayerStore.Instance.contrabandMarkupCritial, "item_feature_contraband_critical");
    }
}

// Cartel Network reputation 0, "Open Trade": two random days each week a Cartel trader comes and buys any
// contraband, paying 5% less. Modelled on vanilla's shady merchant (a reseller: any purity grade, not personal use,
// no retail markup, budget 250-300 + 3 a day). He belongs to the Cartel Network, so vanilla's trade reputation
// (StoreReputation.OnItemTraded) raises the player's standing with the network when they sell to him.
public static class CartelTrader
{
    public const string CLIENT_ID = "vce_cartel_trader";
    public const int VISITS_PER_WEEK = 2;
    public const int PRICE_PERCENT = -5;
    public const string FEATURE_ID = "vceCartelTraderCut";
    public const string WEEK_KEY = "cartel_trader_week";     // week the days below were drawn for (+1)
    public const string DAYS_KEY = "cartel_trader_days";     // the two days, as first * 1000 + second

    public static void Enable()
    {
        ModHook.OnGenerateCustomerNormal += OnGenerateCustomers;
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Cartel Network: trader visits now",
                delegate { PlayerStore.Instance.storeClientManager.AddClient(Create()); }, false);
        }
        catch (Exception e)
        {
            Log.Error("cartel trader debug registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnGenerateCustomerNormal -= OnGenerateCustomers;
    }

    public static bool IsActive()
    {
        if (!IaSettings.CartelEnabled)
        {
            return false;
        }
        return StoreReputation.IsPerkUnlocked(CartelNetworkPerks.OPEN_TRADE_ID);
    }

    public static bool IsTrader(StoreClient client)
    {
        return client != null && client.identifier == CLIENT_ID;
    }

    // Days 1-7 are week 0, 8-14 week 1, and so on; the two days are drawn once per week.
    public static bool IsTradeDay(int today)
    {
        int week = Math.Max(0, today - 1) / 7;
        if (CartelNetwork.Data(WEEK_KEY) != week + 1)
        {
            int first = RNG.GetRandomInt(0, 6);
            int second = RNG.GetRandomInt(0, 5);
            if (second >= first)
            {
                second++;
            }
            int start = week * 7 + 1;
            CartelNetwork.SetData(WEEK_KEY, week + 1);
            CartelNetwork.SetData(DAYS_KEY, (start + first) * 1000 + (start + second));
        }
        int days = CartelNetwork.Data(DAYS_KEY);
        return today == days / 1000 || today == days % 1000;
    }

    public static void OnGenerateCustomers(StoreClientManager manager)
    {
        if (!IaSettings.CartelEnabled)
        {
            return;
        }
        try
        {
            if (manager != null && IsActive() && IsTradeDay(StoreStation.Instance.dayCounter))
            {
                manager.AddClient(Create());
                Log.Info("cartel network: the Cartel trader visits (day " + StoreStation.Instance.dayCounter + ")");
            }
        }
        catch (Exception e)
        {
            Log.Error("cartel trader failed", e);
        }
    }

    public static StoreClient Create()
    {
        StoreClient client = new StoreClient();
        client.identifier = CLIENT_ID;
        client.displayName = Text.Get("cartel_trader_name");
        client.spriteName = SpriteDict.GetRandomShadySpriteName();
        client.clientFaction = CartelNetwork.FACTION_ID;
        client.tradeReputationMultiplier = CartelTradeRep.MULTIPLIER;  // 4x Cartel Network reputation, buying or selling
        client.IsPersonalConsumption = false;
        client.SetBudget(250 + RNG.GetRandomInt(0, 50) + StoreStation.Instance.dayCounter * 3);
        client.clientIntent = StoreClient.ClientIntent.BUY;
        client.acceptedChemicalProductGrade = 0;
        client.CanExposeChem = true;
        client.acceptRetailMarkup = false;
        client.mainDialogue.SetText(client.displayName, Text.Get("cartel_trader_1"))
            .NextDialogue().SetText(client.displayName, Text.Get("cartel_trader_2", client.GetBudget()))
            .SetEndAction(delegate
            {
            });
        client.clientBuyingTagList.Add("CONTRABAND");
        client.AddBasicDialog();
        client.CompleteClientCreation();
        return client;
    }

    public static ItemFeature CreateCut()
    {
        ItemFeature feature = new ItemFeature(ItemFeature.FeatureType.TemporarySelling);
        feature.identifier = FEATURE_ID;
        feature.category = FEATURE_ID;
        feature.valueStage = ItemFeature.ValueStage.Market;
        string display = Text.Get("cartel_trader_cut");
        feature.SetPublicDisplay(display);
        feature.SetActualDisplay(display);
        feature.isFeatureMatch = true;
        feature.SetPreExposedValueMod(PRICE_PERCENT);
        return feature;
    }
}

// The trader's 5% cut on whatever the player puts on the counter for him.
[HarmonyPatch(typeof(GameItem), "OnAddedToWeightedArea")]
public static class CartelTraderPricePatch
{
    private static void Postfix(GameItem __instance)
    {
        try
        {
            if (PlayerStore.Instance.currentClientInstance == null || !GeneralHelper.IsItemOwned(__instance)
                || !CartelTrader.IsTrader(PlayerStore.Instance.currentClientInstance.GetClientBlueprint()))
            {
                return;
            }
            __instance.RemoveItemFeatureByID(CartelTrader.FEATURE_ID);
            __instance.AddItemFeature(CartelTrader.CreateCut());
        }
        catch (Exception e)
        {
            Log.Error("cartel trader price failed", e);
        }
    }
}

// Cartel Network reputation 25, "Tight Lips": the morning reaction to a bad chemical sale (mislabelled purity, toxic
// or lethal contents...: vanilla rolls feedbackLevel x 8% per problem item) comes half as often. Before vanilla
// handles yesterday's sales, each client with a problem sale is let off with a coin flip; a let-off client's
// chemical sales are kept out of vanilla's feedback (CanReturn = false), exactly as vanilla does for clients who
// may not come back. Scripted always-report sales are left alone.
public static class CartelTightLips
{
    public static bool IsActive()
    {
        if (!IaSettings.CartelEnabled)
        {
            return false;
        }
        return StoreReputation.IsPerkUnlocked(CartelNetworkPerks.TIGHT_LIPS_ID);
    }

    public static void Filter()
    {
        List<SoldItem> sold = PlayerStore.Instance.soldItemsTracker;
        if (!IsActive() || sold == null || sold.Count == 0)
        {
            return;
        }
        Dictionary<string, List<SoldItem>> byClient = new Dictionary<string, List<SoldItem>>();
        foreach (SoldItem soldItem in sold)
        {
            if (soldItem == null || !soldItem.CanReturn || soldItem.clientId == null)
            {
                continue;
            }
            GameItem item = soldItem.GetGameItem();
            if (item == null || !item.IsTag("CHEMICAL_INTENT_TAG"))
            {
                continue;
            }
            List<SoldItem> list;
            if (!byClient.TryGetValue(soldItem.clientId, out list))
            {
                list = new List<SoldItem>();
                byClient[soldItem.clientId] = list;
            }
            list.Add(soldItem);
        }
        int spared = 0;
        foreach (KeyValuePair<string, List<SoldItem>> entry in byClient)
        {
            bool problem = false;
            bool always = false;
            foreach (SoldItem soldItem in entry.Value)
            {
                always |= soldItem.alwaysFeedback;
                problem |= ChemicalFeedbackHelper.GetItemFeedbackLevel(soldItem.GetGameItem()) > 0;
            }
            if (!problem || always || !RNG.Roll(50))
            {
                continue;
            }
            foreach (SoldItem soldItem in entry.Value)
            {
                soldItem.CanReturn = false;
            }
            spared++;
        }
        if (spared > 0)
        {
            Log.Info("cartel network: tight lips spared " + spared + " complaint roll(s)");
        }
    }
}

[HarmonyPatch(typeof(SoldItem), "HandleSoldItem")]
public static class CartelTightLipsPatch
{
    private static void Prefix()
    {
        try
        {
            CartelTightLips.Filter();
        }
        catch (Exception e)
        {
            Log.Error("cartel network tight lips failed", e);
        }
    }
}

// Cartel Network reputation 50, "No Strangers": the chemist start's free Cartel Protection instead of insurance.
// Vanilla does the rest once the CARTEL_PROTECTION service is unlocked: the end-of-day list forces a mandatory
// service on, the night skips burglary and vandalism while it is in use, and insurance hides itself while it is
// unlocked (StoreService.UpdateCost). Synced for members only, before the service costs are refreshed and before the
// end-of-day list is built: unlocked while the perk is active, locked (and off) otherwise. Players who are not
// members, the chemist under vanilla's cartel among them, are left to vanilla.
public static class CartelProtection
{
    public static bool IsActive()
    {
        if (!IaSettings.CartelEnabled)
        {
            return false;
        }
        return StoreReputation.IsPerkUnlocked(CartelNetworkPerks.NO_STRANGERS_ID);
    }

    public static void Sync()
    {
        try
        {
            // Expansion off: a Cartel Network member's protection can only have come from this perk (the chemist
            // under vanilla's cartel is not a member yet), so take it back; everyone else is left to vanilla.
            if (!IaSettings.CartelEnabled)
            {
                StoreService off = PlayerStore.Instance != null && PlayerStore.Instance.storeService != null ? StoreService.GetCartelProtectionService() : null;
                if (off != null && CartelNetwork.Find() != null)
                {
                    off.unlocked = false;
                    off.isUsing = false;
                }
                return;
            }
            if (PlayerStore.Instance == null || PlayerStore.Instance.storeService == null || !CartelNetwork.IsMember())
            {
                return;
            }
            StoreService protection = StoreService.GetCartelProtectionService();
            if (protection == null)
            {
                return;
            }
            bool on = IsActive();
            if (protection.unlocked != on)
            {
                Log.Info("cartel network: Cartel Protection " + (on ? "granted" : "withdrawn"));
            }
            protection.unlocked = on;
            if (!on)
            {
                protection.isUsing = false;
            }
        }
        catch (Exception e)
        {
            Log.Error("cartel protection sync failed", e);
        }
    }
}

[HarmonyPatch(typeof(PlayerStore), "UpdateServiceCost")]
public static class CartelProtectionCostPatch
{
    private static void Prefix()
    {
        CartelProtection.Sync();
    }
}

[HarmonyPatch(typeof(EndOfDayUIManager), "InitUI")]
public static class CartelProtectionEndOfDayPatch
{
    private static void Prefix()
    {
        CartelProtection.Sync();
    }
}

// Cartel Network reputation 75, "Supply Network": a "Cartel Network" contact in the phone book, only while the perk
// is active. "Need anything?" offers acid, base, reducing agent, lipobond agent, alcohol and N-solution (vanilla's
// supplier bottles from PreBuildChemHelper; N-solution is the violet N-methylol base precursor bottle), or "wrong
// number". An order brings a courier the next day with 20 of that bottle at a 33% wholesale discount; then the
// contact rests for a few days, like vanilla's suppliers.
public static class CartelSupply
{
    public const long NUMBER = 2278L;                      // C-A-R-T on a keypad
    public const string DIALOG_ID = "VceCartelNetwork";
    public const string COURIER_ID = "vce_cartel_courier";
    public const string STOCK_KEY = "cartel_supply_stock";
    public const int QUANTITY = 20;
    public const int WHOLESALE_DISCOUNT = -33;
    public const int COOLDOWN_DAYS = 3;

    // Liquid name keys (vanilla) for the menu, in menu order; the stock index is the position here.
    private static readonly string[] Products =
    {
        "liquid_acid", "liquid_base", "liquid_reducing_agent", "liquid_lipobond_agent", "liquid_alcohol", "liquid_n_methylol_base",
    };

    public static void Enable()
    {
        PhoneClientList.DialogFuncs[DIALOG_ID] = Dialog;
        StoreClientListDict.storeClientDict[COURIER_ID] = CreateCourier;
        ModHook.OnGameLoadedLate += Sync;
        ModHook.OnWakingUpLate += Sync;
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Cartel Network: supply courier now",
                delegate { PlayerStore.Instance.storeClientManager.AddClient(CreateCourier()); }, false);
        }
        catch (Exception e)
        {
            Log.Error("cartel supply debug registration failed", e);
        }
    }

    public static void Disable()
    {
        PhoneClientList.DialogFuncs.Remove(DIALOG_ID);
        ModHook.OnGameLoadedLate -= Sync;
        ModHook.OnWakingUpLate -= Sync;
    }

    public static bool IsActive()
    {
        if (!IaSettings.CartelEnabled)
        {
            return false;
        }
        return StoreReputation.IsPerkUnlocked(CartelNetworkPerks.SUPPLY_ID) && CartelNetwork.IsMember();
    }

    public static StorePhoneClient Contact()
    {
        StorePhoneClient contact;
        Dictionary<long, StorePhoneClient> book = PlayerStore.Instance != null ? PlayerStore.Instance.PhoneClientDict : null;
        return book != null && book.TryGetValue(NUMBER, out contact) ? contact : null;
    }

    // The contact is in the phone book while the perk is active, and stops answering (hidden) otherwise.
    public static void Sync()
    {
        try
        {
            if (PlayerStore.Instance == null || PlayerStore.Instance.PhoneClientDict == null)
            {
                return;
            }
            StorePhoneClient contact = Contact();
            if (IsActive())
            {
                if (contact == null)
                {
                    contact = new StorePhoneClient
                    {
                        displayName = "Cartel Network",
                        cooldownDuration = COOLDOWN_DAYS,
                        dialogFuncId = DIALOG_ID,
                        phoneClientType = StorePhoneClient.PhoneClientType.Supplier,
                        locID = "vce_cartel_network_contact",
                    };
                    PlayerStore.Instance.PhoneClientDict[NUMBER] = contact;
                    Log.Info("cartel network: supply contact added to the phone book (" + NUMBER + ")");
                }
                contact.dialedBefore = true;
                if (contact.phoneState == StorePhoneClient.PhoneState.NotResponding)
                {
                    contact.phoneState = StorePhoneClient.PhoneState.Regular;
                }
            }
            else if (contact != null)
            {
                contact.phoneState = StorePhoneClient.PhoneState.NotResponding;
            }
        }
        catch (Exception e)
        {
            Log.Error("cartel supply contact sync failed", e);
        }
    }

    private static Dialogue Dialog(StorePhoneClient client)
    {
        if (!IsActive())
        {
            return PhoneDialogList.BugDialog();
        }
        return client.phoneState == StorePhoneClient.PhoneState.Cooldown ? Cooldown(client) : Menu();
    }

    private static Dialogue Menu()
    {
        string name = Text.Get("cartel_supply_name");
        Dialogue menu = new Dialogue();
        menu.SetText(name, Text.Get("cartel_supply_ask"));
        for (int i = 0; i < Products.Length; i++)
        {
            int stock = i;
            Dialogue order = new Dialogue();
            order.SetText("PLAYER", "[" + LocHelper.GetLocalizedMechanic(Products[i]) + "]");
            menu.AddChoice(DirectoryMaster.Item("$", true), "", "", order, delegate
            {
                CartelNetwork.SetData(STOCK_KEY, stock);
                StorePhoneClient contact = Contact();
                if (contact != null)
                {
                    contact.UseService();
                }
                PlayerStore.Instance.QueueFuturClient(COURIER_ID, 1);
                order.NextDialogue().SetText(name, Text.Get("cartel_supply_ready")).SetEndAction(delegate
                {
                    PhoneUIManager.Instance.OnPhoneDialogEnd();
                });
            });
        }
        Dialogue wrong = new Dialogue();
        wrong.SetText("PLAYER", Text.Get("cartel_supply_wrong_number"));
        menu.AddChoice(DirectoryMaster.Item("$", true), "", "", wrong, delegate
        {
            wrong.NextDialogue().SetText(name, Text.Get("cartel_supply_silence")).SetEndAction(delegate
            {
                PhoneUIManager.Instance.OnPhoneDialogEnd();
            });
        });
        return menu;
    }

    private static Dialogue Cooldown(StorePhoneClient client)
    {
        string name = Text.Get("cartel_supply_name");
        Dialogue line = new Dialogue();
        line.SetText(name, Text.Get("cartel_supply_cooldown", client.currentCooldown)).SetEndAction(delegate
        {
            PhoneUIManager.Instance.OnPhoneDialogEnd();
        });
        return line;
    }

    private static GameItem Bottle(int stock)
    {
        switch (stock)
        {
            case 0: return PreBuildChemHelper.CreateAcidBottle();
            case 1: return PreBuildChemHelper.CreateBaseBottle();
            case 2: return PreBuildChemHelper.CreateReducingAgentBottle();
            case 3: return PreBuildChemHelper.CreateLipobondAgentBottle();
            case 4: return PreBuildChemHelper.CreateMiniAlcoholBottle();
            default: return PreBuildChemHelper.CreateFantaPrecusorBottle();
        }
    }

    // A courier who lays out the order and sells it at a 33% wholesale discount, then leaves.
    public static StoreClient CreateCourier()
    {
        StoreClient client = new StoreClient();
        client.identifier = COURIER_ID;
        client.displayName = Text.Get("cartel_courier_name");
        client.spriteName = SpriteDict.GetRandomShadySpriteName();
        client.clientFaction = CartelNetwork.FACTION_ID;
        client.tradeReputationMultiplier = CartelTradeRep.MULTIPLIER;  // 4x Cartel Network reputation, buying or selling
        client.isBargainAvailable = false;
        client.clientIntent = StoreClient.ClientIntent.SELL;
        client.wholesaleDiscount = WHOLESALE_DISCOUNT;
        client.isOfferWholesale = true;
        int stock = CartelNetwork.Data(STOCK_KEY);
        client.mainDialogue.SetText(client.displayName, Text.Get("cartel_courier_1"))
            .SetEndAction(delegate
            {
                for (int i = 0; i < QUANTITY; i++)
                {
                    PlayerStore.Instance.AddDirectSellingItemToTable(Bottle(stock), false, false, false, 0);
                }
            });
        client.allDoneDialogue = new Dialogue().SetText(client.displayName, Text.Get("cartel_courier_done")).SetEndAction(delegate
        {
            DialogUIManager.Instance.CurrentClientLeave();
        });
        client.CompleteClientCreation();
        return client;
    }
}

// The contact's name in the phone book (vanilla looks it up in its own name table).
[HarmonyPatch(typeof(StorePhoneClient), "GetLocalizedDisplayName")]
public static class CartelSupplyNamePatch
{
    private static void Postfix(StorePhoneClient __instance, ref string __result)
    {
        if (__instance != null && __instance.dialogFuncId == CartelSupply.DIALOG_ID)
        {
            __result = Text.Get("cartel_supply_name");
        }
    }
}

// Cartel Network reputation 100, "Friends in High Places": whatever the crime value, Security's sentence is a fine,
// equal to the crime value but at most 1000. Vanilla's sentence officer (StoreClientListSecurity.CreateSecurityOfficer)
// picks fine / arrest / execution by crime value (under 300 / under 1500 / above); with the perk the arrest and
// execution branches graft vanilla's own fine dialogue instead, and the fine itself (SecData.GetFineValue, used by
// that dialogue) is capped. The record's projected sentence reads "fine" too. Unable to pay is still vanilla's arrest.
public static class CartelFriends
{
    public const int FINE_CAP = 1000;

    public static bool IsActive()
    {
        if (!IaSettings.CartelEnabled)
        {
            return false;
        }
        return StoreReputation.IsPerkUnlocked(CartelNetworkPerks.FRIENDS_ID);
    }

    // Vanilla's private fine dialogue.
    public static bool GraftFine(StoreClient client)
    {
        System.Reflection.MethodInfo fine = AccessTools.Method(typeof(StoreClientListSecurity), "GraftFineDialogue");
        if (fine == null)
        {
            Log.Info("cartel network: vanilla fine dialogue not found; the sentence stays as it is");
            return false;
        }
        fine.Invoke(null, new object[] { client });
        Log.Info("cartel network: friends in high places, the sentence is a fine of " + PlayerStore.Instance.secData.GetFineValue());
        return true;
    }
}

[HarmonyPatch(typeof(StoreClientListSecurity), "GraftArrestDialogue")]
public static class CartelFriendsArrestPatch
{
    private static bool Prefix(StoreClient storeClient)
    {
        try
        {
            return !(CartelFriends.IsActive() && CartelFriends.GraftFine(storeClient));
        }
        catch (Exception e)
        {
            Log.Error("cartel network fine instead of arrest failed", e);
            return true;
        }
    }
}

[HarmonyPatch(typeof(StoreClientListSecurity), "GraftExecutionDialogue")]
public static class CartelFriendsExecutionPatch
{
    private static bool Prefix(StoreClient storeClient)
    {
        try
        {
            return !(CartelFriends.IsActive() && CartelFriends.GraftFine(storeClient));
        }
        catch (Exception e)
        {
            Log.Error("cartel network fine instead of execution failed", e);
            return true;
        }
    }
}

[HarmonyPatch(typeof(SecData), "GetFineValue")]
public static class CartelFriendsFinePatch
{
    private static void Postfix(ref int __result)
    {
        if (CartelFriends.IsActive() && __result > CartelFriends.FINE_CAP)
        {
            __result = CartelFriends.FINE_CAP;
        }
    }
}

[HarmonyPatch(typeof(SecData), "GetProjectedSentence")]
public static class CartelFriendsSentencePatch
{
    private static void Postfix(SecData __instance, ref string __result)
    {
        if (CartelFriends.IsActive() && __instance.GetTotalCrimeValue() >= 100)
        {
            __result = LocHelper.GetLocalizedMechanic("sec_data_sentence_fine");
        }
    }
}

// With "Friends in High Places", paying the fine costs no Security reputation. Vanilla's fine dialogue takes 10 from
// Security when the fine is paid (StoreClientListSecurity.GraftFineDialogue); skipped while the sentence officer is
// at the counter. His other -20 ("the evidence is gone") is not a fine and stays.
[HarmonyPatch(typeof(StoreReputation), "ModSecReputation")]
public static class CartelFriendsFineRepPatch
{
    public const int VANILLA_FINE_REP = -10;

    private static bool Prefix(int amount)
    {
        try
        {
            if (amount != VANILLA_FINE_REP || !CartelFriends.IsActive() || PlayerStore.Instance.currentClientInstance == null)
            {
                return true;
            }
            StoreClient client = PlayerStore.Instance.currentClientInstance.GetClientBlueprint();
            return client == null || !client.isSentenceOfficer;
        }
        catch (Exception e)
        {
            Log.Error("cartel network fine reputation check failed", e);
            return true;
        }
    }
}

// Cartel merchants, only for Cartel Network members.
//
// "Peer" (同行): a seller with vanilla's thief look (the shady sprites vanilla thieves use). He lays random contraband
// on the outer counter until it is full or holds MAX_ITEMS of his (so a mod that enlarges the counter does not get a
// flood), at its plain value (no markup), and says nothing but "......" when the player buys. The contraband pool is
// every item vanilla or a mod marks as contraband with a value, scanned once from the item directory, minus vanilla's
// unimplemented items (UnusedDirectory) and story items (IMPORTANT_TAG, including the ones vanilla tags only on load)
// and display-only props; plus a 10% chance of a large hidden compartment. Visits: a 20% chance each day, at most once
// every 3 days.
public static class CartelPeer
{
    public const string CLIENT_ID = "vce_cartel_peer";
    public const int DAILY_CHANCE = 20;
    public const int MIN_DAYS_BETWEEN = 3;
    // Items laid per visit, the large hidden compartment included.
    public const int MAX_ITEMS = 20;
    // Draws before giving up: big items may not fit while small ones still do.
    public const int MAX_TRIES = 60;
    // Vanilla's directory of items that are defined but not in the game.
    public const string UNUSED_DIRECTORY = "UnusedDirectory";
    public const string LAST_VISIT_KEY = "cartel_peer_last_visit";
    // Now and then a large hidden compartment (vanilla smuggler_bay_mod), laid first so it still fits.
    public const string LARGE_BAY_ID = "smuggler_bay_mod";
    public const int LARGE_BAY_CHANCE = 10;

    private static List<string> pool;

    public static void Enable()
    {
        ModHook.OnGenerateCustomerNormal += OnGenerateCustomers;
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Cartel merchants: Peer visits now",
                delegate { PlayerStore.Instance.storeClientManager.AddClient(Create()); }, false);
        }
        catch (Exception e)
        {
            Log.Error("cartel peer debug registration failed", e);
        }
    }

    public static void Disable()
    {
        ModHook.OnGenerateCustomerNormal -= OnGenerateCustomers;
    }

    public static bool IsPeer(StoreClient client)
    {
        return client != null && client.identifier == CLIENT_ID;
    }

    public static void OnGenerateCustomers(StoreClientManager manager)
    {
        if (!IaSettings.CartelEnabled)
        {
            return;
        }
        try
        {
            if (manager == null || !CartelNetwork.IsMember())
            {
                return;
            }
            int today = StoreStation.Instance.dayCounter;
            int last = CartelNetwork.Data(LAST_VISIT_KEY);
            if ((last > 0 && today - last < MIN_DAYS_BETWEEN) || !RNG.Roll(DAILY_CHANCE))
            {
                return;
            }
            CartelNetwork.SetData(LAST_VISIT_KEY, today);
            manager.AddClient(Create());
            Log.Info("cartel merchants: the Peer visits (day " + today + ")");
        }
        catch (Exception e)
        {
            Log.Error("cartel peer visit failed", e);
        }
    }

    // Every contraband item in the directory, built once.
    private static List<string> Pool()
    {
        if (pool != null)
        {
            return pool;
        }
        pool = new List<string>();
        HashSet<string> unused = new HashSet<string>(DirectoryMaster.GetIdentifierList<GameItem>(UNUSED_DIRECTORY));
        foreach (string id in DirectoryMaster.GetIdentifierList<GameItem>(null))
        {
            if (unused.Contains(id))
            {
                continue;
            }
            GameItem item = null;
            try
            {
                item = DirectoryMaster.Item(id, false);
                if (item == null)
                {
                    continue;
                }
                // Some story items get IMPORTANT_TAG only when a save loads, not in their factory.
                GeneralHelper.LoadImportantStoryItemTag(item);
                if (ContrabandHelper.IsContraband(item) && item.unitValue > 0
                    && !item.IsTag("IMPORTANT_TAG") && !item.IsTag(GeneralHelper.DISPLAY_ONLY_TAG))
                {
                    pool.Add(id);
                }
            }
            catch
            {
            }
            finally
            {
                if (item != null)
                {
                    item.Destroy();
                }
            }
        }
        Log.Info("cartel merchants: " + pool.Count + " contraband items in the Peer's pool: " + string.Join(", ", pool.ToArray()));
        return pool;
    }

    // Random contraband until the outer counter takes no more (a few misses in a row: big items may not fit while
    // small ones still do) or MAX_ITEMS are laid.
    private static void LayOut()
    {
        List<string> ids = Pool();
        if (ids.Count == 0)
        {
            return;
        }
        int laid = 0;
        if (RNG.Roll(LARGE_BAY_CHANCE) && TryLay(DirectoryMaster.Item(LARGE_BAY_ID, false)))
        {
            laid++;
        }
        int misses = 0;
        for (int i = 0; i < MAX_TRIES && laid < MAX_ITEMS && misses < 4; i++)
        {
            if (TryLay(DirectoryMaster.Item(ids[RNG.GetRandomInt(0, ids.Count - 1)], false)))
            {
                laid++;
                misses = 0;
            }
            else
            {
                misses++;
            }
        }
    }

    // An item that does not fit is destroyed, not left behind.
    private static bool TryLay(GameItem item)
    {
        if (item == null)
        {
            return false;
        }
        if (PlayerStore.Instance.TryAddDirectSellingItemToTable(item, false, false, true, 0))
        {
            return true;
        }
        item.Destroy();
        return false;
    }

    public static StoreClient Create()
    {
        StoreClient client = new StoreClient();
        client.identifier = CLIENT_ID;
        client.displayName = Text.Get("cartel_peer_name");
        client.spriteName = SpriteDict.GetRandomShadySpriteName();
        client.clientFaction = CartelNetwork.FACTION_ID;
        client.tradeReputationMultiplier = CartelTradeRep.MULTIPLIER;  // 4x Cartel Network reputation, buying or selling
        client.clientIntent = StoreClient.ClientIntent.SELL;
        client.mainDialogue.SetText(client.displayName, Text.Get("cartel_peer_1")).SetEndAction(delegate
        {
            try
            {
                LayOut();
            }
            catch (Exception e)
            {
                Log.Error("cartel peer stock failed", e);
            }
        });
        client.acceptDealDialogue = new Dialogue().SetText(client.displayName, Text.Get("cartel_peer_silence"));
        client.allDoneDialogue = new Dialogue().SetText(client.displayName, Text.Get("cartel_peer_silence")).SetEndAction(delegate
        {
            PlayerStore.Instance.DismissCurrentClient(false);
        });
        client.CompleteClientCreation();
        return client;
    }
}

// After a partial purchase vanilla adds its "anything else?" line; the Peer just stays silent.
[HarmonyPatch(typeof(StoreClientDialogList), "CreateAfterBuyingFromClientDialog")]
public static class CartelPeerAfterBuyPatch
{
    private static void Postfix(string clientName, Dialogue __result)
    {
        try
        {
            if (__result != null && PlayerStore.Instance.currentClientInstance != null
                && CartelPeer.IsPeer(PlayerStore.Instance.currentClientInstance.GetClientBlueprint()))
            {
                __result.SetText(clientName, Text.Get("cartel_peer_silence"));
            }
        }
        catch (Exception e)
        {
            Log.Error("cartel peer line failed", e);
        }
    }
}

// "Black Market Tourist" (黑市游客): an Upper Level visitor slumming it, for Cartel Network members at 50 reputation or
// more. Each day there is a 20% chance he takes the place of one of the day's ordinary buyers. He buys medical,
// narcotic, weapon, access card and supply crate goods, but only contraband; his budget follows vanilla's Upper Level
// buyers (500-600 + the day, plus vanilla's Upper Level budget perks through SetBudget). Vanilla has no tourist
// outfit (its tourists wear citizen sprites), so he wears the Upper Level look.
public static class CartelTourist
{
    public const string CLIENT_ID = "vce_cartel_tourist";
    public const int MIN_REPUTATION = 50;
    public const int DAILY_CHANCE = 20;

    public static void Enable()
    {
        try
        {
            ModHelper.AddDebugFunction(DreamToxinMod.MOD_ID, "Modding", "Cartel merchants: Black Market Tourist now",
                delegate { PlayerStore.Instance.storeClientManager.AddClient(Create()); }, false);
        }
        catch (Exception e)
        {
            Log.Error("cartel tourist debug registration failed", e);
        }
    }

    public static void Disable()
    {
    }

    public static bool IsTourist(StoreClient client)
    {
        return client != null && client.identifier == CLIENT_ID;
    }

    public static bool IsEligible()
    {
        if (!IaSettings.CartelEnabled)
        {
            return false;
        }
        StoreReputation network = CartelNetwork.Find();
        return network != null && network.GetReputation() >= MIN_REPUTATION;
    }

    // Right after vanilla adds the day's random buyers (StoreClientManager.HandleNormalClient): swap one of those for
    // the tourist. Only clients that call added (before: the queue as it was), so story, request and other mods'
    // characters queued elsewhere are never replaced.
    public static void OnNormalClients(StoreClientManager manager, HashSet<StoreClient> before)
    {
        if (!IaSettings.CartelEnabled)
        {
            return;
        }
        try
        {
            if (manager == null || manager.clientStack == null || before == null || !IsEligible() || !RNG.Roll(DAILY_CHANCE))
            {
                return;
            }
            List<int> candidates = new List<int>();
            for (int i = 0; i < manager.clientStack.Count; i++)
            {
                StoreClient c = manager.clientStack[i];
                if (c != null && !before.Contains(c) && c.dismissable && c.clientIntent == StoreClient.ClientIntent.BUY
                    && !c.isSecurity && !c.isSentenceOfficer && !c.isWanted && !c.isAug
                    && (c.identifier == null || !c.identifier.StartsWith("vce_")))
                {
                    candidates.Add(i);
                }
            }
            StoreClient tourist = Create();
            if (candidates.Count == 0)
            {
                manager.AddClient(tourist);
                Log.Info("cartel merchants: the Black Market Tourist visits (no buyer to replace)");
                return;
            }
            int index = candidates[RNG.GetRandomInt(0, candidates.Count - 1)];
            string replaced = manager.clientStack[index].identifier;
            manager.clientStack[index] = tourist;
            Log.Info("cartel merchants: the Black Market Tourist takes the place of " + replaced);
        }
        catch (Exception e)
        {
            Log.Error("cartel tourist visit failed", e);
        }
    }

    public static StoreClient Create()
    {
        StoreClient client = new StoreClient();
        client.identifier = CLIENT_ID;
        client.displayName = Text.Get("cartel_tourist_name");
        client.spriteName = SpriteDict.GetRandomUpperSpriteName();
        StoreClientFactionSetup.InitUpperLevelCitizen(client);
        client.clientIntent = StoreClient.ClientIntent.BUY;
        client.SetBudget(500 + RNG.GetRandomInt(0, 100) + StoreStation.GetDayCounter());
        client.mainDialogue.SetText(client.displayName, Text.Get("cartel_tourist_1"))
            .NextDialogue().SetText(client.displayName, Text.Get("cartel_tourist_2"))
            .SetEndAction(delegate
            {
            });
        foreach (string type in new string[] { "MEDICAL", "NARCOTIC", "WEAPON", "ACCESS_CARD", "SUPPLY_CRATE" })
        {
            client.clientBuyingTagList.Add(type);
        }
        // No separate "deal accepted" line: after each purchase he asks "Is that all?", and when the budget is spent he
        // says the closing line and leaves.
        client.acceptDealDialogue = null;
        client.allDoneDialogue = new Dialogue().SetText(client.displayName, Text.Get("cartel_tourist_done")).SetEndAction(delegate
        {
            PlayerStore.Instance.DismissCurrentClient(false);
        });
        client.CompleteClientCreation();
        return client;
    }
}

[HarmonyPatch(typeof(StoreClientManager), "HandleNormalClient")]
public static class CartelTouristNormalClientPatch
{
    private static void Prefix(StoreClientManager __instance, out HashSet<StoreClient> __state)
    {
        __state = __instance != null && __instance.clientStack != null ? new HashSet<StoreClient>(__instance.clientStack) : null;
    }

    private static void Postfix(StoreClientManager __instance, HashSet<StoreClient> __state)
    {
        CartelTourist.OnNormalClients(__instance, __state);
    }
}

// Only contraband.
[HarmonyPatch(typeof(StoreClient), "IsClientRefusingItem")]
public static class CartelTouristRefusalPatch
{
    private static void Postfix(StoreClient __instance, GameItem gameItem, ref bool __result)
    {
        if (!__result && CartelTourist.IsTourist(__instance) && gameItem != null && !ContrabandHelper.IsContraband(gameItem))
        {
            __result = true;
        }
    }
}

// "Is that all?" after each purchase that leaves budget.
[HarmonyPatch(typeof(StoreClientDialogList), "CreateAfterSellingToClientDialog")]
public static class CartelTouristAfterSellPatch
{
    private static void Postfix(string clientName, Dialogue __result)
    {
        try
        {
            if (__result != null && PlayerStore.Instance.currentClientInstance != null
                && CartelTourist.IsTourist(PlayerStore.Instance.currentClientInstance.GetClientBlueprint()))
            {
                __result.SetText(clientName, Text.Get("cartel_tourist_more"));
            }
        }
        catch (Exception e)
        {
            Log.Error("cartel tourist line failed", e);
        }
    }
}

// Cartel Network reputation from trade. Vanilla turns every trade into reputation for the client's faction, both when
// the player sells (PlayerStore.SellItem) and when the player buys (PlayerStore.OnItemBought): trade value x the
// client's tradeReputationMultiplier / 400 (StoreReputation.OnItemTraded). The cartel's own people (trader, courier,
// Peer) belong to the Cartel Network and trade at 4x. The Black Market Tourist is Upper Level, so his trades keep
// their normal Upper Level reputation and also count for the Cartel Network at 4x.
public static class CartelTradeRep
{
    public const double MULTIPLIER = 4.0;
}

[HarmonyPatch(typeof(StoreReputation), "OnItemTraded")]
public static class CartelTouristTradeRepPatch
{
    private static bool busy;

    private static void Postfix(string factionId, int value, int originalValue, StoreClient storeClient, bool isPlayerSold, GameItem gameItem)
    {
        if (busy || !CartelTourist.IsTourist(storeClient) || factionId == CartelNetwork.FACTION_ID || !CartelNetwork.IsMember())
        {
            return;
        }
        double multiplier = storeClient.tradeReputationMultiplier;
        busy = true;
        try
        {
            storeClient.tradeReputationMultiplier = CartelTradeRep.MULTIPLIER;
            StoreReputation.OnItemTraded(CartelNetwork.FACTION_ID, value, originalValue, storeClient, isPlayerSold, gameItem);
        }
        catch (Exception e)
        {
            Log.Error("cartel network reputation from the tourist failed", e);
        }
        finally
        {
            storeClient.tradeReputationMultiplier = multiplier;
            busy = false;
        }
    }
}
