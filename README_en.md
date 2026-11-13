# Cartel Expansion

**[中文版](README.md)** · The full player guide for the whole mod (in Chinese) is [docs/MOD_GUIDE.md](docs/MOD_GUIDE.md) · The design for the planned antibiotics and strain-breeding system (in Chinese) is [docs/ANTIBIOTICS_DESIGN.md](docs/ANTIBIOTICS_DESIGN.md)

The cartel system of *Vanilla Chemistry Expansion*, a mod for Probably Stolen. Besides vanilla's cartel, which belongs to the chemist start, it adds a **Cartel Network** that every start can run into: join it, be boycotted by it, or help Security tear it out by the roots.

> **Off by default.** Turn it on in the mod settings → "卡特尔 / Cartel" → **Enable the cartel expansion**. While it is off nothing on this page has any effect (a save that already joined still shows the "Cartel Network" row in the reputation panel, but it does nothing). The chemist start's vanilla cartel always runs on vanilla rules.

---

## Contents

1. [Overview](#overview)
2. [Joining the Cartel Network](#joining-the-cartel-network)
3. [Refusing the cartel](#refusing-the-cartel)
4. [Against the cartel: the Security route](#against-the-cartel-the-security-route)
5. [Cartel Network reputation](#cartel-network-reputation)
6. [The cartel's people](#the-cartels-people)
7. [Debug buttons](#debug-buttons)
8. [Compatibility and uninstalling](#compatibility-and-uninstalling)

---

## Overview

```
Chemist start ──authorised exit from vanilla's cartel──────────────► joins the Cartel Network (rep 50)

Other starts ──contraband medical/narcotic sales reach 500──► note: "We see"
             ──reach 1000──► the Cartel Contact visits
                  ├─ pay 100 ──────────────────────────────────► joins the Cartel Network (rep 0)
                  ├─ come back in two days (once)
                  └─ refuse ──► clients refuse your contraband (33%)
                        ├─ 10 refusals ──► the Contact returns: pay 200 to join / refuse again
                        └─ conditions met ──► Security officer: report the cartel
                                                └─► the sweep ──► cartel destroyed ──► Security's thanks
```

---

## Joining the Cartel Network

The Cartel Network is a new faction. Once joined it appears in vanilla's reputation panel; saving, notifications and reputation changes all use vanilla's mechanics.

### Chemist start
After vanilla's "authorised exit" from the cartel, the player joins the Cartel Network at **reputation 50**:

> Your debt to the cartel is finally settled. Now the cartel wants to work with you, and the choice may not rest with either of you alone.

Saves that left the cartel before this expansion existed join on their next load.

### Other starts

**1. Being noticed.** Every sale of an item that is **contraband** and **Medical** or **Narcotic** adds its sale price to a running total.

**2. The note.** Once the total reaches **500**, the next morning an envelope sealed with black wax and an eye lies in the middle of the **inner counter** (the outer one if the inner is full). Inside is an old sheet folded in half, an eye drawn in running ink:

> We see

**3. The Contact.** Once the total reaches **1000**, and the note came on an earlier day (one huge sale still brings the note first), the **Cartel Contact** comes every day until the player answers:

> I hope our last encounter didn't scare you.
> We've been watching you for a long time.
> Nobody moves product under the cartel's eye without being noticed.
> Now you'd better pay for protection, and one day we may let you become our partner.
> Refusing is not an option.

| Choice | Result |
|---|---|
| **[Pay 100 credits to join the Cartel Network]** | Joins at reputation **0**; greyed out without the cash |
| **[Ask him to come back in two days]** | He returns in two days, without this choice |
| **[Refuse the cartel]** | "Ha, funny." "No one alive refuses the cartel." See [Refusing the cartel](#refusing-the-cartel) |

---

## Refusing the cartel

### The boycott
After a refusal, each client decides once, the **first** time you put contraband or stolen goods in front of them: **33%** refuse all contraband and stolen goods for that visit and say:

> Sorry, but we can't take goods from you.

Security clients, and clients who refuse contraband on their own in vanilla (some scavengers and Lower Level citizens), are beyond the cartel's reach and never roll.

### The Contact returns
The day after the **10th** boycott, the Contact comes back (not once you hold the "against the cartel" label):

> I think someone has learned their lesson.
> But some lessons in life only come once.

| Choice | Result |
|---|---|
| **[Pay 200 credits to join the cartel]** | "Welcome aboard. (sneers)" Joins at reputation 0; the boycott ends |
| **[Refuse the cartel]** | He leaves; the second refusal is recorded |

---

## Against the cartel: the Security route

### Conditions (checked every night)
1. At least one boycott;
2. Security reputation 80 or more;
3. Any of: you have sold incapacitant to Security or the Upper Level; or Upper Level + Security − (Lower Level + Revolution + Black Market) ≥ 50; or Lower Level, Revolution or Black Market reputation is negative;
4. Not a member of the Cartel Network.

### The Security officer
He comes the day after the conditions are first met; and once more three days after a second refusal of the cartel, if the conditions hold and you have not reported it yet.

> Citizen, we've noticed cartel activity in the area lately. Report anything suspicious.

- **[Report the cartel]**: "Good. Security has decided to run a sweep of local illegal organizations soon." "Any... voluntary donations... from concerned citizens are highly encouraged." You gain the **against the cartel** label.
- **[Don't report the cartel]**: ends the talk (not offered after a second refusal of the cartel).

### The sweep
From the day after the report, in a three-day cycle:

| Day | Visitor | Buys |
|---|---|---|
| 1 | Security Medical Procurement: "Citizen, on behalf of Security I am openly procuring medical supplies for the fight against local organized crime." | Medical |
| 2 | Security Quartermaster: "...military supplies..." | Food, Weapon |
| 3 | — | — |

- Both pay **50% less** for anything;
- Each has a fixed **500** budget that **carries over** between visits; when it reaches 0 he says "Very good. On behalf of Security, thanks to every citizen for their generous donations." and stops coming.

### The cartel destroyed
When both budgets are spent you gain the **cartel destroyed** label and the boycott ends. **A week later** a Security Logistics Officer comes:

> Citizen, Security's sweep in this area was a great success.
> Security Logistics thanks everyone who supported the operation,
> especially local businesses for their paid sponsorship of Logistics.
> Those supplies did not come cheap, but on a station this short of everything they helped Security a great deal.

Security reputation **+50**, Black Market **−20**, and Security gains a perk:

| Reputation | Perk | Effect |
|---|---|---|
| 120 | **Logistics' Gratitude** ("Some people in Logistics will remember which shops cooperated.") | Security pays a **15%** tip on anything it buys |

---

## Cartel Network reputation

### Gaining it
Trading with the cartel's people (**buying or selling**) counts for the Cartel Network at **4×** vanilla's trade reputation: the Cartel Trader, the Cartel Courier, the Peer, and the Black Market Tourist (who is Upper Level: his trades give normal Upper Level reputation, plus 4× to the Cartel Network).

### Perks

| Rep | Name | Description | Effect |
|---|---|---|---|
| −100 | **Settled Accounts** | The Cartel Network never fires anyone. | Vanilla's cartel hitman waits the next morning; game over |
| −60 | **Boycott** | Other black market traders no longer come to your shop. | Black Market traders and contraband buyers stop coming (story clients and phone-ordered couriers still do) |
| −30 | **Under Suspicion** | The cartel's suspicion means your contraband is no longer trusted. | Contraband markup halved (as with a gunsmith's permit) |
| 0 | **Open Trade** | The cartel's trading network draws two contraband traders to your shop every week. | The Cartel Trader on two random days a week |
| 25 | **Tight Lips** | Customers are less likely to complain about the quality of your goods. | Half the next-morning complaints about mislabelled or harmful chemicals |
| 50 | **No Strangers** | Thieves no longer come to cartel turf. | Free Cartel Protection instead of insurance (as in the chemist start) |
| 75 | **Supply Network** | The Cartel Network supplies the raw materials you need: order by phone, at a good price. | The "Cartel Network" phone contact (below) |
| 100 | **Friends in High Places** | Thanks to the cartel's mediation, any investigation into you can now be settled with money. | Every sentence is a fine equal to the crime value, at most 1000; paying it costs no Security reputation |

Negative perks are active at or below their threshold and stack (at −60, Under Suspicion is active too).

---

## The cartel's people

| Who | When | Trade |
|---|---|---|
| **Cartel Trader** | Rep ≥ 0, two random days a week | Buys any contraband (any grade), 5% cut; budget 250–300 + 3 a day |
| **Cartel Courier** | Rep ≥ 75, the day after a phone order | Sells 20 bottles of the chosen supply at a 33% wholesale discount |
| **Peer** | Members; 20% a day, at least 3 days apart | Sells random contraband until the outer counter is full, at plain value; 10% chance of a large hidden compartment too |
| **Black Market Tourist** | Rep ≥ 50; each day a 20% chance to take an ordinary buyer's place | Buys contraband medical, narcotic, weapon, access card and supply crate goods; Upper Level budget |

### The supply line (2278)

> **Cartel Network:** What do you need?
> [Acid] [Base] [Reducing agent] [Lipobond agent] [Alcohol] [N-solution] [Wrong number]

Pick a supply: "Tomorrow. Better not let the courier remember you." The courier brings 20 of vanilla's supplier bottles the next day; 3-day cooldown. [Wrong number]: "......"

### Lines
- **Peer**: "I thought you might be interested in these." On every purchase: "......"
- **Black Market Tourist**: "I knew it. Down here, all that chaos breeds the most special little shops." "Your higher-ups sent me... you know what I mean, right?" After each purchase: "Is that all?" Budget spent: "Ignoring the law feels amazing!"

---

## Debug buttons

Debug panel → Modding, names starting with `Cartel Network:`, `Cartel raid:`, `Cartel sweep:` and `Cartel merchants:`: join directly, change reputation, call any of the characters, reset the watching path, and so on.

---

## Compatibility and uninstalling

- The Cartel Network is an entry in vanilla's reputation list and Logistics' Gratitude a perk on Security; both are saved with the game. After uninstalling they stay in the save without effect (the panel shows raw text keys).
- If No Strangers' Cartel Protection is on when the mod is removed, vanilla does not turn it off.
- Everything is done through Harmony patches and the mod API; no vanilla files are modified.

Source: [`src/CartelNetwork.cs`](src/CartelNetwork.cs); note and envelope art: [`art/cartel/`](art/cartel/).
