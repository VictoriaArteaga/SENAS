using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Mediapipe;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity.Experimental;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Senas.AI.MediaPipe
{
  /// <summary>
  /// Captures up to two hands from the first available webcam and publishes
  /// copied landmark snapshots on Unity's main thread.
  /// </summary>
  public sealed class HandLandmarkerCapture : MonoBehaviour
  {
    private const string ModelRelativePath = "MediaPipe/hand_landmarker.bytes";

    [SerializeField, Min(1)] private int _maxHands = 2;
    [SerializeField, Min(16)] private int _cameraWidth = 640;
    [SerializeField, Min(16)] private int _cameraHeight = 480;
    [SerializeField, Min(1)] private int _cameraFrameRate = 30;
    private WebCamTexture _sharedWebCamTexture;
    private bool _ownsWebCamTexture;

    /// <summary>
    /// Invoked on the Unity main thread with the latest completed result.
    /// An empty Hands collection means that no hand was detected in that result.
    /// </summary>
    public event Action<HandLandmarkFrame> LandmarksUpdated;

    /// <summary>
    /// Textura de webcam usada para inferencia y vista previa opcional.
    /// </summary>
    public WebCamTexture CameraTexture => _webCamTexture;

    /// <summary>Usa la textura que ya muestra la interfaz para evitar abrir la webcam dos veces.</summary>
    public void UseSharedCameraTexture(WebCamTexture texture)
    {
      if (isActiveAndEnabled && _webCamTexture != null)
        throw new InvalidOperationException("La fuente de cámara debe configurarse antes de iniciar la captura.");
      _sharedWebCamTexture = texture;
    }

    private WebCamTexture _webCamTexture;
    private HandLandmarker _handLandmarker;
    private TextureFramePool _textureFramePool;
    private readonly object _resultLock = new object();
    private HandLandmarkFrame _pendingResult;
    private long _lastTimestampMilliseconds;
    private bool _cameraRestartWarningLogged;

    private void OnEnable()
    {
      if (Application.isPlaying) StartCoroutine(InitializeAfterEnable());
    }

    private IEnumerator InitializeAfterEnable()
    {
      // AddComponent callers can assign a shared webcam immediately after OnEnable.
      yield return null;
      yield return InitializeAndCapture();
    }

    private IEnumerator InitializeAndCapture()
    {
      var devices = WebCamTexture.devices;
      if (_sharedWebCamTexture == null && (devices == null || devices.Length == 0))
      {
        Debug.LogError("MediaPipe hand capture: no webcam is available.", this);
        yield break;
      }

      var modelPath = Path.Combine(Application.streamingAssetsPath, ModelRelativePath);
      if (!File.Exists(modelPath))
      {
        Debug.LogError($"MediaPipe hand model was not found: {modelPath}", this);
        yield break;
      }

      byte[] modelBytes;
      try
      {
        modelBytes = File.ReadAllBytes(modelPath);
      }
      catch (Exception exception)
      {
        Debug.LogException(exception, this);
        yield break;
      }

      if (!TryCreateLandmarker(modelBytes))
      {
        yield break;
      }

      if (_sharedWebCamTexture != null)
      {
        _webCamTexture = _sharedWebCamTexture;
      }
      else
      {
        _webCamTexture = new WebCamTexture(devices[0].name, _cameraWidth, _cameraHeight, _cameraFrameRate);
        _ownsWebCamTexture = true;
        _webCamTexture.Play();
      }

      var timeoutFrames = 2000;
      while (_webCamTexture != null && (_webCamTexture.width <= 16 || !_webCamTexture.isPlaying) && timeoutFrames-- > 0)
      {
        if (!_webCamTexture.isPlaying) _webCamTexture.Play();
        yield return null;
      }

      if (_webCamTexture == null || _webCamTexture.width <= 16)
      {
        Debug.LogError("MediaPipe hand capture: the webcam did not start.", this);
        StopWebcam();
        yield break;
      }

      var waitForEndOfFrame = new WaitForEndOfFrame();

      while (isActiveAndEnabled && _webCamTexture != null)
      {
        if (!_webCamTexture.isPlaying || _webCamTexture.width <= 16)
        {
          _webCamTexture.Play();
          var restartTimeout = 300;
          while (isActiveAndEnabled && _webCamTexture != null && (!_webCamTexture.isPlaying || _webCamTexture.width <= 16) && restartTimeout-- > 0)
            yield return null;
          if (_webCamTexture == null || !_webCamTexture.isPlaying || _webCamTexture.width <= 16)
          {
            if (!_cameraRestartWarningLogged)
            {
              Debug.LogWarning("MediaPipe hand capture: webcam paused; retrying camera startup.", this);
              _cameraRestartWarningLogged = true;
            }
            yield return new WaitForSecondsRealtime(1f);
            continue;
          }
          _cameraRestartWarningLogged = false;
        }

        if (_textureFramePool == null)
          _textureFramePool = new TextureFramePool(_webCamTexture.width, _webCamTexture.height, TextureFormat.RGBA32, 3);
        if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
        {
          yield return waitForEndOfFrame;
          continue;
        }

        yield return waitForEndOfFrame;

        textureFrame.ReadTextureOnCPU(_webCamTexture, flipHorizontally: false, flipVertically: _webCamTexture.videoVerticallyMirrored);
        var image = textureFrame.BuildCPUImage();
        textureFrame.Release();
        var imageProcessingOptions = new ImageProcessingOptions(rotationDegrees: _webCamTexture.videoRotationAngle);
        _handLandmarker.DetectAsync(image, GetNextTimestampMilliseconds(), imageProcessingOptions);
      }
    }

    private void Update()
    {
      HandLandmarkFrame result;
      lock (_resultLock)
      {
        result = _pendingResult;
        _pendingResult = null;
      }

      if (result != null)
      {
        LandmarksUpdated?.Invoke(result);
      }
    }

    private void OnDisable()
    {
      StopAllCoroutines();
      StopWebcam();

      if (_handLandmarker != null)
      {
        try
        {
          ((IDisposable)_handLandmarker).Dispose();
        }
        catch (Exception exception)
        {
          Debug.LogException(exception, this);
        }
        _handLandmarker = null;
      }

      _textureFramePool?.Dispose();
      _textureFramePool = null;

      lock (_resultLock)
      {
        _pendingResult = null;
      }
    }

    private bool TryCreateLandmarker(byte[] modelBytes)
    {
      try
      {
        ResourceUtil.EnableCustomResolver();
        var options = new HandLandmarkerOptions(
          new BaseOptions(BaseOptions.Delegate.CPU, modelAssetBuffer: modelBytes),
          runningMode: RunningMode.LIVE_STREAM,
          numHands: Mathf.Max(1, _maxHands),
          resultCallback: OnHandLandmarkerResult
        );
        _handLandmarker = HandLandmarker.CreateFromOptions(options);
        return true;
      }
      catch (Exception exception)
      {
        Debug.LogException(exception, this);
        return false;
      }
    }

    private void OnHandLandmarkerResult(HandLandmarkerResult result, Image image, long timestampMilliseconds)
    {
      var hands = new List<DetectedHandLandmarks>();
      var landmarkLists = result.handLandmarks;

      if (landmarkLists != null)
      {
        for (var handIndex = 0; handIndex < landmarkLists.Count; handIndex++)
        {
          var normalized = CopyNormalizedLandmarks(landmarkLists[handIndex].landmarks);
          var world = result.handWorldLandmarks != null && handIndex < result.handWorldLandmarks.Count
            ? CopyWorldLandmarks(result.handWorldLandmarks[handIndex].landmarks)
            : Array.AsReadOnly(Array.Empty<HandLandmarkPoint>());

          var handedness = string.Empty;
          var handednessConfidence = 0f;
          if (result.handedness != null && handIndex < result.handedness.Count)
          {
            var categories = result.handedness[handIndex].categories;
            if (categories != null && categories.Count > 0)
            {
              handedness = categories[0].categoryName ?? categories[0].displayName ?? string.Empty;
              handednessConfidence = categories[0].score;
            }
          }

          hands.Add(new DetectedHandLandmarks(handedness, handednessConfidence, normalized, world));
        }
      }

      var snapshot = new HandLandmarkFrame(timestampMilliseconds, hands);
      lock (_resultLock)
      {
        _pendingResult = snapshot;
      }
    }

    private static IReadOnlyList<HandLandmarkPoint> CopyNormalizedLandmarks(List<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> landmarks)
    {
      if (landmarks == null)
      {
        return Array.AsReadOnly(Array.Empty<HandLandmarkPoint>());
      }

      var copy = new HandLandmarkPoint[landmarks.Count];
      for (var index = 0; index < landmarks.Count; index++)
      {
        var landmark = landmarks[index];
        copy[index] = new HandLandmarkPoint(landmark.x, landmark.y, landmark.z);
      }
      return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<HandLandmarkPoint> CopyWorldLandmarks(List<Mediapipe.Tasks.Components.Containers.Landmark> landmarks)
    {
      if (landmarks == null)
      {
        return Array.AsReadOnly(Array.Empty<HandLandmarkPoint>());
      }

      var copy = new HandLandmarkPoint[landmarks.Count];
      for (var index = 0; index < landmarks.Count; index++)
      {
        var landmark = landmarks[index];
        copy[index] = new HandLandmarkPoint(landmark.x, landmark.y, landmark.z);
      }
      return Array.AsReadOnly(copy);
    }

    private long GetNextTimestampMilliseconds()
    {
      var timestamp = Stopwatch.GetTimestamp() * 1000L / Stopwatch.Frequency;
      if (timestamp <= _lastTimestampMilliseconds)
      {
        timestamp = _lastTimestampMilliseconds + 1;
      }
      _lastTimestampMilliseconds = timestamp;
      return timestamp;
    }

    private void StopWebcam()
    {
      if (_webCamTexture != null && _ownsWebCamTexture)
      {
        _webCamTexture.Stop();
        Destroy(_webCamTexture);
      }
      _webCamTexture = null;
      _ownsWebCamTexture = false;
    }
  }

  public readonly struct HandLandmarkPoint
  {
    public readonly float X;
    public readonly float Y;
    public readonly float Z;

    public HandLandmarkPoint(float x, float y, float z)
    {
      X = x;
      Y = y;
      Z = z;
    }
  }

  public sealed class DetectedHandLandmarks
  {
    public string Handedness { get; }
    public float HandednessConfidence { get; }
    public IReadOnlyList<HandLandmarkPoint> NormalizedLandmarks { get; }
    public IReadOnlyList<HandLandmarkPoint> WorldLandmarks { get; }

    internal DetectedHandLandmarks(
      string handedness,
      float handednessConfidence,
      IReadOnlyList<HandLandmarkPoint> normalizedLandmarks,
      IReadOnlyList<HandLandmarkPoint> worldLandmarks)
    {
      Handedness = handedness;
      HandednessConfidence = handednessConfidence;
      NormalizedLandmarks = normalizedLandmarks;
      WorldLandmarks = worldLandmarks;
    }
  }

  public sealed class HandLandmarkFrame
  {
    public long TimestampMilliseconds { get; }
    public IReadOnlyList<DetectedHandLandmarks> Hands { get; }

    internal HandLandmarkFrame(long timestampMilliseconds, IList<DetectedHandLandmarks> hands)
    {
      TimestampMilliseconds = timestampMilliseconds;
      Hands = Array.AsReadOnly(new List<DetectedHandLandmarks>(hands).ToArray());
    }
  }
}
