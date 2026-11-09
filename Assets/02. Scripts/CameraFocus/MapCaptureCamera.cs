using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 맵 촬영 모드 - 플레이어 표시와 입력을 숨기고 독립 카메라로 전환.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera), typeof(AudioListener), typeof(DungeonFreeCamera))]
public class MapCaptureCamera : MonoBehaviour
{
    [Header("----- 씬 연결 -----")]
    [SerializeField] private Camera _gameCamera;
    [SerializeField] private Transform _heroRoot;
    [SerializeField] private InputHandler _inputHandler;
    [SerializeField] private CameraFocus _cameraFocus;

    [Header("----- 촬영 -----")]
    [SerializeField] private bool _startInCaptureMode = true;
    [SerializeField] private bool _pauseWorld;

    private Camera _captureCamera;
    private AudioListener _listener;
    private DungeonFreeCamera _freeCamera;
    private bool _isCapturing;
    private bool _inputWasEnabled;
    private bool _focusWasEnabled;
    private float _previousTimeScale;
    private CursorLockMode _previousCursorLock;
    private bool _previousCursorVisible;
    private Coroutine _screenshotRoutine;
    private readonly Dictionary<Camera, bool> _cameras = new Dictionary<Camera, bool>();
    private readonly Dictionary<AudioListener, bool> _listeners = new Dictionary<AudioListener, bool>();
    private readonly Dictionary<Renderer, bool> _renderers = new Dictionary<Renderer, bool>();
    private readonly Dictionary<Canvas, bool> _canvases = new Dictionary<Canvas, bool>();

    private void Awake()
    {
        _captureCamera = GetComponent<Camera>();
        _listener = GetComponent<AudioListener>();
        _freeCamera = GetComponent<DungeonFreeCamera>();
        _captureCamera.enabled = false;
        _listener.enabled = false;
        _freeCamera.enabled = false;
    }

    private void Start()
    {
        if (_startInCaptureMode)
        {
            BeginCapture();
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
        {
            if (_isCapturing)
            {
                EndCapture();
            }
            else
            {
                BeginCapture();
            }
        }

        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
        {
            SaveScreenshot();
        }
    }

    /// <summary>
    /// 촬영 모드의 Game 화면을 현재 해상도 그대로 PNG로 저장.
    /// </summary>
    public void SaveScreenshot()
    {
        if (!_isCapturing || !isActiveAndEnabled || _screenshotRoutine != null)
        {
            return;
        }

        _screenshotRoutine = StartCoroutine(SaveScreenshotAfterRendering());
    }

    private IEnumerator SaveScreenshotAfterRendering()
    {
        // LateUpdate의 캐릭터/UI 숨김과 카메라 렌더링이 모두 끝난 화면 사용.
        // Time.timeScale이 0인 촬영에서도 동작.
        yield return new WaitForEndOfFrame();

        Texture2D screenshot = null;
        try
        {
            if (!_isCapturing)
            {
                yield break;
            }

#if UNITY_EDITOR
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures"));
#else
            string folder = Path.Combine(Application.persistentDataPath, "Captures");
#endif
            Directory.CreateDirectory(folder);
            string sceneName = gameObject.scene.name;
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                sceneName = sceneName.Replace(invalidChar, '_');
            }

            string fileName = $"{sceneName}_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid().ToString("N").Substring(0, 8)}.png";
            string path = Path.Combine(folder, fileName);
            screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            byte[] png = screenshot.EncodeToPNG();
            // 같은 이름의 파일이 있더라도 덮어쓰지 않음.
            using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            {
                stream.Write(png, 0, png.Length);
            }

            Debug.Log($"맵 캡처 저장 완료 ({screenshot.width} x {screenshot.height}): {path}", this);
        }
        catch (Exception exception)
        {
            Debug.LogError($"맵 캡처 저장 실패: {exception.Message}", this);
        }
        finally
        {
            if (screenshot != null)
            {
                Destroy(screenshot);
            }

            _screenshotRoutine = null;
        }
    }

    private void LateUpdate()
    {
        if (!_isCapturing)
        {
            return;
        }

        // 실행 중 생성되는 HUD와 무기 이펙트도 다음 렌더링 전에 숨김.
        HideCaptureVisuals();
    }

