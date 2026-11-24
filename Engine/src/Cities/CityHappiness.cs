using System;
using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.MapObjects;
using Model.Constants;
using Model.Core;
using Model.Core.Cities;
using Model.Core.Mapping;

namespace RhyCiv.Engine;

/// <summary>
/// The final citizen attitudes produced by Civ II's five happiness passes.
/// UnhappyCitizens includes AngryCitizens, matching the values stored by Civ II.
/// </summary>
public readonly record struct CityHappiness(
    int HappyCitizens,
    int ContentCitizens,
    int UnhappyCitizens,
    int AngryCitizens,
    int Specialists)
{
    public bool IsInDisorder => UnhappyCitizens > HappyCitizens;
    public bool CanCelebrate(int citySize) =>
        citySize >= 3 && UnhappyCitizens == 0 && HappyCitizens * 2 >= citySize;
}

/// <summary>
/// One of the five happiness passes, in the order the original applies them.
/// </summary>
public enum HappinessPass
{
    BaseMood,
    Luxuries,
    Improvements,
    Government,
    Wonders
}

/// <summary>
/// The citizen counts after one happiness pass, plus what the pass credits for
/// changing them. Civ II's Happiness Analysis panel draws these rows with the
/// buildings that did the work beside them; Causes is the engine's equivalent.
/// </summary>
public readonly record struct HappinessStep(
    HappinessPass Pass,
    int Happy,
    int Content,
    int Unhappy,
    int Angry,
    string[] Causes);

/// <summary>
/// The five happiness passes in order, followed by the counts the game actually
/// uses. The Wonders step always equals Final, so a breakdown shown to a player
/// cannot drift from the number behind civil disorder and celebration.
/// </summary>
public readonly record struct HappinessAnalysis(
    HappinessStep BaseMood,
    HappinessStep Luxuries,
    HappinessStep Improvements,
    HappinessStep Government,
    HappinessStep Wonders,
    CityHappiness Final)
{
    public HappinessStep[] Steps => [BaseMood, Luxuries, Improvements, Government, Wonders];
}

public static class CityHappinessExtensions
{
    /// <summary>
    /// Reproduces the ordering and integer arithmetic of the original Civ II
    /// happiness calculation: base mood, luxuries, improvements, government,
    /// and wonders. Each pass is normalized before the next one is applied.
    /// </summary>
    public static CityHappiness CalculateHappiness(this City city, IGame game)
    {
        var result = AnalyzeHappiness(city, game).Final;

        city.HappyCitizens = result.HappyCitizens;
        city.UnhappyCitizens = result.UnhappyCitizens;
        return result;
    }

    /// <summary>
    /// Runs the same five passes and keeps the counts after each one, so the
    /// "citizens before anything / after luxuries / after improvements" rows of
    /// Civ II's Happiness Analysis panel can be shown with what produced them.
    /// Read-only: unlike CalculateHappiness it does not write the city's citizen
    /// counts.
    /// </summary>
    public static HappinessAnalysis AnalyzeHappiness(this City city, IGame game)
    {
        var size = Math.Max(0, city.Size);
        var specialists = Math.Clamp(city.NoOfSpecialistsx4 / 4, 0, size);
        var state = new MoodState(size, specialists);

        var baseMoodCauses = new List<string>();
        ApplyBaseMood(city, game, ref state, baseMoodCauses);
        state.Normalize();
        var baseMood = state.Snapshot(HappinessPass.BaseMood, baseMoodCauses);

        // Entertainers provide two luxuries; taxmen and scientists contribute to
        // their respective city outputs instead.
        var luxuryPoints = city.GetLuxury(city.CountSpecialists(PeopleType.Elvis) * 2);
        var luxuryHappy = luxuryPoints / 2;
        state.Happy = luxuryHappy;
        state.Normalize();
        var luxuryCauses = new List<string>();
        if (luxuryHappy > 0)
        {
            luxuryCauses.Add($"luxuries: {luxuryHappy} happy ({luxuryPoints} points)");
        }

        var luxuries = state.Snapshot(HappinessPass.Luxuries, luxuryCauses);

        var improvementCauses = new List<string>();
        ApplyImprovements(city, ref state, improvementCauses);
        state.Normalize();
        var improvements = state.Snapshot(HappinessPass.Improvements, improvementCauses);

        var governmentCauses = new List<string>();
        ApplyGovernment(city, ref state, governmentCauses);
        state.Normalize();
        var government = state.Snapshot(HappinessPass.Government, governmentCauses);

        var wonderCauses = new List<string>();
        ApplyWonders(city, ref state, wonderCauses);
        state.Normalize();
        var wonders = state.Snapshot(HappinessPass.Wonders, wonderCauses);

        return new HappinessAnalysis(baseMood, luxuries, improvements, government, wonders,
            state.ToFinal());
    }

