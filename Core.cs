using Alta;
using Alta.Blacksmithing;
using Alta.Carpentry;
using Alta.Chunks;
using Alta.Inventory;
using Alta.Networking;
using Alta.Pages;
using HarmonyLib;
using MelonLoader;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using static SpawnHelper;

[assembly: MelonInfo(typeof(CustomRecipesAPI.Core), "CustomRecipesAPI", "1.0.0", "CGNik", null)]
[assembly: MelonGame("Alta", "A Township Tale")]

namespace CustomRecipesAPI
{
    public class SetDefinitionPatch
    {
        internal static void Postfix(Mould __instance, MouldDefinition definition, bool isOnvalidate)
        {
            if (ApplicationManager.IsHeadless)
            {
                return;
            }

            Material material = Core.itemSetCustomMaterials[definition.AllowedMaterials];

            if (material == null) { return; }

            MeshFilter meshFilter = (MeshFilter)typeof(Mould).GetField("graphic", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);
            meshFilter.gameObject.GetComponent<MeshRenderer>().material = material;
        }
    }

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
        // For modders' information, SetUpRecipes is only a good place to set your recipes up when you're not using any custom items
        // If you're using any custom items for your recipes, those won't be registered until after all of these Actions run, meaning you'll instead have to use SyncLib's registration Actions for recipes
        // This is unfortunate, as the whole point of this Action system is to "guarantee" that all recipes are set up in PostSetUpRecipes, and not set up in PreSetUpRecipes, but I can't really fix this issue
        public static event Action PreSetUpRecipes = () => { };
        public static event Action SetUpRecipes = () => { };
        public static event Action PostSetUpRecipes = () => { };
        public static event Action PostPatches = () => { };

        // Adding an Item to this will add it to the filter that the Standard Mould Press uses for the weapon to be turned into a Mould
        // These are "added" or "applied" by AddItemsToFilters(), which is automatically called directly after PostSetUpRecipes()
        // You can manually call AddItemsToFilters() if you need to add an item to a filter after the initial call
        // This allows you to use said Item to make Moulds with the Standard Mould Press
        public static List<Item> itemsToAddToStandardMouldPress = [];
        // Same thing, but with the Hebios Mould Press
        public static List<Item> itemsToAddToHebiosMouldPress = [];
        // Same thing, but with the Smelter's input instead of making Moulds with a Mould Press
        public static List<Item> itemsToAddToSmelter = [];
        // Same thing, but with the Smelter's output auto dock
        public static List<Item> itemsToAddToSmelterAutodock = [];
        // Adding an Item's Prefab's Hash to this will cause the Smelter to spawn the Item with that Vector3 as a position offset when creating said Item with a Mould
        // The purpose of this is to prevent certain items from getting stuck in the Smelter
        public static Dictionary<uint, Vector3> smelterSpawnPositionOffsets = [];
        // Same thing, but with rotation instead of position
        public static Dictionary<uint, Vector3> smelterSpawnRotationOffsets = [];
        // If a certain ItemSet is used for a Mould, this will cause the Graphic of that Mould to instead use the set custom Material instead of the metal one
        public static Dictionary<ItemSet, Material> itemSetCustomMaterials = [];
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

