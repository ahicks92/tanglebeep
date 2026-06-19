using System;
using TangledeepAccess.Speech;
using UnityEngine;

namespace TangledeepAccess.Gameplay {
    /// <summary>One registered navigation aid: a named audio cue with an on/off toggle and a fire action.</summary>
    internal sealed class NavAid {
        public string Name { get; }
        public bool Enabled;
        private readonly Action _fire;

        public NavAid(string name, bool defaultEnabled, Action fire) {
            Name = name;
            Enabled = defaultEnabled;
            _fire = fire;
        }

        public void Fire() {
            _fire();
        }
    }

    /// <summary>
    /// The navigation-aid framework. A small registry of audio aids, each on an F-key slot (F1 =
    /// index 0, F2 = 1, …). Shift+Fn toggles an aid on or off (spoken, since there is no visual);
    /// Ctrl+Fn fires it once without moving. Enabled aids also auto-fire on each hero step — the
    /// hero-tile edge detection lives here, once for all aids (mirrors <see cref="MovementWatcher"/>).
    /// Adding an aid is one entry in <see cref="Aids"/>.
    /// </summary>
    internal static class NavAids {
        private static readonly NavAid[] Aids = {
            new NavAid("wall echo", defaultEnabled: true, WallEcho.Play), // F1
        };

        // Last hero tile, shared across all aids for the on-move trigger.
        private static bool _have;
        private static int _lastX;
        private static int _lastY;

        /// <summary>Flip an aid on/off; returns the spoken confirmation (or null for a bad index).</summary>
        public static MessageBuilder Toggle(int index) {
            if (index < 0 || index >= Aids.Length) {
                return null;
            }

            NavAid aid = Aids[index];
            aid.Enabled = !aid.Enabled;
            return new MessageBuilder().Fragment(aid.Name).Fragment(aid.Enabled ? "on" : "off");
        }

        /// <summary>Fire an aid once on demand, regardless of its toggle state.</summary>
        public static void FireNow(int index) {
            if (index < 0 || index >= Aids.Length) {
                return;
            }

            Aids[index].Fire();
        }

        /// <summary>Fire every enabled aid when the hero's tile changes. Polled once per frame.</summary>
        public static void PollOnMove() {
            HeroPC hero = GameMasterScript.heroPCActor;
            if (hero == null || !GameMasterScript.actualGameStarted || MapMasterScript.activeMap == null) {
                _have = false;
                return;
            }

            Vector2 pos = hero.GetPos();
            int x = (int)pos.x;
            int y = (int)pos.y;
            if (_have && x == _lastX && y == _lastY) {
                return;
            }

            bool first = !_have;
            _have = true;
            _lastX = x;
            _lastY = y;
            if (first) {
                return; // don't fire on the arrival tile
            }

            foreach (NavAid aid in Aids) {
                if (aid.Enabled) {
                    aid.Fire();
                }
            }
        }
    }
}
