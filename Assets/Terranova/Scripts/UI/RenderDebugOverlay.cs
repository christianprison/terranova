// On-screen diagnostic overlay for debugging the iPad grey screen issue.
//
// Displays: camera state, active URP pipeline asset, graphics API,
// quality level, chunk count, mesh vertex totals, shader support status,
// and screen resolution.
//
// Attach to the same Canvas as ResourceDisplay (or any Canvas).
// Always visible on device builds; toggle with F4 in Editor.
//
// Temporary debugging tool – remove once the iPad rendering issue is resolved.

using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Terranova.Terrain;

namespace Terranova.UI
{
    public class RenderDebugOverlay : MonoBehaviour
    {
        private const int FONT_SIZE = 14;
        private const float UPDATE_INTERVAL = 1f; // Refresh every second

        private GameObject _panel;
        private Text _debugText;
        private float _updateTimer;
        private StringBuilder _sb = new();

        // Start hidden by default. Toggle with F4 in both Editor and device builds.
        private bool _isVisible;

        private void Start()
        {
            _isVisible = false;
            CreateOverlayUI();
            _panel.SetActive(_isVisible);

            // Force an immediate refresh
            RefreshDisplay();
        }

        private void Update()
        {
            // F4 toggles the overlay (new Input System)
            var kb = Keyboard.current;
            if (kb != null && kb.f4Key.wasPressedThisFrame)
            {
                _isVisible = !_isVisible;
                _panel.SetActive(_isVisible);
            }

            if (!_isVisible) return;

            _updateTimer -= Time.unscaledDeltaTime;
            if (_updateTimer > 0f) return;
            _updateTimer = UPDATE_INTERVAL;

            RefreshDisplay();
        }

        /// <summary>
        /// Gather all diagnostic info and write it to the overlay text.
        /// </summary>
        private void RefreshDisplay()
        {
            _sb.Clear();
            _sb.AppendLine("=== RENDER DEBUG (F4) ===");
            _sb.AppendLine();

            // --- Graphics API and Quality ---
            _sb.AppendLine("--- Graphics & Quality ---");
            _sb.AppendLine($"  API: {SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceName})");
            int qualityLevel = QualitySettings.GetQualityLevel();
            string[] qualityNames = QualitySettings.names;
            string qualityName = qualityLevel < qualityNames.Length
                ? qualityNames[qualityLevel]
                : $"Index {qualityLevel}";
            _sb.AppendLine($"  Quality: {qualityName} (index {qualityLevel})");
            _sb.AppendLine($"  Screen: {Screen.width}x{Screen.height} @ {Screen.dpi:F0} dpi");
            _sb.AppendLine();

            // --- Render Pipeline ---
            _sb.AppendLine("--- Render Pipeline ---");
            var rpAsset = GraphicsSettings.currentRenderPipeline;
            if (rpAsset != null)
            {
                _sb.AppendLine($"  Asset: {rpAsset.name}");
                _sb.AppendLine($"  Type: {rpAsset.GetType().Name}");
            }
            else
            {
                _sb.AppendLine("  WARNING: No render pipeline asset active!");
                _sb.AppendLine("  (Built-in pipeline fallback = grey screen)");
            }
            _sb.AppendLine();

            // --- Camera ---
            _sb.AppendLine("--- Camera ---");
            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                Vector3 pos = cam.transform.position;
                _sb.AppendLine($"  Position: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
                _sb.AppendLine($"  Rotation: {cam.transform.eulerAngles}");
                _sb.AppendLine($"  ClearFlags: {cam.clearFlags}");
                _sb.AppendLine($"  Background: {cam.backgroundColor}");
                _sb.AppendLine($"  Near/Far: {cam.nearClipPlane:F2} / {cam.farClipPlane:F0}");
                _sb.AppendLine($"  CullingMask: {cam.cullingMask} (Everything={~0})");
                _sb.AppendLine($"  RenderTarget: {(cam.targetTexture != null ? cam.targetTexture.name : "Screen")}");
            }
            else
            {
                _sb.AppendLine("  WARNING: No main camera found!");
            }
            _sb.AppendLine();

            // --- Chunks ---
            _sb.AppendLine("--- Terrain Chunks ---");
            var world = WorldManager.Instance;
            if (world != null)
            {
                // Find all ChunkRenderers in the world hierarchy
                var chunks = world.GetComponentsInChildren<ChunkRenderer>();
                int totalChunks = chunks.Length;
                int activeChunks = 0;
                int totalVertices = 0;
                int emptyMeshChunks = 0;

                foreach (var chunk in chunks)
                {
                    if (!chunk.gameObject.activeInHierarchy) continue;
                    activeChunks++;

                    var mf = chunk.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        int verts = mf.sharedMesh.vertexCount;
                        totalVertices += verts;
                        if (verts == 0) emptyMeshChunks++;
                    }
                    else
                    {
                        emptyMeshChunks++;
                    }
                }

                _sb.AppendLine($"  Total: {totalChunks}  Active: {activeChunks}");
                _sb.AppendLine($"  Vertices: {totalVertices:N0}");
                if (emptyMeshChunks > 0)
                    _sb.AppendLine($"  WARNING: {emptyMeshChunks} chunks have EMPTY meshes!");
                else
                    _sb.AppendLine($"  All chunks have valid meshes.");
            }
            else
            {
                _sb.AppendLine("  WARNING: WorldManager not found!");
            }
            _sb.AppendLine();

