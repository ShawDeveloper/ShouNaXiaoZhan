using System.Collections;
using UnityEngine;

namespace StorageStation
{
    public sealed class FeedbackController : MonoBehaviour
    {
        private static readonly Color SuccessColor = new Color(0.12f, 0.65f, 0.35f);
        private static readonly Color ErrorColor = new Color(0.88f, 0.16f, 0.2f);
        private GameHud hud;
        private readonly float[] nextHintTime = new float[2];

        public void Initialize(GameHud gameHud)
        {
            hud = gameHud;
        }

        public void Correct(HandSide side, StorageItem item, DropTarget target)
        {
            target.Flash(SuccessColor);
            hud.ShowMessage("放置正确：" + item.DisplayName, SuccessColor);
            hud.ShowHint(side, "放置正确", SuccessColor);
        }

        public void PuzzleCorrect(PuzzleTarget target)
        {
            target.Flash(new Color(0.95f, 0.72f, 0.15f));
            hud.ShowMessage("双手拼合成功！", SuccessColor, 1.5f);
            hud.ShowHint(HandSide.Left, "拼合成功", SuccessColor, 1.3f);
            hud.ShowHint(HandSide.Right, "拼合成功", SuccessColor, 1.3f);
        }

        public void Wrong(HandSide side, StorageItem item, string reason, DropTarget target)
        {
            if (item.State != ItemState.Held) return;
            if (target != null) target.Flash(ErrorColor);
            hud.ShowMessage(reason + "，物品已掉落", ErrorColor);
            hud.ShowHint(side, reason, ErrorColor);
            item.BeginDrop();
            StartCoroutine(DropAndReturn(item));
        }

        public void WrongPuzzle(HandSide side, StorageItem item, string reason, PuzzleTarget target)
        {
            if (item.State != ItemState.Held) return;
            if (target != null) target.Flash(ErrorColor);
            hud.ShowMessage(reason + "，物品已掉落", ErrorColor);
            hud.ShowHint(side, reason, ErrorColor);
            item.BeginDrop();
            StartCoroutine(DropOffscreenAndRecycle(item));
        }

        public void Hint(HandSide side, string text, bool error = false)
        {
            var index = (int)side;
            if (Time.unscaledTime < nextHintTime[index]) return;
            nextHintTime[index] = Time.unscaledTime + 1.1f;
            hud.ShowHint(side, text, error ? ErrorColor : SuccessColor, 1.0f);
        }

        public void DropForLostHand(HandSide side, StorageItem item)
        {
            if (item.State != ItemState.Held) return;
            hud.ShowMessage("手部跟踪丢失，物品已退回", ErrorColor);
            hud.ShowHint(side, "跟踪丢失，请重新拾取", ErrorColor);
            item.BeginDrop();
            StartCoroutine(DropAndReturn(item));
        }

        public void ResetFeedback()
        {
            StopAllCoroutines();
            nextHintTime[0] = nextHintTime[1] = 0f;
        }

        private IEnumerator DropOffscreenAndRecycle(StorageItem item)
        {
            var start = (Vector2)item.transform.position;
            const float duration = 0.75f;
            var time = 0f;
            while (time < duration && item.State == ItemState.Dropping)
            {
                time += Time.deltaTime;
                var t = Mathf.Clamp01(time / duration);
                var x = Mathf.Lerp(start.x, start.x + 0.35f, t);
                var y = Mathf.Lerp(start.y, -7.2f, t * t);
                item.transform.position = new Vector2(x, y);
                item.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.75f, t);
                yield return null;
            }
            if (item.State == ItemState.Dropping) item.BeginTransit(2f);
        }

        private IEnumerator DropAndReturn(StorageItem item)
        {
            var start = (Vector2)item.transform.position;
            const float duration = 0.55f;
            var time = 0f;
            while (time < duration && item.State == ItemState.Dropping)
            {
                time += Time.deltaTime;
                var t = Mathf.Clamp01(time / duration);
                var x = start.x + Mathf.Sin(t * Mathf.PI) * 0.22f;
                var y = start.y - 1.45f * t * t;
                item.transform.position = new Vector2(x, y);
                item.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.88f, t);
                yield return null;
            }
            if (item.State == ItemState.Dropping) item.BeginReturn();
        }
    }
}