    public static PeopleType[] GetPeopleTypes(this City city, IGame game)
    {
        var mood = city.CalculateHappiness(game);
        var people = new List<PeopleType>(city.Size);
        people.AddRange(Enumerable.Repeat(PeopleType.Happy, mood.HappyCitizens));
        people.AddRange(Enumerable.Repeat(PeopleType.Content, mood.ContentCitizens));
        people.AddRange(Enumerable.Repeat(PeopleType.Unhappy,
            mood.UnhappyCitizens - mood.AngryCitizens));
        people.AddRange(Enumerable.Repeat(PeopleType.Angry, mood.AngryCitizens));
        people.AddRange(city.GetSpecialistTypes());
        return people.ToArray();
    }

    public static PeopleType[] GetSpecialistTypes(this City city)
    {
        var count = Math.Clamp(city.NoOfSpecialistsx4 / 4, 0, Math.Max(0, city.Size));
        var normalized = new PeopleType[count];
        for (var i = 0; i < count; i++)
        {
            normalized[i] = i < city.SpecialistTypes.Length &&
                            city.SpecialistTypes[i] is >= (int)PeopleType.Elvis and <= (int)PeopleType.Scientist
                ? (PeopleType)city.SpecialistTypes[i]
                : PeopleType.Elvis;
        }

        city.SpecialistTypes = normalized.Select(type => (int)type).ToArray();
        return normalized;
    }

    public static int CountSpecialists(this City city, PeopleType type) =>
        city.GetSpecialistTypes().Count(specialist => specialist == type);

    public static void SetSpecialistType(this City city, int index, PeopleType type)
    {
        var specialists = city.GetSpecialistTypes();
        if (index < 0 || index >= specialists.Length)
        {
            return;
        }

        specialists[index] = type is >= PeopleType.Elvis and <= PeopleType.Scientist
            ? type
            : PeopleType.Elvis;
        city.SpecialistTypes = specialists.Select(specialist => (int)specialist).ToArray();
    }

    // Each pass also reports what it did, so the breakdown can name the cause of
    // each row. The arithmetic, comparisons and clamps are untouched.
    private static void ApplyBaseMood(City city, IGame game, ref MoodState state, List<string> causes)
    {
        var cosmic = game.Rules.Cosmic;
        var difficulty = Math.Clamp(game.DifficultyLevel, 0, (int)DifficultyType.Deity);

        // Computer players always use the King-level base population threshold.
        var baseUnhappy = city.Owner.PlayerType == PlayerType.Ai
            ? state.Size - 1 - (cosmic.CitySizeUnhappyChieftain - 5)
            : state.Size - 1 - ((cosmic.CitySizeUnhappyChieftain - difficulty) - 2);

        var empireUnhappy = 0;
        if (city.Owner.PlayerType != PlayerType.Ai &&
            city.Owner.Government != (int)GovernmentType.Communism)
        {
            var largeMapBonus = city.Location.Map.XDim * city.Location.Map.YDim >= 6000 ? 2 : 0;
            var riotFactor = Math.Max(1, cosmic.RiotFactor - 2 * difficulty + largeMapBonus);
            var governmentFactor = city.Owner.Government / 2 + 2;
            var combinedFactor = Math.Max(1, governmentFactor * riotFactor / 2);
            var cityIndex = Math.Max(0, city.Owner.Cities.IndexOf(city));
            empireUnhappy = Math.Max(0,
                (city.Owner.Cities.Count - combinedFactor + cityIndex % combinedFactor) /
                combinedFactor);
        }

        var totalUnhappy = Math.Max(0, baseUnhappy + empireUnhappy);
        state.Unhappy = Math.Min(totalUnhappy, state.Size);
        state.Angry = Math.Max(0, totalUnhappy - state.Size);

        if (totalUnhappy > 0)
        {
            var bornUnhappy = Math.Max(0, baseUnhappy);
            var cause = $"difficulty {(DifficultyType)difficulty}: {bornUnhappy} unhappy";
            if (empireUnhappy > 0)
            {
                cause += $", empire size: {empireUnhappy} unhappy";
            }

            causes.Add(cause);
        }
    }

