using System;
using System.Collections.Generic;
using System.IO;
using StorageStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace StorageStationEditor
{
    public static class StorageStationSceneBaker
    {
        private const string ShapeFolder = "Assets/Resources/StorageStation/Shapes";
        private const string ScenePath = "Assets/Scenes/StorageStation.unity";

        [MenuItem("Storage Station/Bake Editable Scene Objects")]
        public static void BakeActiveSceneFromMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("请先退出 Play 模式再生成场景对象。");
                return;
            }
            var level = Object.FindAnyObjectByType<LevelController>();
            if (level == null)
            {
                Debug.LogError("当前场景没有 LevelController。请先打开 StorageStation.unity。");
                return;
            }
            if (level.transform.Find("Items") != null &&
                !EditorUtility.DisplayDialog("重新生成可编辑对象", "此操作会替换 World、Targets、Items、Systems 和 HUD 下的人工修改。是否继续？", "继续", "取消"))
            {
                return;
            }

            Bake(level);
            EditorSceneManager.SaveScene(level.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("可编辑场景对象已生成并保存。物品、目标、猫爪、手套及 UI 均可在 Scene 和 Inspector 中编辑。");
        }

        public static void BakeBatch()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var level = Object.FindAnyObjectByType<LevelController>();
            if (level == null) throw new InvalidOperationException("StorageStation 场景缺少 LevelController");
            Bake(level);
            EditorSceneManager.SaveScene(level.gameObject.scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        public static void Bake(LevelController level)
        {
            EnsurePrimitiveAssets();
            ClearPreviousBake(level);

            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5.625f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.96f, 0.95f, 0.92f);
            }

            var runner = Object.FindAnyObjectByType<StorageHandLandmarkerRunner>();
            if (runner == null)
            {
                var tracking = new GameObject("Tracking");
                runner = tracking.AddComponent<StorageHandLandmarkerRunner>();
            }

            var runnerSettings = new SerializedObject(runner);
            var bootstrapReference = runnerSettings.FindProperty("_bootstrapPrefab");
            if (bootstrapReference != null && bootstrapReference.objectReferenceValue == null)
            {
                bootstrapReference.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/StorageStationBootstrap.prefab");
                runnerSettings.ApplyModifiedPropertiesWithoutUndo();
            }

            BuildWorld(level.transform);
            var sceneTargets = BuildTargets(level.transform, out var puzzle);

            var systems = CreateChild(level.transform, "Systems");
            var conveyor = CreateSystem<ConveyorController>(systems, "ConveyorController");
            conveyor.Initialize(level);
            var sceneItems = BuildItems(level.transform, conveyor);

            var input = CreateSystem<HandInputService>(systems, "HandInputService");
            var leftCursor = CreateCursor(input.transform, HandSide.Left);
            var rightCursor = CreateCursor(input.transform, HandSide.Right);
            input.Configure(runner, camera, leftCursor, rightCursor);

            var cat = CreateSystem<CatController>(systems, "CatController");
            cat.BuildVisual(level);

            var hudObject = CreateChild(level.transform, "HUD");
            var hud = hudObject.gameObject.AddComponent<GameHud>();
            hud.BuildVisual(level);

            var feedback = CreateSystem<FeedbackController>(systems, "FeedbackController");
            feedback.Initialize(hud);
            var interaction = CreateSystem<HandInteractionController>(systems, "HandInteractionController");
            interaction.Initialize(level);

            level.Configure(sceneItems, sceneTargets, puzzle, conveyor, input, runner, feedback, hud, interaction, cat, camera);
            EnsureEventSystem();

            EditorUtility.SetDirty(level);
            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            Selection.activeGameObject = level.gameObject;
            Debug.Log($"Storage Station bake complete: {sceneItems.Count} items, {sceneTargets.Count} normal targets, puzzle, cursors, cat and HUD.");
        }

        private static void ClearPreviousBake(LevelController level)
        {
            foreach (var name in new[] { "World", "Targets", "Items", "Systems", "HUD", "ConveyorController", "HandInputService", "CatController" })
            {
                var child = level.transform.Find(name);
                if (child != null) Undo.DestroyObjectImmediate(child.gameObject);
            }
            foreach (var component in level.GetComponents<Component>())
            {
                if (component is FeedbackController || component is GameHud || component is HandInteractionController)
                {
                    Undo.DestroyObjectImmediate(component);
                }
            }
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static T CreateSystem<T>(Transform parent, string name) where T : Component
        {
            var child = CreateChild(parent, name);
            return child.gameObject.AddComponent<T>();
        }

        private static void BuildWorld(Transform parent)
        {
            var world = CreateChild(parent, "World");
            Geometry.Create(world, "Background", PrimitiveShape.Square, Vector2.zero,
                new Vector2(20f, 11.25f), new Color(0.96f, 0.95f, 0.92f), -100);
            Geometry.Create(world, "CabinetFrame", PrimitiveShape.Square, new Vector2(2.45f, 1.25f),
                new Vector2(13.75f, 7.5f), new Color(0.12f, 0.08f, 0.15f), 0);
            Geometry.Create(world, "Cabinet", PrimitiveShape.Square, new Vector2(2.45f, 1.25f),
                new Vector2(13.45f, 7.2f), new Color(0.58f, 0.08f, 0.75f), 1);
            Geometry.Create(world, "Conveyor", PrimitiveShape.Square, new Vector2(0f, -4.12f),
                new Vector2(18.2f, 1.45f), new Color(0.56f, 0.58f, 0.6f), 5);
            Geometry.Create(world, "ConveyorBelt", PrimitiveShape.Square, new Vector2(0f, -3.98f),
                new Vector2(17.8f, 0.62f), new Color(0.72f, 0.73f, 0.74f), 6);
            Geometry.Create(world, "LeftSupport", PrimitiveShape.Square, new Vector2(-8.6f, -4.45f),
                new Vector2(0.75f, 1.5f), new Color(0.35f, 0.36f, 0.38f), 4);
            Geometry.Create(world, "RightSupport", PrimitiveShape.Square, new Vector2(8.6f, -4.45f),
                new Vector2(0.75f, 1.5f), new Color(0.35f, 0.36f, 0.38f), 4);
            Geometry.Create(world, "OutletPort", PrimitiveShape.Square, new Vector2(-9.05f, -3.95f),
                new Vector2(1.1f, 1.75f), new Color(0.16f, 0.18f, 0.22f), 8);
            Geometry.Create(world, "InletPort", PrimitiveShape.Square, new Vector2(9.05f, -3.95f),
                new Vector2(1.1f, 1.75f), new Color(0.16f, 0.18f, 0.22f), 8);
            var outletArrow = Geometry.Create(world, "OutletArrow", PrimitiveShape.Triangle, new Vector2(-8.85f, -3.9f),
                new Vector2(0.62f, 0.72f), new Color(0.2f, 0.85f, 0.48f), 9);
            outletArrow.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            var inletArrow = Geometry.Create(world, "InletArrow", PrimitiveShape.Triangle, new Vector2(8.75f, -3.9f),
                new Vector2(0.62f, 0.72f), new Color(0.95f, 0.72f, 0.15f), 9);
            inletArrow.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            CreateWorldText(world, "OutletLabel", "传出口", new Vector2(-8.85f, -2.85f));
            CreateWorldText(world, "InletLabel", "传入口", new Vector2(8.85f, -2.85f));
        }

        private static List<DropTarget> BuildTargets(Transform parent, out PuzzleTarget puzzle)
        {
            var root = CreateChild(parent, "Targets");
            var targets = new List<DropTarget>
            {
                CreateTarget(root, "GreenTarget", "green_bag", new Vector2(6.55f, 2.55f), new Vector2(3.5f, 3.45f),
                    new Color(0.05f, 0.72f, 0.42f), ItemKind.Bag),
                CreateTarget(root, "OrangeTarget", "orange_bag", new Vector2(-2.1f, -0.75f), new Vector2(4.3f, 3.3f),
                    new Color(0.93f, 0.53f, 0.06f), ItemKind.Bag),
                CreateTarget(root, "PurpleTarget", "purple_bag", new Vector2(-1.65f, 2.75f), new Vector2(2.25f, 2.55f),
                    new Color(0.58f, 0.08f, 0.75f), ItemKind.Bag)
            };

            var puzzleObject = CreateChild(root, "PuzzleTarget");
            puzzleObject.position = new Vector2(2.75f, 0.65f);
            puzzle = puzzleObject.gameObject.AddComponent<PuzzleTarget>();
            puzzle.BuildVisual();
            targets.Add(CreateTarget(root, "PhoneTarget", "phone", new Vector2(6.4f, -1.55f), new Vector2(3.4f, 1.25f),
                new Color(0.58f, 0.08f, 0.75f), ItemKind.Phone));
            return targets;
        }

        private static DropTarget CreateTarget(Transform parent, string name, string itemId, Vector2 position,
            Vector2 size, Color color, ItemKind kind)
        {
            var go = CreateChild(parent, name);
            go.position = position;
            var target = go.gameObject.AddComponent<DropTarget>();
            target.Configure(itemId, size, color, kind);
            return target;
        }

        private static List<StorageItem> BuildItems(Transform parent, ConveyorController conveyor)
        {
            var root = CreateChild(parent, "Items");
            var items = new List<StorageItem>
            {
                CreateItem(root, conveyor, "GreenBag", "green_bag", "绿包", ItemKind.Bag, new Color(0.05f, 0.78f, 0.43f), 0),
                CreateItem(root, conveyor, "OrangeBag", "orange_bag", "橙包", ItemKind.Bag, new Color(0.98f, 0.58f, 0.08f), 1),
                CreateItem(root, conveyor, "PurpleBag", "purple_bag", "紫包", ItemKind.Bag, new Color(0.72f, 0.16f, 0.9f), 2),
                CreateItem(root, conveyor, "LeftFragment", "left_fragment", "左半圆碎片", ItemKind.LeftFragment, new Color(0.88f, 0.46f, 0.96f), 3),
                CreateItem(root, conveyor, "RightFragment", "right_fragment", "右半圆碎片", ItemKind.RightFragment, new Color(0.88f, 0.46f, 0.96f), 4),
                CreateItem(root, conveyor, "Phone", "phone", "手机", ItemKind.Phone, new Color(0.08f, 0.08f, 0.1f), 5)
            };
            return items;
        }

        private static StorageItem CreateItem(Transform parent, ConveyorController conveyor, string name, string id,
            string displayName, ItemKind kind, Color color, int beltIndex)
        {
            var go = CreateChild(parent, name);
            var item = go.gameObject.AddComponent<StorageItem>();
            var position = new Vector2(conveyor.GetInitialBeltX(beltIndex), conveyor.BeltY);
            item.Configure(id, displayName, kind, color, beltIndex, position);
            return item;
        }

        private static HandCursorView CreateCursor(Transform parent, HandSide side)
        {
            var go = CreateChild(parent, side + "HandCursor");
            var cursor = go.gameObject.AddComponent<HandCursorView>();
            cursor.Build(side);
            go.gameObject.SetActive(false);
            return cursor;
        }

        private static void CreateWorldText(Transform parent, string name, string content, Vector2 position)
        {
            var go = CreateChild(parent, name);
            go.position = position;
            var text = go.gameObject.AddComponent<TextMesh>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 42;
            text.characterSize = 0.08f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.16f, 0.18f, 0.22f);
            go.GetComponent<MeshRenderer>().sortingOrder = 20;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var eventObject = new GameObject("EventSystem");
            eventObject.AddComponent<EventSystem>();
            eventObject.AddComponent<StandaloneInputModule>();
        }

        private static void EnsurePrimitiveAssets()
        {
            Directory.CreateDirectory(ShapeFolder);
            foreach (PrimitiveShape shape in Enum.GetValues(typeof(PrimitiveShape)))
            {
                var path = ShapeFolder + "/" + shape + ".png";
                if (!File.Exists(path))
                {
                    const int resolution = 64;
                    var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
                    var pixels = new Color[resolution * resolution];
                    for (var y = 0; y < resolution; y++)
                    {
                        for (var x = 0; x < resolution; x++)
                        {
                            var uv = new Vector2((x + 0.5f) / resolution, (y + 0.5f) / resolution);
                            var p = uv - Vector2.one * 0.5f;
                            var visible = shape == PrimitiveShape.Square;
                            if (shape == PrimitiveShape.Circle || shape == PrimitiveShape.Ring ||
                                shape == PrimitiveShape.HalfLeft || shape == PrimitiveShape.HalfRight)
                            {
                                var radius = p.sqrMagnitude;
                                visible = radius <= 0.25f;
                                if (shape == PrimitiveShape.Ring) visible = visible && radius >= 0.12f;
                                if (shape == PrimitiveShape.HalfLeft) visible = visible && p.x <= 0f;
                                if (shape == PrimitiveShape.HalfRight) visible = visible && p.x >= 0f;
                            }
                            else if (shape == PrimitiveShape.Triangle)
                            {
                                visible = uv.y <= 1f - Mathf.Abs(p.x) * 2f && uv.y >= 0f;
                            }
                            pixels[y * resolution + x] = visible ? Color.white : Color.clear;
                        }
                    }
                    texture.SetPixels(pixels);
                    texture.Apply();
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                    Object.DestroyImmediate(texture);
                }

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("无法导入几何资源 " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 64f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            AssetDatabase.Refresh();
        }
    }
}
