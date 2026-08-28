using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using StreamOn.Minigames.Runner;
using StreamOn.Broadcast;

public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "MainScene";
    [SerializeField] private string roomSceneName = "StreamerRoom";
    [SerializeField] private SFXManager.Sfx buttonSfx = SFXManager.Sfx.Interact;
    [SerializeField, Min(0f)] private float actionDelay = 0.12f;
    [SerializeField, Min(0f)] private float fadeDuration = 1.4f;
    [SerializeField] private RectTransform startButtonRect;
    [SerializeField] private RectTransform exitButtonRect;
    [SerializeField] private Image fadeImage;

    [Header("Scene-authored Tutorial")]
    [SerializeField] private Button tutorialButton;
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private Button tutorialCloseButton;

    [Header("Title Audio")]
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField] private AudioClip uiButtonSound;
    [SerializeField, Range(0f, 1f)] private float backgroundMusicVolume = .7f;
    [SerializeField, Range(0f, 1f)] private float uiButtonVolume = .8f;

    private bool isTransitioning;
    private BroadcastTitleScreenPresentation presentation;

    private void Awake()
    {
        Canvas canvas = startButtonRect != null ? startButtonRect.GetComponentInParent<Canvas>()
            : FindFirstObjectByType<Canvas>();
        presentation = GetComponent<BroadcastTitleScreenPresentation>();
        if (presentation == null) presentation = gameObject.AddComponent<BroadcastTitleScreenPresentation>();
        presentation.Configure(canvas, tutorialButton, tutorialPanel, backgroundMusic, uiButtonSound,
            backgroundMusicVolume, uiButtonVolume, tutorialCloseButton);
    }

    private void Update()
    {
        if (!isTransitioning && presentation != null && presentation.TutorialOpen)
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                RectTransform closeButtonRect = tutorialCloseButton != null
                    ? tutorialCloseButton.transform as RectTransform
                    : null;
                if (IsPointerInside(closeButtonRect, Mouse.current.position.ReadValue()))
                {
                    CloseTutorial();
                }
            }
            return;
        }
        if (isTransitioning || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        RectTransform tutorialButtonRect = tutorialButton != null
            ? tutorialButton.transform as RectTransform
            : null;
        if (IsPointerInside(tutorialButtonRect, screenPosition))
        {
            OpenTutorial();
        }
        else if (IsPointerInside(startButtonRect, screenPosition))
        {
            StartGame();
        }
        else if (IsPointerInside(exitButtonRect, screenPosition))
        {
            ExitGame();
        }
    }

    public void StartGame()
    {
        if (isTransitioning) return;
        StartCoroutine(StartGameRoutine());
    }

    public void OpenTutorial()
    {
        if (isTransitioning) return;

        if (presentation != null)
        {
            presentation.OpenTutorial();
            return;
        }

        if (tutorialPanel == null) return;
        tutorialPanel.transform.SetAsLastSibling();
        tutorialPanel.SetActive(true);
    }

    public void CloseTutorial()
    {
        if (presentation != null)
        {
            presentation.CloseTutorial();
            return;
        }

        if (tutorialPanel != null) tutorialPanel.SetActive(false);
    }

    public void ExitGame()
    {
        if (isTransitioning) return;
        if (RunnerBroadcastSessionStore.IsActive)
        {
            RunnerBroadcastSessionStore.OpenGameSelectionOnRoomLoad = true;
            RunnerSaveSession.RequireSlotSelection = false;
            SceneManager.LoadScene(roomSceneName);
            return;
        }
        StartCoroutine(ExitGameRoutine());
    }

    private IEnumerator StartGameRoutine()
    {
        isTransitioning = true;
        DontDestroyOnLoad(gameObject);
        PrepareFade();

        PlayButtonSound();
        if (actionDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(actionDelay);
        }

        yield return Fade(fadeImage, 0f, 1f);
        SceneManager.LoadScene(gameSceneName);
        yield return null;
        yield return Fade(fadeImage, 1f, 0f);

        if (fadeImage != null) Destroy(fadeImage.transform.root.gameObject);

        Destroy(gameObject);
    }

    private IEnumerator ExitGameRoutine()
    {
        isTransitioning = true;
        PrepareFade();

        PlayButtonSound();
        if (actionDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(actionDelay);
        }

        yield return Fade(fadeImage, 0f, 1f);
        QuitApplication();
    }

    private void PrepareFade()
    {
        if (fadeImage == null) Debug.LogError("MainMenu의 씬 기반 Fade Image가 연결되지 않았습니다.", this);
        else
        {
            fadeImage.color = Color.clear;
            DontDestroyOnLoad(fadeImage.transform.root.gameObject);
        }
    }

    private IEnumerator Fade(Image fadeImage, float fromAlpha, float toAlpha)
    {
        if (fadeImage == null)
        {
            yield break;
        }

        if (fadeDuration <= 0f)
        {
            fadeImage.color = new Color(0f, 0f, 0f, toAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            fadeImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(fromAlpha, toAlpha, eased));
            yield return null;
        }

        fadeImage.color = new Color(0f, 0f, 0f, toAlpha);
    }

    private static bool IsPointerInside(RectTransform target, Vector2 screenPosition)
    {
        return target != null && RectTransformUtility.RectangleContainsScreenPoint(target, screenPosition);
    }

    private static void QuitApplication()
    {
        StreamOn.Platform.WebGLPlatformBridge.QuitOrShowBrowserMessage();
    }

    private void PlayButtonSound()
    {
        if (uiButtonSound != null) presentation?.PlayButtonSound();
        else SFXManager.PlayGlobal(buttonSfx);
    }
}
