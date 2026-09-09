using System.Text.Json.Serialization;

namespace Model.Core
{
    public class Options
    {
        public Options()
        {
            
        }
        public Options(GameInitializationConfig config)
        {
            SimplifiedCombat = config.SimplifiedCombat;
            Bloodlust = config.Bloodlust;
            DontRestartIfEliminated = config.DontRestartEliminatedPlayers;
            FlatEarth = config.FlatWorld;
        }

        // START GAME RULES
        [JsonInclude] public bool SimplifiedCombat { get; private set; }
        [JsonInclude] public bool Bloodlust { get; private set; }
        [JsonInclude] public bool DontRestartIfEliminated { get; private set; }
        [JsonInclude] public bool FlatEarth { get; private set; }

        // GAME OPTIONS
        public bool Music { get; set; }
        public bool SoundEffects { get; set; }
        public bool CheatMenu { get; set; }
        public bool AlwaysWaitAtEndOfTurn { get; set; }
        public bool AutosaveEachTurn { get; set; }
        public bool ShowEnemyMoves { get; set; }
        public bool NoPauseAfterEnemyMoves { get; set; }
        public bool FastPieceSlide { get; set; }
        public bool InstantAdvice { get; set; }
        public bool TutorialHelp { get; set; }
        public bool EnterClosestCityScreen { get; set; }
        public bool MoveUnitsWithoutMouse { get; set; }

        // GRAPHICS OPTIONS
        public bool ThroneRoomGraphics { get; set; }
        public bool DiplomacyScreenGraphics { get; set; }
        public bool AnimatedHeralds { get; set; }
        public bool CivilopediaForAdvances { get; set; }
        public bool HighCouncil { get; set; }
        public bool WonderMovies { get; set; }

        // CITY REPORT OPTIONS
        //
        // On to begin with, as they are in Civ II: these decide which of the
        // messages a city sends its ruler actually get through, and a player who
        // has never opened the City Report Options dialog expects to be told when
        // a city falls into disorder. They all defaulted to off, which was
        // invisible only because nothing consulted them at all.
        public bool WarnWhenCityGrowthHalted { get; set; } = true;
        public bool ShowCityImprovementsBuilt { get; set; } = true;
        public bool ShowNonCombatUnitsBuilt { get; set; } = true;
        public bool ShowInvalidBuildInstructions { get; set; } = true;
        public bool AnnounceCitiesInDisorder { get; set; } = true;
        public bool AnnounceOrderRestored { get; set; } = true;
        public bool AnnounceWeLoveKingDay { get; set; } = true;
        public bool WarnWhenFoodDangerouslyLow { get; set; } = true;
        public bool WarnWhenPollutionOccurs { get; set; } = true;
        public bool WarnChangProductWillCostShields { get; set; } = true;
        public bool ZoomToCityNotDefaultAction { get; set; }

        public bool CheatPenaltyWarningDisabled { get; set; }
        public bool Grid { get; set; }
        public bool ScoringComplete { get; set; }
        public bool ScenarioFile { get; set; }
        public bool ScenarioFlagToggled { get; set; }

        public Options(bool[] optionsArray)
        {
            SimplifiedCombat = optionsArray[0];
            Bloodlust = optionsArray[1];
            DontRestartIfEliminated = optionsArray[2];
            FlatEarth = optionsArray[3];
            Music = optionsArray[4];
            SoundEffects = optionsArray[5];
            Grid = optionsArray[6];
            EnterClosestCityScreen = optionsArray[7];
            MoveUnitsWithoutMouse = optionsArray[8];
            TutorialHelp = optionsArray[9];
            InstantAdvice = optionsArray[10];
            FastPieceSlide = optionsArray[11];
            NoPauseAfterEnemyMoves = optionsArray[12];
            ShowEnemyMoves = optionsArray[13];
            AutosaveEachTurn = optionsArray[14];
            AlwaysWaitAtEndOfTurn = optionsArray[15];
            CheatMenu = optionsArray[16];
            WonderMovies = optionsArray[17];
            ThroneRoomGraphics = optionsArray[18];
            DiplomacyScreenGraphics = optionsArray[19];
            CivilopediaForAdvances = optionsArray[20];
            HighCouncil = optionsArray[21];
            AnimatedHeralds = optionsArray[22];
            CheatPenaltyWarningDisabled = optionsArray[23];
            ScoringComplete = optionsArray[24];
            ScenarioFile = optionsArray[25];
            ScenarioFlagToggled = optionsArray[26];
            WarnWhenCityGrowthHalted = optionsArray[27];
            ShowCityImprovementsBuilt = optionsArray[28];
            ShowNonCombatUnitsBuilt = optionsArray[29];
            ShowInvalidBuildInstructions = optionsArray[30];
            AnnounceCitiesInDisorder = optionsArray[31];
            AnnounceOrderRestored = optionsArray[32];
            AnnounceWeLoveKingDay = optionsArray[33];
            WarnWhenFoodDangerouslyLow = optionsArray[34];
            WarnWhenPollutionOccurs = optionsArray[35];
            WarnChangProductWillCostShields = optionsArray[36];
            ZoomToCityNotDefaultAction = optionsArray[37];
        }
    }
}
