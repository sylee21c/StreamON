using System.Runtime.InteropServices;
using UnityEngine;

namespace StreamOn.Platform
{
    /// <summary>
    /// Browser-only lifecycle and IndexedDB bridge. It creates no visible UI and
    /// becomes a no-op on non-Web platforms.
    /// </summary>
    public sealed class WebGLPlatformBridge : MonoBehaviour
    {
        private const string BridgeObjectName = "StreamOn WebGL Platform Bridge";
        private const float DeferredSyncSeconds = .35f;

        private static WebGLPlatformBridge _instance;
        private bool _syncQueued;
        private bool _syncInFlight;
        private bool _syncAgain;
        private float _syncAt;
        private bool _pausedByVisibility;
        private float _timeScaleBeforeVisibilityPause = 1f;
        private bool _audioPausedBeforeVisibility;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void StreamOnWebGL_SyncFileSystem(string callbackObject);
        [DllImport("__Internal")] private static extern void StreamOnWebGL_RegisterLifecycle(string callbackObject);
        [DllImport("__Internal")] private static extern void StreamOnWebGL_RequestFullscreen();
        [DllImport("__Internal")] private static extern void StreamOnWebGL_ShowQuitMessage();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize() => EnsureInstance();

        public static void RequestFileSystemSync(bool immediate = false)
        {
            WebGLPlatformBridge bridge = EnsureInstance();
            if (bridge == null) return;
            if (immediate) bridge.BeginSync();
            else
            {
                bridge._syncQueued = true;
                bridge._syncAt = Time.realtimeSinceStartup + DeferredSyncSeconds;
            }
        }

        public static void RequestFullscreen()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            StreamOnWebGL_RequestFullscreen();
#else
            Screen.fullScreen = !Screen.fullScreen;
#endif
        }

        public static void QuitOrShowBrowserMessage()
        {
            RequestFileSystemSync(true);
#if UNITY_WEBGL && !UNITY_EDITOR
            StreamOnWebGL_ShowQuitMessage();
#elif UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static WebGLPlatformBridge EnsureInstance()
        {
            if (_instance != null) return _instance;
            _instance = FindFirstObjectByType<WebGLPlatformBridge>();
            if (_instance != null) return _instance;
            GameObject bridgeObject = new GameObject(BridgeObjectName);
            _instance = bridgeObject.AddComponent<WebGLPlatformBridge>();
            DontDestroyOnLoad(bridgeObject);
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            gameObject.name = BridgeObjectName;
            DontDestroyOnLoad(gameObject);
#if UNITY_WEBGL && !UNITY_EDITOR
            StreamOnWebGL_RegisterLifecycle(gameObject.name);
#endif
        }

        private void Update()
        {
            if (_syncQueued && Time.realtimeSinceStartup >= _syncAt) BeginSync();
        }

        private void BeginSync()
        {
            _syncQueued = false;
            if (_syncInFlight)
            {
                _syncAgain = true;
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            _syncInFlight = true;
            StreamOnWebGL_SyncFileSystem(gameObject.name);
#endif
        }

        // Called by StreamOnWebGL.jslib through SendMessage.
        public void OnFileSystemSyncCompleted(string error)
        {
            _syncInFlight = false;
            if (!string.IsNullOrWhiteSpace(error))
                Debug.LogWarning("STREAM ON WebGL IndexedDB sync failed: " + error);
            if (!_syncAgain) return;
            _syncAgain = false;
            BeginSync();
        }

        // Called by StreamOnWebGL.jslib: "1" means the document is hidden.
        public void OnBrowserVisibilityChanged(string hiddenValue)
        {
            bool hidden = hiddenValue == "1";
            if (hidden)
            {
                RequestFileSystemSync(true);
                _audioPausedBeforeVisibility = AudioListener.pause;
                if (!_pausedByVisibility && Time.timeScale > 0f)
                {
                    _timeScaleBeforeVisibilityPause = Time.timeScale;
                    Time.timeScale = 0f;
                    _pausedByVisibility = true;
                }
                AudioListener.pause = true;
            }
            else
            {
                if (_pausedByVisibility)
                {
                    Time.timeScale = _timeScaleBeforeVisibilityPause > 0f ? _timeScaleBeforeVisibilityPause : 1f;
                    _pausedByVisibility = false;
                }
                AudioListener.pause = _audioPausedBeforeVisibility;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) RequestFileSystemSync(true);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) RequestFileSystemSync(true);
        }

        private void OnApplicationQuit() => RequestFileSystemSync(true);
    }
}
