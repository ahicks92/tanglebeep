using System.Collections.Generic;
using TangledeepAccess.Audio;
using TangledeepAccess.Speech;
using UnityEngine;

namespace TangledeepAccess.Gameplay {
    /// <summary>
    /// Object radar (F2): a continuous sweep of everything in line of sight. Ctrl+F2 toggles it on
    /// or off (off by default); there is no Shift+F2 — it is a pure toggle, not a per-event aid. While
    /// on, it pings one visible entity every <see cref="ScanCue.IntervalSeconds"/>, encoding its
    /// left-right offset as pan and its north-south offset as the reference/second-grain interval.
    /// An <see cref="ObjectRadarRing"/> keeps the rotation: entities that move keep their slot, newcomers
    /// jump to the front so they are heard at once. Reconciles at each ping (cheap, and a newcomer found
    /// then is played immediately).
    /// </summary>
    internal sealed class ObjectRadar : NavAid {
        private readonly ObjectRadarRing _ring = new ObjectRadarRing();
        private readonly List<ObjectRadarRing.Entry> _current = new List<ObjectRadarRing.Entry>();
        private double _timer;

        public ObjectRadar() : base("object radar", enabled: false) { }

        public override MessageBuilder OnCtrl() {
            MessageBuilder spoken = ToggleSpoken();
            if (!Enabled) {
                _ring.Clear();
                _timer = 0.0;
            }
            return spoken;
        }

        public override void Tick(double dt) {
            if (!Enabled) {
                return;
            }

            HeroPC hero = GameMasterScript.heroPCActor;
            if (hero == null || !GameMasterScript.actualGameStarted || MapMasterScript.activeMap == null
                || UIManagerScript.AnyInteractableWindowOpen()) {
                return; // pause the sweep out of play or while a menu/dialog is up
            }

            _timer += dt;
            if (_timer < ScanCue.IntervalSeconds) {
                return;
            }
            _timer = 0.0;

            Reconcile(hero);
            ObjectRadarRing.Entry? next = _ring.Next();
            if (next.HasValue) {
                TonePlayer.PlayTimeline(VoicePing(next.Value));
            }
        }

        // The ping for one entity: its category's radar sample if one is loaded, else the default
        // triangle tone. Both share ScanCue's pan/pitch/level/envelope, so only the timbre differs.
        private static GrainTimeline VoicePing(ObjectRadarRing.Entry e) {
            if (SampleBank.TryGet(e.Category, out float[] samples, out int sampleRate)) {
                return ScanCue.BuildSample(e.X, e.Y, samples, sampleRate);
            }
            return ScanCue.Build(e.X, e.Y);
        }

        // Rebuild the current visible set (offsets relative to the hero) and reconcile the ring.
        private void Reconcile(HeroPC hero) {
            Vector2 hp = hero.GetPos();
            _current.Clear();
            foreach (Poi poi in Surroundings.CollectVisible(hero)) {
                int x = (int)poi.Pos.x - (int)hp.x;
                int y = (int)poi.Pos.y - (int)hp.y;
                _current.Add(new ObjectRadarRing.Entry(poi.Handle, x, y, poi.Category));
            }
            _ring.Reconcile(_current);
        }
    }
}
