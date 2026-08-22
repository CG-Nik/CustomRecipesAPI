using Alta;
using Alta.Blacksmithing;
using Alta.Chunks;
using Alta.Inventory;
using Alta.Networking;
using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;
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
                default:
                    break;
            }
        }
    }
    public class Core : MelonMod
    {
        public static event Action PreSetUpMoulds = () => { };
        public static event Action SetUpMoulds = () => { };
        public static event Action PostSetUpMoulds = () => { };
        public static event Action PostPatches = () => { };

        // Adding an Item to this will add it to the filter that the Standard Mould Press uses for the weapon to be turned into a Mould
        // This allows you to use said Item to make Moulds with the Standard Mould Press
        public static List<Item> itemsToAddToStandardMouldPress = [];
        // Same thing, but with the Hebios Mould Press
        public static List<Item> itemsToAddToHebiosMouldPress = [];
        // Adding an Item's Prefab's Hash to this will cause the Smelter to spawn the Item with that Vector3 as a position offset when creating said Item with a Mould
        // The purpose of this is to prevent certain items from getting stuck in the Smelter
        public static Dictionary<uint, Vector3> smelterSpawnPositionOffsets = [];
        // Same thing, but with rotation instead of position
        public static Dictionary<uint, Vector3> smelterSpawnRotationOffsets = [];

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Initialized.");
        }

        public override void OnLateInitializeMelon()
        {
            PreSetUpMoulds.Invoke();

            SetUpMoulds.Invoke();

            PostSetUpMoulds.Invoke();

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

        public static void RegisterMouldDefinition(MouldDefinition mouldDefinition)
        {
            MouldDefinition.CheckItems();
            Dictionary<uint, MouldDefinition> items = (Dictionary<uint, MouldDefinition>)typeof(HashedGeneralValue<MouldDefinition>).GetField("items", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            items.Add(mouldDefinition.Hash, mouldDefinition);
        }
    }
}