using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

// Adds a "Chemistry Expansion" section at the very end of the vanilla Out of
// Stock sign. The sign already stores allowed/disallowed ids as a semicolon
// separated tag on the sign item, so this only exposes those ids in the same UI
// and makes StoreClientManager.IsClientValid consult them.
public static class OutOfStockSignPatch
{
    private const string SECTION_TITLE_KEY = "out_of_stock_sign_section_vce";
    private const string VISITS_LABEL_KEY = "ui_out_of_stock_sign_visits";
    private const string VISITS_LABEL_FALLBACK = "Visits: {0}";
    private const string RANDOM_VISITS_LABEL_KEY = "ui_out_of_stock_sign_visits_random";
    private const string RANDOM_VISITS_LABEL_FALLBACK = "Visits: at random";
    private const string INFO_DEALER_ID = "shadyCustomerInformation";
    private static readonly HashSet<string> TurnedAway = new HashSet<string>();
    private static bool TurnedAwayLoaded;

    private static readonly List<Entry> Entries = new List<Entry>
    {
        new Entry(
            "nc_waste_broker",
            "merchant_name",
            delegate { return true; },
            delegate
            {
                return VisitLabel(ScheduledDay(
                    WasteMerchant.LAST_VISIT_KEY,
                    WasteMerchant.INTERVAL_DAYS,
                    WasteMerchant.INTERVAL_DAYS,
                    0));
            }),
        new Entry(
            "vce_cartel_vendor",
            "cartel_vendor_name",
            delegate { return true; },
            delegate
            {
                return VisitLabel(ScheduledDay(
                    CartelVendor.LAST_VISIT_KEY,
                    CartelVendor.FIRST_DAY,
                    CartelVendor.INTERVAL_DAYS,
                    -CartelVendor.INTERVAL_DAYS));
            }),
        new Entry(
            "vce_cartel_trader",
            "cartel_trader_name",
            delegate { return CartelTrader.IsActive(); },
            delegate { return RandomVisitLabel(); }),
        new Entry(
            "vce_cartel_peer",
            "cartel_peer_name",
            delegate { return CartelNetwork.IsMember(); },
            delegate { return RandomVisitLabel(); }),
        new Entry(
            "vce_cartel_tourist",
            "cartel_tourist_name",
            delegate { return CartelTourist.IsEligible(); },
            delegate { return RandomVisitLabel(); })
    };

    // The last vanilla row when there are no inspectable items.
    [HarmonyPatch(typeof(OutOfStockSignHelper), "AddRandomRow")]
    public static class AddRandomRowPatch
    {
        private static void Postfix(object[] __args)
        {
            if (__args == null || __args.Length < 4)
            {
                return;
            }
            if (!string.Equals(__args[2] as string, INFO_DEALER_ID, StringComparison.Ordinal))
            {
                return;
            }
            ICollection inspectable = InspectableKinds();
            if (inspectable == null || inspectable.Count == 0)
            {
                AddChemistryExpansionRows(__args[0] as CustomUIBuilder, __args[1] as GameItem);
            }
        }
    }

    // The last vanilla row when inspectable item rows are present.
    [HarmonyPatch(typeof(OutOfStockSignHelper), "AddKindRow")]
    public static class AddKindRowPatch
    {
        private static void Postfix(object[] __args)
        {
            if (__args == null || __args.Length < 3)
            {
                return;
            }
            IList inspectable = InspectableKinds();
            if (inspectable == null || inspectable.Count == 0)
            {
                return;
            }
            object current = __args[2];
            object last = inspectable[inspectable.Count - 1];
            if (current == null || !current.Equals(last))
            {
                return;
            }
            AddChemistryExpansionRows(__args[0] as CustomUIBuilder, __args[1] as GameItem);
        }
    }

    [HarmonyPatch(typeof(StoreClientManager), "AddClient")]
    public static class AddClientPatch
    {
        private static bool Prefix(StoreClient storeClient)
        {
            if (storeClient == null || !ShouldTurnAway(storeClient.identifier))
            {
                return true;
            }
            return false;
        }
    }

    public static bool ShouldTurnAway(string clientId)
    {
        if (!IsChemistryExpansionClient(clientId))
        {
            return false;
        }
        EnsureTurnedAwayCache();
        return TurnedAway.Contains(clientId);
    }

    public static void Enable()
    {
        ModHook.OnGameLoadedLate += RefreshTurnedAwayIds;
    }

