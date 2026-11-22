using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StorageStation
{
    public sealed class HandCursorView : MonoBehaviour
    {
        private readonly List<SpriteRenderer> coloredParts = new List<SpriteRenderer>();
        private Transform gloveVisual;
        private Image progressImage;
        private Color handColor;

        public void Build(HandSide side)
        {
            handColor = side == HandSide.Left
                ? new Color(0.15f, 0.72f, 0.95f)
                : new Color(0.98f, 0.45f, 0.62f);

            gloveVisual = new GameObject(side + "GloveVisual").transform;
            gloveVisual.SetParent(transform, false);

            Geometry.Create(gloveVisual, "PalmOutline", PrimitiveShape.Circle, Vector2.zero,
                new Vector2(0.92f, 1.02f), new Color(0.08f, 0.09f, 0.13f), 101);
            coloredParts.Add(Geometry.Create(gloveVisual, "Palm", PrimitiveShape.Circle, Vector2.zero,
                new Vector2(0.78f, 0.88f), handColor, 102));

            for (var i = 0; i < 4; i++)
            {
                var x = -0.3f + i * 0.2f;
                var height = i == 1 || i == 2 ? 0.78f : 0.66f;
                Geometry.Create(gloveVisual, "FingerOutline" + i, PrimitiveShape.Square,
                    new Vector2(x, 0.53f), new Vector2(0.2f, height), new Color(0.08f, 0.09f, 0.13f), 100);
                coloredParts.Add(Geometry.Create(gloveVisual, "Finger" + i, PrimitiveShape.Square,
                    new Vector2(x, 0.55f), new Vector2(0.14f, height - 0.06f), handColor, 103));
            }

            var thumbOutline = Geometry.Create(gloveVisual, "ThumbOutline", PrimitiveShape.Square,
                new Vector2(-0.48f, 0.02f), new Vector2(0.22f, 0.62f), new Color(0.08f, 0.09f, 0.13f), 101);
            thumbOutline.transform.localRotation = Quaternion.Euler(0f, 0f, 42f);
            var thumb = Geometry.Create(gloveVisual, "Thumb", PrimitiveShape.Square,
                new Vector2(-0.47f, 0.03f), new Vector2(0.15f, 0.52f), handColor, 103);
            thumb.transform.localRotation = Quaternion.Euler(0f, 0f, 42f);
            coloredParts.Add(thumb);

            var canvasObject = new GameObject(side + "ProgressCanvas");
            canvasObject.transform.SetParent(transform, false);
            canvasObject.transform.localPosition = new Vector3(0f, 0f, -0.2f);
            canvasObject.transform.localScale = Vector3.one * 0.016f;
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 210;
            var rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(100f, 100f);

            var imageObject = new GameObject("ProgressRing");
            imageObject.transform.SetParent(canvasObject.transform, false);
            var imageRect = imageObject.AddComponent<RectTransform>();
            imageRect.sizeDelta = new Vector2(100f, 100f);
            progressImage = imageObject.AddComponent<Image>();
            progressImage.sprite = Geometry.GetSprite(PrimitiveShape.Ring);
            progressImage.type = Image.Type.Filled;
            progressImage.fillMethod = Image.FillMethod.Radial360;
            progressImage.fillOrigin = (int)Image.Origin360.Top;
            progressImage.fillClockwise = true;
            progressImage.color = new Color(1f, 0.85f, 0.2f, 1f);
            progressImage.fillAmount = 0f;
            progressImage.raycastTarget = false;
        }

        public void SetState(Vector2 position, float angle, bool valid, float progress, bool holding)
        {
            gameObject.SetActive(valid);
            if (!valid) return;
            transform.position = position;
            gloveVisual.localRotation = Quaternion.Euler(0f, 0f, angle);
            progressImage.fillAmount = Mathf.Clamp01(progress);
            progressImage.enabled = progress > 0.001f;
            foreach (var part in coloredParts)
            {
                part.color = holding ? new Color(1f, 0.78f, 0.22f) : handColor;
            }
        }
    }
}
