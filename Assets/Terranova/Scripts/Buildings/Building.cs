using UnityEngine;
using UnityEngine.AI;

namespace Terranova.Buildings
{
    /// <summary>
    /// Represents a placed building in the world.
    ///
    /// Story 2.3: Provides a NavMesh entrance point and carves the building
    /// footprint from the NavMesh so settlers walk around it.
    ///
    /// The entrance position is where settlers stop when navigating to this
    /// building (e.g., to deliver resources or start construction).
    /// </summary>
    public class Building : MonoBehaviour
    {
        private BuildingDefinition _definition;
        private NavMeshObstacle _obstacle;

        /// <summary>The definition (type) of this building.</summary>
        public BuildingDefinition Definition => _definition;

        /// <summary>
        /// World-space position of the building entrance.
        /// Settlers navigate here instead of the building center.
        /// </summary>
        public Vector3 EntrancePosition
        {
            get
            {
                if (_definition != null)
                    return transform.position + _definition.EntranceOffset;
                // Fallback: slightly in front of the building
                return transform.position + Vector3.back;
            }
        }

        /// <summary>
        /// Initialize the building with its definition. Sets up NavMeshObstacle
        /// to carve the building footprint from the NavMesh.
        /// </summary>
        public void Initialize(BuildingDefinition definition)
        {
            _definition = definition;

            // Carve building footprint from NavMesh so settlers walk around it
            _obstacle = gameObject.AddComponent<NavMeshObstacle>();
            _obstacle.shape = NavMeshObstacleShape.Box;
            _obstacle.size = new Vector3(
                definition.FootprintSize.x,
                definition.VisualHeight,
                definition.FootprintSize.y
            );
            _obstacle.center = Vector3.zero;
            _obstacle.carving = true;
            _obstacle.carvingMoveThreshold = 0.1f;
        }
    }
}
