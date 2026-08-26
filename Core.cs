using Alta;
using Alta.Api.DataTransferModels.Extensions;
using Alta.Blacksmithing;
using Alta.Chunks;
using Alta.Inventory;
using Alta.Networking;
using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;
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
            if (stateMachineType.Name.Substring(0,13) != "<TrySmelt>d__") { return true; }

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

    public class InitializePatch
    {
        internal static void Postfix(NetworkPrefab __instance)
        {
            switch (__instance.Hash)
            {
                case 13362u: // This is the Standard Mould Press
                    List<NetworkEntity> embeddedEntities_StandardMouldPress = (List<NetworkEntity>)typeof(NetworkEntityParent).GetField("embeddedEntities", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);
                    NetworkEntity networkEntity_StandardMouldPress = embeddedEntities_StandardMouldPress.Where(entity => entity.Hash == 36080u).First();
                    PickupDock pickupDock_StandardMouldPress = networkEntity_StandardMouldPress.gameObject.GetComponent<PickupDock>();
                    pickupDock_StandardMouldPress.Settings.IncludedItems.AddUnique(Core.itemsToAddToStandardMouldPress);
                    break;
                case 2594u: // This is the Hebios Mould Press
                    List<NetworkEntity> embeddedEntities_HebiosMouldPress = (List<NetworkEntity>)typeof(NetworkEntityParent).GetField("embeddedEntities", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);
                    NetworkEntity networkEntity_inputDock_HebiosMouldPress = embeddedEntities_HebiosMouldPress.Where(entity => entity.Hash == 36080u).First();
                    PickupDock pickupDock_inputDock_HebiosMouldPress = networkEntity_inputDock_HebiosMouldPress.gameObject.GetComponent<PickupDock>();
                    pickupDock_inputDock_HebiosMouldPress.Settings.IncludedItems.AddUnique(Core.itemsToAddToHebiosMouldPress);
                    break;
                case 44646u: // This is the Smelter
                    List<NetworkEntity> embeddedEntities_Smelter = (List<NetworkEntity>)typeof(NetworkEntityParent).GetField("embeddedEntities", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);
                    NetworkEntity networkEntity_oreDockB_Smelter = embeddedEntities_Smelter.Where(entity => entity.Hash == 42990u).First();
                    PickupDock pickupDock_oreDockB_Smelter = networkEntity_oreDockB_Smelter.gameObject.GetComponent<PickupDock>();
                    pickupDock_oreDockB_Smelter.Settings.IncludedItems.AddUnique(Core.itemsToAddToSmelter);
                    break;
                default:
                    break;
            }
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

            HarmonyInstance.Patch(AccessTools.Method(typeof(NetworkPrefab), "Initialize"), postfix: new HarmonyMethod(typeof(InitializePatch), nameof(InitializePatch.Postfix)));
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

        public static void AddMouldItemComponent(Item item, MouldItemComponent mouldItemComponent)
        {
            List<ItemComponent> components = (List<ItemComponent>)typeof(Item).GetField("components", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(item);
            components.Add(mouldItemComponent);
            typeof(Item).GetField("components", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, components);
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

        public static void SetUpMould(uint itemHash, MouldDefinition mouldDefinition, MouldItemComponent mouldItemComponent = null, bool addToStandardPress = false, bool addToHebiosPress = false, Vector3? positionOffset = null, Vector3? rotationOffset = null)
        {
            Item item = Item.All.Where(item => item.Hash == itemHash).First();

            if (mouldItemComponent != null)
            {
                AddMouldItemComponent(item, mouldItemComponent);
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
    }
}