        public enum PageCollectionHashes
        {
            BlacksmithBasicsCollection = 11654,
            ChiselingCollection = 716,
            EdibleObjectsCollection = 17390,
            GenericCraftingCollection = 57634,
            HuntingBasicsCollection = 29810,
            MiningBasicsCollection = 13678,
            StewIngredientCollection = 62188,
            StewingRecipesCollection = 27908,
            WoodcuttingBasicsCollection = 48982
        }

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Initialized.");
        }

        private static void InvokeEach(Action action, string eventName)
        {
            if (action == null)
                return;

            foreach (Action subscriber in action.GetInvocationList())
            {
                try
                {
                    subscriber();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"{eventName} subscriber {subscriber.Method.DeclaringType?.FullName}.{subscriber.Method.Name} from {subscriber.Method.DeclaringType?.Assembly.GetName().Name} failed, continuing with the others");
                    MelonLogger.Error(ex);
                }
            }
        }

        public static void AddItemsToFilters()
        {
            PickupDockSettings mouldPressInputDockStandard = Resources.FindObjectsOfTypeAll<PickupDockSettings>().Where(settings => settings.name == "Mould Press Input Dock Standard").First();
            mouldPressInputDockStandard.IncludedItems.AddUnique(itemsToAddToStandardMouldPress);

            PickupDockSettings mouldPressInputDockHebios = Resources.FindObjectsOfTypeAll<PickupDockSettings>().Where(settings => settings.name == "Mould Press Input Dock Hebios").First();
            mouldPressInputDockHebios.IncludedItems.AddUnique(itemsToAddToHebiosMouldPress);

            PickupDockSettings oreDock = Resources.FindObjectsOfTypeAll<PickupDockSettings>().Where(settings => settings.name == "Ore Dock").First();
            oreDock.IncludedItems.AddUnique(itemsToAddToSmelter);
            PickupDockSettings orePickupDock = Resources.FindObjectsOfTypeAll<PickupDockSettings>().Where(settings => settings.name == "Ore Pickup Dock").First();
            orePickupDock.IncludedItems.AddUnique(itemsToAddToSmelter);

            PickupTag smelterAutodockTag = Resources.Load<PickupTag>("tags/Smelter Autodock");
            foreach (Item item in itemsToAddToSmelterAutodock)
            {
                item.Tags.AddUnique(smelterAutodockTag);
            }
        }

        public override void OnLateInitializeMelon()
        {
            mouldDefinition_axeHeadCurveMould = MouldDefinition.All.Where(mould => mould.Hash == 22952u).First();

            InvokeEach(PreSetUpRecipes, nameof(PreSetUpRecipes));

            InvokeEach(SetUpRecipes, nameof(SetUpRecipes));

            InvokeEach(PostSetUpRecipes, nameof(PostSetUpRecipes));

            AddItemsToFilters();

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

            HarmonyInstance.Patch(AccessTools.Method(typeof(Mould), "SetDefinition"), postfix: new HarmonyMethod(typeof(SetDefinitionPatch), nameof(SetDefinitionPatch.Postfix)));

            InvokeEach(PostPatches, nameof(PostPatches));
        }

        public static MouldItemComponent CreateMouldItemComponent(string name, MouldDefinition mouldDefinition)
        {
            MouldItemComponent mouldItemComponent = ScriptableObject.CreateInstance<MouldItemComponent>();
            mouldItemComponent.name = name;
            typeof(MouldItemComponent).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mouldItemComponent, mouldDefinition);
            return mouldItemComponent;
        }

        public static void AddItemComponent(Item item, ItemComponent itemComponent)
        {
            ((List<ItemComponent>)typeof(Item).GetField("components", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(item)).Add(itemComponent);
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

        public static ItemSet CreateItemSet(string name, List<Item> items)
        {
            ItemSet itemSet = ScriptableObject.CreateInstance<ItemSet>();
            itemSet.name = name;
            typeof(ItemSet).GetField("items", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(itemSet, items);
            return itemSet;
        }

        public static void AddToItemSet(ItemSet itemSet, Item item)
        {
            ((List<Item>)typeof(ItemSet).GetField("items", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(itemSet)).Add(item);
        }

        public static Ingot CreateIngot(string name, float smeltingDuration, PhysicalMaterial physicalMaterial)
        {
            Ingot ingot = ScriptableObject.CreateInstance<Ingot>();
            ingot.name = name;
            typeof(Ingot).GetField("smeltingDuration", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ingot, smeltingDuration);
            typeof(Ingot).GetField("physicalMaterial", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ingot, physicalMaterial);
            return ingot;
        }

        public static void SetUpMould(Item item, MouldDefinition mouldDefinition, bool addMouldItemComponent = false, bool addToStandardPress = false, bool addToHebiosPress = false, Vector3? positionOffset = null, Vector3? rotationOffset = null, ItemSet itemSet = null)
        {
            if (addMouldItemComponent)
            {
                MouldItemComponent mouldItemComponent = CreateMouldItemComponent(item.name + " Mould Item Component", mouldDefinition);
                AddItemComponent(item, mouldItemComponent);
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

            if (itemSet != null)
            {
                typeof(MouldDefinition).GetField("allowedMaterials", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mouldDefinition, itemSet);
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

        public static void AddPhysicalMaterialToSmelterUpgrades(PhysicalMaterial physicalMaterial, SmelterUpgrades smelterUpgrades = null, bool addToSimpleServerDefaultUpgrades = true)
        {
            if (addToSimpleServerDefaultUpgrades)
            {
                SmelterUpgrades smelterUpgrades_simpleServerDefaultUpgrades = SmelterUpgrades.All.Where(upgrade => upgrade.Hash == 5674u).First();
                List<PhysicalMaterial> originalPhysicalMaterials = ((PhysicalMaterial[])typeof(SmelterUpgrades).GetField("physicalMaterials", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(smelterUpgrades_simpleServerDefaultUpgrades)).ToList();
                originalPhysicalMaterials.Add(physicalMaterial);
                typeof(SmelterUpgrades).GetField("physicalMaterials", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(smelterUpgrades_simpleServerDefaultUpgrades, originalPhysicalMaterials.ToArray());
            }

            if (smelterUpgrades != null)
            {
                List<PhysicalMaterial> originalPhysicalMaterials = ((PhysicalMaterial[])typeof(SmelterUpgrades).GetField("physicalMaterials", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(smelterUpgrades)).ToList();
                originalPhysicalMaterials.Add(physicalMaterial);
                typeof(SmelterUpgrades).GetField("physicalMaterials", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(smelterUpgrades, originalPhysicalMaterials.ToArray());
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

        public static void AddToPageCollection(PageCollection pageCollection, UnityEngine.Object obj)
        {
            UnityEngine.Object[] pageBasesArray = (UnityEngine.Object[])typeof(PageCollection).GetField("pageBases", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pageCollection);
            List<UnityEngine.Object> pageBasesList = pageBasesArray.ToList();
            pageBasesList.Add(obj);
            typeof(PageCollection).GetField("pageBases", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pageCollection, pageBasesList.ToArray());
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
            AddToPageCollection(PageCollection.All.Where(collection => collection.Hash == (int)PageCollectionHashes.ChiselingCollection).First(), chiselDefinition);
        }

        public static ChiselDefinition SetUpChiselDefinitionThroughCode(string name, int hash, Glyph glyph, ChiselBlockSize size, List<int> chiselOrder, PrefabSpawnPosition[] prefabs)
        {
            ChiselDefinition chiselDefinition = CreateChiselDefinition(name, hash, glyph, size, chiselOrder, prefabs);
            RegisterChiselDefinition(chiselDefinition);
            AddToPageCollection(PageCollection.All.Where(collection => collection.Hash == (int)PageCollectionHashes.ChiselingCollection).First(), chiselDefinition);
            return chiselDefinition;
        }
    }
}