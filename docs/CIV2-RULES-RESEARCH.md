# Civ II Happiness Rule Research for Issue #131

This file records the source-checked rules for issue #131. It separates:

- **Verified**: supported by the local `.civ2-wiki/` source and at least one independent Civ II source.
- **Likely verified**: current code matches the verified sources.
- **Conflict**: sources disagree; do not change code until the conflict is resolved.
- **Unresolved formula**: current code is a reconstruction and needs a stronger source.

Primary local sources:

- `.civ2-wiki/Happiness_(Civ2).wiki`
- `.civ2-wiki/Luxuries_(Civ2).wiki`
- `.civ2-wiki/Difficulty_level_(Civ2).wiki`
- `.civ2-wiki/List_of_city_improvements_in_Civ2.wiki`

Independent cross-check used:

- Archived IGN Civ II walkthrough:
  - https://web.archive.org/web/20180308170158/https://www.ign.com/faqs/2004/sid-meiers-civilization-ii-walkthrough-377214

## Difficulty and base unhappiness

Verified:

- Chieftain: first unhappy citizen appears at size 6.
- Warlord: size 5.
- Prince: size 4.
- King: size 3.
- Emperor: size 2.
- Deity: size 1.

Local wiki quote:

> The player's difficulty level determines cities' base level of contentment before unhappy citizens appear.

Current implementation:

- `CitySizeUnhappyChieftain = 7`
- Human difficulty offsets are derived from that value.
- Existing `CityHappinessTests.DifficultyControlsNumberOfBornContentCitizens` covers these thresholds.

Status: verified.

## Civil disorder

Verified:

- Disorder occurs when unhappy/angry citizens outnumber happy citizens.

Local wiki quote:

> Civil disorder occurs when a city's Unhappy / Angry citizens outnumber its Happy citizens.

Current implementation:

- `CityHappiness.IsInDisorder => UnhappyCitizens > HappyCitizens`

Status: likely verified.

## We Love the King Day / celebration

Verified:

- Celebration requires no unhappy/angry citizens.
- At least half the city population must be happy.
- Minimum city size is 3.

Local wiki quote:

> "We Love the King Day" occurs when at least half a city's population is Happy and no Unhappy / Angry citizens are present. A city requires a minimum population of 3 to celebrate.

Current implementation:

- `CanCelebrate(int citySize) => citySize >= 3 && UnhappyCitizens == 0 && HappyCitizens * 2 >= citySize`

Status: likely verified.

## Martial law

Verified:

- Martial law applies for Anarchy, Despotism, Monarchy, and Communism.
- Up to three combat units in the city suppress unhappiness.
- Each unit normally reduces unhappy citizens by 1.
- Communism doubles martial law effectiveness.

Local wiki quote:

> Military governments ... feature martial law: up to three combat units stationed in a city automatically suppress unhappiness, at a rate of -1 Unhappy per unit. Under Communism, martial law is doubly effective (-2 Unhappy per unit).

Current implementation:

- `Math.Min(3, martialLawUnits) * effectPerUnit`
- `effectPerUnit = government == Communism ? 2 : 1`

Status: verified.

## Colosseum and Electronics

Verified:

- Colosseum makes 3 unhappy citizens content.
- Electronics adds 1 more effective unhappy citizen for the Colosseum.

Local wiki quote:

> Colosseum: -3 Unhappy. Additional -1 Unhappy with Electronics.

Independent source:

> Colosseum - Up to three discontented citizens are made content

Current implementation:

- `state.Unhappy -= HasAdvance(civ, AdvanceType.Electronics) ? 4 : 3`

Status: verified.

## Cathedral, Michelangelo's Chapel, Theology, and Communism

Verified:

- Cathedral makes 3 unhappy citizens content.
- Theology improves Cathedral effectiveness by 1.
- Communism reduces Cathedral effectiveness by 1.
- Michelangelo's Chapel provides Cathedral behavior to every city.

Local wiki quote:

> Cathedral: -3 Unhappy. Additional -1 Unhappy with Theology. -1 effectiveness with Communism. Provided by Michelangelo's Chapel.

Independent source:

> Cathedral - Up to four discontented citizens are made content

Current implementation:

- Cathedral effect starts at 3.
- `Theology` adds 1.
- Communism subtracts 1.
- Michelangelo's Chapel is treated as owning active Cathedral behavior.

Status: verified.

## Temple, Mysticism, Ceremonial Burial, and Oracle

Verified:

- Temple effectiveness comes from advances:
  - Ceremonial Burial provides 1 unhappy citizen reduction.
  - Mysticism provides another 1 unhappy citizen reduction.
- Oracle doubles Temple effectiveness.
- Oracle is obsolete by Theology.

Local wiki quote:

