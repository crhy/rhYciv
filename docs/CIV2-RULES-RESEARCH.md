# Civ II rule research

Findings that back gameplay changes, one section per issue. Sources: the local
copies of the Civilization fandom wiki's Civ2 pages in `.civ2-wiki/`, plus at
least one independent source (the official Civ2 manual or CivFanatics). Freeciv,
civ2civ3, xconq and Civ III/IV/V material do not count.

## #140 · Meeting a new civilisation opens the negotiation, not just a greeting

**Rule.** Contact is not a silent flag: it is a meeting. Contact between two
civilisations is established automatically when rival ground units enter
adjacency or a unit approaches a rival city, and the computer civ then requests
an audience — a herald introducing the leader — which is the frame in which
all negotiation happens. A Diplomat opening an embassy is likewise a contact
event (walking up to a rival city is itself contact), so it must announce the
meeting like any other introduction rather than quietly flipping a bit.

**Sources.**

- `.civ2-wiki/Diplomacy_(Civ2).wiki` ("Diplomatic relations" / "Negotiation"):
  "Contact is established when rival ground units enter adjacency, or a unit
  approaches a rival city." And: "Computer-controlled civs will normally request
  a meeting the first time their ground units encounter the player's, and may
  seek an audience on subsequent contact. The player may choose to accept or
  refuse... Meetings are framed as the player receiving an emissary from the
  civilization. If enabled in graphic options, this uses a special screen
  featuring a portrait of the civ's leader..."
- Official *Sid Meier's Civilization II* user guide (ManualShelf mirror,
  https://www.manualshelf.com/manual/games-pc/civilization-ii/user-guide-english.html),
  "Conducting Diplomacy with Computer Opponents" (p. 123): "Diplomacy is
  conducted face to face with one rival emissary at a time. An opponent can
  contact you any time after units from each of your civilizations have met, and
  the reverse is also true." The guide's own tutorial enacts the first-contact
  audience (p. 30): "As soon as you enter Sioux territory, their leader, Sitting
  Bull, requests an audience with you. Accept Sitting Bull's invitation by
  clicking OK."
- `.civ2-wiki/Espionage_(Civ2).wiki` ("Establish Embassy"): the embassy action
  is performed by a Diplomat standing at a rival city — the same approach that
  the Diplomacy page says establishes contact.

**Agreement.** Wiki and manual agree on both parts: contact happens on
encounter, and an encounter is a meeting with the leader, not a name quietly
added to a list.

**Unverified.** Whether the player-initiated route ("Send Emissary" from the
Foreign Minister) also lands straight on the leader's screen or on a list of
known civs; the sources describe the AI's request for an audience but not the
exact player-side menu shape. Herald animation and portrait are a graphics
setting (absent in *Test of Time*), so they are presentation, not rules.

**Code consequence (already landed in Release 0.2.2, verified here).**
`LocalPlayer.ContactMade` announces the herald (GREETINGS) and, once it is
dismissed, seats the player opposite the leader just met
(`GameScreen.MeetByParley` → `Diplomacy.ParleyWith`) instead of leaving the
Foreign Ministry list as the only door to diplomacy;
`DiplomatActions.EstablishEmbassy` routes through
`DiplomacyFunctions.MakeContact`, so a Diplomat's embassy is a real
introduction for both sides. Covered by
`RhyCiv.Tests/Diplomacy/EmbassyMakesContactTests.cs`. Same root cause as #149,
whose rule was verified the same way against the wiki and the manual.
