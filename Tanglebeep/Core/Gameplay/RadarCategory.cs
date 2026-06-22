namespace Tanglebeep.Gameplay {
    /// <summary>
    /// What kind of thing a scanned entity is, which selects the radar ping's voice: a per-category
    /// sample (monster, container, powerup, shop, stairs) or, for any category with no loaded sample
    /// (<see cref="Default"/>, <see cref="Fountain"/>, <see cref="Altar"/>), the radar's triangle tone.
    /// Classified from the entity's game type at collection time and carried through the radar snapshot
    /// to the ping. Pure (Core): the enum is data; the engine maps it to samples. The same enum also
    /// names the points-of-interest read's membership (everything but <see cref="Default"/>,
    /// <see cref="Monster"/>, and <see cref="Shop"/>).
    /// </summary>
    public enum RadarCategory {
        Default,
        Monster,
        Container,
        Powerup,
        Shop,
        Stairs,
        Fountain, // regen fountain — beneficial station; no sample, triangle ping
        Altar,    // prayer/blessing altar — beneficial station; no sample, triangle ping
    }
}
