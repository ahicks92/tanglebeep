namespace Tanglebeep.Gameplay {
    /// <summary>
    /// Map features the mod deliberately re-categorizes for navigation because the game models them as
    /// something other than what they functionally are.
    ///
    /// <para>The only case so far is the Legend of Shara DLC's <b>Riverstone Waterway</b> — the alternate
    /// first dungeon (floors 1-5). It is entered not by a staircase but by an interactable town NPC
    /// (<c>npc_jumpintoriver</c>) you "jump into the river" at; the game places it in Riverstone Camp once
    /// the Waterway is unlocked (<c>DLCManager.CheckForWaterwayUnlockAndJumpPoint</c>), and interacting
    /// runs <c>DialogEventsScript.JumpIntoRiverstoneRiver</c>, which warps the hero to the first Waterway
    /// floor. Functionally it is a down-staircase into an alternate dungeon, so the object radar and the
    /// scanner surface it as <b>stairs</b> rather than as a service NPC. That is also why it would
    /// otherwise vanish from the radar: the trigger NPC carries no display name, and an unnamed actor is
    /// dropped — hence the explicit <see cref="WaterwayLabel"/> below.</para>
    /// </summary>
    internal static class SpecialEntrances {
        // The Waterway entrance NPC, placed in town by DLCManager.CheckForWaterwayUnlockAndJumpPoint.
        private const string WaterwayEntranceRef = "npc_jumpintoriver";

        /// <summary>The label the radar and scanner speak for the Waterway entrance, in place of the
        /// trigger NPC's (empty) display name.</summary>
        public const string WaterwayLabel = "Riverstone Waterway entrance";

        /// <summary>Whether this actor is an entrance the mod presents as a staircase.</summary>
        public static bool IsStairsLikeEntrance(Actor actor) {
            return actor != null
                && actor.GetActorType() == ActorTypes.NPC
                && actor.actorRefName == WaterwayEntranceRef;
        }

        /// <summary>The override label for a stairs-like entrance, or null when the actor isn't one
        /// (so callers can <c>?? fall back</c> to their normal naming).</summary>
        public static string Label(Actor actor) {
            return IsStairsLikeEntrance(actor) ? WaterwayLabel : null;
        }
    }
}
