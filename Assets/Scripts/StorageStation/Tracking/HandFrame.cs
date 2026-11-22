using UnityEngine;

namespace StorageStation
{
    public enum HandSide
    {
        Left,
        Right
    }

    public struct HandSample
    {
        public bool Valid;
        public Vector2 Palm;
        public Vector2 Direction;
    }

    public struct HandFrame
    {
        public long Timestamp;
        public HandSample Left;
        public HandSample Right;
    }
}