    private static void ApplyImprovements(City city, ref MoodState state, List<string> causes)
    {
        var civ = city.Owner;
        var government = (GovernmentType)civ.Government;

        if (city.ImprovementExists((int)ImprovementType.Colosseum))
        {
            var colosseumEffect = HasAdvance(civ, AdvanceType.Electronics) ? 4 : 3;
            state.Unhappy -= colosseumEffect;
            causes.Add($"Colosseum -{colosseumEffect}" +
                       (HasAdvance(civ, AdvanceType.Electronics) ? " (Electronics)" : ""));
        }

        if (city.ImprovementExists((int)ImprovementType.Cathedral) ||
            OwnsActiveWonder(city, ImprovementType.MichChapel))
        {
            var effect = 3;
            if (government == GovernmentType.Communism) effect--;
            if (HasAdvance(civ, AdvanceType.Theology)) effect++;
            state.Unhappy -= effect;
            var cathedral = city.ImprovementExists((int)ImprovementType.Cathedral)
                ? "Cathedral"
                : "Michelangelo's";
            causes.Add($"{cathedral} -{effect}" +
                       (government == GovernmentType.Communism ? " (Communism)" : "") +
                       (HasAdvance(civ, AdvanceType.Theology) ? " (Theology)" : ""));
        }

        if (city.ImprovementExists((int)ImprovementType.Temple))
        {
            var effect = 1 + (HasAdvance(civ, AdvanceType.Mysticism) ? 1 : 0);
            if (OwnsActiveWonder(city, ImprovementType.Oracle)) effect *= 2;
            state.Unhappy -= effect;
            causes.Add($"Temple -{effect}" +
                       (HasAdvance(civ, AdvanceType.Mysticism) ? " (Mysticism)" : "") +
                       (OwnsActiveWonder(city, ImprovementType.Oracle) ? " x2 (Oracle)" : ""));
        }

        if (government == GovernmentType.Democracy &&
            (city.ImprovementExists((int)ImprovementType.Palace) ||
             city.ImprovementExists((int)ImprovementType.Courthouse)))
        {
            state.Happy++;
            var seat = city.ImprovementExists((int)ImprovementType.Palace) ? "Palace" : "Courthouse";
            causes.Add($"{seat} +1 happy (Democracy)");
        }
    }

    private static void ApplyGovernment(City city, ref MoodState state, List<string> causes)
    {
        var government = (GovernmentType)city.Owner.Government;
        if (government == GovernmentType.Fundamentalism)
        {
            state.Angry = 0;
            state.Unhappy = 0;
            causes.Add($"{government}: no unhappy citizens");
            return;
        }

        if (government is GovernmentType.Republic or GovernmentType.Democracy)
        {
            var deployedUnits = city.SupportedUnits.Count(UnitCausesFieldUnhappiness);
            if (government == GovernmentType.Republic && deployedUnits > 0) deployedUnits--;

            var penaltyPerUnit = government == GovernmentType.Democracy ? 2 : 1;
            if (UsesFieldRelief(city))
            {
                penaltyPerUnit--;
            }

            var fieldPenalty = deployedUnits * Math.Max(0, penaltyPerUnit);
            state.Unhappy += fieldPenalty;
            if (fieldPenalty > 0)
            {
                causes.Add($"{government}: {deployedUnits} deployed unit" +
                           $"{(deployedUnits == 1 ? "" : "s")} +{fieldPenalty} unhappy" +
                           (UsesFieldRelief(city) ? " (Police Station or Women's Suffrage)" : ""));
            }

            return;
        }

        var martialLawUnits = city.UnitsInCity.Count(unit => unit.Owner == city.Owner && unit.AttackBase > 0);
        var effectPerUnit = government == GovernmentType.Communism ? 2 : 1;
        var garrisoned = Math.Min(3, martialLawUnits);
        var pacified = garrisoned * effectPerUnit;
        state.Unhappy -= pacified;
        if (pacified > 0)
        {
            causes.Add($"martial law: {garrisoned} unit{(garrisoned == 1 ? "" : "s")} " +
                       $"x{effectPerUnit} ({government}) -{pacified}");
        }
    }

    private static void ApplyWonders(City city, ref MoodState state, List<string> causes)
    {
        var hangingGardens = FindActiveWonder(city, ImprovementType.HangingGardens);
        if (hangingGardens != null)
        {
            var gardensEffect = hangingGardens == city ? 3 : 1;
            state.Happy += gardensEffect;
            causes.Add($"Hanging Gardens +{gardensEffect} " +
                       (hangingGardens == city ? "(owner)" : $"({hangingGardens.Name})"));
        }

        if (OwnsActiveWonder(city, ImprovementType.CureCancer))
        {
            state.Happy++;
            causes.Add("Cure for Cancer +1 happy");
        }

        if (city.ImprovementExists((int)ImprovementType.ShakespTheat))
        {
            state.Unhappy = 0;
            causes.Add("Shakespeare's Theatre: no unhappy citizens");
        }

        var bach = FindActiveWonder(city, ImprovementType.JsbCathedral);
        if (bach != null && bach.Location.Map == city.Location.Map &&
            bach.Location.Island == city.Location.Island)
        {
            state.Unhappy -= 2;
            causes.Add("J.S. Bach's Cathedral -2 (same island)");
        }
    }

