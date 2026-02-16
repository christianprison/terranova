using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Terranova.Terrain;
using Terranova.Buildings;
using Terranova.Camera;
using Terranova.UI;
using Terranova.Population;
using Terranova.Resources;
using Terranova.Input;
using Terranova.Discovery;

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
            // Show main menu on every fresh Play; skip once the player has started a game.
            // GameState.GameStarted is reset to false by ResetStatics (SubsystemRegistration)
            // and set to true by MainMenuUI before loading the game scene.
            if (!GameState.GameStarted)
            {
                EnsureMainMenu();
                EnsureEventSystem();
                Debug.Log("GameBootstrapper: Main menu ready.");
                return;
            }

            // Game scene bootstrap
            EnsureWorldManager();
            EnsureResourceManager();
            EnsureMaterialInventory(); // MS4: Material system
            EnsureBuildingRegistry();
            EnsureCamera();
            EnsureBuildingPlacer();
            EnsureUI();
            EnsureEventSystem();
            EnsureSettlerSpawner();
            EnsureResourceSpawner();
            EnsureResourceTaskAssigner();
            EnsureConstructionTaskAssigner();
            EnsureBuildingFunctionManager();
            EnsureDebugTerrainModifier();
            EnsureSelectionManager();
            EnsureDiscoverySystem();
            EnsureDayNightCycle(); // MS4: Day-night cycle

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

        /// <summary>
        /// Registry of all buildable building types.
        /// Story 4.3: Gebäude-Typen Epoche I.1
        /// </summary>
        private static void EnsureBuildingRegistry()
        {
            if (BuildingRegistry.Instance != null)
                return;

            var go = new GameObject("BuildingRegistry");
            go.AddComponent<BuildingRegistry>();
            Debug.Log("GameBootstrapper: Created BuildingRegistry.");
        }

        /// <summary>
        /// Central resource storage. Must exist before UI and BuildingPlacer.
        /// Story 4.1: Baukosten-System
        /// </summary>
        private static void EnsureResourceManager()
        {
            if (ResourceManager.Instance != null)
                return;

            var go = new GameObject("ResourceManager");
            go.AddComponent<ResourceManager>();
            Debug.Log("GameBootstrapper: Created ResourceManager.");
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
            var placer = Object.FindFirstObjectByType<BuildingPlacer>();

            if (placer == null)
            {
                var go = new GameObject("GameManager");
                placer = go.AddComponent<BuildingPlacer>();
                Debug.Log("GameBootstrapper: Created BuildingPlacer.");
            }

            // Always ensure a building is assigned (even on manually-added placers)
            if (!placer.HasBuilding)
            {
                var campfire = ScriptableObject.CreateInstance<BuildingDefinition>();
                campfire.DisplayName = "Campfire";
                campfire.Description = "A simple campfire. The heart of your settlement.";
                campfire.Type = BuildingType.Campfire;
                campfire.WoodCost = 5;
                campfire.StoneCost = 0;
                campfire.FootprintSize = Vector2Int.one;
                campfire.PreviewColor = new Color(1f, 0.8f, 0.2f); // Warm yellow
                campfire.VisualHeight = 1f;

                placer.SetBuilding(campfire);
                Debug.Log("GameBootstrapper: Assigned default Campfire to BuildingPlacer.");
            }
        }

        private static void EnsureUI()
        {
            if (Object.FindFirstObjectByType<ResourceDisplay>() != null)
                return;

            var go = new GameObject("HUD");
            go.AddComponent<ResourceDisplay>();
            // Story 4.5: Build menu lives on the same Canvas
            go.AddComponent<BuildMenu>();
            // Story 6.1: Info panel for selection
            go.AddComponent<InfoPanel>();
            // Loading screen (auto-destroys after terrain generation)
            go.AddComponent<LoadingScreen>();
            // Render debug overlay for iPad grey screen diagnosis (temporary)
            go.AddComponent<RenderDebugOverlay>();
            // Game-state debug overlay (F3 toggle)
            go.AddComponent<DebugOverlay>();
            Debug.Log("GameBootstrapper: Created HUD with ResourceDisplay, BuildMenu, InfoPanel, LoadingScreen, RenderDebugOverlay, and DebugOverlay.");
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

        private static void EnsureSettlerSpawner()
        {
            if (Object.FindFirstObjectByType<SettlerSpawner>() != null)
                return;

            var go = new GameObject("SettlerSpawner");
            go.AddComponent<SettlerSpawner>();
            Debug.Log("GameBootstrapper: Created SettlerSpawner.");
        }

        private static void EnsureResourceSpawner()
        {
            if (Object.FindFirstObjectByType<ResourceSpawner>() != null)
                return;

            var go = new GameObject("ResourceSpawner");
            go.AddComponent<ResourceSpawner>();
            Debug.Log("GameBootstrapper: Created ResourceSpawner.");
        }

        /// <summary>
        /// DEBUG ONLY - Click terrain to modify blocks (left=remove, right=add).
        /// Remove this when a proper terrain editing tool exists.
        /// Story 0.5: Terrain-Modifikation aktualisiert Mesh
        /// </summary>
        private static void EnsureDebugTerrainModifier()
        {
            if (Object.FindFirstObjectByType<DebugTerrainModifier>() != null)
                return;

            var go = new GameObject("DebugTerrainModifier");
            go.AddComponent<DebugTerrainModifier>();
            Debug.Log("GameBootstrapper: Created DebugTerrainModifier (left-click=remove, right-click=add).");
        }

        /// <summary>
        /// Automatic resource task assignment for idle settlers.
        /// Story 3.2: Sammel-Interaktion
        /// </summary>
        private static void EnsureResourceTaskAssigner()
        {
            if (Object.FindFirstObjectByType<ResourceTaskAssigner>() != null)
                return;

            var go = new GameObject("ResourceTaskAssigner");
            go.AddComponent<ResourceTaskAssigner>();
            Debug.Log("GameBootstrapper: Created ResourceTaskAssigner.");
        }

        /// <summary>
        /// Assigns idle settlers to unfinished construction sites.
        /// Story 4.2: Baufortschritt
        /// </summary>
        private static void EnsureConstructionTaskAssigner()
        {
            if (Object.FindFirstObjectByType<ConstructionTaskAssigner>() != null)
                return;

            var go = new GameObject("ConstructionTaskAssigner");
            go.AddComponent<ConstructionTaskAssigner>();
            Debug.Log("GameBootstrapper: Created ConstructionTaskAssigner.");
        }

        /// <summary>
        /// Manages building functions (worker assignment, housing capacity).
        /// Story 4.4: Gebäude-Funktion
        /// </summary>
        private static void EnsureBuildingFunctionManager()
        {
            if (Object.FindFirstObjectByType<BuildingFunctionManager>() != null)
                return;

            var go = new GameObject("BuildingFunctionManager");
            go.AddComponent<BuildingFunctionManager>();
            Debug.Log("GameBootstrapper: Created BuildingFunctionManager.");
        }

        /// <summary>
        /// Selection manager for tap/long-press on settlers and buildings.
        /// Story 6.1–6.4: Selektion & Info-Panel
        /// </summary>
        private static void EnsureSelectionManager()
        {
            if (Object.FindFirstObjectByType<SelectionManager>() != null)
                return;

            var go = new GameObject("SelectionManager");
            go.AddComponent<SelectionManager>();
            Debug.Log("GameBootstrapper: Created SelectionManager.");
        }

        /// <summary>
        /// Discovery system: activity tracking, probability engine, state manager, registry.
        /// Story 1.1–1.5: Discovery Engine
        /// </summary>
        private static void EnsureDiscoverySystem()
        {
            if (Object.FindFirstObjectByType<DiscoveryEngine>() != null)
                return;

            var go = new GameObject("DiscoverySystem");
            go.AddComponent<ActivityTracker>();
            go.AddComponent<DiscoveryStateManager>();
            go.AddComponent<DiscoveryEngine>();
            go.AddComponent<DiscoveryRegistry>();
            go.AddComponent<DiscoveryEffectsManager>();
            Debug.Log("GameBootstrapper: Created DiscoverySystem (ActivityTracker, StateManager, Engine, Registry, EffectsManager).");
        }

        /// <summary>
        /// MS4 Feature 1.1: Main Menu UI.
        /// </summary>
        private static void EnsureMainMenu()
        {
            if (Object.FindFirstObjectByType<MainMenuUI>() != null)
                return;

            var go = new GameObject("MainMenu");
            go.AddComponent<MainMenuUI>();
            Debug.Log("GameBootstrapper: Created MainMenuUI.");
        }

        /// <summary>
        /// MS4 Feature 1.5: Day-Night Cycle.
        /// </summary>
        private static void EnsureDayNightCycle()
        {
            if (Object.FindFirstObjectByType<DayNightCycle>() != null)
                return;

            var go = new GameObject("DayNightCycle");
            go.AddComponent<DayNightCycle>();
            Debug.Log("GameBootstrapper: Created DayNightCycle.");
        }

        /// <summary>
        /// MS4 Feature 2: Material Inventory system.
        /// </summary>
        private static void EnsureMaterialInventory()
        {
            if (MaterialInventory.Instance != null)
                return;

            var go = new GameObject("MaterialInventory");
            go.AddComponent<MaterialInventory>();
            Debug.Log("GameBootstrapper: Created MaterialInventory.");
        }
    }
}
