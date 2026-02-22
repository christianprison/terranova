using UnityEngine;

namespace Terranova.Terrain
{
    /// <summary>
    /// v0.5.1: Centralized shader cache and material factory.
    ///
    /// Solves the pink material problem: "Universal Render Pipeline/Lit" gets
    /// stripped from code-only builds because no material assets reference it.
    /// Our custom Terranova/* shaders are always compiled since they live in Assets/.
    ///
    /// Provides factory methods for every material type:
    ///   - PropLit: rocks, wood, berries, generic resources (Terranova/PropLit)
    ///   - Foliage: tree canopies, bushes with wind + alpha cutout (Terranova/WindFoliage)
    ///   - Water: enhanced water with waves, fresnel, ripples (Terranova/WaterSurface)
    ///   - Fog: fog of war overlay with gradient edges + noise (Terranova/FogOfWar)
    ///   - Path: trampled path decals with soft edges (Terranova/TrampledPath)
    /// </summary>
    public static class TerrainShaderLibrary
    {
        // ─── Cached shaders ──────────────────────────────────────

        private static Shader _propLit;
        private static Shader _windFoliage;
        private static Shader _waterSurface;
        private static Shader _fogOfWar;
        private static Shader _trampledPath;
        private static Shader _vertexColorOpaque;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _propLit = null;
            _windFoliage = null;
            _waterSurface = null;
            _fogOfWar = null;
            _trampledPath = null;
            _vertexColorOpaque = null;
            _replacementFoliage = null;
            _replacementRock = null;
            _replacementWood = null;
            _replacementSkin = null;
            _replacementParticle = null;
            _replacementDefault = null;
        }

        // ─── Shader accessors ────────────────────────────────────

        /// <summary>Get the PropLit shader (opaque lit props).</summary>
        public static Shader PropLit => _propLit ??= FindShader("Terranova/PropLit");

        /// <summary>Get the WindFoliage shader (alpha cutout + wind).</summary>
        public static Shader WindFoliage => _windFoliage ??= FindShader("Terranova/WindFoliage");

        /// <summary>Get the WaterSurface shader (transparent + waves).</summary>
        public static Shader WaterSurface => _waterSurface ??= FindShader("Terranova/WaterSurface");

        /// <summary>Get the FogOfWar shader (transparent + noise).</summary>
        public static Shader FogOfWarShader => _fogOfWar ??= FindTransparentShader("Terranova/FogOfWar");

        /// <summary>Get the TrampledPath shader (transparent decal).</summary>
        public static Shader TrampledPath => _trampledPath ??= FindShader("Terranova/TrampledPath");

        /// <summary>Get the VertexColorOpaque shader (terrain fallback).</summary>
        public static Shader VertexColorOpaque => _vertexColorOpaque ??= FindShader("Terranova/VertexColorOpaque");

