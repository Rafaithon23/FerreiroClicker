using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// Short introduction; purchased upgrades affect gameplay without adding scenery overlays.
public class WorkshopPresentation : MonoBehaviour
{
    public static WorkshopPresentation Instance { get; private set; }
    private const string IntroKey = "bc_intro_complete";
    private GameManager game;
    private Canvas canvas;
    private RectTransform intro;
    private Text hint;
    private Text arrow;
    private HammerPickup pickup;
    private Font font;
    private int introStep = -1;
    private double initialGold;
    private bool ready;
    public int IntroductionStep => introStep;

    private void Awake() { Instance = this; }

    private void Start()
    {
        game = GameManager.Instance;
        canvas = GetComponentInParent<Canvas>();
        if (game == null || canvas == null) return;
        font = UIManager.Instance.goldText.font;
        pickup = FindAnyObjectByType<HammerPickup>();
        intro = Rect("Introduction", canvas.transform, new Vector2(.5f, .88f), new Vector2(640, 100));
        Image back = intro.gameObject.AddComponent<Image>();
        back.color = new Color(.07f, .04f, .025f, .94f); back.raycastTarget = false;
        Outline edge = intro.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(.65f, .35f, .10f); edge.effectDistance = new Vector2(2, -2);
        hint = Label("Hint", intro, 23);
        hint.rectTransform.anchorMin = Vector2.zero; hint.rectTransform.anchorMax = Vector2.one;
        hint.rectTransform.offsetMin = new Vector2(16, 10); hint.rectTransform.offsetMax = new Vector2(-16, -10);
        arrow = Label("IntroductionArrow", canvas.transform, 42);
        arrow.text = "▼"; arrow.rectTransform.sizeDelta = new Vector2(60, 60);
        arrow.gameObject.SetActive(false);
        intro.gameObject.SetActive(false);
        ready = true;
        // Returning saves do not have to repeat the introductory steps.
        if (!PlayerPrefs.HasKey(IntroKey) && game.TotalGoldEarned < 10 && game.GetUpgrade("hammer").level == 0)
            BeginIntroduction();
    }

    private void Update()
    {
        if (!ready) return;
        bool busy = HordeManager.Instance != null && HordeManager.Instance.IsHordeInProgress;
        bool shopping = ShopManager.Instance != null && ShopManager.Instance.IsShopOpen;
        float clock = Time.time;
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) BeginIntroduction();
        if (introStep < 0) return;
        intro.gameObject.SetActive(!busy && !shopping);
        arrow.gameObject.SetActive(!busy && !shopping);
        bool holding = HammerFollowMouse.Instance != null && HammerFollowMouse.Instance.IsHolding;
        if (introStep == 0 && holding) introStep = 1;
        if (introStep == 2 && shopping) { FinishIntroduction(); return; }
        if (introStep == 0)
        {
            hint.text = "1 / 3  •  PEGUE O MARTELO\nClique no suporte ao lado da bigorna.";
            PointAt(pickup != null ? pickup.transform.position + Vector3.up * .8f : Vector3.zero);
        }
        else if (introStep == 1)
        {
            hint.text = holding ? "2 / 3  •  DÊ SUA PRIMEIRA MARTELADA\nAcerte a bigorna para ganhar ouro."
                : "2 / 3  •  PEGUE O MARTELO NOVAMENTE\nClique no suporte e depois acerte a bigorna.";
            if (holding) PointAt(AnvilClicker.Instance.transform.position + Vector3.up * .8f);
            else if (pickup != null) PointAt(pickup.transform.position + Vector3.up * .8f);
        }
        else
        {
            hint.text = holding ? "3 / 3  •  CONHEÇA A LOJA\nDevolva o martelo ao suporte e clique em LOJA."
                : "3 / 3  •  CONHEÇA A LOJA\nClique em LOJA para ver sua próxima melhoria.";
            if (holding)
            {
                if (pickup != null) PointAt(pickup.transform.position + Vector3.up * .8f);
            }
            else if (UIManager.Instance.shopButton != null)
                arrow.rectTransform.position = UIManager.Instance.shopButton.transform.position + Vector3.up * (45 + 4 * Mathf.Sin(clock * 5)) * canvas.scaleFactor;
        }
        float width = ((RectTransform)canvas.transform).rect.width;
        intro.localScale = Vector3.one * Mathf.Min(1, Mathf.Max(.1f, (width - 48) / 640));
    }

    private void PointAt(Vector3 world)
    {
        if (Camera.main == null) return;
        Vector2 screen = Camera.main.WorldToScreenPoint(world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out Vector2 local);
        arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(.5f, .5f);
        arrow.rectTransform.anchoredPosition = local + Vector2.up * (8 + 4 * Mathf.Sin(Time.time * 5));
    }

    public void BeginIntroduction()
    {
        if (!ready) return;
        initialGold = game.TotalGoldEarned;
        introStep = HammerFollowMouse.Instance != null && HammerFollowMouse.Instance.IsHolding ? 1 : 0;
    }

    public void NotifyAnvilHit()
    {
        if (introStep == 1 && game.TotalGoldEarned > initialGold) introStep = 2;
    }

    private void FinishIntroduction()
    {
        introStep = -1; intro.gameObject.SetActive(false); arrow.gameObject.SetActive(false);
        PlayerPrefs.SetInt(IntroKey, 1); PlayerPrefs.Save();
    }

    private Text Label(string name, Transform parent, int size)
    {
        Text label = Rect(name, parent, new Vector2(.5f, .5f), new Vector2(200, 40)).gameObject.AddComponent<Text>();
        label.font = font; label.fontSize = size; label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter; label.color = new Color(1, .91f, .69f);
        label.raycastTarget = false;
        Outline shadow = label.gameObject.AddComponent<Outline>();
        shadow.effectColor = new Color(.025f, .018f, .01f); shadow.effectDistance = new Vector2(1, -1);
        return label;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); Anchor(rect, anchor, size); return rect;
    }

    private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size; rect.anchoredPosition = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (intro != null) Destroy(intro.gameObject);
        if (arrow != null) Destroy(arrow.gameObject);
    }
}
