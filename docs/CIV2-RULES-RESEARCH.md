# Civ II rule research

Findings that back gameplay changes, one section per issue. Sources: the local
copies of the Civilization fandom wiki's Civ2 pages in `.civ2-wiki/`, plus at
least one independent source (the official Civ2 manual or CivFanatics). Freeciv,
civ2civ3, xconq and Civ III/IV/V material do not count.

## #149 · First contact opens the negotiation

**Rule.** Contact between two civilisations is established automatically when
rival ground units enter adjacency or a unit approaches a rival city; the
computer civ then requests a meeting, which is framed as receiving an emissary
(a herald introducing the leader), and negotiations proceed from there. After
contact, either side may open an audience at will ("Send Emissary" from the
Foreign Minister). A Diplomat establishing an embassy is likewise a contact
event, not a silent flag write.

**Sources.**

- `.civ2-wiki/Diplomacy_(Civ2).wiki` (Negotiation section): "Contact is
  established when rival ground units enter adjacency, or a unit approaches a
  rival city. Known civilizations are listed in the Foreign Minister window;
  whereas the original *Civilization* required the human player dispatch a
  diplomat to initiate a meeting, in *Civ II* rivals can be contacted at any
  time, subject to leaders' patience." And: "Computer-controlled civs will
  normally request a meeting the first time their ground units encounter the
  player's, and may seek an audience on subsequent contact... Meetings are
  framed as the player receiving an emissary from the civilization."
- Official *Sid Meier's Civilization II* user guide (ManualShelf mirror,
  https://www.manualshelf.com/manual/games-pc/civilization-ii/user-guide-english.html),
  "Conducting Diplomacy with Computer Opponents" (p. 123): "Diplomacy is
  conducted face to face with one rival emissary at a time. An opponent can
  contact you any time after units from each of your civilizations have met,
  and the reverse is also true. You can contact an opponent any time after your
  units have been adjacent to his or hers. Just select the SEND EMISSARY option
  from the FOREIGN MINISTER's report in the ADVISORS menu." The manual's own
  tutorial enacts the first-contact meeting: "As soon as you enter Sioux
  territory, their leader, Sitting Bull, requests an audience with you. Accept
  Sitting Bull's invitation by clicking OK."

**Agreement.** The wiki and the manual agree on both parts: adjacency or
approaching a city makes contact automatically, and the meeting (the diplomacy
screen with the leader/herald) follows, rather than the player merely getting a
name added to a menu.

**Unverified.** The exact timing of the AI's request versus the greeting popup
(wiki says the AI "normally requests" a meeting on first encounter and the
player "may choose to accept or refuse"; rhYciv presents the herald and then
opens the parley directly, with no refuse-a-meeting option). Herald animation
and portrait details are a graphics setting and absent in *Test of Time*, so
they are not rules the engine must enforce.

**Code consequence (already landed in Release 0.2.2, verified here).**
`LocalPlayer.ContactMade` announces the herald (GREETINGS) and then opens the
audience with the leader just met (`GameScreen.MeetByParley` →
`Diplomacy.ParleyWith`) instead of leaving the player to find them in the
Foreign Ministry list; `DiplomatActions.EstablishEmbassy` routes through
`DiplomacyFunctions.MakeContact` so a diplomat's embassy is a proper
introduction (herald, negotiation, record) rather than a silent flag write.
Covered by `RhyCiv.Tests/Diplomacy/EmbassyMakesContactTests.cs`. Also applies
to #140.
