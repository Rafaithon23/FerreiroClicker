using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("HUD - Ouro")]
    public Text goldText;
    public Text goldPerClickText;
    public Text goldPerSecondText;
    [Header("Resistência")]
    public Slider staminaSlider;
    [Header("Loja e saída")]
    public Button shopButton;
    public Button quitButton;

    public static UIManager Instance { get; private set; }
    private Text staminaLabel;
    private Text staminaHint;
    private Image staminaFill;
    private RectTransform hud;
    private RectTransform canvasRect;
    private Font hudFont;
    private float targetStamina = 1f;
    private float staminaCurrent;
    private float staminaMax = 100f;
    private float goldPulse;
    private bool criticalPulse;
    private GameManager game;

    private void Awake() { Instance = this; }

    private void Start()
    {
        game = GameManager.Instance;
        if (game == null) { Debug.LogWarning("GameManager nao encontrado na cena."); return; }
        BuildHud();
        if (GetComponent<CampaignGoalUI>() == null) gameObject.AddComponent<CampaignGoalUI>();
        if (GetComponent<WorkshopPresentation>() == null) gameObject.AddComponent<WorkshopPresentation>();
        game.OnGoldChanged += HandleGoldChanged;
        game.OnUpgradeChanged += RefreshAll;
        game.OnStaminaChanged += HandleStaminaChanged;
        if (shopButton != null) shopButton.onClick.AddListener(OnShopButtonClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitButtonClicked);
        RefreshAll();
        if (staminaSlider != null) staminaSlider.SetValueWithoutNotify(targetStamina);
    }

    private void BuildHud()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        canvasRect = canvas.transform as RectTransform;
        hudFont = goldText != null ? goldText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (ShopManager.Instance != null && ShopManager.Instance.cards != null)
            foreach (var card in ShopManager.Instance.cards)
                if (card.infoText != null && card.infoText.font != null) { hudFont = card.infoText.font; break; }

        Transform existing = canvas.transform.Find("HudPanel");
        hud = existing as RectTransform;
        if (hud == null)
        {
            hud = NewRect("HudPanel", canvas.transform);
            Image panel = hud.gameObject.AddComponent<Image>();
            panel.color = new Color(.10f, .07f, .045f, .95f);
            panel.raycastTarget = false;
        }
        hud.SetAsFirstSibling();
        Place(hud, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(600, 202));
        Image hudImage = hud.GetComponent<Image>();
        if (hudImage != null) { hudImage.color = Color.white; hudImage.raycastTarget = false; }

        RectTransform shade = NewRect("HudContrast", hud);
        Place(shade, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(504, 136));
        Image scrim = shade.gameObject.AddComponent<Image>();
        scrim.color = new Color(.025f, .018f, .012f, .72f);
        scrim.raycastTarget = false;
        Text heading = NewText("GoldLabel", hud, 18, new Color(.9f, .74f, .46f));
        Place(heading.rectTransform, Vector2.one * .5f, Vector2.one * .5f, new Vector2(0, 57), new Vector2(450, 26));
        heading.text = "OURO";
        StyleGoldText(goldText, new Vector2(0, 17), new Vector2(450, 58), 48);
        StyleGoldText(goldPerClickText, new Vector2(-130, -48), new Vector2(242, 36), 21);
        StyleGoldText(goldPerSecondText, new Vector2(130, -48), new Vector2(242, 36), 21);
        RectTransform divider = NewRect("HudDivider", hud);
        Place(divider, Vector2.one * .5f, Vector2.one * .5f, new Vector2(0, -22), new Vector2(460, 2));
        Image line = divider.gameObject.AddComponent<Image>();
        line.color = new Color(.68f, .40f, .16f, .75f);
        line.raycastTarget = false;

        if (staminaSlider == null) return;
        RectTransform sliderRect = staminaSlider.transform as RectTransform;
        Place(sliderRect, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(440, 108));
        staminaSlider.minValue = 0f;
        staminaSlider.maxValue = 1f;
        staminaSlider.interactable = false;
        staminaSlider.transition = Selectable.Transition.None;
        staminaSlider.navigation = new Navigation { mode = Navigation.Mode.None };
        Transform background = staminaSlider.transform.Find("Background");
        if (background is RectTransform bg)
        {
            Place(bg, new Vector2(.5f, 0), Vector2.one * .5f, new Vector2(0, 42), new Vector2(440, 48));
            Image image = bg.GetComponent<Image>();
            if (image != null) image.color = new Color(.70f, .62f, .50f, 1);
        }
        if (staminaSlider.fillRect != null)
        {
            staminaFill = staminaSlider.fillRect.GetComponent<Image>();
            RectTransform area = staminaSlider.fillRect.parent as RectTransform;
            if (area != null && area != sliderRect)
                Place(area, new Vector2(.5f, 0), Vector2.one * .5f, new Vector2(0, 42), new Vector2(404, 23));
            staminaSlider.fillRect.sizeDelta = Vector2.zero;
            staminaSlider.fillRect.anchoredPosition = Vector2.zero;
        }
        if (staminaSlider.handleRect != null) staminaSlider.handleRect.gameObject.SetActive(false);
        foreach (Graphic graphic in staminaSlider.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        staminaLabel = NewText("StaminaLabel", sliderRect, 21, new Color(1, .93f, .76f));
        Place(staminaLabel.rectTransform, new Vector2(.5f, 0), Vector2.one * .5f, new Vector2(0, 83), new Vector2(440, 28));
        staminaHint = NewText("StaminaHint", sliderRect, 17, new Color(.90f, .84f, .70f));
        Place(staminaHint.rectTransform, new Vector2(.5f, 0), Vector2.one * .5f, new Vector2(0, 9), new Vector2(440, 23));
        FitHud();
    }

    private void StyleGoldText(Text text, Vector2 position, Vector2 size, int fontSize)
    {
        if (text == null) return;
        text.transform.SetParent(hud, false);
        StyleText(text, fontSize, new Color(1, .92f, .72f));
        Place(text.rectTransform, Vector2.one * .5f, Vector2.one * .5f, position, size);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private Text NewText(string name, Transform parent, int size, Color color)
    {
        Text text = NewRect(name, parent).gameObject.AddComponent<Text>();
        StyleText(text, size, color);
        return text;
    }

    private void StyleText(Text text, int size, Color color)
    {
        text.font = hudFont;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.supportRichText = true;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(14, size - 8);
        text.resizeTextMaxSize = size;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        Outline outline = text.GetComponent<Outline>();
        if (outline == null) outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(.035f, .018f, .008f, .95f);
        outline.effectDistance = new Vector2(1, -1);
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private void FitHud()
    {
        if (canvasRect == null || hud == null) return;
        float scale = Mathf.Min(1f, Mathf.Max(.1f, (canvasRect.rect.width - 48f) / 600f));
        hud.localScale = Vector3.one * scale;
        if (staminaSlider != null)
            staminaSlider.transform.localScale = Vector3.one * Mathf.Min(1f, Mathf.Max(.1f, (canvasRect.rect.width - 40f) / 440f));
    }

    private void Update()
    {
        FitHud();
        goldPulse = Mathf.Max(0, goldPulse - Time.deltaTime);
        if (goldText != null)
        {
            float pulse = Mathf.Sin(Mathf.Clamp01(goldPulse / .22f) * Mathf.PI);
            goldText.rectTransform.localScale = Vector3.one * (1f + pulse * (criticalPulse ? .10f : .055f));
            goldText.color = Color.Lerp(new Color(1f, .92f, .72f), Color.white, pulse);
        }
        if (staminaSlider != null)
            staminaSlider.SetValueWithoutNotify(Mathf.Lerp(staminaSlider.value, targetStamina, 1f - Mathf.Exp(-Time.deltaTime * 18f)));
        bool tired = game != null && staminaCurrent < game.StaminaCostPerHit;
        bool low = targetStamina <= .25f;
        Color color = tired || low ? new Color(1f, .30f, .16f) : targetStamina <= .5f ? new Color(1f, .70f, .38f) : Color.white;
        if (staminaFill != null)
        {
            if (low) color.a = .83f + Mathf.Sin(Time.unscaledTime * 6f) * .12f;
            staminaFill.color = color;
        }
        if (staminaHint != null)
        {
            string hint = tired ? "CANSADO • RECUPERANDO..." : low ? "RESISTÊNCIA BAIXA" : targetStamina < .995f ? "Recuperando resistência..." : "Pronto para martelar";
            if (staminaHint.text != hint) staminaHint.text = hint;
            staminaHint.color = tired || low ? new Color(1f, .52f, .33f) : new Color(.94f, .86f, .70f);
        }
    }

    public void NotifyHit(bool critical)
    {
        goldPulse = .22f;
        criticalPulse = critical;
    }

    private void HandleGoldChanged(double gold)
    {
        if (goldText != null) goldText.text = FormatNumber(gold);
    }

    private void HandleStaminaChanged(float current, float max)
    {
        staminaCurrent = current;
        staminaMax = max;
        targetStamina = max > 0 ? Mathf.Clamp01(current / max) : 0f;
        if (staminaLabel != null)
            staminaLabel.text = "RESISTÊNCIA   " + Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(staminaMax);
    }

    private void RefreshAll()
    {
        if (game == null) return;
        HandleGoldChanged(game.Gold);
        if (goldPerClickText != null) goldPerClickText.text = "<color=#FFE0A0>" + FormatNumber(game.GoldPerClick) + "</color> ouro / golpe";
        if (goldPerSecondText != null) goldPerSecondText.text = "<color=#FFE0A0>" + FormatNumber(game.GoldPerSecond) + "</color> ouro / segundo";
        HandleStaminaChanged(game.Stamina, game.MaxStamina);
    }

    private void OnShopButtonClicked()
    {
        if (ShopManager.Instance == null) return;
        if (!ShopManager.Instance.IsShopOpen && HammerFollowMouse.Instance != null && HammerFollowMouse.Instance.IsHolding)
        {
            AnvilClicker.Instance?.SpawnFloatingText("Solte o martelo primeiro!");
            return;
        }
        ShopManager.Instance.ToggleShop();
    }

    private void OnQuitButtonClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (game != null)
        {
            game.OnGoldChanged -= HandleGoldChanged;
            game.OnUpgradeChanged -= RefreshAll;
            game.OnStaminaChanged -= HandleStaminaChanged;
        }
        if (shopButton != null) shopButton.onClick.RemoveListener(OnShopButtonClicked);
        if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitButtonClicked);
    }

    public static string FormatNumber(double value)
    {
        if (value < 1000) return value.ToString("0");
        string[] suffixes = { "", "K", "M", "B", "T" };
        int index = 0;
        while (value >= 1000 && index < suffixes.Length - 1) { value /= 1000; index++; }
        return value.ToString("0.0") + suffixes[index];
    }
}
