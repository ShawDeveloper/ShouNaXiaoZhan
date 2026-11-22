using System.IO;
using Mediapipe.Unity.Sample;
using StorageStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StorageStationEditor
{
    [InitializeOnLoad]
    public static class StorageStationSetup
    {
        static StorageStationSetup()
        {
            EditorApplication.delayCall += AutoCreateScene;
        }

        private const string ScenePath = "Assets/Scenes/StorageStation.unity";
        private const string SourceBootstrap = "Assets/Samples/MediaPipe Unity Plugin/0.16.3/Official Solutions/Resources/Bootstrap.prefab";
        private const string SourceSettings = "Assets/Samples/MediaPipe Unity Plugin/0.16.3/Official Solutions/Scenes/AppSettings.asset";
        private const string TargetBootstrap = "Assets/Prefabs/StorageStationBootstrap.prefab";
        private const string TargetSettings = "Assets/Settings/StorageStationAppSettings.asset";

        [MenuItem("Storage Station/Create Prototype Scene")]
        public static void CreatePrototypeScene()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Settings");
            EnsureFolder("Assets/Scenes");

            if (AssetDatabase.LoadAssetAtPath<AppSettings>(TargetSettings) == null)
            {
                AssetDatabase.CopyAsset(SourceSettings, TargetSettings);
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TargetBootstrap) == null)
            {
                AssetDatabase.CopyAsset(SourceBootstrap, TargetBootstrap);
            }
            AssetDatabase.Refresh();

            var settings = AssetDatabase.LoadAssetAtPath<AppSettings>(TargetSettings);
            var settingsObject = new SerializedObject(settings);
            settingsObject.FindProperty("_preferredDefaultWebCamWidth").intValue = 640;
            settingsObject.FindProperty("_preferableInferenceMode").enumValueIndex = 0;
            settingsObject.FindProperty("_assetLoaderType").enumValueIndex = 2;
            settingsObject.ApplyModifiedPropertiesWithoutUndo();

            var prefabRoot = PrefabUtility.LoadPrefabContents(TargetBootstrap);
            var bootstrap = prefabRoot.GetComponent<Bootstrap>();
            var bootstrapObject = new SerializedObject(bootstrap);
            bootstrapObject.FindProperty("_appSettings").objectReferenceValue = settings;
            bootstrapObject.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, TargetBootstrap);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.625f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.96f, 0.95f, 0.92f);

            var bootstrapInstance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(TargetBootstrap));
            bootstrapInstance.name = "Bootstrap";

            var gameRoot = new GameObject("GameRoot");
            gameRoot.AddComponent<LevelController>();

            var tracking = new GameObject("Tracking");
            tracking.AddComponent<StorageHandLandmarkerRunner>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = gameRoot;
            Debug.Log("Storage Station prototype scene created: " + ScenePath);
        }

        public static void BuildBatch()
        {
            CreatePrototypeScene();
        }

        private static void AutoCreateScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;
            if (EditorSceneManager.GetActiveScene().isDirty) return;
            CreatePrototypeScene();
        }

        private static void EnsureFolder(string path)
        {
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }
}
