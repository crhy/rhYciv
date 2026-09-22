using System.Numerics;
using RhyCiv.Engine.Events;
using RhyCiv.Engine.MapObjects;
using RaylibUI.RunGame.GameControls.Mapping.Views.ViewElements;
using Model;
using Model.ImageSets;
using Model.Core;
using Model.Interface;
using ExtensionMethods;

namespace RaylibUI.RunGame.GameControls.Mapping.Views;

internal class AttackAnimation : BaseGameView
{
    public AttackAnimation(GameScreen gameScreen, CombatEventArgs args, IGameView? previousView, int viewHeight,
        int viewWidth, bool forceRedraw) : base(gameScreen, args.Location.FirstOrDefault(l => l != null) ?? gameScreen.Game.ActivePlayer.ActiveTile, previousView, viewHeight, viewWidth,
        false, 70, args.Location.Where(l => l != null).ToList(), forceRedraw)
    {
        var active = gameScreen.Main.ActiveInterface;
        var game = gameScreen.Game;

        var unitAnimations = new List<IViewElement>();
        var attackerPos  = ActivePos with{ Y = ActivePos.Y + Dimensions.TileHeight - active.UnitImages.UnitRectangle.Height.ZoomScale(gameScreen.Zoom) };
        ImageUtils.GetUnitTextures(args.Attacker, active, game, unitAnimations, attackerPos, useMapArt: true);
        var defPos = args.Defender.CurrentLocation != null ? GetPosForTile(args.Defender.CurrentLocation) : ActivePos;
        var defenderPos = defPos with { Y = defPos.Y + Dimensions.TileHeight - active.UnitImages.UnitRectangle.Height.ZoomScale(gameScreen.Zoom) };
        ImageUtils.GetUnitTextures(args.Defender, active, game, unitAnimations,
            defenderPos, useMapArt: true);
        var explosion = 0;
        //SetAnimation(unitAnimations);
        var battleAnimation = active.UnitImages.BattleAnim.Select(a => TextureCache.GetImage(a)).ToArray();
        if (battleAnimation.Length > 0)
        {
            var attackPos = ActivePos  + new Vector2(Dimensions.HalfWidth - battleAnimation[0].Width/2f, Dimensions.HalfHeight - battleAnimation[0].Height /2f);

            defPos += new Vector2(Dimensions.HalfWidth - battleAnimation[0].Width / 2f, Dimensions.HalfHeight - battleAnimation[0].Height /2f);
        }
        while (explosion < args.CombatRoundsAttackerWins.Count)
        {
            var attackerWins = args.CombatRoundsAttackerWins[explosion];
            unitAnimations = AddJustAnimations(unitAnimations, active.UnitShield((int)args.Attacker.Type), args.Attacker.Hitpoints[explosion], args.Defender.Hitpoints[explosion]);
            foreach (var battleTexture in battleAnimation)
            {
                var target = attackerWins ? defPos : ActivePos;
                SetAnimation(unitAnimations.Concat([new TextureElement(battleTexture, target, Location)])
                    .ToList());
            }

            explosion += 5;
        }

        ShowTheFallen(gameScreen, args, active, game);
    }

    /// <summary>
    /// Number of frames the aftermath is held for. At this view's 70ms interval
    /// that is a little under a second: long enough to see who was left standing
    /// and where, short enough not to be in the way when a war is being fought a
    /// dozen battles a turn.
    /// </summary>
    private const int AftermathFrames = 12;

    /// <summary>
    /// Holds the map still on the square where the loser fell, and marks it.
    /// <para>
    /// Combat used to end on the last frame of the explosion and hand straight back
    /// to whatever came next, so a unit killed during someone else's turn was gone
    /// before it could be seen to die -- the barbarians would take a Horsemen and
    /// the only evidence was that it was no longer there. The pause is the whole
    /// point; the marker says which square it happened on.
    /// </para>
    /// </summary>
    private void ShowTheFallen(GameScreen gameScreen, CombatEventArgs args,
        IUserInterface active, IGame game)
    {
        // Use the hitpoints captured when the exchange finished, not the per-round
        // series. That series records each unit's hitpoints at the *start* of a
        // round, before the round's damage, so the loser's last entry is its health
        // just before the fatal blow -- always above zero. Reading it meant this
        // never once decided anybody had died, and the pause and the marker never
        // appeared at all.
        var defenderLost = args.Defender.RemainingHitpoints <= 0;
        var attackerLost = args.Attacker.RemainingHitpoints <= 0;
        if (!defenderLost && !attackerLost)
        {
            return;
        }

        var fallen = defenderLost ? args.Defender : args.Attacker;
        var survivor = defenderLost ? args.Attacker : args.Defender;

        // Only the winner is still on the map, so the aftermath is drawn from
        // scratch rather than from the frames the exchange was animated with.
        var aftermath = new List<IViewElement>();
        if (survivor.CurrentLocation != null)
        {
            var survivorPos = GetPosForTile(survivor.CurrentLocation);
            ImageUtils.GetUnitTextures(survivor, active, game, aftermath,
                survivorPos with
                {
                    Y = survivorPos.Y + Dimensions.TileHeight -
                        active.UnitImages.UnitRectangle.Height.ZoomScale(gameScreen.Zoom)
                }, useMapArt: true);
        }

        var marker = FossArt.GetTexture(Path.Combine("Other", "deadtroop.png"));
        if (marker.HasValue && fallen.CurrentLocation != null)
        {
            var texture = marker.Value;
            // Fit the marker to the same box a unit occupies, so it reads as
            // standing on the square rather than floating over the map.
            var box = new Vector2(active.UnitImages.UnitRectangle.Width,
                active.UnitImages.UnitRectangle.Height);
            var renderScale = MathF.Min(box.X / texture.Width, box.Y / texture.Height);
            var fallenPos = GetPosForTile(fallen.CurrentLocation);
            aftermath.Add(new TextureElement(
                texture: texture,
                location: fallenPos with
                {
                    Y = fallenPos.Y + Dimensions.TileHeight - box.Y.ZoomScale(gameScreen.Zoom)
                },
                tile: fallen.CurrentLocation,
                offset: new Vector2(MathF.Max(0f, (box.X - texture.Width * renderScale) / 2f),
                                    MathF.Max(0f, box.Y - texture.Height * renderScale)),
                renderScale: renderScale,
                maxDrawSize: box));
        }

        for (var frame = 0; frame < AftermathFrames; frame++)
        {
            SetAnimation(aftermath.Select(element => element.CloneForLocation(element.Location)).ToList());
        }
    }

    private List<IViewElement> AddJustAnimations(List<IViewElement> unitAnimations, UnitShield shield, params int[] hitpoints)
    {
        int idx = 0;
        return unitAnimations.Select(a =>
        {
            if (a is HealthBar health)
            {
                return new HealthBar(health.Location, health.Tile, hitpoints[idx++], health.BaseHitpoints, health.Offset, shield);
            }

            return a;
        }).ToList();
    }
}