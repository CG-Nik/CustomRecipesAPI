using Alta;
using Alta.Inventory;
using Alta.Networking;
using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;
using System.Reflection;
using UnityEngine;

[assembly: MelonInfo(typeof(CustomRecipesAPI.Core), "CustomRecipesAPI", "1.0.0", "CGNik", null)]
[assembly: MelonGame("Alta", "A Township Tale")]

namespace CustomRecipesAPI
{
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
        public static event Action PostInitializePatch = () => { };

        public static List<Item> itemsToAddToStandardMouldPress = [];
        public static List<Item> itemsToAddToHebiosMouldPress = [];

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

            PostInitializePatch.Invoke();
        }

        public static void RegisterMouldDefinition(MouldDefinition mouldDefinition)
        {
            MouldDefinition.CheckItems();
            Dictionary<uint, MouldDefinition> items = (Dictionary<uint, MouldDefinition>)typeof(HashedGeneralValue<MouldDefinition>).GetField("items", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            items.Add(mouldDefinition.Hash, mouldDefinition);
        }
    }
}