    private static bool UsesFieldRelief(City city) =>
        city.ImprovementExists((int)ImprovementType.PoliceStat) ||
        OwnsActiveWonder(city, ImprovementType.WomenSuffrage);

    private static bool UnitCausesFieldUnhappiness(Model.Core.Units.Unit unit)
    {
        if (unit.AttackBase <= 0 || unit.AiRole is AiRoleType.Diplomacy or AiRoleType.Trade)
            return false;

        var location = unit.CurrentLocation;
        if (location.CityHere?.Owner == unit.Owner) return false;

        // Air-superiority units are the fighter exception in Civ II.
        if (unit.Domain == UnitGas.Air && unit.AiRole == AiRoleType.AirSuperiority) return false;

        if (location.Improvements.Any(i => i.Improvement == ImprovementTypes.Fortress) &&
            unit.Owner.Cities.Any(c => IsWithinThreeTiles(location, c.Location)))
        {
            return false;
        }

        return true;
    }

    private static bool IsWithinThreeTiles(Tile start, Tile destination)
    {
        if (start.Map != destination.Map) return false;
        if (start == destination) return true;

        var visited = new HashSet<Tile> { start };
        var frontier = new HashSet<Tile> { start };
        for (var distance = 1; distance <= 3; distance++)
        {
            frontier = frontier.SelectMany(t => t.Neighbours()).Where(visited.Add).ToHashSet();
            if (frontier.Contains(destination)) return true;
        }

        return false;
    }

    private static bool HasAdvance(Model.Core.Civilization civilization, AdvanceType advance) =>
        (int)advance < civilization.Advances.Length && civilization.Advances[(int)advance];

    private static bool OwnsActiveWonder(City city, ImprovementType wonder) =>
        FindActiveWonder(city, wonder) != null;

    private static City? FindActiveWonder(City city, ImprovementType wonder)
    {
        var wonderCity = city.Owner.Cities.FirstOrDefault(c => c.ImprovementExists((int)wonder));
        if (wonderCity == null) return null;

        var improvement = wonderCity.Improvements.First(i => i.Type == (int)wonder);
        return improvement.ExpiresAt >= 0 && HasAdvance(city.Owner, (AdvanceType)improvement.ExpiresAt)
            ? null
            : wonderCity;
    }

    private struct MoodState(int size, int specialists)
    {
        public int Size { get; } = size;
        public int Specialists { get; } = specialists;
        public int Happy;
        public int Unhappy;
        public int Angry;

        // The clamps the final result has always applied, applied to every pass
        // so each row of the breakdown is a set of real citizens.
        public HappinessStep Snapshot(HappinessPass pass, List<string> causes)
        {
            var clamped = Clamped();
            return new HappinessStep(pass, clamped.Happy, clamped.Content, clamped.Unhappy,
                clamped.Angry, causes.ToArray());
        }

        public CityHappiness ToFinal()
        {
            var clamped = Clamped();
            return new CityHappiness(clamped.Happy, clamped.Content, clamped.Unhappy, clamped.Angry,
                Specialists);
        }

        private (int Happy, int Content, int Unhappy, int Angry) Clamped()
        {
            var unhappy = Math.Clamp(Unhappy, 0, Size - Specialists);
            var angry = Math.Clamp(Angry, 0, unhappy);
            var happy = Math.Clamp(Happy, 0, Size - Specialists - unhappy);
            var content = Math.Max(0, Size - Specialists - happy - unhappy);
            return (happy, content, unhappy, angry);
        }

        public void Normalize()
        {
            Happy = Math.Clamp(Happy, 0, Size);

            while (Angry != 0 && Angry > Unhappy)
            {
                Angry--;
                Unhappy++;
            }

            var availableCitizens = Math.Clamp(Size - Specialists, 0, 99);
            Unhappy = Math.Clamp(Unhappy, 0, Size);
            while (availableCitizens < Happy + Unhappy)
            {
                if (Angry != 0)
                {
                    Angry--;
                }
                else
                {
                    Happy = Math.Clamp(Happy - 1, 0, Size);
                }

                Unhappy = Math.Clamp(Unhappy - 1, 0, Size);
            }

            while (Angry != 0 && Size - Specialists > Happy + Unhappy)
            {
                Angry--;
                Unhappy++;
            }
        }
    }
}
