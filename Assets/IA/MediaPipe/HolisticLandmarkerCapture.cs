using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Mediapipe;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.HolisticLandmarker;
using Mediapipe.Unity.Experimental;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Senas.AI.MediaPipe
{
  /// <summary>Captura en una sola pasada las dos manos, la cara y el torso para evaluar saludos.</summary>
  public sealed class HolisticLandmarkerCapture : MonoBehaviour
  {
    private const string ModelRelativePath = "MediaPipe/holistic_landmarker.task";
    private static readonly int[] FaceAnchorIndices = { 10, 152, 234, 454 };
    private static readonly int[] BodyAnchorIndices = { 0, 7, 8, 11, 12, 13, 14, 15, 16 };
    private WebCamTexture _sharedTexture;
    private WebCamTexture _texture;
    private bool _ownsTexture;
    private HolisticLandmarker _landmarker;
    private TextureFramePool _framePool;
    private long _lastTimestamp;
    private readonly object _resultLock = new object();
    private HolisticLandmarkObservation _pending;
    private bool _cameraRestartWarningLogged;

    public event Action<HolisticLandmarkObservation> LandmarksUpdated;
    public WebCamTexture CameraTexture => _texture;
    public void UseSharedCameraTexture(WebCamTexture texture)
    {
      if (isActiveAndEnabled && _texture != null) throw new InvalidOperationException("Configura la cámara antes de iniciar Holistic.");
      _sharedTexture = texture;
    }

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
      if (_sharedTexture == null && (devices == null || devices.Length == 0)) { Debug.LogError("Holistic: no hay webcam disponible.", this); yield break; }
      var modelPath = Path.Combine(Application.streamingAssetsPath, ModelRelativePath);
      if (!File.Exists(modelPath)) { Debug.LogError($"No se encontró el modelo Holistic: {modelPath}", this); yield break; }
      byte[] model;
      try { model = File.ReadAllBytes(modelPath); }
      catch (Exception exception) { Debug.LogException(exception, this); yield break; }

      try
      {
        ResourceUtil.EnableCustomResolver();
        var options = new HolisticLandmarkerOptions(new BaseOptions(BaseOptions.Delegate.CPU, modelAssetBuffer: model),
          runningMode: RunningMode.LIVE_STREAM, resultCallback: OnResult);
        _landmarker = HolisticLandmarker.CreateFromOptions(options);
      }
      catch (Exception exception) { Debug.LogException(exception, this); yield break; }

      if (_sharedTexture != null) _texture = _sharedTexture;
      else
      {
        _texture = new WebCamTexture(devices[0].name, 640, 480, 30);
        _ownsTexture = true;
        _texture.Play();
      }
      var timeout = 2000;
      while (_texture != null && (_texture.width <= 16 || !_texture.isPlaying) && timeout-- > 0)
      {
        if (!_texture.isPlaying) _texture.Play();
        yield return null;
      }
      if (_texture == null || _texture.width <= 16) { Debug.LogError("Holistic: la cámara no inició.", this); StopWebcam(); yield break; }

      _framePool = new TextureFramePool(_texture.width, _texture.height, TextureFormat.RGBA32, 3);
      var endOfFrame = new WaitForEndOfFrame();
      while (isActiveAndEnabled && _texture != null)
      {
        if (!_texture.isPlaying || _texture.width <= 16)
        {
          _texture.Play();
          var restartTimeout = 300;
          while (isActiveAndEnabled && _texture != null && (!_texture.isPlaying || _texture.width <= 16) && restartTimeout-- > 0)
            yield return null;
          if (_texture == null || !_texture.isPlaying || _texture.width <= 16)
          {
            if (!_cameraRestartWarningLogged)
            {
              Debug.LogWarning("Holistic capture: webcam paused; retrying camera startup.", this);
              _cameraRestartWarningLogged = true;
            }
            yield return new WaitForSecondsRealtime(1f);
            continue;
          }
          _cameraRestartWarningLogged = false;
        }
        if (!_framePool.TryGetTextureFrame(out var frame)) { yield return endOfFrame; continue; }
        yield return endOfFrame;
        frame.ReadTextureOnCPU(_texture, flipHorizontally: false, flipVertically: _texture.videoVerticallyMirrored);
        var image = frame.BuildCPUImage();
        frame.Release();
        _landmarker.DetectAsync(image, NextTimestamp());
      }
    }

    private void Update()
    {
      HolisticLandmarkObservation snapshot;
      lock (_resultLock) { snapshot = _pending; _pending = null; }
      if (snapshot != null) LandmarksUpdated?.Invoke(snapshot);
    }

    private void OnResult(in HolisticLandmarkerResult result, Image image, long timestamp)
    {
      var left = Copy(result.leftHandLandmarks.landmarks);
      var right = Copy(result.rightHandLandmarks.landmarks);
      var face = Copy(result.faceLandmarks.landmarks);
      var pose = Copy(result.poseLandmarks.landmarks);
      var features = BuildFeatures(left, right, face, pose);
      var snapshot = new HolisticLandmarkObservation(timestamp, features, left.Length == 21, right.Length == 21,
        face.Length > 454, face.Length, pose.Length, pose.Length >= 17);
      lock (_resultLock) _pending = snapshot;
    }

    private static Vector3[] Copy(IReadOnlyList<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> source)
    {
      if (source == null || source.Count == 0) return Array.Empty<Vector3>();
      var result = new Vector3[source.Count];
      for (var i = 0; i < source.Count; i++) result[i] = new Vector3(source[i].x, source[i].y, source[i].z);
      return result;
    }

    private static List<Vector3> BuildFeatures(Vector3[] left, Vector3[] right, Vector3[] face, Vector3[] pose)
    {
      var output = new List<Vector3>(GreetingMotionFrame.FeatureCount);
      var validBody = pose.Length >= 17;
      var shoulderCenter = validBody ? (pose[11] + pose[12]) * 0.5f : Vector3.zero;
      var shoulderScale = validBody ? Vector2.Distance(new Vector2(pose[11].x, pose[11].y), new Vector2(pose[12].x, pose[12].y)) : 0f;
      if (shoulderScale < 0.01f) shoulderScale = 0.2f;
      for (var i = 0; i < 21; i++) output.Add(i < left.Length ? (left[i] - shoulderCenter) / shoulderScale : Vector3.zero);
      for (var i = 0; i < 21; i++) output.Add(i < right.Length ? (right[i] - shoulderCenter) / shoulderScale : Vector3.zero);
      foreach (var index in FaceAnchorIndices)
        output.Add(index < face.Length ? (face[index] - shoulderCenter) / shoulderScale : Vector3.zero);
      foreach (var index in BodyAnchorIndices)
        output.Add(index < pose.Length ? (pose[index] - shoulderCenter) / shoulderScale : Vector3.zero);
      return output;
    }

    private long NextTimestamp()
    {
      var value = Stopwatch.GetTimestamp() * 1000L / Stopwatch.Frequency;
      if (value <= _lastTimestamp) value = _lastTimestamp + 1;
      return _lastTimestamp = value;
    }

    private void OnDisable()
    {
      StopAllCoroutines();
      StopWebcam();
      if (_landmarker != null) { try { ((IDisposable)_landmarker).Dispose(); } catch (Exception exception) { Debug.LogException(exception, this); } _landmarker = null; }
      _framePool?.Dispose(); _framePool = null;
      lock (_resultLock) _pending = null;
    }

    private void StopWebcam()
    {
      if (_texture != null && _ownsTexture) { _texture.Stop(); Destroy(_texture); }
      _texture = null; _ownsTexture = false;
    }
  }

  public sealed class HolisticLandmarkObservation
  {
    public long TimestampMilliseconds { get; }
    public IReadOnlyList<Vector3> Features { get; }
    public bool HasLeftHand { get; }
    public bool HasRightHand { get; }
    public bool HasFace { get; }
    public int FaceLandmarkCount { get; }
    public int PoseLandmarkCount { get; }
    public bool HasHeadAndBody { get; }
    internal HolisticLandmarkObservation(long timestamp, IReadOnlyList<Vector3> features, bool left, bool right, bool face, int faceCount, int poseCount, bool body)
    {
      TimestampMilliseconds = timestamp; Features = features; HasLeftHand = left; HasRightHand = right;
      HasFace = face; FaceLandmarkCount = faceCount; PoseLandmarkCount = poseCount; HasHeadAndBody = body;
    }
  }
}