        private static Shader FindShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null)
            {
                Debug.LogWarning($"[TerrainShaderLibrary] Shader '{name}' not found, using opaque fallback.");
                shader = Shader.Find("Terranova/VertexColorOpaque")
                      ?? Shader.Find("Sprites/Default");
            }
            return shader;
        }

        private static Shader FindTransparentShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null)
            {
                Debug.LogWarning($"[TerrainShaderLibrary] Shader '{name}' not found, using transparent fallback.");
                shader = Shader.Find("Terranova/VertexColorTransparent")
                      ?? Shader.Find("Sprites/Default");
            }
            return shader;
        }

        // ─── Material factory: Props ─────────────────────────────

        /// <summary>Create an opaque prop material (rocks, wood, generic).</summary>
        public static Material CreatePropMaterial(string name, Color color,
            float smoothness = 0.2f, float metallic = 0f)
        {
            var mat = new Material(PropLit);
            mat.name = name;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            mat.SetColor("_EmissionColor", Color.black);
            return mat;
        }

        /// <summary>Create a prop material with emission (glowing berries).</summary>
        public static Material CreateEmissivePropMaterial(string name, Color color,
            Color emissionColor, float smoothness = 0.3f)
        {
            var mat = CreatePropMaterial(name, color, smoothness, 0f);
            mat.SetColor("_EmissionColor", emissionColor);
            return mat;
        }

        // ─── Material factory: Foliage ───────────────────────────

        /// <summary>Create a foliage material with wind animation.</summary>
        public static Material CreateFoliageMaterial(string name, Color color,
            float cutoff = 0.35f, float windStrength = 0.08f, float windSpeed = 1.5f)
        {
            var mat = new Material(WindFoliage);
            mat.name = name;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Cutoff", cutoff);
            mat.SetFloat("_WindStrength", windStrength);
            mat.SetFloat("_WindSpeed", windSpeed);
            return mat;
        }

        // ─── Material factory: Rock variants ─────────────────────

        /// <summary>Rock: gray, rough, low smoothness.</summary>
        public static Material CreateRockMaterial(string name, Color color)
        {
            return CreatePropMaterial(name, color, 0.1f, 0f);
        }

        /// <summary>Granite: slight metallic sparkle.</summary>
        public static Material CreateGraniteMaterial(string name, Color color)
        {
            return CreatePropMaterial(name, color, 0.15f, 0.05f);
        }

        // ─── Material factory: Wood ──────────────────────────────

        /// <summary>Wood: brown, low smoothness, no metallic.</summary>
        public static Material CreateWoodMaterial(string name, Color color)
        {
            return CreatePropMaterial(name, color, 0.15f, 0f);
        }

        // ─── Material factory: Water ─────────────────────────────

        /// <summary>Create the enhanced water material.</summary>
        public static Material CreateWaterMaterial()
        {
            var mat = new Material(WaterSurface);
            mat.name = "Water_Mat";
            mat.SetColor("_ShallowColor", new Color(0.30f, 0.65f, 0.85f, 0.55f));
            mat.SetColor("_DeepColor", new Color(0.08f, 0.25f, 0.55f, 0.85f));
            mat.SetFloat("_WaveSpeed", 0.8f);
            mat.SetFloat("_WaveHeight", 0.04f);
            mat.SetFloat("_RippleScale", 6f);
            mat.SetFloat("_RippleSpeed", 1.2f);
            mat.SetFloat("_FresnelPower", 3f);
            mat.SetColor("_ReflectColor", new Color(0.7f, 0.85f, 1f, 1f));
            return mat;
        }

        // ─── Material factory: Fog of War ────────────────────────

        /// <summary>Create the fog of war overlay material.</summary>
        public static Material CreateFogMaterial()
        {
            var shader = FogOfWarShader;
            var mat = new Material(shader);
            mat.name = "FogOfWar_Mat";
            mat.renderQueue = 3100;

            if (shader.name == "Terranova/FogOfWar")
            {
                mat.SetColor("_FogColor", new Color(0.03f, 0.04f, 0.03f, 0.85f));
                mat.SetFloat("_NoiseScale", 8f);
                mat.SetFloat("_NoiseSpeed", 0.3f);
                mat.SetFloat("_NoiseAmount", 0.15f);
                mat.SetFloat("_EdgeSoftness", 0.2f);
            }
            else
            {
                // Fallback: use vertex color alpha for transparency
                mat.SetColor("_BaseColor", new Color(0.03f, 0.04f, 0.03f, 0.85f));
            }
            return mat;
        }

        // ─── Material factory: Trampled Paths ────────────────────

        /// <summary>Create a light path material (10+ walks).</summary>
        public static Material CreateLightPathMaterial()
        {
            var mat = new Material(TrampledPath);
            mat.name = "LightPath_Mat";
            mat.SetColor("_BaseColor", new Color(0.50f, 0.40f, 0.28f, 0.35f));
            mat.SetFloat("_EdgeSoftness", 0.35f);
            mat.SetFloat("_WearAmount", 0.3f);
            return mat;
        }

        /// <summary>Create a clear path material (30+ walks).</summary>
        public static Material CreateClearPathMaterial()
        {
            var mat = new Material(TrampledPath);
            mat.name = "ClearPath_Mat";
            mat.SetColor("_BaseColor", new Color(0.55f, 0.42f, 0.25f, 0.6f));
            mat.SetFloat("_EdgeSoftness", 0.25f);
            mat.SetFloat("_WearAmount", 0.7f);
            return mat;
        }

        // ─── Material factory: Special ───────────────────────────

        /// <summary>Campfire stone ring material.</summary>
        public static Material CreateCampfireStoneMaterial()
        {
            return CreateRockMaterial("CampfireStone_Mat", new Color(0.45f, 0.43f, 0.40f));
        }

        /// <summary>Campfire flame material (emissive orange).</summary>
        public static Material CreateFlameMaterial()
        {
            return CreateEmissivePropMaterial("Flame_Mat",
                new Color(1f, 0.55f, 0.1f),
                new Color(0.8f, 0.3f, 0.0f));
        }

        /// <summary>Campfire inner glow material (bright emissive yellow).</summary>
        public static Material CreateGlowMaterial()
        {
            return CreateEmissivePropMaterial("Glow_Mat",
                new Color(1f, 0.85f, 0.3f),
                new Color(1f, 0.6f, 0.1f));
        }

        /// <summary>Campfire scorch ground material (dark burnt ring).</summary>
        public static Material CreateScorchMaterial()
        {
            var mat = new Material(TrampledPath);
            mat.name = "Scorch_Mat";
            mat.SetColor("_BaseColor", new Color(0.15f, 0.12f, 0.08f, 0.5f));
            mat.SetFloat("_EdgeSoftness", 0.4f);
            mat.SetFloat("_WearAmount", 0.8f);
            return mat;
        }

        /// <summary>Tree stump material.</summary>
        public static Material CreateStumpMaterial()
        {
            return CreateWoodMaterial("Stump_Mat", new Color(0.40f, 0.28f, 0.15f));
        }

        /// <summary>Particle material for campfire effects (flame, ember, smoke).
        /// Uses VertexColorTransparent so ParticleSystem per-particle colors work.
        /// Prevents pink when URP built-in particle shaders are stripped.</summary>
        public static Material CreateParticleMaterial()
        {
            var shader = Shader.Find("Terranova/VertexColorTransparent");
            if (shader == null)
            {
                Debug.LogWarning("[TerrainShaderLibrary] VertexColorTransparent not found for particles.");
                shader = Shader.Find("Sprites/Default");
            }
            var mat = new Material(shader);
            mat.name = "Particle_Mat";
            mat.SetColor("_BaseColor", Color.white);
            return mat;
        }

        /// <summary>Cave interior darkening material (used for shelter depth illusion).</summary>
        public static Material CreateCaveInteriorMaterial()
        {
            var mat = new Material(PropLit);
            mat.name = "CaveInterior_Mat";
            mat.SetColor("_BaseColor", new Color(0.12f, 0.10f, 0.08f));
            mat.SetFloat("_Smoothness", 0.05f);
            return mat;
        }

        // ─── BiRP → URP material replacement ───────────────────────

        // Cached replacement materials (avoid creating duplicates)
        private static Material _replacementFoliage;
        private static Material _replacementRock;
        private static Material _replacementWood;
        private static Material _replacementSkin;
        private static Material _replacementParticle;
        private static Material _replacementDefault;

        /// <summary>
        /// Replace all BiRP (Built-in Render Pipeline) materials on an instantiated
        /// EXPLORER prefab with URP-compatible Terranova materials.
        ///
        /// The EXPLORER - Stone Age asset pack uses BiRP Standard/Surface shaders
        /// which render as dark/invisible under URP. This method detects BiRP materials
        /// by shader name and replaces them with the closest Terranova equivalent,
        /// preserving the original base color where possible.
        /// </summary>
        public static void ReplaceWithURPMaterials(GameObject instance)
        {
            if (instance == null) return;

            // Process MeshRenderers
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                ReplaceMaterials(renderer);

            // Process SkinnedMeshRenderers (avatars)
            foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                ReplaceMaterials(renderer);

            // Process ParticleSystemRenderers
            foreach (var psr in instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (psr.sharedMaterial != null && IsBiRPShader(psr.sharedMaterial.shader))
                    psr.sharedMaterial = GetParticleReplacement();
            }
        }

        private static void ReplaceMaterials(Renderer renderer)
        {
            var mats = renderer.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                if (!IsBiRPShader(mats[i].shader)) continue;

                Color originalColor = GetOriginalColor(mats[i]);
                string matName = mats[i].name.ToLowerInvariant();
                string shaderName = mats[i].shader.name.ToLowerInvariant();

                // Determine replacement type based on material/shader name
                if (shaderName.Contains("wind") || shaderName.Contains("animated vert") ||
                    matName.Contains("tree") || matName.Contains("pine") ||
                    matName.Contains("bush") || matName.Contains("fern") ||
                    matName.Contains("flower") || matName.Contains("leaf") ||
                    matName.Contains("plant") || matName.Contains("agricultural") ||
                    matName.Contains("vegetation"))
                {
                    mats[i] = GetFoliageReplacement(originalColor);
                }
                else if (matName.Contains("avatar") || matName.Contains("prehistoric") ||
                         matName.Contains("skin") || matName.Contains("character"))
                {
                    mats[i] = GetSkinReplacement();
                }
                else if (matName.Contains("rock") || matName.Contains("cliff") ||
                         matName.Contains("canyon") || matName.Contains("cave") ||
                         matName.Contains("stone") || matName.Contains("slab") ||
                         matName.Contains("pavement") || matName.Contains("formation"))
                {
                    mats[i] = GetRockReplacement(originalColor);
                }
                else if (matName.Contains("wood") || matName.Contains("log") ||
                         matName.Contains("trunk") || matName.Contains("stump") ||
                         matName.Contains("twig") || matName.Contains("bone") ||
                         matName.Contains("carcass") || matName.Contains("tent") ||
                         matName.Contains("hut") || matName.Contains("leather"))
                {
                    mats[i] = GetWoodReplacement(originalColor);
                }
                else
                {
                    // Generic fallback: use PropLit with original color
                    mats[i] = GetDefaultReplacement(originalColor);
                }
                changed = true;
            }

            if (changed)
                renderer.sharedMaterials = mats;
        }

        private static bool IsBiRPShader(Shader shader)
        {
            if (shader == null) return false;
            string name = shader.name;
            // URP and Terranova shaders are fine
            if (name.StartsWith("Terranova/") || name.StartsWith("Universal Render Pipeline/"))
                return false;
            // BiRP built-in shaders
            if (name == "Standard" || name == "Standard (Specular setup)" ||
                name.StartsWith("Legacy Shaders/") || name.StartsWith("Mobile/") ||
                name.StartsWith("Particles/") || name == "VertexLit" ||
                name.StartsWith("Nature/") || name.StartsWith("EXPLORER"))
                return true;
            // Hidden/error shaders also need replacement
            if (name.StartsWith("Hidden/"))
                return true;
            return false;
        }

        private static Color GetOriginalColor(Material mat)
        {
            // BiRP Standard uses _Color, URP uses _BaseColor
            if (mat.HasProperty("_Color"))
                return mat.GetColor("_Color");
            if (mat.HasProperty("_BaseColor"))
                return mat.GetColor("_BaseColor");
            return Color.gray;
        }

        private static Material GetFoliageReplacement(Color tint)
        {
            if (_replacementFoliage == null)
            {
                // Use a natural green tint for foliage
                _replacementFoliage = CreateFoliageMaterial("URP_Foliage", new Color(0.3f, 0.6f, 0.2f));
                _replacementFoliage.enableInstancing = true;
            }
            return _replacementFoliage;
        }

        private static Material GetRockReplacement(Color originalColor)
        {
            if (_replacementRock == null)
            {
                _replacementRock = CreateRockMaterial("URP_Rock", new Color(0.55f, 0.52f, 0.48f));
                _replacementRock.enableInstancing = true;
            }
            return _replacementRock;
        }

        private static Material GetWoodReplacement(Color originalColor)
        {
            if (_replacementWood == null)
            {
                _replacementWood = CreateWoodMaterial("URP_Wood", new Color(0.40f, 0.28f, 0.15f));
                _replacementWood.enableInstancing = true;
            }
            return _replacementWood;
        }

        private static Material GetSkinReplacement()
        {
            if (_replacementSkin == null)
            {
                _replacementSkin = CreatePropMaterial("URP_Skin", new Color(0.72f, 0.56f, 0.42f), 0.3f);
                _replacementSkin.enableInstancing = true;
            }
            return _replacementSkin;
        }

        private static Material GetParticleReplacement()
        {
            if (_replacementParticle == null)
                _replacementParticle = CreateParticleMaterial();
            return _replacementParticle;
        }

        private static Material GetDefaultReplacement(Color originalColor)
        {
            if (_replacementDefault == null)
            {
                _replacementDefault = CreatePropMaterial("URP_Default", Color.gray, 0.2f);
                _replacementDefault.enableInstancing = true;
            }
            return _replacementDefault;
        }
    }
}