> Temple: -1 Unhappy with Ceremonial Burial; -1 Unhappy with Mysticism. Double effect with Oracle.
> Oracle: Doubles Temple effect. Obsolete with Theology.

Current implementation:

- Temple effect is `1 + Mysticism`.
- Oracle doubles effect.
- Active-wonder logic checks obsolescence via `ExpiresAt`.

Status: verified.

Note: the current implementation does not currently require Ceremonial Burial as a separate advance for the first Temple effect. This may be a minor divergence, but it is not the main focus of issue #131.

## Palace, Courthouse, Police Station, and Women's Suffrage

Verified:

- Palace or Courthouse gives +1 happy citizen only under Democracy.
- Police Station reduces war weariness from deployed units.
- Women's Suffrage provides Police Station behavior to every city.

Local wiki quote:

> Palace / Courthouse: +1 Happy. Democracy only.
> Police Station: -1 Unhappy from foreign deployment. Only relevant to Republic and Democracy. Provided by Women's Suffrage.

Current implementation:

- Democracy + Palace/Courthouse adds happy citizens.
- Police Station or Women's Suffrage reduces per-unit war weariness penalty.

Status: likely verified.

## Shakespeare's Theatre

Verified:

- Shakespeare's Theatre makes all unhappy/angry citizens in the city content.

Local wiki quote:

> Shakespeare's Theatre: Renders all Unhappy / Angry citizens content in city.

Current implementation:

- `state.Unhappy = 0`
- Final normalization clears angry citizens.

Status: likely verified. No fix appears necessary.

## Bach's Cathedral

Conflict:

- Local wiki says Bach's Cathedral is universal and explicitly notes Civilopedia describes "same continent", but says effect is universal.
- Archived independent Civ II walkthrough says:
  > J.S. Bach's Cth - Decreases unhappy citizens on same continent by 2 per city
- Existing audit notes also say same continent.
- Current implementation uses same map/island:
  ```csharp
  bach.Location.Map == city.Location.Map &&
  bach.Location.Island == city.Location.Island
  ```

Status: unresolved. Do not change until a stronger source or empirical confirmation is available.

## Hanging Gardens

Conflict:

- Local wiki says:
  - +1 happy in every city,
  - additional +2 happy in host city.
- Archived independent walkthrough says:
  > Hanging Gardens - One extra happy citizen in every city

Current implementation:

```csharp
state.Happy += hangingGardens == city ? 3 : 1;
```

Status: unresolved. Current code matches the local wiki but not the archived walkthrough's simple wording.

## Empire-size unhappiness

Unresolved formula:

Local wiki confirms the general rule:

- Empires with many cities gain extra unhappiness.
- Riot factor is affected by difficulty, government, and map size.
- Communism ignores sprawl unhappiness.
- Angry citizens come from empire sprawl.

Current implementation:

```csharp
var largeMapBonus = city.Location.Map.XDim * city.Location.Map.YDim >= 6000 ? 2 : 0;
var riotFactor = Math.Max(1, cosmic.RiotFactor - 2 * difficulty + largeMapBonus);
var governmentFactor = city.Owner.Government / 2 + 2;
var combinedFactor = Math.Max(1, governmentFactor * riotFactor / 2);
var cityIndex = Math.Max(0, city.Owner.Cities.IndexOf(city));
empireUnhappy = Math.Max(0,
    (city.Owner.Cities.Count - combinedFactor + cityIndex % combinedFactor) /
    combinedFactor);
```

Status: unverified. Do not treat as final Civ II rule unless a stronger source is found.

## Luxuries

Verified general rule:

- Every two luxuries make one happy citizen.
- Specialists can generate luxuries through entertainers.
- Economic improvements increase luxury output.

Local wiki quote:

> For every two luxuries generated in the city, one Content becomes Happy; if no content citizens exist, luxuries instead make one Unhappy Content, or one Angry Happy.

Current implementation:

- Sets happy citizens from luxury total:
  ```csharp
  state.Happy = city.GetLuxury(city.CountSpecialists(PeopleType.Elvis) * 2) / 2;
  ```
- Normalization prevents happy plus unhappy from exceeding available citizens.

Status: partially verified. The exact priority conversion from angry/unhappy/content is not fully modeled by the current code.

## Practical tests added

The following behavior is covered by tests in `RhyCiv.Tests/Cities/CityHappinessTests.cs`:

- Colosseum/Electronics magnitude.
- Cathedral/Theology/Communism magnitude.
- Michelangelo's Chapel provides Cathedral behavior.
- Temple/Oracle doubling.
- Shakespeare's Theatre clears unhappiness.
- Celebration requires no unhappiness and at least half happy.
- Existing difficulty, martial law, specialist, and luxury tests remain.
