# Issue #128 research: caravan delivery payment

## Question

Issue #128 says that caravan delivery payment was still using Civilization I's
formula and had never been checked against Civilization II. The question is what
Civilization II actually pays when a caravan or freight unit delivers goods to a
city.

## Rule implemented

The immediate delivery payment is:

```text
(distance + 10) * (T1 + T2) / 24
```

where:

- `distance` is the map distance between the home city and the destination city.
- `T1` and `T2` are the two cities' **base trade**, i.e. trade produced by their
  worked squares before trade-route bonuses and before corruption.
- Each division truncates to an integer.
- The payment is doubled if the two cities are on different continents or islands.
- The payment is halved if the two cities belong to the same civilization.
- The science gained from delivery is the same as the gold payment.

This was implemented in `Engine/src/UnitActions/CaravanActions.cs` by using
`City.TileTrade` rather than `City.Trade`, adding the different-island doubling,
keeping the same-owner halving, and removing the invented `DeliveryMultiplier`.

The game still doubles delivery when the destination demands the caravan's
commodity. That demand behavior is kept for compatibility with the existing
implementation, but it is not confirmed by the sources below and should not be
treated as verified Civilization II behavior.

## Sources used

- Local Civ II wiki:
  `.civ2-wiki/Trade_route_(Civ2).wiki`

  It states that trade routes are established by caravans or freight units and
  that the immediate cash payment depends on factors such as distance, continents,
  and whether the cities are in the same empire. It also points to the Apolyton
  thread below.

- Apolyton, "Calculating caravan and freight delivery payments":
  <https://apolyton.net/forum/civilization-series/civilization-i-and-civilization-ii/76838-calculating-caravan-and-freight-delivery-payments>

  This is the main source for the formula above. It gives the distance/trade base,
  the different-continent doubling, the same-civilization halving, and the integer
  truncation behavior.

- CivFanatics Civ2 strategy scrolls:
  <https://www.civfanatics.com/civ2/strategy/scrolls/>

  Used as a second independent statement of the same immediate delivery payment.

## Agreement

The two independent written sources agree on the core formula:

```text
(distance + 10) * (T1 + T2) / 24
```

with these modifiers:

```text
different continent/island: double
same civilization: halve
integer truncation: yes
```

They also agree that delivery is not the same calculation as the ongoing trade
route bonus. The ongoing route bonus is:

```text
(T1 + T2 + 4) / 8
```

halved for own cities, while the delivery payment is a separate immediate payment
that genuinely uses distance.

## Open questions

These details are **not** implemented and should be treated as unresolved:

1. **Freight or map-size modifiers.**  
   Some Civ II discussions refer to freight units or map size affecting delivery
   payments. The sources consulted here do not give a clean, testable modifier for
   this implementation.

2. **Demand/commodity multiplier.**  
   The current code doubles delivery when the destination demands the caravan's
   commodity. The Apolyton thread is relevant to delivery calculations, but this
   exact demand-doubling behavior is not independently verified in the same way as
   the distance base and the continent/ownership modifiers.

3. **Exact commodity-specific behavior.**  
   Food, luxury goods, and other commodities may behave differently in Civ II. The
   minimal fix for #128 does not implement commodity-specific rules.

## Why the old code was wrong

The old delivery code multiplied the Civ I-style distance formula by an invented
`DeliveryMultiplier = 8`. That made deliveries far larger than Civ II and was not
supported by the sources checked for this issue.

The important distinction is:

```text
Civ II ongoing route bonus: (T1 + T2 + 4) / 8
Civ II immediate delivery payment: (distance + 10) * (T1 + T2) / 24
```

The previous bug was that the distance-based delivery formula had been used as if
it were the ongoing route bonus.

## Tests

Added or updated tests in `RhyCiv.Tests/UnitActions/CaravanActionsTests.cs`:

- cross-world foreign delivery pays the verified Civ II value;
- delivery to the player's own city is halved;
- different-island delivery pays more than same-island delivery;
- the existing route-opening and science/gold behavior still passes.

Mock map distances are deliberately simple: the mock world uses a flat map, so
distance is calculated without wrap-around. The expected values from that mock map
are:

- home to Rome, foreign and different island: `82`;
- home to own neighbour: `5`;
- home to Paris, foreign but same island: `30`.
