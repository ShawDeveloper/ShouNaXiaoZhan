using System.Collections.Generic;
using UnityEngine;

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
        [Header("场景对象")]
        [SerializeField] private List<StorageItem> items = new List<StorageItem>();
        [SerializeField] private List<DropTarget> targets = new List<DropTarget>();
        [SerializeField] private PuzzleTarget puzzle;
        [SerializeField] private ConveyorController conveyor;

        [Header("系统引用")]
        [SerializeField] private HandInputService input;
        [SerializeField] private StorageHandLandmarkerRunner runner;
        [SerializeField] private FeedbackController feedback;
        [SerializeField] private GameHud hud;
        [SerializeField] private HandInteractionController interaction;
        [SerializeField] private CatController cat;
        [SerializeField] private Camera gameCamera;

        private float bootTime;

        public LevelState State { get; private set; } = LevelState.Booting;
        public List<StorageItem> Items => items;
        public List<DropTarget> Targets => targets;
        public ConveyorController Conveyor => conveyor;
        public PuzzleTarget Puzzle => puzzle;
        public HandInputService Input => input;
        public StorageHandLandmarkerRunner Runner => runner;
        public FeedbackController Feedback => feedback;
        public GameHud Hud => hud;

        public void Configure(List<StorageItem> sceneItems, List<DropTarget> sceneTargets, PuzzleTarget scenePuzzle,
            ConveyorController sceneConveyor, HandInputService sceneInput, StorageHandLandmarkerRunner sceneRunner,
            FeedbackController sceneFeedback, GameHud sceneHud, HandInteractionController sceneInteraction,
            CatController sceneCat, Camera sceneCamera)
        {
            items = sceneItems;
            targets = sceneTargets;
            puzzle = scenePuzzle;
            conveyor = sceneConveyor;
            input = sceneInput;
            runner = sceneRunner;
            feedback = sceneFeedback;
            hud = sceneHud;
            interaction = sceneInteraction;
            cat = sceneCat;
            gameCamera = sceneCamera;
        }

        private void Awake()
        {
            Application.runInBackground = true;
            if (runner == null) runner = FindAnyObjectByType<StorageHandLandmarkerRunner>();
            if (gameCamera == null) gameCamera = Camera.main;
            if (!HasSceneReferences())
            {
                Debug.LogError("场景对象引用不完整。请执行 Storage Station > Bake Editable Scene Objects。", this);
                enabled = false;
                return;
            }

            conveyor.Initialize(this);
            foreach (var item in items) item.InitializeRuntime(this);
            foreach (var target in targets) target.InitializeRuntime();
            puzzle.InitializeRuntime();
            input.Initialize(runner, gameCamera);
            hud.Initialize(this);
            feedback.Initialize(hud);
            interaction.Initialize(this);
            cat.Initialize(this);
            bootTime = Time.time;
        }

        private bool HasSceneReferences()
        {
            return items != null && items.Count > 0 && targets != null && puzzle != null && conveyor != null &&
                   input != null && feedback != null && hud != null && interaction != null && cat != null && gameCamera != null;
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) RestartLevel();
            if (UnityEngine.Input.GetKeyDown(KeyCode.C)) cat.ForceTrigger();

            if (State == LevelState.Booting || State == LevelState.CameraError)
            {
                if (input.DebugMode || (runner != null && runner.IsReady))
                {
                    State = LevelState.Playing;
                    hud.HidePanel();
                }
                else if (runner != null && !string.IsNullOrEmpty(runner.LastError))
                {
                    SetCameraError(runner.LastError);
                }
                else if (State == LevelState.Booting && Time.time - bootTime > 20f)
                {
                    SetCameraError("摄像头初始化超时，请检查设备占用和权限");
                }
            }

            foreach (var item in items) item.UpdateItem();
            if (State == LevelState.Playing && AllItemsCorrect())
            {
                State = LevelState.Won;
                hud.ShowWon();
                hud.SetCatWarning("");
            }
        }

        public DropTarget FindNormalTargetAt(Vector2 position)
        {
            foreach (var target in targets)
            {
                if (target.Contains(position)) return target;
            }
            return null;
        }

        public void RemovePuzzlePiece(StorageItem item)
        {
            var puzzleTarget = item.CurrentPuzzleTarget;
            if (puzzleTarget == null) return;
            var other = puzzleTarget.GetOther(item);
            puzzleTarget.Clear();
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
            feedback.ResetFeedback();
            cat.ResetCat();
            foreach (var target in targets) target.ResetTarget();
            puzzle.ResetTarget();
            conveyor.ResetConveyor();
            foreach (var item in items) item.ResetImmediate();
            hud.HidePanel();
            hud.SetCatWarning("");
            bootTime = Time.time;
            State = input.DebugMode || (runner != null && runner.IsReady) ? LevelState.Playing : LevelState.Booting;
        }

        public void RetryCamera()
        {
            if (runner == null) return;
            hud.HidePanel();
            State = LevelState.Booting;
            bootTime = Time.time;
            runner.RestartTracking();
        }

        private void SetCameraError(string error)
        {
            if (State == LevelState.CameraError) return;
            State = LevelState.CameraError;
            hud.ShowCameraError(error);
        }

        private bool AllItemsCorrect()
        {
            foreach (var target in targets)
            {
                if (target.Occupant == null || target.Occupant.ItemId != target.AcceptedItemId || !target.Occupant.IsPlaced)
                {
                    return false;
                }
            }
            return puzzle.IsComplete && puzzle.LeftPiece != null && puzzle.RightPiece != null &&
                   puzzle.LeftPiece.IsPlaced && puzzle.RightPiece.IsPlaced;
        }
    }
}
