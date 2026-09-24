# Civ II rule research

Findings are recorded here before any code change. A rule is only implemented when
at least two independent Civ II sources agree (wiki page plus a forum thread or the
manual). Freeciv/civ2civ3/xconq/Civ III+ pages are not Civ II sources.

## Issue #130 — do the two ends of a standing trade route receive different amounts, and does the demanded commodity raise it?

**Question.** In `tradesave.sav` (A.D. 1700) the Cardiff–Carmarthen route shows
`Carmarthen Silk: +1` on Cardiff's screen and `Cardiff Gems: +2+` on Carmarthen's
screen, from the same pair of cities (base trade 11 and 14). Does Civ II really pay
the two ends different amounts, and is the cause the demanded commodity (Cardiff
demands Gems; the route's commodity shown to Cardiff is Silk) rather than the
unmodelled transport modifier k?

**What the standing-route formula says.** The symmetric formula
`foreign: (T1 + T2 + 4) / 8`, `own cities: k(T1 + T2 + 4) / 16` was verified for
#127/#128 and matches Cardiff exactly. Nothing in it distinguishes the two ends of
a route.

**Sources consulted.**

1. **Civ2 wiki, `Trade_route_(Civ2).wiki`** (local copy of the fandom page;
   https://civilization.fandom.com/wiki/Trade_route_(Civ2)), Civilopedia entry:
   > "The value of a trade route is also affected by the type of goods traded. When
   > trading a commodity that is demanded by the destination city, the trade route
   > is much more profitable."

   This suggests a demand bonus on the standing route, but gives no formula and no
   per-city detail.

2. **Apolyton, "Trade Route Formula"**
   https://apolyton.net/forum/civilization-series/civilization-i-and-civilization-ii/130326-trade-route-formula
   (the thread behind the formula already implemented). Post #1 (Elephant) lists
   the multipliers applied to `TR = (T1 + T2 + 4) / 8` — same-civilisation ×0.5,
   key road ×1.5, key railroad ×2, superhighways, airports — and explicitly ends
   the list with:
   > "(NO Demand multiplier, just Dem/Sup blocking effects)"

   i.e. demand only decides which commodity a route carries and which routes
   replace which ("Base Trade values ... Multipliers are ignored in comparing Trade
   Routes for replacement"), not the standing amount. Post #12 (magic9mushroom)
   reports a Berlin/Vienna own-empire route as "both ways it is 8", consistent with
   a symmetric standing amount.

3. **Apolyton, "Calculating Caravan and Freight Delivery Payments"**
   https://apolyton.net/forum/civilization-series/civilization-i-and-civilization-ii/76838-calculating-caravan-and-freight-delivery-payments
   Step 7 of the delivery-payment formula:
   > "Apply the DemandBonus. If the commodity being delivered is demanded by the
   > destination city, then the amount from Step 6 is multiplied by a factor based
   > on the type of commodity involved"

   This is the one-time delivery payment, not the standing route, and supports the
   "demand affects delivery only" reading. (CivFanatics thread
   https://forums.civfanatics.com/threads/trade-routes.358181/ cited in the issue
   could not be fetched — Cloudflare challenge — but the issue quotes it as stating
   both cities receive the same amount and demand affects only delivery.)

**Assessment.** The wiki's Civilopedia text and the two forum threads disagree:
the wiki says a demanded commodity makes the route "much more profitable", the
formula threads explicitly exclude a demand multiplier for the standing bonus. No
source gives a per-city evaluation, and the k-transport explanation (each end using
its own road/rail modifier) is speculation with no source. The single Cardiff /
Carmarthen observation (+1 vs +2, with the trailing `+` on Carmarthen's line
unexplained) is exactly the kind of evidence the rule needs but does not yet have.

**Decision (rule 5): sources disagree, nothing conclusive — no game-code change, no
GitHub comment.** Needed to settle it: a second route pair measured from a Civ II
save (demands running the other way / both ways / neither), or the Civ2 manual
text on standing route income.

**Note on docs:** `docs/CIV2-COMPARISON.md` already records this asymmetry as
unresolved ("needs either a second source or a second pair of cities to measure")
and does not misstate the rule, so it needed no correction.
