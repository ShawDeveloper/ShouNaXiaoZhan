using UnityEngine;

namespace StorageStation
{
    public sealed class HandInteractionController : MonoBehaviour
    {
        private const float HoverDuration = 2f;
        private LevelController level;
        private readonly InteractionState[] states = { new InteractionState(), new InteractionState() };
        private float mergeTime;

        private sealed class InteractionState
        {
            public StorageItem Held;
            public StorageItem HoverItem;
            public DropTarget HoverTarget;
            public bool HoverPuzzle;
            public bool MergeEligible;
            public float Timer;
            public float LockUntil;
            public bool MustLeave;
            public float HeldLostSince;
        }

        public void Initialize(LevelController owner)
        {
            level = owner;
        }

        private void Update()
        {
            if (level == null) return;
            if (level.State != LevelState.Playing)
            {
                level.Input.SetProgress(HandSide.Left, 0f);
                level.Input.SetProgress(HandSide.Right, 0f);
                return;
            }

            ProcessHand(HandSide.Left);
            ProcessHand(HandSide.Right);
            UpdatePuzzleMerge();
        }

        public void ResetInteractions()
        {
            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i];
                state.Held = null;
                ResetHover(state, (HandSide)i);
                state.LockUntil = 0f;
                state.MustLeave = false;
                state.HeldLostSince = 0f;
            }
            mergeTime = 0f;
            level.Input.SetHolding(HandSide.Left, false);
            level.Input.SetHolding(HandSide.Right, false);
            level.Input.SetProgress(HandSide.Left, 0f);
            level.Input.SetProgress(HandSide.Right, 0f);
        }

        private void ProcessHand(HandSide side)
        {
            var state = states[(int)side];
            var hand = level.Input.Get(side);
            level.Input.SetHolding(side, state.Held != null);

            if (state.Held != null && hand.Valid)
            {
                state.Held.transform.position = hand.Position + new Vector2(0f, 0.32f);
            }

            if (!hand.Valid)
            {
                ResetHover(state, side);
                if (state.Held != null)
                {
                    if (state.HeldLostSince <= 0f) state.HeldLostSince = Time.time;
                    if (Time.time - state.HeldLostSince > 1.0f)
                    {
                        var lostItem = state.Held;
                        state.Held = null;
                        level.Feedback.DropForLostHand(side, lostItem);
                        LockAction(side, state);
                    }
                }
                return;
            }
            state.HeldLostSince = 0f;

            if (hand.IsWaving)
            {
                ResetHover(state, side);
                return;
            }

            if (Time.unscaledTime < state.LockUntil)
            {
                ResetHover(state, side);
                return;
            }

            if (state.Held == null)
            {
                ProcessEmptyHand(side, state, hand);
            }
            else
            {
                ProcessHoldingHand(side, state, hand);
            }
        }

        private void ProcessEmptyHand(HandSide side, InteractionState state, HandRuntimeState hand)
        {
            var item = FindItemAt(hand.Position, side);
            if (state.MustLeave)
            {
                if (item != null || level.FindNormalTargetAt(hand.Position) != null || level.Puzzle.ContainsAny(hand.Position))
                {
                    ResetHover(state, side);
                    return;
                }
                state.MustLeave = false;
            }

            if (item == null)
            {
                ResetHover(state, side);
                return;
            }

            if (!item.CanBePickedBy(side))
            {
                level.Feedback.Hint(side, item.Kind == ItemKind.LeftFragment ? "左碎片请使用左手" : "右碎片请使用右手", true);
                ResetHover(state, side);
                return;
            }

            if (state.HoverItem != item)
            {
                state.HoverItem = item;
                state.Timer = 0f;
            }
            state.HoverTarget = null;
            state.HoverPuzzle = false;
            state.Timer += Time.deltaTime;
            level.Input.SetProgress(side, state.Timer / HoverDuration);

            if (state.Timer >= HoverDuration)
            {
                PickUp(side, state, item);
            }
        }

        private void ProcessHoldingHand(HandSide side, InteractionState state, HandRuntimeState hand)
        {
            var item = state.Held;
            var normalTarget = level.FindNormalTargetAt(hand.Position);

            if (item.IsFragment)
            {
                var correctSlot = item.Kind == ItemKind.LeftFragment
                    ? level.Puzzle.ContainsLeft(hand.Position)
                    : level.Puzzle.ContainsRight(hand.Position);
                state.MergeEligible = correctSlot && normalTarget == null;

                if (normalTarget != null)
                {
                    ProcessNormalTarget(side, state, item, normalTarget);
                }
                else if (level.Puzzle.ContainsAny(hand.Position) && !correctSlot)
                {
                    ProcessWrongPuzzle(side, state, item);
                }
                else if (!correctSlot)
                {
                    ResetHover(state, side);
                }
                return;
            }

            state.MergeEligible = false;
            if (normalTarget != null)
            {
                ProcessNormalTarget(side, state, item, normalTarget);
            }
            else if (level.Puzzle.ContainsAny(hand.Position))
            {
                ProcessWrongPuzzle(side, state, item);
            }
            else
            {
                ResetHover(state, side);
            }
        }

        private void ProcessNormalTarget(HandSide side, InteractionState state, StorageItem item, DropTarget target)
        {
            if (state.HoverTarget != target || state.HoverPuzzle)
            {
                state.HoverTarget = target;
                state.HoverPuzzle = false;
                state.HoverItem = null;
                state.Timer = 0f;
            }

            state.Timer += Time.deltaTime;
            level.Input.SetProgress(side, state.Timer / HoverDuration);
            if (state.Timer < HoverDuration) return;

            if (target.CanAccept(item))
            {
                target.Place(item);
                state.Held = null;
                level.Feedback.Correct(side, item, target);
            }
            else
            {
                var reason = target.Occupant != null ? "该位置已占用" : "目标不匹配";
                state.Held = null;
                level.Feedback.Wrong(side, item, reason, target);
            }
            LockAction(side, state);
        }

        private void ProcessWrongPuzzle(HandSide side, InteractionState state, StorageItem item)
        {
            if (!state.HoverPuzzle || state.HoverTarget != null)
            {
                state.HoverPuzzle = true;
                state.HoverTarget = null;
                state.HoverItem = null;
                state.Timer = 0f;
            }
            state.Timer += Time.deltaTime;
            level.Input.SetProgress(side, state.Timer / HoverDuration);
            if (state.Timer < HoverDuration) return;

            state.Held = null;
            var reason = item.IsFragment ? "左右碎片位置不匹配" : "这里需要双手碎片";
            level.Feedback.WrongPuzzle(side, item, reason, level.Puzzle);
            LockAction(side, state);
        }

        private void UpdatePuzzleMerge()
        {
            if (level.Puzzle.IsComplete)
            {
                mergeTime = 0f;
                level.Puzzle.SetProgress(1f);
                return;
            }

            var left = states[0];
            var right = states[1];
            var leftHand = level.Input.Left;
            var rightHand = level.Input.Right;
            var bothReady = left.Held != null && left.Held.Kind == ItemKind.LeftFragment && left.MergeEligible &&
                            right.Held != null && right.Held.Kind == ItemKind.RightFragment && right.MergeEligible &&
                            leftHand.Valid && rightHand.Valid && !leftHand.IsWaving && !rightHand.IsWaving;

            if (bothReady)
            {
                mergeTime += Time.deltaTime;
                var progress = mergeTime / HoverDuration;
                level.Input.SetProgress(HandSide.Left, progress);
                level.Input.SetProgress(HandSide.Right, progress);
                level.Puzzle.SetProgress(progress);

                if (mergeTime >= HoverDuration)
                {
                    var leftItem = left.Held;
                    var rightItem = right.Held;
                    left.Held = null;
                    right.Held = null;
                    level.Puzzle.SetComplete(leftItem, rightItem);
                    level.Feedback.PuzzleCorrect(level.Puzzle);
                    LockAction(HandSide.Left, left);
                    LockAction(HandSide.Right, right);
                    mergeTime = 0f;
                }
            }
            else
            {
                mergeTime = 0f;
                level.Puzzle.SetProgress(0f);
                if (left.MergeEligible) level.Input.SetProgress(HandSide.Left, 0f);
                if (right.MergeEligible) level.Input.SetProgress(HandSide.Right, 0f);
            }
        }

        private void PickUp(HandSide side, InteractionState state, StorageItem item)
        {
            if (item.CurrentDropTarget != null)
            {
                item.CurrentDropTarget.Remove(item);
            }
            if (item.CurrentPuzzleTarget != null)
            {
                level.RemovePuzzlePiece(item);
            }
            item.SetHeld(side);
            state.Held = item;
            state.MustLeave = false;
            ResetHover(state, side);
            level.Feedback.Hint(side, "已拾取 " + item.DisplayName);
        }

        private void LockAction(HandSide side, InteractionState state)
        {
            state.LockUntil = Time.unscaledTime + 0.35f;
            state.MustLeave = true;
            ResetHover(state, side);
        }

        private void ResetHover(InteractionState state, HandSide side)
        {
            state.HoverItem = null;
            state.HoverTarget = null;
            state.HoverPuzzle = false;
            state.Timer = 0f;
            state.MergeEligible = false;
            level.Input.SetProgress(side, 0f);
        }

        private StorageItem FindItemAt(Vector2 position, HandSide side)
        {
            StorageItem nearest = null;
            var nearestDistance = float.MaxValue;
            var nearestCompatible = false;
            foreach (var item in level.Items)
            {
                if (item.State != ItemState.OnBelt && item.State != ItemState.Placed) continue;
                if (!item.Contains(position)) continue;
                var compatible = item.CanBePickedBy(side);
                var distance = ((Vector2)item.transform.position - position).sqrMagnitude;
                if (nearest == null || (compatible && !nearestCompatible) ||
                    (compatible == nearestCompatible && distance < nearestDistance))
                {
                    nearest = item;
                    nearestDistance = distance;
                    nearestCompatible = compatible;
                }
            }
            return nearest;
        }
    }
}
