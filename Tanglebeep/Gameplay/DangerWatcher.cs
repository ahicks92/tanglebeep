using Tanglebeep.Audio;
using UnityEngine;

namespace Tanglebeep.Gameplay {
    /// <summary>
    /// Plays a warning cue at the start of any turn the hero begins standing on a telegraphed danger
    /// tile — a charging monster's incoming-attack square (<see cref="TileDescriber.HasDangerSquare"/>).
    /// The game gives no audible warning that you are about to be hit where you stand; this is the
    /// blind-player substitute for seeing the red warning square under your feet, and a prompt to move.
    ///
    /// <para>Polled every frame from the pump and edge-triggered on the danger state of the hero's
    /// current tile, so the cue fires the moment a square lights up underfoot — whether you stepped
    /// onto it, a monster began charging it while you stood still, or a fast monster telegraphed it
    /// mid-resolution of one of your turns (a turn-counter edge would sample at the wrong instant and
    /// miss that last case). To keep prompting you to move, it also re-fires once per hero turn
    /// (<c>GameMasterScript.turnNumber</c>) for as long as you remain on a danger square. The first
    /// observation after entering play only arms the detector (no cue on spawn / load). Pure audio,
    /// no speech.</para>
    /// </summary>
    internal static class DangerWatcher {
        private static bool _have;
        private static bool _wasDanger;
        private static int _lastTurn;
        private static int _lastX;
        private static int _lastY;

        public static void PollTurn() {
            HeroPC hero = GameMasterScript.heroPCActor;
            if (hero == null || !GameMasterScript.actualGameStarted || MapMasterScript.activeMap == null) {
                _have = false; // out of play; re-arm so re-entry doesn't fire on the first frame back
                _wasDanger = false;
                return;
            }

            Vector2 p = hero.GetPos();
            int x = (int)p.x;
            int y = (int)p.y;
            int turn = GameMasterScript.turnNumber;
            bool dangerous = TileDescriber.HasDangerSquare(MapMasterScript.GetTile(p));

            if (!_have) {
                // Arm only; don't cue if we spawn / load already standing on a danger square.
                _have = true;
                _wasDanger = dangerous;
                _lastTurn = turn;
                _lastX = x;
                _lastY = y;
                return;
            }

            // Fire when the tile is dangerous AND there's a fresh reason to warn: it just lit up
            // (false -> true), the hero moved onto it, or a new turn began while we still stand on it
            // (the keep-moving nag). A persistent square that's already been announced this turn and
            // this position stays silent until one of those changes.
            bool appeared = dangerous && !_wasDanger;
            bool moved = x != _lastX || y != _lastY;
            bool newTurn = turn != _lastTurn;
            if (dangerous && (appeared || moved || newTurn)) {
                CursorSounds.PlayDangerous();
            }

            _wasDanger = dangerous;
            _lastTurn = turn;
            _lastX = x;
            _lastY = y;
        }
    }
}
