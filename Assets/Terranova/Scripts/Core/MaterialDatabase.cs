using System.Collections.Generic;

namespace Terranova.Core
{
    /// <summary>
    /// Central registry of all material definitions.
    /// MS4 Feature 2.1: Material Data Model.
    /// All materials from the design doc are defined here.
    /// </summary>
    public static class MaterialDatabase
    {
        private static Dictionary<string, MaterialDefinition> _materials;
        private static bool _initialized;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _materials = null;
            _initialized = false;
        }

        public static Dictionary<string, MaterialDefinition> All
        {
            get
            {
                if (!_initialized) Initialize();
                return _materials;
            }
        }

        public static MaterialDefinition Get(string id)
        {
            if (!_initialized) Initialize();
            return _materials.TryGetValue(id, out var mat) ? mat : null;
        }

        public static List<MaterialDefinition> GetByCategory(MaterialCategory category)
        {
            if (!_initialized) Initialize();
            var result = new List<MaterialDefinition>();
            foreach (var mat in _materials.Values)
                if (mat.Category == category)
                    result.Add(mat);
            return result;
        }

        private static void Initialize()
        {
            _initialized = true;
            _materials = new Dictionary<string, MaterialDefinition>();

            // === WOOD ===
            Register(new MaterialDefinition {
                Id = "deadwood", DisplayName = "Deadwood/Branches", Category = MaterialCategory.Wood,
                Properties = MaterialProperty.Hard, PreferredBiome = BiomeType.Forest,
                GatherDuration = 0f, GenericName = "Wood", DiscoveryRequired = null
            });
            Register(new MaterialDefinition {
                Id = "hardwood", DisplayName = "Hardwood (Oak)", Category = MaterialCategory.Wood,
                Properties = MaterialProperty.Hard, PreferredBiome = BiomeType.Forest,
                GatherDuration = 3f, RequiresTool = true, MinToolQuality = 4,
                GenericName = "Wood", DiscoveryRequired = "WoodTypes"
            });
            Register(new MaterialDefinition {
                Id = "softwood", DisplayName = "Softwood (Birch/Pine)", Category = MaterialCategory.Wood,
                Properties = MaterialProperty.Soft, PreferredBiome = BiomeType.Forest,
                GatherDuration = 2f, RequiresTool = true, MinToolQuality = 3,
                GenericName = "Wood", DiscoveryRequired = "WoodTypes"
            });
            Register(new MaterialDefinition {
                Id = "willow_shoots", DisplayName = "Willow Shoots", Category = MaterialCategory.Wood,
                Properties = MaterialProperty.Flexible, PreferredBiome = BiomeType.Coast,
                GatherDuration = 1f, GenericName = "Wood", DiscoveryRequired = "WoodTypes"
            });
            Register(new MaterialDefinition {
                Id = "birch_bark", DisplayName = "Birch Bark", Category = MaterialCategory.Wood,
                Properties = MaterialProperty.Flexible, PreferredBiome = BiomeType.Forest,
                GatherDuration = 1.5f, GenericName = "Wood", DiscoveryRequired = "WoodTypes"
            });
            Register(new MaterialDefinition {
                Id = "resin", DisplayName = "Resin", Category = MaterialCategory.Wood,
                Properties = MaterialProperty.Sticky, PreferredBiome = BiomeType.Forest,
                GatherDuration = 2f, GenericName = "Wood", DiscoveryRequired = "WoodTypes"
            });

            // === STONE ===
            Register(new MaterialDefinition {
                Id = "river_stone", DisplayName = "River Stone", Category = MaterialCategory.Stone,
                Properties = MaterialProperty.Hard, PreferredBiome = BiomeType.Coast,
                GatherDuration = 0f, GenericName = "Stone", DiscoveryRequired = null
            });
            Register(new MaterialDefinition {
                Id = "flint", DisplayName = "Flint", Category = MaterialCategory.Stone,
                Properties = MaterialProperty.Hard | MaterialProperty.Sharp,
                PreferredBiome = BiomeType.Mountains,
                GatherDuration = 0.5f, GenericName = "Stone", DiscoveryRequired = "RockKnowledge"
            });
            Register(new MaterialDefinition {
                Id = "sandstone", DisplayName = "Sandstone", Category = MaterialCategory.Stone,
                Properties = MaterialProperty.Soft, PreferredBiome = BiomeType.Coast,
                GatherDuration = 1f, GenericName = "Stone", DiscoveryRequired = "RockKnowledge"
            });
            Register(new MaterialDefinition {
                Id = "granite", DisplayName = "Granite", Category = MaterialCategory.Stone,
                Properties = MaterialProperty.Hard, PreferredBiome = BiomeType.Mountains,
                GatherDuration = 2f, RequiresTool = true, MinToolQuality = 3,
                GenericName = "Stone", DiscoveryRequired = "RockKnowledge"
            });
            Register(new MaterialDefinition {
                Id = "limestone", DisplayName = "Limestone", Category = MaterialCategory.Stone,
                Properties = MaterialProperty.Soft, PreferredBiome = BiomeType.Mountains,
                GatherDuration = 1.5f, GenericName = "Stone", DiscoveryRequired = "RockKnowledge"
            });

            // === PLANT ===
            Register(new MaterialDefinition {
                Id = "plant_fibers", DisplayName = "Plant Fibers", Category = MaterialCategory.Plant,
                Properties = MaterialProperty.Flexible, PreferredBiome = BiomeType.Forest,
                GatherDuration = 1f, GenericName = "Plants", DiscoveryRequired = null
            });
            Register(new MaterialDefinition {
                Id = "berries_safe", DisplayName = "Berries (Safe)", Category = MaterialCategory.Plant,
                Properties = MaterialProperty.Edible | MaterialProperty.Soft,
                PreferredBiome = BiomeType.Forest,
                GatherDuration = 0.5f, NutritionValue = 15, SpoilageRate = 4f,
                GenericName = "Berries", DiscoveryRequired = "EdiblePlants"
            });
            Register(new MaterialDefinition {
                Id = "berries_poison", DisplayName = "Berries (Poisonous)", Category = MaterialCategory.Plant,
                Properties = MaterialProperty.Edible | MaterialProperty.Poisonous | MaterialProperty.Soft,
                PreferredBiome = BiomeType.Forest,
                GatherDuration = 0.5f, NutritionValue = 5, SpoilageRate = 4f,
                GenericName = "Berries", DiscoveryRequired = "EdiblePlants"
            });
            Register(new MaterialDefinition {
                Id = "roots", DisplayName = "Roots", Category = MaterialCategory.Plant,
                Properties = MaterialProperty.Edible, PreferredBiome = BiomeType.Coast,
                GatherDuration = 3f, NutritionValue = 25, SpoilageRate = 8f,
                GenericName = "Food", DiscoveryRequired = null
            });
            Register(new MaterialDefinition {
                Id = "grasses_reeds", DisplayName = "Grasses/Reeds", Category = MaterialCategory.Plant,
                Properties = MaterialProperty.Flexible, PreferredBiome = BiomeType.Coast,
                GatherDuration = 0.5f, GenericName = "Plants", DiscoveryRequired = null
            });
            Register(new MaterialDefinition {
                Id = "honey", DisplayName = "Honey", Category = MaterialCategory.Plant,
                Properties = MaterialProperty.Edible | MaterialProperty.Sticky,
                PreferredBiome = BiomeType.Forest,
                GatherDuration = 4f, NutritionValue = 30, SpoilageRate = 0f,
                GenericName = "Food", DiscoveryRequired = null
            });

            // === ANIMAL ===
            Register(new MaterialDefinition {
                Id = "small_meat", DisplayName = "Small Meat", Category = MaterialCategory.Animal,
                Properties = MaterialProperty.Edible | MaterialProperty.Soft,
                PreferredBiome = BiomeType.Forest,
                GatherDuration = 2f, NutritionValue = 35, SpoilageRate = 4f,
                GenericName = "Meat", DiscoveryRequired = null
            });
            Register(new MaterialDefinition {
                Id = "large_meat", DisplayName = "Large Meat", Category = MaterialCategory.Animal,
                Properties = MaterialProperty.Edible | MaterialProperty.Soft,
                PreferredBiome = BiomeType.Forest,
                GatherDuration = 5f, NutritionValue = 60, SpoilageRate = 4f,
                RequiresTool = true, MinToolQuality = 3,
                GenericName = "Meat", DiscoveryRequired = null
            });
            Register(new MaterialDefinition {
                Id = "bone_marrow", DisplayName = "Bone Marrow", Category = MaterialCategory.Animal,
                Properties = MaterialProperty.Edible, PreferredBiome = BiomeType.Forest,
                GatherDuration = 3f, NutritionValue = 50, SpoilageRate = 6f,
                RequiresTool = true, MinToolQuality = 1,
                GenericName = "Food", DiscoveryRequired = null
            });
            Register(new MaterialDefinition {
                Id = "bones", DisplayName = "Bones", Category = MaterialCategory.Animal,
                Properties = MaterialProperty.Hard, PreferredBiome = BiomeType.Forest,
                GatherDuration = 1f, GenericName = "Bones", DiscoveryRequired = null
            });
            Register(new MaterialDefinition {
                Id = "sinew", DisplayName = "Sinew", Category = MaterialCategory.Animal,
                Properties = MaterialProperty.Flexible, PreferredBiome = BiomeType.Forest,
                GatherDuration = 2f, GenericName = "Animal Parts", DiscoveryRequired = null
            });
            Register(new MaterialDefinition {
                Id = "hide", DisplayName = "Hide", Category = MaterialCategory.Animal,
                Properties = MaterialProperty.Flexible, PreferredBiome = BiomeType.Forest,
                GatherDuration = 3f, RequiresTool = true, MinToolQuality = 3,
                GenericName = "Animal Parts", DiscoveryRequired = null
            });
            Register(new MaterialDefinition {
                Id = "fish", DisplayName = "Fish", Category = MaterialCategory.Animal,
                Properties = MaterialProperty.Edible | MaterialProperty.Soft,
                PreferredBiome = BiomeType.Coast,
                GatherDuration = 5f, NutritionValue = 30, SpoilageRate = 3f,
                GenericName = "Food", DiscoveryRequired = null
            });

            // === OTHER ===
            Register(new MaterialDefinition {
                Id = "water", DisplayName = "Water", Category = MaterialCategory.Other,
                Properties = MaterialProperty.None, PreferredBiome = BiomeType.Coast,
                GatherDuration = 0.5f, GenericName = "Water", DiscoveryRequired = null
            });
            Register(new MaterialDefinition {
                Id = "clay", DisplayName = "Clay", Category = MaterialCategory.Other,
                Properties = MaterialProperty.Soft, PreferredBiome = BiomeType.Coast,
                GatherDuration = 2f, GenericName = "Clay", DiscoveryRequired = null
            });
            Register(new MaterialDefinition {
                Id = "salt", DisplayName = "Salt", Category = MaterialCategory.Other,
                Properties = MaterialProperty.Hard, PreferredBiome = BiomeType.Coast,
                GatherDuration = 3f, GenericName = "Minerals", DiscoveryRequired = null
            });
            Register(new MaterialDefinition {
                Id = "ochre", DisplayName = "Ochre/Pigments", Category = MaterialCategory.Other,
                Properties = MaterialProperty.Soft, PreferredBiome = BiomeType.Mountains,
                GatherDuration = 2f, GenericName = "Minerals", DiscoveryRequired = null
            });

            // === FOOD SOURCES (insects, eggs, carrion) ===
            Register(new MaterialDefinition {
                Id = "insects", DisplayName = "Insects/Larvae", Category = MaterialCategory.Animal,
                Properties = MaterialProperty.Edible, PreferredBiome = BiomeType.Forest,
                GatherDuration = 1f, NutritionValue = 10, SpoilageRate = 2f,
                GenericName = "Food", DiscoveryRequired = null
            });
            Register(new MaterialDefinition {
                Id = "eggs", DisplayName = "Eggs", Category = MaterialCategory.Animal,
                Properties = MaterialProperty.Edible, PreferredBiome = BiomeType.Forest,
                GatherDuration = 1f, NutritionValue = 20, SpoilageRate = 6f,
                GenericName = "Food", DiscoveryRequired = null
            });
        }

        private static void Register(MaterialDefinition mat)
        {
            _materials[mat.Id] = mat;
        }
    }
}