    /// <summary>
    /// 현재 게임 시점을 이어받아 촬영 시작.
    /// </summary>
    public void BeginCapture()
    {
        if (_isCapturing || !isActiveAndEnabled)
        {
            return;
        }

        if (_gameCamera == null || _heroRoot == null || _inputHandler == null || _cameraFocus == null)
        {
            Debug.LogError("촬영 카메라의 Game Camera, Hero Root, Input Handler, Camera Focus를 연결하세요.", this);
            return;
        }

        _previousCursorLock = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;
        _previousTimeScale = Time.timeScale;
        _inputWasEnabled = _inputHandler.enabled;
        _focusWasEnabled = _cameraFocus.enabled;
        _isCapturing = true;

        // 캐릭터 루트는 유지해 게임 로직의 참조와 수명에 영향을 주지 않음.
        _inputHandler.enabled = false;
        _cameraFocus.enabled = false;
        transform.SetPositionAndRotation(_gameCamera.transform.position, _gameCamera.transform.rotation);
        _captureCamera.CopyFrom(_gameCamera);
        _captureCamera.targetTexture = null;
        CopyRenderSettings();

        foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (camera == _captureCamera || camera.targetTexture != null)
            {
                continue;
            }

            _cameras.Add(camera, camera.enabled);
            camera.enabled = false;
        }

        foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
        {
            if (listener == _listener)
            {
                continue;
            }

            _listeners.Add(listener, listener.enabled);
            listener.enabled = false;
        }

        HideCaptureVisuals();
        if (_pauseWorld)
        {
            Time.timeScale = 0f;
        }

        _captureCamera.enabled = true;
        _listener.enabled = true;
        _freeCamera.enabled = true;
    }

    private void CopyRenderSettings()
    {
        UniversalAdditionalCameraData source = _gameCamera.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData target = _captureCamera.GetUniversalAdditionalCameraData();
        target.renderPostProcessing = source.renderPostProcessing;
        target.renderShadows = source.renderShadows;
        target.antialiasing = source.antialiasing;
        target.antialiasingQuality = source.antialiasingQuality;
        target.volumeLayerMask = source.volumeLayerMask;
        // 촬영 위치를 기준으로 볼륨 판정.
        target.volumeTrigger = transform;
        target.requiresDepthOption = source.requiresDepthOption;
        target.requiresColorOption = source.requiresColorOption;
    }

    private void HideCaptureVisuals()
    {
        if (_heroRoot != null)
        {
            foreach (Renderer renderer in _heroRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (!_renderers.ContainsKey(renderer))
                {
                    _renderers.Add(renderer, renderer.forceRenderingOff);
                }

                renderer.forceRenderingOff = true;
            }
        }

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!_canvases.ContainsKey(canvas))
            {
                _canvases.Add(canvas, canvas.enabled);
            }

            canvas.enabled = false;
        }
    }

    /// <summary>
    /// 진입 전 표시, 입력, 카메라 및 커서 상태 복구.
    /// </summary>
    public void EndCapture()
    {
        if (!_isCapturing)
        {
            return;
        }

        _isCapturing = false;
        // 렌더링 대기 중 촬영을 끝내면 복구된 UI가 저장되지 않도록 취소.
        if (_screenshotRoutine != null)
        {
            StopCoroutine(_screenshotRoutine);
            _screenshotRoutine = null;
        }

        _freeCamera.enabled = false;
        _captureCamera.enabled = false;
        _listener.enabled = false;

        foreach (KeyValuePair<Camera, bool> item in _cameras)
        {
            if (item.Key != null) item.Key.enabled = item.Value;
        }
        foreach (KeyValuePair<AudioListener, bool> item in _listeners)
        {
            if (item.Key != null) item.Key.enabled = item.Value;
        }
        foreach (KeyValuePair<Renderer, bool> item in _renderers)
        {
            if (item.Key != null) item.Key.forceRenderingOff = item.Value;
        }
        foreach (KeyValuePair<Canvas, bool> item in _canvases)
        {
            if (item.Key != null) item.Key.enabled = item.Value;
        }

        _cameras.Clear();
        _listeners.Clear();
        _renderers.Clear();
        _canvases.Clear();
        if (_inputHandler != null) _inputHandler.enabled = _inputWasEnabled;
        if (_cameraFocus != null) _cameraFocus.enabled = _focusWasEnabled;
        if (_pauseWorld) Time.timeScale = _previousTimeScale;
        Cursor.lockState = _previousCursorLock;
        Cursor.visible = _previousCursorVisible;
    }

    private void OnDisable()
    {
        EndCapture();
    }
}
