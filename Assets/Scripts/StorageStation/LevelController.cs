using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace StorageStation
{
    public enum LevelState
    {
        Booting,
        Playing,
        Won,
        CameraError
    }

    [DefaultExecutionOrder(20)]
    public sealed class LevelController : MonoBehaviour
    {
        private float bootTime;
        private HandInteractionController interaction;
        private CatController cat;

        public LevelState State { get; private set; } = LevelState.Booting;
        public List<StorageItem> Items { get; } = new List<StorageItem>();
        public List<DropTarget> Targets { get; } = new List<DropTarget>();
        public ConveyorController Conveyor { get; private set; }
        public PuzzleTarget Puzzle { get; private set; }
        public HandInputService Input { get; private set; }
        public StorageHandLandmarkerRunner Runner { get; private set; }
        public FeedbackController Feedback { get; private set; }
        public GameHud Hud { get; private set; }

        private void Awake()
        {
            Application.runInBackground = true;
            Runner = FindAnyObjectByType<StorageHandLandmarkerRunner>();
            var gameCamera = Camera.main;
            if (gameCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                gameCamera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                gameCamera.orthographic = true;
                gameCamera.orthographicSize = 5.625f;
            }

            BuildWorld();

            var conveyorObject = new GameObject("ConveyorController");
            conveyorObject.transform.SetParent(transform, false);
            Conveyor = conveyorObject.AddComponent<ConveyorController>();
            Conveyor.Initialize(this);

            BuildTargets();
            BuildItems();

            var inputObject = new GameObject("HandInputService");
            inputObject.transform.SetParent(transform, false);
            Input = inputObject.AddComponent<HandInputService>();
            Input.Initialize(Runner, gameCamera);

            Feedback = gameObject.AddComponent<FeedbackController>();
            Hud = gameObject.AddComponent<GameHud>();
            Hud.Initialize(this);
            Feedback.Initialize(Hud);

            interaction = gameObject.AddComponent<HandInteractionController>();
            interaction.Initialize(this);

            var catObject = new GameObject("CatController");
            catObject.transform.SetParent(transform, false);
            cat = catObject.AddComponent<CatController>();
            cat.Initialize(this);

            EnsureEventSystem();
            bootTime = Time.time;
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) RestartLevel();
            if (UnityEngine.Input.GetKeyDown(KeyCode.C)) cat.ForceTrigger();

            if (State == LevelState.Booting || State == LevelState.CameraError)
            {
                if (Input.DebugMode || (Runner != null && Runner.IsReady))
                {
                    State = LevelState.Playing;
                    Hud.HidePanel();
                }
                else if (Runner != null && !string.IsNullOrEmpty(Runner.LastError))
                {
                    SetCameraError(Runner.LastError);
                }
                else if (State == LevelState.Booting && Time.time - bootTime > 20f)
                {
                    SetCameraError("摄像头初始化超时，请检查设备占用和权限");
                }
            }

            if (State == LevelState.Playing)
            {
                foreach (var item in Items) item.UpdateItem();
                if (AllItemsCorrect())
                {
                    State = LevelState.Won;
                    Hud.ShowWon();
                    Hud.SetCatWarning("");
                }
            }
            else
            {
                foreach (var item in Items) item.UpdateItem();
            }
        }

        public DropTarget FindNormalTargetAt(Vector2 position)
        {
            foreach (var target in Targets)
            {
                if (target.Contains(position)) return target;
            }
            return null;
        }

        public void RemovePuzzlePiece(StorageItem item)
        {
            var puzzle = item.CurrentPuzzleTarget;
            if (puzzle == null) return;
            var other = puzzle.GetOther(item);
            puzzle.Clear();
            item.CurrentPuzzleTarget = null;
            if (other != null && other != item) other.BeginReturn();
        }

        public void DetachPlacedForCat(StorageItem item)
        {
            if (item.CurrentDropTarget != null) item.CurrentDropTarget.Remove(item);
            if (item.CurrentPuzzleTarget != null) RemovePuzzlePiece(item);
            item.CurrentDropTarget = null;
            item.CurrentPuzzleTarget = null;
        }

        public void RestartLevel()
        {
            interaction.ResetInteractions();
            Feedback.ResetFeedback();
            cat.ResetCat();
            foreach (var target in Targets) target.ResetTarget();
            Puzzle.ResetTarget();
            Conveyor.ResetConveyor();
            foreach (var item in Items) item.ResetImmediate();
            Hud.HidePanel();
            Hud.SetCatWarning("");
            bootTime = Time.time;
            State = Input.DebugMode || (Runner != null && Runner.IsReady) ? LevelState.Playing : LevelState.Booting;
        }

        public void RetryCamera()
        {
            if (Runner == null) return;
            Hud.HidePanel();
            State = LevelState.Booting;
            bootTime = Time.time;
            Runner.RestartTracking();
        }

        private void SetCameraError(string error)
        {
            if (State == LevelState.CameraError) return;
            State = LevelState.CameraError;
            Hud.ShowCameraError(error);
        }

        private bool AllItemsCorrect()
        {
            foreach (var target in Targets)
            {
                if (target.Occupant == null || target.Occupant.ItemId != target.AcceptedItemId || !target.Occupant.IsPlaced)
                {
                    return false;
                }
            }
            return Puzzle.IsComplete && Puzzle.LeftPiece != null && Puzzle.RightPiece != null &&
                   Puzzle.LeftPiece.IsPlaced && Puzzle.RightPiece.IsPlaced;
        }

        private void BuildWorld()
        {
            var world = new GameObject("World").transform;
            world.SetParent(transform, false);
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

        private void BuildTargets()
        {
            CreateTarget("GreenTarget", "green_bag", new Vector2(6.55f, 2.55f), new Vector2(3.5f, 3.45f),
                new Color(0.05f, 0.72f, 0.42f));
            CreateTarget("OrangeTarget", "orange_bag", new Vector2(-2.1f, -0.75f), new Vector2(4.3f, 3.3f),
                new Color(0.93f, 0.53f, 0.06f));
            CreateTarget("PurpleTarget", "purple_bag", new Vector2(-1.65f, 2.75f), new Vector2(2.25f, 2.55f),
                new Color(0.58f, 0.08f, 0.75f));

            var puzzleObject = new GameObject("PuzzleTarget");
            puzzleObject.transform.SetParent(transform, false);
            puzzleObject.transform.position = new Vector2(2.75f, 0.65f);
            Puzzle = puzzleObject.AddComponent<PuzzleTarget>();
            Puzzle.Initialize();

            CreateTarget("PhoneTarget", "phone", new Vector2(6.4f, -1.55f), new Vector2(3.4f, 1.25f),
                new Color(0.58f, 0.08f, 0.75f), ItemKind.Phone);
        }

        private void CreateTarget(string name, string itemId, Vector2 position, Vector2 size, Color color,
            ItemKind kind = ItemKind.Bag)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var target = go.AddComponent<DropTarget>();
            target.Initialize(itemId, size, color, kind);
            Targets.Add(target);
        }

        private void BuildItems()
        {
            CreateItem("GreenBag", "green_bag", "绿包", ItemKind.Bag, new Color(0.05f, 0.78f, 0.43f), 0);
            CreateItem("OrangeBag", "orange_bag", "橙包", ItemKind.Bag, new Color(0.98f, 0.58f, 0.08f), 1);
            CreateItem("PurpleBag", "purple_bag", "紫包", ItemKind.Bag, new Color(0.72f, 0.16f, 0.9f), 2);
            CreateItem("LeftFragment", "left_fragment", "左半圆碎片", ItemKind.LeftFragment, new Color(0.88f, 0.46f, 0.96f), 3);
            CreateItem("RightFragment", "right_fragment", "右半圆碎片", ItemKind.RightFragment, new Color(0.88f, 0.46f, 0.96f), 4);
            CreateItem("Phone", "phone", "手机", ItemKind.Phone, new Color(0.08f, 0.08f, 0.1f), 5);
        }

        private void CreateItem(string name, string id, string displayName, ItemKind kind, Color color, int beltIndex)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var item = go.AddComponent<StorageItem>();
            Items.Add(item);
            item.Initialize(this, id, displayName, kind, color, beltIndex);
        }

        private static void CreateWorldText(Transform parent, string name, string content, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var text = go.AddComponent<TextMesh>();
            text.text = content;
            text.font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 42);
            text.fontSize = 42;
            text.characterSize = 0.08f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.16f, 0.18f, 0.22f);
            go.GetComponent<MeshRenderer>().sortingOrder = 20;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var eventObject = new GameObject("EventSystem");
            eventObject.AddComponent<EventSystem>();
            eventObject.AddComponent<StandaloneInputModule>();
        }
    }
}
