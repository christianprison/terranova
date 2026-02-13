using UnityEngine;

namespace Terranova.Buildings
{
    /// <summary>
    /// ScriptableObject defining a building type's properties.
    ///
    /// Create instances via: Assets → Create → Terranova → Building Definition.
    /// Each building type (Campfire, Hut, Woodcutter's Hut, etc.) gets its own asset.
    ///
    /// For MS1, only the Campfire is needed. More buildings come in MS2.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBuilding", menuName = "Terranova/Building Definition")]
    public class BuildingDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display name shown in UI.")]
        public string DisplayName = "Building";

        [Tooltip("Short description for tooltips.")]
        [TextArea(2, 4)]
        public string Description = "";

        [Header("Costs")]
        [Tooltip("Wood required to build.")]
        public int WoodCost;

        [Tooltip("Stone required to build.")]
        public int StoneCost;

        [Header("Placement")]
        [Tooltip("Size in blocks (X × Z). Campfire = 1×1, Hut = 2×2.")]
        public Vector2Int FootprintSize = Vector2Int.one;

        [Tooltip("Can this building be rotated 90°?")]
        public bool NeedsRotation;

        [Header("Navigation (Story 2.3)")]
        [Tooltip("Local offset from building center to entrance. Settlers walk here.")]
        public Vector3 EntranceOffset = new Vector3(0f, 0f, -1f);

        [Header("Visuals (Prototype)")]
        [Tooltip("Color for the placeholder cube. Replaced by proper models later.")]
        public Color PreviewColor = Color.yellow;

        [Tooltip("Height of the placeholder cube in blocks.")]
        public float VisualHeight = 1f;
    }
}