            // --- Shader Support ---
            _sb.AppendLine("--- Shader Status ---");
            CheckShader("Terranova/TerrainSplat");
            CheckShader("Terranova/VertexColorOpaque");
            CheckShader("Terranova/VertexColorTransparent");
            CheckShader("Universal Render Pipeline/Lit");
            CheckShader("Universal Render Pipeline/Particles/Unlit");

            // Check what material the chunks are actually using
            if (world != null)
            {
                var firstChunk = world.GetComponentInChildren<MeshRenderer>();
                if (firstChunk != null)
                {
                    var materials = firstChunk.sharedMaterials;
                    _sb.AppendLine($"  Chunk materials ({materials.Length}):");
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var mat = materials[i];
                        if (mat != null)
                        {
                            bool supported = mat.shader != null && mat.shader.isSupported;
                            string status = supported ? "OK" : "NOT SUPPORTED";
                            _sb.AppendLine($"    [{i}] {mat.shader?.name ?? "null"} → {status}");
                        }
                        else
                        {
                            _sb.AppendLine($"    [{i}] null material!");
                        }
                    }
                }
            }
            _sb.AppendLine();

            // --- System Info ---
            _sb.AppendLine("--- System ---");
            _sb.AppendLine($"  Platform: {Application.platform}");
            _sb.AppendLine($"  Unity: {Application.unityVersion}");
            _sb.AppendLine($"  GPU: {SystemInfo.graphicsDeviceVersion}");
            _sb.AppendLine($"  Max Tex: {SystemInfo.maxTextureSize}");
            _sb.AppendLine($"  Compute: {SystemInfo.supportsComputeShaders}");

            _debugText.text = _sb.ToString();
        }

        /// <summary>
        /// Check if a shader exists and is supported on this device.
        /// </summary>
        private void CheckShader(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                _sb.AppendLine($"  {shaderName}: NOT FOUND (stripped?)");
            }
            else if (!shader.isSupported)
            {
                _sb.AppendLine($"  {shaderName}: NOT SUPPORTED on {SystemInfo.graphicsDeviceType}");
            }
            else
            {
                _sb.AppendLine($"  {shaderName}: OK");
            }
        }

        /// <summary>
        /// Create a scrollable overlay panel anchored to the right side of the screen.
        /// Uses a semi-transparent background for readability over any content.
        /// </summary>
        private void CreateOverlayUI()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = GetComponent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;

            // Panel background – right side of screen, full height
            _panel = new GameObject("RenderDebugPanel");
            _panel.transform.SetParent(parent, false);
            var panelImage = _panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.8f);
            var panelRect = _panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1, 0);
            panelRect.anchorMax = new Vector2(1, 1);
            panelRect.pivot = new Vector2(1, 0.5f);
            panelRect.anchoredPosition = new Vector2(-5, 0);
            panelRect.sizeDelta = new Vector2(420, 0);

            // Debug text
            var textGo = new GameObject("DebugText");
            textGo.transform.SetParent(_panel.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 8);
            textRect.offsetMax = new Vector2(-8, -8);

            _debugText = textGo.AddComponent<Text>();
            _debugText.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _debugText.fontSize = FONT_SIZE;
            _debugText.color = new Color(0f, 1f, 0.4f); // Green terminal look
            _debugText.alignment = TextAnchor.UpperLeft;
            _debugText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _debugText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        /// <summary>Whether the debug overlay is currently visible.</summary>
        public bool IsVisible => _isVisible;
    }
}
