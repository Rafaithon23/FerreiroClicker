using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Fade preto simples entre cenas, pra nao ter o corte seco padrao do
/// Unity ao trocar de cena. Um unico objeto, marcado DontDestroyOnLoad,
/// cobre a tela com uma Image preta e anima o alpha antes de carregar a
/// proxima cena e depois que ela termina de carregar.
///
/// COMO USAR: coloque este script num GameObject vazio na cena do MENU
/// (a primeira cena que carrega). Ele sobrevive a troca de cena sozinho.
/// Pra trocar de cena com fade, chame SceneTransition.Instance.LoadScene("NomeDaCena")
/// em vez de SceneManager.LoadScene diretamente.
/// </summary>
public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    [Tooltip("Duracao de cada metade do fade (fechar e depois abrir), em segundos")]
    public float fadeDuration = 0.5f;

    [Header("Abertura do jogo")]
    [Tooltip("Tempo para o menu surgir suavemente a partir do preto.")]
    [Min(0.1f)] public float initialFadeDuration = 1.8f;
    [Tooltip("Breve espera para preparar a interface antes de revelar o menu.")]
    [Min(0f)] public float initialBlackHold = 0.12f;

    private Image fadeImage;
    private bool isLoading;
    private Coroutine initialFade;
    public bool IsTransitioning => isLoading || initialFade != null || (fadeImage != null && fadeImage.color.a > .01f);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUi();
    }

    private void Start()
    {
        initialFade = StartCoroutine(RevealInitialScene());
    }

    private IEnumerator RevealInitialScene()
    {
        // O tema cria os painéis em Start e termina no frame seguinte.
        // Mantém a tela coberta até a interface final estar pronta.
        yield return null;
        yield return null;
        if (initialBlackHold > 0f) yield return new WaitForSecondsRealtime(initialBlackHold);
        yield return FadeRoutine(1f, 0f, initialFadeDuration);
        initialFade = null;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void BuildUi()
    {
        GameObject canvasGo = new GameObject("SceneTransitionCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // sempre por cima de qualquer outra UI/canvas da cena

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject imgGo = new GameObject("FadeImage", typeof(RectTransform));
        imgGo.transform.SetParent(canvasGo.transform, false);
        RectTransform rt = imgGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        fadeImage = imgGo.AddComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 1f); // comeca preto (cobre o primeiro frame carregando)
        fadeImage.raycastTarget = true; // bloqueia clique durante a transicao
    }

    /// <summary>Troca de cena com fade preto no meio (fecha, carrega, abre).</summary>
    public void LoadScene(string sceneName)
    {
        if (isLoading) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("Cena indisponível: " + sceneName);
            return;
        }
        isLoading = true;
        if (initialFade != null) { StopCoroutine(initialFade); initialFade = null; }
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        yield return FadeRoutine(fadeImage != null ? fadeImage.color.a : 0f, 1f, fadeDuration);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op != null)
        {
            while (!op.isDone) yield return null;
        }

        // Awake/Start, tema e primeiro rebuild do Canvas ficam cobertos.
        yield return null;
        yield return null;
        float readyDeadline = Time.realtimeSinceStartup + 5f;
        string activeName = SceneManager.GetActiveScene().name;
        if (activeName == "MainMenu" || activeName == "SampleScene")
            while (!MinimalVisualTheme.IsReady && Time.realtimeSinceStartup < readyDeadline) yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;
        yield return FadeRoutine(1f, 0f, fadeDuration);
        isLoading = false;
    }

    private IEnumerator FadeRoutine(float from, float to, float duration)
    {
        if (fadeImage == null) yield break;

        fadeImage.raycastTarget = true;
        float t = 0f;
        duration = Mathf.Max(0.01f, duration);
        SetAlpha(from);

        while (t < duration)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, .05f); // Um frame lento não pula o fade inteiro.
            float progress = Mathf.Clamp01(t / duration);
            // Smootherstep evita mudança brusca de velocidade nas duas pontas.
            progress = progress * progress * progress * (progress * (progress * 6f - 15f) + 10f);
            SetAlpha(Mathf.Lerp(from, to, progress));
            yield return null;
        }

        SetAlpha(to);
        fadeImage.raycastTarget = to > 0.01f;
    }

    private void SetAlpha(float a)
    {
        if (fadeImage == null) return;
        Color c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
    }
}
