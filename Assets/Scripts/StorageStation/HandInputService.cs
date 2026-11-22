using System.Collections.Generic;
using UnityEngine;

namespace StorageStation
{
    public sealed class HandRuntimeState
    {
        public bool Valid;
        public Vector2 Position;
        public float Angle;
        public float LastSeen;
        public float WaveUntil;
        public readonly List<WavePoint> History = new List<WavePoint>();
        public bool IsWaving => Time.unscaledTime < WaveUntil;
    }

    public struct WavePoint
    {
        public float Time;
        public Vector2 Position;
    }

    [DefaultExecutionOrder(-100)]
    public sealed class HandInputService : MonoBehaviour
    {
        private StorageHandLandmarkerRunner runner;
        private Camera gameCamera;
        private float worldWidth;
        private float worldHeight;
        private bool debugMode;
        private HandSide debugHand = HandSide.Right;
        private readonly float[] progress = new float[2];
        private readonly bool[] holding = new bool[2];

        public HandRuntimeState Left { get; } = new HandRuntimeState();
        public HandRuntimeState Right { get; } = new HandRuntimeState();
        public HandCursorView LeftCursor { get; private set; }
        public HandCursorView RightCursor { get; private set; }
        public bool AnyHandValid => Left.Valid || Right.Valid;
        public bool BothHandsValid => Left.Valid && Right.Valid;
        public bool DebugMode => debugMode;

        public void Initialize(StorageHandLandmarkerRunner handRunner, Camera camera)
        {
            runner = handRunner;
            gameCamera = camera;
            worldHeight = camera.orthographicSize * 2f;
            worldWidth = worldHeight * 16f / 9f;

            LeftCursor = CreateCursor(HandSide.Left);
            RightCursor = CreateCursor(HandSide.Right);
            Left.Valid = false;
            Right.Valid = false;
        }

        public HandRuntimeState Get(HandSide side) => side == HandSide.Left ? Left : Right;

        public void SetProgress(HandSide side, float value)
        {
            progress[(int)side] = Mathf.Clamp01(value);
        }

        public void SetHolding(HandSide side, bool value)
        {
            holding[(int)side] = value;
        }

        public void EnableDebugMode()
        {
            debugMode = true;
            Left.Valid = true;
            Right.Valid = true;
            Left.Position = new Vector2(-2.5f, -1.5f);
            Right.Position = new Vector2(2.5f, -1.5f);
            Left.LastSeen = Right.LastSeen = Time.unscaledTime;
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.M))
            {
                debugMode = !debugMode;
                if (debugMode) EnableDebugMode();
                else
                {
                    Left.Valid = Right.Valid = false;
                    Left.History.Clear();
                    Right.History.Clear();
                }
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab)) debugHand = debugHand == HandSide.Left ? HandSide.Right : HandSide.Left;

            if (debugMode)
            {
                UpdateDebugHand(debugHand);
                UpdateWave(Left);
                UpdateWave(Right);
            }
            else
            {
                if (runner != null && runner.TryGetLatestFrame(out var frame))
                {
                    ApplySample(HandSide.Left, frame.Left, Left);
                    ApplySample(HandSide.Right, frame.Right, Right);
                }
                ExpireIfNeeded(Left);
                ExpireIfNeeded(Right);
                if (Left.Valid) UpdateWave(Left);
                if (Right.Valid) UpdateWave(Right);
            }

            LeftCursor.SetState(Left.Position, Left.Angle, Left.Valid, progress[0], holding[0]);
            RightCursor.SetState(Right.Position, Right.Angle, Right.Valid, progress[1], holding[1]);
        }

        private HandCursorView CreateCursor(HandSide side)
        {
            var go = new GameObject(side + "HandCursor");
            go.transform.SetParent(transform, false);
            var cursor = go.AddComponent<HandCursorView>();
            cursor.Build(side);
            return cursor;
        }

        private void ApplySample(HandSide side, HandSample sample, HandRuntimeState state)
        {
            if (!sample.Valid)
            {
                return;
            }

            var target = new Vector2(
                (sample.Palm.x - 0.5f) * worldWidth,
                (0.5f - sample.Palm.y) * worldHeight);
            var wasValid = state.Valid;
            state.Position = wasValid
                ? Vector2.Lerp(state.Position, target, 1f - Mathf.Exp(-18f * Time.deltaTime))
                : target;
            state.Valid = true;
            state.LastSeen = Time.unscaledTime;

            var direction = new Vector2(sample.Direction.x, -sample.Direction.y);
            if (direction.sqrMagnitude > 0.0001f)
            {
                state.Angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            }
        }

        private void ExpireIfNeeded(HandRuntimeState state)
        {
            if (state.Valid && Time.unscaledTime - state.LastSeen > 0.25f)
            {
                state.Valid = false;
                state.History.Clear();
            }
        }

        private void UpdateDebugHand(HandSide side)
        {
            var mouse = UnityEngine.Input.mousePosition;
            mouse.z = 10f;
            var world = gameCamera.ScreenToWorldPoint(mouse);
            var state = Get(side);
            state.Position = world;
            state.Angle = 0f;
            state.Valid = true;
            state.LastSeen = Time.unscaledTime;
        }

        private void UpdateWave(HandRuntimeState state)
        {
            state.History.Add(new WavePoint { Time = Time.unscaledTime, Position = state.Position });
            while (state.History.Count > 0 && Time.unscaledTime - state.History[0].Time > 0.8f)
            {
                state.History.RemoveAt(0);
            }
            if (state.History.Count < 4) return;

            var distance = 0f;
            var reversals = 0;
            var lastSign = 0;
            for (var i = 1; i < state.History.Count; i++)
            {
                var delta = state.History[i].Position.x - state.History[i - 1].Position.x;
                if (Mathf.Abs(delta) < 0.025f) continue;
                var sign = delta > 0f ? 1 : -1;
                if (lastSign != 0 && sign != lastSign) reversals++;
                lastSign = sign;
                distance += Mathf.Abs(delta);
            }

            var duration = state.History[state.History.Count - 1].Time - state.History[0].Time;
            if (reversals >= 2 && distance >= worldWidth * 0.35f && duration > 0.05f && distance / duration >= 3.5f)
            {
                state.WaveUntil = Time.unscaledTime + 0.2f;
            }
        }
    }
}
