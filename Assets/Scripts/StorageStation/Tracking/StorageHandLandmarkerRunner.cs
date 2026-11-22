using System.Collections;
using System.Collections.Concurrent;
using Mediapipe;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;
using UnityEngine.Rendering;

namespace StorageStation
{
    public sealed class StorageHandLandmarkerRunner : VisionTaskApiRunner<HandLandmarker>
    {
        [SerializeField] private bool swapHandedness;
        [SerializeField] private float inferenceInterval = 0.05f;

        private readonly ConcurrentQueue<HandFrame> frames = new ConcurrentQueue<HandFrame>();
        private readonly HandLandmarkDetectionConfig config = new HandLandmarkDetectionConfig();
        private Mediapipe.Unity.Experimental.TextureFramePool textureFramePool;
        private float nextInferenceTime;
        private int readbackFailures;

        public bool IsReady { get; private set; }
        public string LastError { get; private set; }
        public string Status { get; private set; } = "等待摄像头初始化";

        private void Awake()
        {
            config.NumHands = 2;
            config.ImageReadMode = ImageReadMode.CPUAsync;
            config.RunningMode = Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM;
        }

        public bool TryGetLatestFrame(out HandFrame frame)
        {
            frame = default;
            var found = false;
            while (frames.TryDequeue(out var candidate))
            {
                frame = candidate;
                found = true;
            }
            return found;
        }

        public void RestartTracking()
        {
            if (ImageSourceProvider.ImageSource == null)
            {
                Status = "插件仍在初始化，请稍候";
                return;
            }
            Stop();
            LastError = null;
            Status = "正在重新启动摄像头";
            Play();
        }

        public override void Stop()
        {
            IsReady = false;
            base.Stop();
            textureFramePool?.Dispose();
            textureFramePool = null;
            while (frames.TryDequeue(out _)) { }
        }

        protected override IEnumerator Run()
        {
            IsReady = false;
            LastError = null;
            Status = "正在加载手部模型";
            nextInferenceTime = 0f;
            readbackFailures = 0;

            yield return AssetLoader.PrepareAssetAsync(config.ModelPath);

            var options = config.GetHandLandmarkerOptions(OnHandLandmarkDetectionOutput);
            taskApi = HandLandmarker.CreateFromOptions(options, GpuManager.GpuResources);
            var imageSource = ImageSourceProvider.ImageSource;

            Status = "正在启动摄像头";
            yield return imageSource.Play();

            if (!imageSource.isPrepared)
            {
                LastError = "摄像头启动失败，请检查设备与 Windows 隐私权限";
                Status = LastError;
                Debug.LogError(LastError);
                yield break;
            }

            textureFramePool = new Mediapipe.Unity.Experimental.TextureFramePool(
                imageSource.textureWidth,
                imageSource.textureHeight,
                TextureFormat.RGBA32,
                6);

            var transformationOptions = imageSource.GetTransformationOptions(expectedToBeMirrored: true);
            var flipHorizontally = transformationOptions.flipHorizontally;
            var flipVertically = transformationOptions.flipVertically;
            var imageProcessingOptions = new Mediapipe.Tasks.Vision.Core.ImageProcessingOptions(
                rotationDegrees: (int)transformationOptions.rotationAngle);

            AsyncGPUReadbackRequest request = default;
            var waitUntilRequestDone = new WaitUntil(() => request.done);
            var waitForEndOfFrame = new WaitForEndOfFrame();
            var canUseGpuImage = SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3 && GpuManager.GpuResources != null;
            using var glContext = canUseGpuImage ? GpuManager.GetGlContext() : null;

            IsReady = true;
            Status = "双手追踪已就绪";

            while (true)
            {
                if (isPaused)
                {
                    yield return new WaitWhile(() => isPaused);
                }

                if (Time.unscaledTime < nextInferenceTime)
                {
                    yield return null;
                    continue;
                }

                if (!textureFramePool.TryGetTextureFrame(out var textureFrame))
                {
                    yield return waitForEndOfFrame;
                    continue;
                }

                Image image;
                if (config.ImageReadMode == ImageReadMode.CPUAsync)
                {
                    request = textureFrame.ReadTextureAsync(
                        imageSource.GetCurrentTexture(),
                        flipHorizontally,
                        flipVertically);
                    yield return waitUntilRequestDone;
                    if (request.hasError)
                    {
                        textureFrame.Release();
                        readbackFailures++;
                        Debug.LogWarning("读取摄像头画面失败");
                        if (readbackFailures >= 10)
                        {
                            LastError = "连续读取摄像头画面失败，请重试";
                            Status = LastError;
                            IsReady = false;
                            yield break;
                        }
                        continue;
                    }
                    readbackFailures = 0;
                    image = textureFrame.BuildCPUImage();
                    textureFrame.Release();
                }
                else
                {
                    yield return waitForEndOfFrame;
                    textureFrame.ReadTextureOnCPU(
                        imageSource.GetCurrentTexture(),
                        flipHorizontally,
                        flipVertically);
                    image = textureFrame.BuildCPUImage();
                    textureFrame.Release();
                }

                taskApi.DetectAsync(image, GetCurrentTimestampMillisec(), imageProcessingOptions);
                nextInferenceTime = Time.unscaledTime + Mathf.Max(0.02f, inferenceInterval);
            }
        }

        private void OnHandLandmarkDetectionOutput(HandLandmarkerResult result, Image image, long timestamp)
        {
            var frame = new HandFrame { Timestamp = timestamp };
            if (result.handLandmarks == null)
            {
                frames.Enqueue(frame);
                return;
            }

            var duplicateLeft = false;
            var duplicateRight = false;
            var count = result.handLandmarks.Count;
            for (var i = 0; i < count; i++)
            {
                var landmarks = result.handLandmarks[i].landmarks;
                if (landmarks == null || landmarks.Count < 18)
                {
                    continue;
                }

                var isLeft = true;
                if (result.handedness != null && i < result.handedness.Count)
                {
                    var categories = result.handedness[i].categories;
                    if (categories != null && categories.Count > 0)
                    {
                        isLeft = categories[0].categoryName == "Left";
                    }
                }
                if (swapHandedness)
                {
                    isLeft = !isLeft;
                }

                var palm = Vector2.zero;
                int[] palmIndices = { 0, 5, 9, 13, 17 };
                foreach (var index in palmIndices)
                {
                    palm += new Vector2(landmarks[index].x, landmarks[index].y);
                }
                palm /= palmIndices.Length;

                var direction = new Vector2(
                    landmarks[9].x - landmarks[0].x,
                    landmarks[9].y - landmarks[0].y);
                var sample = new HandSample { Valid = true, Palm = palm, Direction = direction };

                if (isLeft)
                {
                    if (frame.Left.Valid) duplicateLeft = true;
                    frame.Left = sample;
                }
                else
                {
                    if (frame.Right.Valid) duplicateRight = true;
                    frame.Right = sample;
                }
            }

            if (duplicateLeft) frame.Left.Valid = false;
            if (duplicateRight) frame.Right.Valid = false;
            frames.Enqueue(frame);
        }

        private void OnDestroy()
        {
            if (taskApi != null)
            {
                Stop();
            }
            else
            {
                textureFramePool?.Dispose();
                textureFramePool = null;
            }
        }
    }
}