    public static void Disable()
    {
        ModHook.OnGameLoadedLate -= RefreshTurnedAwayIds;
        TurnedAway.Clear();
        TurnedAwayLoaded = false;
    }

    public static void RefreshTurnedAwayIds()
    {
        try
        {
            TurnedAway.Clear();
            HashSet<string> off = OutOfStockSignHelper.GetTurnedAwayIds();
            if (off != null)
            {
                for (int index = 0; index < Entries.Count; index++)
                {
                    if (off.Contains(Entries[index].Id))
                    {
                        TurnedAway.Add(Entries[index].Id);
                    }
                }
            }
            TurnedAwayLoaded = true;
        }
        catch (Exception e)
        {
            Log.Error("loading the chemistry expansion out of stock state failed", e);
        }
    }

    private static void EnsureTurnedAwayCache()
    {
        if (!TurnedAwayLoaded)
        {
            RefreshTurnedAwayIds();
        }
    }

    private static void AddChemistryExpansionRows(CustomUIBuilder builder, GameItem sign)
    {
        if (builder == null || sign == null)
        {
            return;
        }
        try
        {
            builder.AddSpace(10f);
            builder.AddLabel("<b>" + Text.Get(SECTION_TITLE_KEY) + "</b>", null);
            for (int index = 0; index < Entries.Count; index++)
            {
                Entry entry = Entries[index];
                if (!entry.Visible())
                {
                    continue;
                }
                builder.BeginRow(6f, TextAnchor.MiddleLeft);
                builder.AddLabel(Text.Get(entry.NameKey), null);
                builder.AddFlexibleSpace();
                builder.AddToggle(
                    entry.VisitLabel(),
                    !OutOfStockSignHelper.IsOff(sign, entry.Id),
                    delegate(bool allowed)
                    {
                        OutOfStockSignHelper.SetOff(sign, entry.Id, !allowed);
                        SetTurnedAway(entry.Id, !allowed);
                    },
                    entry.Id);
                builder.End();
            }
        }
        catch (Exception e)
        {
            Log.Error("adding the chemistry expansion section to the out of stock sign failed", e);
        }
    }

    private static IList InspectableKinds()
    {
        return OutOfStockSignHelper.GetUnlockedInspectableKinds() as IList;
    }

    private static string VisitLabel(int day)
    {
        return LocHelper.GetLocalizedUIOrDefault(
            VISITS_LABEL_KEY,
            VISITS_LABEL_FALLBACK,
            OutOfStockSignHelper.WeekdayName(day));
    }

    private static string RandomVisitLabel()
    {
        return LocHelper.GetLocalizedUIOrDefault(
            RANDOM_VISITS_LABEL_KEY,
            RANDOM_VISITS_LABEL_FALLBACK);
    }

    private static int ScheduledDay(
        string lastVisitKey,
        int firstDay,
        int interval,
        int defaultLastVisit)
    {
        int today = StoreStation.Instance != null
            ? StoreStation.GetDayCounter()
            : 0;
        int last = ModHelper.GetModDataInt(
            DreamToxinMod.MOD_ID,
            lastVisitKey,
            defaultLastVisit);
        int scheduled = Math.Max(firstDay, last + interval);
        return today > scheduled ? today : scheduled;
    }

    private static bool IsChemistryExpansionClient(string clientId)
    {
        if (string.IsNullOrEmpty(clientId))
        {
            return false;
        }
        for (int index = 0; index < Entries.Count; index++)
        {
            if (string.Equals(Entries[index].Id, clientId, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static void SetTurnedAway(string clientId, bool turnedAway)
    {
        if (turnedAway)
        {
            TurnedAway.Add(clientId);
        }
        else
        {
            TurnedAway.Remove(clientId);
        }
    }

    private sealed class Entry
    {
        internal readonly string Id;
        internal readonly string NameKey;
        private readonly Func<bool> visible;
        private readonly Func<string> visitLabel;

        internal Entry(string id, string nameKey, Func<bool> visible, Func<string> visitLabel)
        {
            Id = id;
            NameKey = nameKey;
            this.visible = visible;
            this.visitLabel = visitLabel;
        }

        internal bool Visible()
        {
            return visible != null && visible();
        }

        internal string VisitLabel()
        {
            return visitLabel != null ? visitLabel() : string.Empty;
        }
    }
}
