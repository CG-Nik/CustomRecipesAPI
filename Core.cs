using Alta;
using Alta.Blacksmithing;
using Alta.Carpentry;
using Alta.Chunks;
using Alta.Inventory;
using Alta.Networking;
using Alta.Pages;
using HarmonyLib;
using MelonLoader;
using Mono.CSharp;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using static SpawnHelper;

[assembly: MelonInfo(typeof(CustomRecipesAPI.Core), "CustomRecipesAPI", "1.0.0", "CGNik", null)]
[assembly: MelonGame("Alta", "A Township Tale")]

namespace CustomRecipesAPI
{
    public class SpawnPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(SpawnHelper),
                "Spawn",
                new Type[]
                {
                    typeof(NetworkPrefab),
                    typeof(SpawnData),
                    typeof(Chunk),
                    typeof(Vector3),
                    typeof(Quaternion),
                    typeof(SpawnHelper.SpawningCallback)
                }
            );
        }

        internal static bool Prefix(NetworkPrefab prefab, SpawnData spawnData, Chunk chunk, ref Vector3 position, ref Quaternion rotation, SpawningCallback preChunking)
        {
            StackTrace stackTrace = new StackTrace();
            StackFrame[] stackFrames = stackTrace.GetFrames();
            MethodBase method = stackFrames[2].GetMethod();
            if (method == null) { return true; }
            Type stateMachineType = method.DeclaringType;
            if (stateMachineType == null) { return true; }
            Type originalType = stateMachineType.DeclaringType;
            if (originalType == null) { return true; }
            if (originalType.Name != "Smelter") { return true; }
            if (stateMachineType.Name.Substring(0, 13) != "<TrySmelt>d__") { return true; }

            if (Core.smelterSpawnPositionOffsets.ContainsKey(prefab.Hash))
            {
                Vector3 p = Core.smelterSpawnPositionOffsets[prefab.Hash];
                float a = -rotation.eulerAngles.y * Mathf.Deg2Rad;
                position += new Vector3(p.x * Mathf.Cos(a) - p.z * Mathf.Sin(a), p.y, p.z * Mathf.Cos(a) + p.x * Mathf.Sin(a));
            }

            if (Core.smelterSpawnRotationOffsets.ContainsKey(prefab.Hash))
            {
                rotation.eulerAngles += Core.smelterSpawnRotationOffsets[prefab.Hash];
            }

            return true;
        }
    }

    public class Core : MelonMod
    {
        public static event Action PreSetUpRecipes = () => { };
        public static event Action SetUpRecipes = () => { };
        public static event Action PostSetUpRecipes = () => { };
        public static event Action PostPatches = () => { };

        // Adding an Item to this will add it to the filter that the Standard Mould Press uses for the weapon to be turned into a Mould
        // This allows you to use said Item to make Moulds with the Standard Mould Press
        public static List<Item> itemsToAddToStandardMouldPress = [];
        // Same thing, but with the Hebios Mould Press
        public static List<Item> itemsToAddToHebiosMouldPress = [];
        // Same thing, but with the Smelter's input instead of making Moulds with a Mould Press
        public static List<Item> itemsToAddToSmelter = [];
        // Adding an Item's Prefab's Hash to this will cause the Smelter to spawn the Item with that Vector3 as a position offset when creating said Item with a Mould
        // The purpose of this is to prevent certain items from getting stuck in the Smelter
        public static Dictionary<uint, Vector3> smelterSpawnPositionOffsets = [];
        // Same thing, but with rotation instead of position
        public static Dictionary<uint, Vector3> smelterSpawnRotationOffsets = [];
        // Used for FixMouldDefinitionInspectorValues, gets set here to prevent having to get it every time FixMouldDefinitionInspectorValues is called
        private static MouldDefinition mouldDefinition_axeHeadCurveMould;
        // Used to add SmeltingRecipes to the SmelterUnlockManager, which is the actually important "registering" that it does

        public static Dictionary<int, Item> VanillaOreAndIngotItems = new Dictionary<int, Item> {
            {42614, Item.All.Where(item => item.Hash == 42614u).First()},
            {42566, Item.All.Where(item => item.Hash == 42566u).First()},
            {5732, Item.All.Where(item => item.Hash == 5732u).First()},
            {5698, Item.All.Where(item => item.Hash == 5698u).First()},
            {4758, Item.All.Where(item => item.Hash == 4758u).First()},
            {5802, Item.All.Where(item => item.Hash == 5802u).First()},
            {7204, Item.All.Where(item => item.Hash == 7204u).First()},
            {17090, Item.All.Where(item => item.Hash == 17090u).First()},
            {57718, Item.All.Where(item => item.Hash == 57718u).First()},
            {60398, Item.All.Where(item => item.Hash == 60398u).First()},
            {24084, Item.All.Where(item => item.Hash == 24084u).First()},
            {32224, Item.All.Where(item => item.Hash == 32224u).First()},
            {16422, Item.All.Where(item => item.Hash == 16422u).First()},
            {30996, Item.All.Where(item => item.Hash == 30996u).First()},
            {24016, Item.All.Where(item => item.Hash == 24016u).First()}
        };
        public enum VanillaOreAndIngotIndexers
        {
            CopperOre = 42614,
            IronOre = 42566,
            GoldOre = 5732,
            SilverOre = 5698,
            MythrilOre = 4758,
            CopperIngot = 5802,
            IronIngot = 7204,
            GoldIngot = 17090,
            SilverIngot = 57718,
            MythrilIngot = 60398,
            CarsiIngot = 24084,
            EvinonSteelIngot = 32224,
            OrchiIngot = 16422,
            RedIronIngot = 30996,
            WhiteGoldIngot = 24016
        }

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Initialized.");
        }

        public override void OnLateInitializeMelon()
        {
            mouldDefinition_axeHeadCurveMould = MouldDefinition.All.Where(mould => mould.Hash == 22952u).First();

            PreSetUpRecipes.Invoke();

            SetUpRecipes.Invoke();

            PostSetUpRecipes.Invoke();

            GameObject mouldPressStandard = (GameObject)Resources.Load("network prefabs/crafting/mould press prefabs/Mould Press Standard");
            List<NetworkEntity> embeddedEntities_mouldPressStandard = (List<NetworkEntity>)typeof(NetworkEntityParent).GetField("embeddedEntities", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mouldPressStandard.GetComponent<NetworkPrefab>());
            NetworkEntity networkEntity_mouldPressStandard = embeddedEntities_mouldPressStandard.Where(entity => entity.Hash == 36080u).First();
            PickupDock pickupDock_mouldPressStandard = networkEntity_mouldPressStandard.gameObject.GetComponent<PickupDock>();
            pickupDock_mouldPressStandard.Settings.IncludedItems.AddUnique(itemsToAddToStandardMouldPress);

            GameObject mouldPressHebios = (GameObject)Resources.Load("network prefabs/crafting/mould press prefabs/Mould Press Hebios");
            List<NetworkEntity> embeddedEntities_mouldPressHebios = (List<NetworkEntity>)typeof(NetworkEntityParent).GetField("embeddedEntities", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mouldPressHebios.GetComponent<NetworkPrefab>());
            NetworkEntity networkEntity_mouldPressHebios = embeddedEntities_mouldPressHebios.Where(entity => entity.Hash == 36080u).First();
            PickupDock pickupDock_mouldPressHebios = networkEntity_mouldPressHebios.gameObject.GetComponent<PickupDock>();
            pickupDock_mouldPressHebios.Settings.IncludedItems.AddUnique(itemsToAddToHebiosMouldPress);

            GameObject smelter = (GameObject)Resources.Load("network prefabs/environment/blacksmithing/Smelter");
            List<NetworkEntity> embeddedEntities_Smelter = (List<NetworkEntity>)typeof(NetworkEntityParent).GetField("embeddedEntities", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(smelter.GetComponent<NetworkPrefab>());
            NetworkEntity networkEntity_oreDockB_Smelter = embeddedEntities_Smelter.Where(entity => entity.Hash == 42990u).First();
            PickupDock pickupDock_oreDockB_Smelter = networkEntity_oreDockB_Smelter.gameObject.GetComponent<PickupDock>();
            pickupDock_oreDockB_Smelter.Settings.IncludedItems.AddUnique(itemsToAddToSmelter);

            HarmonyInstance.Patch(AccessTools.Method(
                typeof(SpawnHelper),
                "Spawn",
                new Type[]
                {
                    typeof(NetworkPrefab),
                    typeof(SpawnData),
                    typeof(Chunk),
                    typeof(Vector3),
                    typeof(Quaternion),
                    typeof(SpawnHelper.SpawningCallback)
                }
            ), prefix: new HarmonyMethod(typeof(SpawnPatch), nameof(SpawnPatch.Prefix)));

            PostPatches.Invoke();
        }

        public static void AddMouldItemComponent(Item item, MouldDefinition mouldDefinition)
        {
            MouldItemComponent mouldItemComponent = new MouldItemComponent();
            mouldItemComponent.name = item.name + "Mould Item Component";
            typeof(MouldItemComponent).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mouldItemComponent, mouldDefinition);
            List<ItemComponent> components = (List<ItemComponent>)typeof(Item).GetField("components", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(item);
            components.Add(mouldItemComponent);
        }

        public static void FixMouldDefinitionInspectorValues(Item item, MouldDefinition mouldDefinition)
        {
            typeof(MouldDefinition).GetField("allowedMaterials", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mouldDefinition,
                typeof(MouldDefinition).GetField("allowedMaterials", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mouldDefinition_axeHeadCurveMould)
            );

            typeof(MouldDefinition).GetField("product", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mouldDefinition,
                item
            );
        }

        public static void RegisterMouldDefinition(MouldDefinition mouldDefinition)
        {
            MouldDefinition.CheckItems();
            Dictionary<uint, MouldDefinition> items = (Dictionary<uint, MouldDefinition>)typeof(HashedGeneralValue<MouldDefinition>).GetField("items", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            items.Add(mouldDefinition.Hash, mouldDefinition);
        }

        public static void AddSmelterOffsets(Item item, Vector3? positionOffset, Vector3? rotationOffset)
        {
            if (positionOffset != null)
            {
                smelterSpawnPositionOffsets[item.Prefab.Hash] = (Vector3)positionOffset;
            }
            else
            {
                smelterSpawnPositionOffsets.Remove(item.Prefab.Hash);
            }

            if (rotationOffset != null)
            {
                smelterSpawnRotationOffsets[item.Prefab.Hash] = (Vector3)rotationOffset;
            }
            else
            {
                smelterSpawnRotationOffsets.Remove(item.Prefab.Hash);
            }
        }

        public static void SetUpMould(Item item, MouldDefinition mouldDefinition, bool addMouldItemComponent = false, bool addToStandardPress = false, bool addToHebiosPress = false, Vector3? positionOffset = null, Vector3? rotationOffset = null)
        {
            if (addMouldItemComponent)
            {
                AddMouldItemComponent(item, mouldDefinition);
            }

            FixMouldDefinitionInspectorValues(item, mouldDefinition);

            RegisterMouldDefinition(mouldDefinition);

            AddSmelterOffsets(item, positionOffset, rotationOffset);

            if (addToStandardPress)
            {
                itemsToAddToStandardMouldPress.Add(item);
            }

            if (addToHebiosPress)
            {
                itemsToAddToHebiosMouldPress.Add(item);
            }
        }

        public static void FixSmeltingRecipeInspectorValues(SmeltingRecipe smeltingRecipe, Item[] inputs, Item[] outputs)
        {
            ItemCount[] originalInputs = (ItemCount[])typeof(SmeltingRecipe).GetField("input", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(smeltingRecipe);
            for (int i = 0; i < originalInputs.Length; i++)
            {
                typeof(ItemCount).GetField("item", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(originalInputs[i], inputs[i]);
            }
            typeof(SmeltingRecipe).GetField("input", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(smeltingRecipe,
                originalInputs
            );

            ItemCount[] originalOutputs = (ItemCount[])typeof(SmeltingRecipe).GetField("output", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(smeltingRecipe);
            for (int i = 0; i < originalOutputs.Length; i++)
            {
                typeof(ItemCount).GetField("item", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(originalOutputs[i], outputs[i]);
            }
            typeof(SmeltingRecipe).GetField("output", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(smeltingRecipe,
                originalOutputs
            );
        }

        public static void RegisterSmeltingRecipe(SmeltingRecipe smeltingRecipe)
        {
            SmeltingRecipe.CheckItems();
            Dictionary<uint, SmeltingRecipe> items = (Dictionary<uint, SmeltingRecipe>)typeof(HashedGeneralValue<SmeltingRecipe>).GetField("items", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            items.Add(smeltingRecipe.Hash, smeltingRecipe);
        }

        public static void AddSmeltingRecipeToSmelterUpgrades(SmeltingRecipe smeltingRecipe, SmelterUpgrades smelterUpgrades = null, bool addToSimpleServerDefaultUpgrades = true)
        {
            if (addToSimpleServerDefaultUpgrades)
            {
                SmelterUpgrades smelterUpgrades_simpleServerDefaultUpgrades = SmelterUpgrades.All.Where(upgrade => upgrade.Hash == 5674u).First();
                List<SmeltingRecipe> originalSmeltingRecipes = ((SmeltingRecipe[])typeof(SmelterUpgrades).GetField("recipes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(smelterUpgrades_simpleServerDefaultUpgrades)).ToList();
                originalSmeltingRecipes.Add(smeltingRecipe);
                typeof(SmelterUpgrades).GetField("recipes", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(smelterUpgrades_simpleServerDefaultUpgrades, originalSmeltingRecipes.ToArray());
            }

            if (smelterUpgrades != null)
            {
                List<SmeltingRecipe> originalSmeltingRecipes = ((SmeltingRecipe[])typeof(SmelterUpgrades).GetField("recipes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(smelterUpgrades)).ToList();
                originalSmeltingRecipes.Add(smeltingRecipe);
                typeof(SmelterUpgrades).GetField("recipes", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(smelterUpgrades, originalSmeltingRecipes.ToArray());
            }
        }

        public static void SetUpSmeltingRecipe(SmeltingRecipe smeltingRecipe, Item[] inputs, Item[] outputs, SmelterUpgrades smelterUpgrades = null, bool addToSimpleServerDefaultUpgrades = true)
        {
            FixSmeltingRecipeInspectorValues(smeltingRecipe, inputs, outputs);
            RegisterSmeltingRecipe(smeltingRecipe);
            AddSmeltingRecipeToSmelterUpgrades(smeltingRecipe, smelterUpgrades, addToSimpleServerDefaultUpgrades);
        }

        public static ItemCount CreateItemCount(Item item, int count)
        {
            ItemCount itemCount = new ItemCount();
            typeof(ItemCount).GetField("item", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(itemCount, item);
            typeof(ItemCount).GetField("count", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(itemCount, count);
            return itemCount;
        }

        public static SmeltingRecipe CreateSmeltingRecipe(string name, int hash, float duration, ItemCount[] inputs, ItemCount[] outputs)
        {
            SmeltingRecipe smeltingRecipe = GameObject.Instantiate(SmeltingRecipe.All.Where(recipe => recipe.Hash == 31642u).First());
            smeltingRecipe.name = name;
            typeof(HashedGeneralValue<SmeltingRecipe>).GetField("hash", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(smeltingRecipe, hash);
            typeof(SmeltingRecipe).GetField("duration", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(smeltingRecipe, duration);
            typeof(SmeltingRecipe).GetField("input", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(smeltingRecipe, inputs);
            typeof(SmeltingRecipe).GetField("output", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(smeltingRecipe, outputs);
            return smeltingRecipe;
        }

        public static SmeltingRecipe SetUpSmeltingRecipeThroughCode(string name, int hash, float duration, ItemCount[] inputs, ItemCount[] outputs, SmelterUpgrades smelterUpgrades = null, bool addToSimpleServerDefaultUpgrades = true)
        {
            SmeltingRecipe smeltingRecipe = CreateSmeltingRecipe(name, hash, duration, inputs, outputs);
            RegisterSmeltingRecipe(smeltingRecipe);
            AddSmeltingRecipeToSmelterUpgrades(smeltingRecipe, smelterUpgrades, addToSimpleServerDefaultUpgrades);
            return smeltingRecipe;
        }

        public static void FixChiselDefinitionInspectorValues(ChiselDefinition chiselDefinition, Glyph glyph)
        {
            chiselDefinition.recipeGlyph = glyph;
        }

        public static void RegisterChiselDefinition(ChiselDefinition chiselDefinition)
        {
            ChiselDefinition.CheckItems();
            Dictionary<uint, ChiselDefinition> items = (Dictionary<uint, ChiselDefinition>)typeof(HashedGeneralValue<ChiselDefinition>).GetField("items", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            items.Add(chiselDefinition.Hash, chiselDefinition);
        }

        public static void AddChiselDefinitionToChiselingCollection(ChiselDefinition chiselDefinition)
        {
            PageCollection chiselingCollection = PageCollection.All.Where(coll => coll.Hash == 716u).First();
            UnityEngine.Object[] pageBasesArray = (UnityEngine.Object[])typeof(PageCollection).GetField("pageBases", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(chiselingCollection);
            List<UnityEngine.Object> pageBasesList = pageBasesArray.ToList();
            pageBasesList.Add(chiselDefinition);
            typeof(PageCollection).GetField("pageBases", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(chiselingCollection, pageBasesList.ToArray());
        }

        public static PrefabSpawnPosition CreatePrefabSpawnPosition(uint prefabHash, Vector3 position, Quaternion rotation)
        {
            PrefabSpawnPosition prefabSpawnPosition = new PrefabSpawnPosition();
            typeof(PrefabSpawnPosition).GetField("prefabHash", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(prefabSpawnPosition, prefabHash);
            typeof(PrefabSpawnPosition).GetField("position", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(prefabSpawnPosition, position);
            typeof(PrefabSpawnPosition).GetField("rotation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(prefabSpawnPosition, rotation);
            return prefabSpawnPosition;
        }

        public static ChiselDefinition CreateChiselDefinition(string name, int hash, Glyph glyph, ChiselBlockSize size, List<int> chiselOrder, PrefabSpawnPosition[] prefabs)
        {
            ChiselDefinition chiselDefinition = GameObject.Instantiate(ChiselDefinition.All.Where(def => def.Hash == 46222u).First());
            chiselDefinition.name = name;
            typeof(HashedGeneralValue<ChiselDefinition>).GetField("hash", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(chiselDefinition, hash);
            chiselDefinition.recipeGlyph = glyph;
            typeof(ChiselDefinition).GetField("size", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(chiselDefinition, size);
            typeof(ChiselDefinition).GetField("chiselOrder", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(chiselDefinition, chiselOrder);
            typeof(ChiselDefinition).GetField("prefabs", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(chiselDefinition, prefabs);
            return chiselDefinition;
        }

        public static void SetUpChiselDefinition(ChiselDefinition chiselDefinition, Glyph glyph)
        {
            FixChiselDefinitionInspectorValues(chiselDefinition, glyph);
            RegisterChiselDefinition(chiselDefinition);
            AddChiselDefinitionToChiselingCollection(chiselDefinition);
        }

        public static ChiselDefinition SetUpChiselDefinitionThroughCode(string name, int hash, Glyph glyph, ChiselBlockSize size, List<int> chiselOrder, PrefabSpawnPosition[] prefabs)
        {
            ChiselDefinition chiselDefinition = CreateChiselDefinition(name, hash, glyph, size, chiselOrder, prefabs);
            RegisterChiselDefinition(chiselDefinition);
            AddChiselDefinitionToChiselingCollection(chiselDefinition);
            return chiselDefinition;
        }
    }
}