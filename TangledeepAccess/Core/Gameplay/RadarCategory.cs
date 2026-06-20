namespace TangledeepAccess.Gameplay {
    /// <summary>
    /// What kind of thing a scanned entity is, which selects the radar ping's voice: a per-category
    /// sample (monster, container, powerup, shop, stairs) or, for <see cref="Default"/>, the scanner's
    /// triangle tone. Classified from the entity's game type at collection time and carried through the
    /// scan ring to the ping. Pure (Core): the enum is data; the engine maps it to samples.
    /// </summary>
    public enum RadarCategory {
        Default,
        Monster,
        Container,
        Powerup,
        Shop,
        Stairs,
    }
}
