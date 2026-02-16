namespace Terranova.Core
{
    /// <summary>
    /// Personality/capability traits for settlers.
    /// Each settler gets one random trait at spawn that affects gameplay.
    /// v0.4.0 bugfix: 5 traits with gold label in info panel.
    /// </summary>
    public enum SettlerTrait
    {
        /// <summary>+20% discovery chance when near new things.</summary>
        Curious,
        /// <summary>-30% chance of food poisoning.</summary>
        Cautious,
        /// <summary>+15% work/gather speed.</summary>
        Skilled,
        /// <summary>Hunger and thirst drain 25% slower.</summary>
        Robust,
        /// <summary>+30% starvation/dehydration grace period.</summary>
        Enduring
    }
}
