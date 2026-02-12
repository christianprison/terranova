using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Terranova.Terrain;
using Terranova.Buildings;
using Terranova.Camera;
using Terranova.UI;

namespace Terranova.Core
{
    /// <summary>
    /// Auto-creates all required game systems at runtime if they are missing.
    /// This avoids manual scene setup: just hit Play and everything works.
    ///
    /// Execution order is set early (-100) so systems exist before other scripts
    /// look for them. Each system is only created if not already present,
    /// so manual scene setup always takes priority.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public static class GameBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            // Defer actual creation to AfterSceneLoad so scene objects are available
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapAfterScene()
        {
            EnsureWorldManager();
            EnsureCamera();
            EnsureBuildingPlacer();
            EnsureUI();
            EnsureEventSystem();

            Debug.Log("GameBootstrapper: All systems ready.");
        }

        private static void EnsureWorldManager()
        {
            if (Object.FindFirstObjectByType<WorldManager>() != null)
                return;

            var go = new GameObject("World");
            go.AddComponent<WorldManager>();
            Debug.Log("GameBootstrapper: Created WorldManager.");
        }

        private static void EnsureCamera()
        {
            if (Object.FindFirstObjectByType<RTSCameraController>() != null)
                return;

            var cam = UnityEngine.Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("GameBootstrapper: No Main Camera found.");
                return;
            }

            cam.gameObject.AddComponent<RTSCameraController>();
            Debug.Log("GameBootstrapper: Added RTSCameraController to Main Camera.");
        }

        private static void EnsureBuildingPlacer()
        {
            if (Object.FindFirstObjectByType<BuildingPlacer>() != null)
                return;

            var go = new GameObject("GameManager");
            var placer = go.AddComponent<BuildingPlacer>();

            // Create a default Campfire definition so building works out of the box
            var campfire = ScriptableObject.CreateInstance<BuildingDefinition>();
            campfire.DisplayName = "Campfire";
            campfire.Description = "A simple campfire. The heart of your settlement.";
            campfire.WoodCost = 5;
            campfire.StoneCost = 0;
            campfire.FootprintSize = Vector2Int.one;
            campfire.PreviewColor = new Color(1f, 0.8f, 0.2f); // Warm yellow
            campfire.VisualHeight = 1f;

            placer.SetBuilding(campfire);
            Debug.Log("GameBootstrapper: Created BuildingPlacer with default Campfire.");
        }

        private static void EnsureUI()
        {
            if (Object.FindFirstObjectByType<ResourceDisplay>() != null)
                return;

            var go = new GameObject("HUD");
            go.AddComponent<ResourceDisplay>();
            Debug.Log("GameBootstrapper: Created HUD with ResourceDisplay.");
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            Debug.Log("GameBootstrapper: Created EventSystem.");
        }
    }
}
