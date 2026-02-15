namespace Terranova.Core
{
    /// <summary>
    /// Persistent game state that survives scene transitions.
    /// Stores the seed and biome selected in the main menu.
    /// MS4 Feature 1.1: Main Menu.
    /// </summary>
    public static class GameState
    {
        public static int Seed { get; set; } = 42;
        public static BiomeType SelectedBiome { get; set; } = BiomeType.Forest;
        public static bool IsNewGame { get; set; } = true;
        public static int DayCount { get; set; } = 1;
        public static float GameTimeSeconds { get; set; }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Seed = 42;
            SelectedBiome = BiomeType.Forest;
            IsNewGame = true;
            DayCount = 1;
            GameTimeSeconds = 0f;
        }
    }
}
