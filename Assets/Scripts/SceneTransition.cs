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

    private Image fadeImage;

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
        // Fade-in ao nascer (cobre o load inicial da propria cena do menu).
        StartCoroutine(FadeRoutine(1f, 0f));
    }

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
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        yield return StartCoroutine(FadeRoutine(0f, 1f));

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op != null)
        {
            while (!op.isDone) yield return null;
        }

        yield return StartCoroutine(FadeRoutine(1f, 0f));
    }

    private IEnumerator FadeRoutine(float from, float to)
    {
        if (fadeImage == null) yield break;

        fadeImage.raycastTarget = true;
        float t = 0f;
        SetAlpha(from);

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / fadeDuration)));
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
