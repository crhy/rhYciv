
using RhyCiv.Engine.IO;
using RhyCiv.Engine;
using RhyCiv.Engine.Units;
using Model;
using Model.Core;
using Model.Core.GameRules;
using Model.Core.Units;
using RaylibUI.RunGame;

namespace RaylibUI
{
    public partial class Main
    {


        public void StartGame(IGame game, IDictionary<string, string?>? viewData)
        {
            game.UpdatePlayerViewData();

            _activeScreen = new GameScreen(this, game, Soundman, viewData);

            if (game.TurnNumber == 0)
            {
                game.StartNextTurn();
            }
            else
            {
                // If we're not on turn one start with active player
                game.StartPlayerTurn(game.ActivePlayer);
            }
        }

        /// <summary>
        /// Ruleset metadata keys that saves written before the defork still carry,
        /// mapped to the key the matching interface advertises today. Without this
        /// an old save falls back to path matching and can pick the wrong interface.
        /// </summary>
        private static readonly Dictionary<string, string> LegacyRulesetMetadataKeys = new()
        {
            [RhyCiv.UI.Compact.CompactInterface.LegacyRulesetMetadataKey] =
                RhyCiv.UI.Compact.CompactInterface.RulesetMetadataKey
        };

        public IUserInterface SetActiveRulesetFromFile(string root, string subDirectory,
            Dictionary<string, string> extendedMetadata)
        {
            var metadata = extendedMetadata.ToDictionary(
                pair => LegacyRulesetMetadataKeys.GetValueOrDefault(pair.Key, pair.Key),
                pair => pair.Value);

            if (AllRuleSets.Length == 0) return ActiveInterface;
            var maxScore = -1;
            Ruleset selected = AllRuleSets[0];
            foreach (var set in AllRuleSets)
            {
                var score = metadata
                    .Where(thing => set.Metadata.ContainsKey(thing.Key) && set.Metadata[thing.Key] == thing.Value)
                    .Sum(thing => 1000);

                if (set.Paths.Contains(subDirectory))
                {
                    score += 100;
                }

                if (set.Root == root)
                {
                    score += 10;
                }

                if (score > maxScore)
                {
                    maxScore = score;
                    selected = set;
                }
            }

            ActiveRuleSet = !selected.Paths.Contains(subDirectory) ? new Ruleset(selected, subDirectory) : selected;
            if (selected.InterfaceIndex != Interfaces.IndexOf(ActiveInterface))
            {
                ActiveInterface = Interfaces[selected.InterfaceIndex];
            }

            TextureCache.Clear();
            ImageUtils.SetLook(ActiveInterface);
            return ActiveInterface;
        }

        public Ruleset[] AllRuleSets { get; set; } = [];
        public Ruleset ActiveRuleSet { get; private set; } = null!;

        public IUserInterface SetActiveRuleSet(int ruleSetIndex)
        {
            ActiveRuleSet = AllRuleSets[ruleSetIndex];
            if (ActiveRuleSet.InterfaceIndex != Interfaces.IndexOf(ActiveInterface))
            {
                ActiveInterface = Interfaces[ActiveRuleSet.InterfaceIndex];
            }

            return ActiveInterface;
        }
    }
}
