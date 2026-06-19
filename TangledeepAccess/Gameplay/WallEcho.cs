using TangledeepAccess.Audio;
using UnityEngine;

namespace TangledeepAccess.Gameplay {
    /// <summary>
    /// "Wall echo" navigation aid: pings the four cardinal walls around the hero as panned,
    /// pitched tones so the player can hear the shape of the space they are in. For each
    /// direction it walks outward to the line-of-sight limit, finds the first impassable terrain
    /// tile, and feeds its distance to <see cref="WallEchoCue"/>; the resulting stereo buffer is
    /// played through <see cref="TonePlayer"/>. All audio tuning lives in the cue; this file only
    /// owns the sight range and the ray-march.
    /// </summary>
    internal static class WallEcho {
        /// <summary>
        /// LOS radius is ~8.5 (a 17×17 box centered on the hero), so a cardinal ray reaches at
        /// most 8 tiles. Walls farther than this are never seen and never pinged.
        /// </summary>
        public const int MaxSightTiles = 8;

        // The synth pre-filters its noise pools for a given output rate; keep one and rebuild only
        // if the device sample rate changes (rare).
        private static WallEchoSynth _synth;

        public static void Play() {
            HeroPC hero = GameMasterScript.heroPCActor;
            if (hero == null || !GameMasterScript.actualGameStarted || MapMasterScript.activeMap == null) {
                return;
            }

            Vector2 pos = hero.GetPos();
            double? left = WallDistance(hero, pos, -1, 0);
            double? right = WallDistance(hero, pos, 1, 0);
            double? up = WallDistance(hero, pos, 0, 1);   // +y is north / up
            double? down = WallDistance(hero, pos, 0, -1);

            int sampleRate = AudioSettings.outputSampleRate;
            if (_synth == null || _synth.SampleRate != sampleRate) {
                _synth = new WallEchoSynth(sampleRate);
            }

            GrainTimeline timeline = _synth.Build(left, right, up, down);
            if (timeline.Placements.Count == 0) {
                return; // open in all four directions to the sight limit — nothing to ping
            }

            TonePlayer.PlayStereo(timeline.RenderStereo(sampleRate), sampleRate);
        }

        /// <summary>
        /// Tiles to the first impassable wall along (dx, dy), or null if no visible wall is in
        /// range. The march passes through passable tiles even when an individual tile is not
        /// currently flagged visible — only the wall's own visibility gates the ping (see
        /// <see cref="WallRay"/>). The visibility predicate is only called on in-bounds wall tiles.
        /// </summary>
        private static double? WallDistance(HeroPC hero, Vector2 origin, int dx, int dy) {
            int? d = WallRay.DistanceToWall(
                (int)origin.x,
                (int)origin.y,
                dx,
                dy,
                MaxSightTiles,
                (x, y) => MapMasterScript.InBounds(new Vector2(x, y)),
                (x, y) => TerrainQuery.IsImpassableWall(new Vector2(x, y)),
                (x, y) => IsVisible(hero, x, y));
            return d.HasValue ? (double?)d.Value : null;
        }

        private static bool IsVisible(HeroPC hero, int x, int y) {
            bool[,] visible = hero.visibleTilesArray;
            return visible != null && visible[x, y];
        }
    }
}
