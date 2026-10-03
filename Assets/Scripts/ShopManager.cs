using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla o painel da loja de upgrades: abre/fecha por cima do jogo e
/// atualiza os cards (nome, descricao, nivel, custo, bloqueado ou nao).
/// Coloque este script num GameObject da Canvas e arraste o painel, o
/// botao de fechar e os cards no Inspector.
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [Tooltip("Raiz do painel da loja (comeca desativada)")]
    public GameObject shopPanel;

    [Tooltip("Botao 'X' que fecha a loja")]
    public Button closeButton;

    /// <summary>True enquanto o painel da loja esta aberto. AnvilClicker usa isso pra nao deixar bater na bigorna por baixo da loja.</summary>
    public bool IsShopOpen => shopPanel != null && shopPanel.activeSelf;

    [System.Serializable]
    public class UpgradeCardUI
    {
        [Tooltip("Precisa bater com o id definido em GameManager.BuildCatalog")]
        public string upgradeId;
        public Button button;
        public Text infoText;
        public Sprite icon;
    }

    public UpgradeCardUI[] cards;

    [Header("Arte da loja")]
    public Sprite windowSprite;
    public Sprite cardSprite;
    public Sprite categoryPlaque;
    public Sprite iconFrame;
    public Sprite lockSprite;
    public Sprite closeSprite;
    public Font titleFont;
    public Vector4 cardCrop = new Vector4(0.01749540f, 0.18646409f, 0.96500921f, 0.64640884f);
    public Vector4 plaqueCrop = new Vector4(0.01012891f, 0.26104972f, 0.98020258f, 0.51795580f);
    public Vector4 frameCrop = new Vector4(0.08373206f, 0.10845295f, 0.83333333f, 0.79106858f);
    public Vector4 closeCrop = new Vector4(0.12123552f, 0.11696870f, 0.75752896f, 0.76276771f);
    public Vector4 lockCrop = new Vector4(0.23427673f, 0.16585761f, 0.53223270f, 0.66585761f);

    private RectTransform window;
    private Vector2 lastPanelSize;
    private Text shopGoldText;
    private Text purchaseFeedback;
    private float feedbackUntil;
    private bool cardsDirty;
    private float nextRefresh;
    private readonly System.Collections.Generic.Dictionary<string, Image> lockImages = new System.Collections.Generic.Dictionary<string, Image>();
    private readonly System.Collections.Generic.Dictionary<Sprite, Sprite> croppedSprites = new System.Collections.Generic.Dictionary<Sprite, Sprite>();

    // One shared layout for the scene and every screen size. Keep the window's
    // proportions instead of stretching its art or letting the last row escape.
    public void ApplyLayout()
    {
        if (shopPanel == null) return;
        window = shopPanel.transform.Find("ShopWindow") as RectTransform;
        if (window == null) return;
        Place(window, Vector2.zero, new Vector2(1400f, 1080f));

        var art = Resources.Load<ShopArtCatalog>("ShopArtCatalog");
        if (art != null)
        {
            if (windowSprite == null) windowSprite = art.windowSprite;
            if (cardSprite == null) cardSprite = art.cardSprite;
            if (categoryPlaque == null) categoryPlaque = art.categoryPlaque;
            if (iconFrame == null) iconFrame = art.iconFrame;
            if (lockSprite == null) lockSprite = art.lockSprite;
            if (closeSprite == null) closeSprite = art.closeSprite;
            if (titleFont == null) titleFont = art.titleFont;
        }
        else Debug.LogError("Catálogo da arte da loja não encontrado em Resources.");
        var windowImage = window.GetComponent<Image>();
        if (windowImage != null && windowSprite != null)
        {
            windowImage.sprite = windowSprite;
            windowImage.type = Image.Type.Simple;
        }

        SetHeading("ShopTitle", "SHOP", 465f, 64);
        SetHeading("HeaderMartelo", "MARTELO", 345f, 30);
        SetHeading("HeaderOficina", "OFICINA", 130f, 30);
        SetHeading("HeaderResistencia", "RESISTÊNCIA", -85f, 30);
        SetHeading("HeaderMarteloLendario", "MARTELO LENDÁRIO", -300f, 30);
        var goldBadge = EnsureImage(window, "ShopGoldBadge", VisibleSprite(categoryPlaque, plaqueCrop));
        Place(goldBadge.rectTransform, new Vector2(455f, 465f), new Vector2(240f, 62f));
        var goldLabel = goldBadge.transform.Find("Value");
        if (goldLabel == null)
        {
            var labelObject = new GameObject("Value", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.layer = window.gameObject.layer;
            labelObject.transform.SetParent(goldBadge.transform, false);
            goldLabel = labelObject.transform;
        }
        shopGoldText = goldLabel.GetComponent<Text>();
        Place(shopGoldText.rectTransform, Vector2.zero, new Vector2(175f, 48f));
        if (cards != null && cards.Length > 0 && cards[0].infoText != null) shopGoldText.font = cards[0].infoText.font;
        shopGoldText.fontSize = 24;
        shopGoldText.fontStyle = FontStyle.Bold;
        shopGoldText.alignment = TextAnchor.MiddleCenter;
        shopGoldText.color = new Color(1f, 0.91f, 0.5f, 1f);
        shopGoldText.raycastTarget = false;

        string[] ids = { "hammer", "crit", "assistant", "forge", "bellows", "gloves", "hammer_tier2", "hammer_tier3" };
        for (int i = 0; i < ids.Length; i++)
        {
            var card = System.Array.Find(cards, c => c.upgradeId == ids[i]);
            if (card == null || card.button == null) continue;
            var rect = card.button.transform as RectTransform;
            Place(rect, new Vector2(i % 2 == 0 ? -322f : 322f, 250f - (i / 2) * 215f), new Vector2(600f, 150f));
            var background = card.button.GetComponent<Image>();
            if (background != null && cardSprite != null)
            {
                background.sprite = VisibleSprite(cardSprite, cardCrop);
                background.type = Image.Type.Simple;
            }
            var colors = card.button.colors;
            colors.disabledColor = new Color(0.8f, 0.8f, 0.85f, 1f);
            card.button.colors = colors;
            var frame = EnsureImage(rect, "IconFrame", VisibleSprite(iconFrame, frameCrop));
            Place(frame.rectTransform, new Vector2(-206f, 0f), new Vector2(128f, 128f));
            frame.transform.SetAsFirstSibling();

            if (card.icon == null && art != null) card.icon = art.FindIcon(card.upgradeId);

            Transform iconTransform = rect.Find("Card_" + card.upgradeId + "_Icon");
            Image iconImage = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            if (iconImage == null && card.icon != null)
            {
                var iconObject = new GameObject("Card_" + card.upgradeId + "_Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.layer = rect.gameObject.layer;
                iconObject.transform.SetParent(rect, false);
                iconImage = iconObject.GetComponent<Image>();
            }
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(true);
                iconImage.enabled = true;
                if (card.icon != null) iconImage.sprite = card.icon;
                iconImage.color = Color.white;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
                Place(iconImage.rectTransform, new Vector2(-206f, 0f), new Vector2(82f, 82f));
                iconImage.transform.SetAsLastSibling();
            }
            if (card.infoText != null)
            {
                Place(card.infoText.rectTransform, new Vector2(72f, 0f), new Vector2(386f, 118f));
                card.infoText.fontSize = 22;
                card.infoText.resizeTextForBestFit = false;
                card.infoText.alignment = TextAnchor.MiddleLeft;
                card.infoText.horizontalOverflow = HorizontalWrapMode.Wrap;
                card.infoText.verticalOverflow = VerticalWrapMode.Truncate;
                card.infoText.supportRichText = true;
                card.infoText.raycastTarget = false;
                card.infoText.transform.SetAsLastSibling();
            }
            var padlock = EnsureImage(rect, "LockedIndicator", VisibleSprite(lockSprite, lockCrop));
            Place(padlock.rectTransform, new Vector2(245f, 0f), new Vector2(44f, 54f));
            padlock.gameObject.SetActive(false);
            lockImages[card.upgradeId] = padlock;
        }
        if (closeButton != null)
        {
            Place(closeButton.transform as RectTransform, new Vector2(611f, 457f), new Vector2(66f, 66f));
            // Match the click to the visible glove, as on the other shop buttons.
            var closeTarget = closeButton.GetComponent<HammerClickTarget>();
            if (closeTarget == null) closeTarget = closeButton.gameObject.AddComponent<HammerClickTarget>();
            closeTarget.hitPadding = 20f;
            var closeImage = closeButton.GetComponent<Image>();
            if (closeImage != null)
            {
                closeImage.raycastTarget = false;
                if (closeSprite != null) closeImage.sprite = VisibleSprite(closeSprite, closeCrop);
            }
            var label = closeButton.GetComponentInChildren<Text>();
            if (label != null) label.enabled = false; // The close sprite already contains its X.
        }
        if (purchaseFeedback == null)
        {
            var footer = new GameObject("PurchaseFeedback", typeof(RectTransform), typeof(Text));
            footer.transform.SetParent(window, false);
            purchaseFeedback = footer.GetComponent<Text>();
            purchaseFeedback.font = shopGoldText.font;
            purchaseFeedback.fontSize = 19;
            purchaseFeedback.alignment = TextAnchor.MiddleCenter;
            purchaseFeedback.color = new Color(1, .91f, .66f);
            purchaseFeedback.raycastTarget = false;
        }
        Place(purchaseFeedback.rectTransform, new Vector2(0, -488), new Vector2(1100, 30));
        purchaseFeedback.text = "Cartão iluminado: disponível para comprar.";
        lastPanelSize = Vector2.zero;
        FitWindow();
    }

    // Slice out transparent export padding in Unity, without resampling the
    // pixel art. Normalized bounds also work when the texture importer scales it.
    private Sprite VisibleSprite(Sprite source, Vector4 bounds)
    {
        if (source == null || bounds.z <= 0f || bounds.w <= 0f) return source;
        if (croppedSprites.TryGetValue(source, out Sprite result)) return result;
        var texture = source.texture;
        var rect = new Rect(Mathf.Round(bounds.x * texture.width), Mathf.Round(bounds.y * texture.height),
            Mathf.Round(bounds.z * texture.width), Mathf.Round(bounds.w * texture.height));
        result = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        croppedSprites[source] = result;
        return result;
    }



    private static Image EnsureImage(Transform parent, string objectName, Sprite sprite)
    {
        var child = parent.Find(objectName);
        Image image = child != null ? child.GetComponent<Image>() : null;
        if (image == null)
        {
            var obj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.layer = parent.gameObject.layer;
            obj.transform.SetParent(parent, false);
            image = obj.GetComponent<Image>();
        }
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;
        image.preserveAspect = false;
        image.enabled = sprite != null;
        return image;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private void SetHeading(string objectName, string label, float y, int fontSize)
    {
        var heading = window.Find(objectName);
        if (heading == null) return;
        bool isTitle = objectName == "ShopTitle";
        Vector2 plaqueSize = isTitle ? new Vector2(600f, 112f) : new Vector2(440f, 66f);
        var plaque = EnsureImage(window, objectName + "_Plaque", VisibleSprite(categoryPlaque, plaqueCrop));
        Place(plaque.rectTransform, new Vector2(0f, y), plaqueSize);
        plaque.transform.SetSiblingIndex(heading.GetSiblingIndex());
        Place(heading as RectTransform, new Vector2(0f, y), new Vector2(plaqueSize.x - 110f, plaqueSize.y - 12f));
        var text = heading.GetComponent<Text>();
        if (text == null) return;
        text.text = label;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        if (isTitle && titleFont != null) text.font = titleFont;
        else if (cards != null && cards.Length > 0 && cards[0].infoText != null) text.font = cards[0].infoText.font;
        text.color = new Color(1f, 0.94f, 0.8f, 1f);
        var outline = text.GetComponent<Outline>();
        if (outline == null) outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.1f, 0.04f, 0.01f, 1f);
        outline.effectDistance = isTitle ? new Vector2(3f, -3f) : new Vector2(1f, -1f);
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
    }

    private void FitWindow()
    {
        if (window == null || shopPanel == null) return;
        var panel = shopPanel.transform as RectTransform;
        if (panel == null) return;
        Vector2 available = panel.rect.size;
        if (available.x <= 0f || available.y <= 0f || available == lastPanelSize) return;
        lastPanelSize = available;
        float scale = Mathf.Min(1f, Mathf.Min((available.x - 48f) / 1400f, (available.y - 48f) / 1080f));
        window.localScale = Vector3.one * Mathf.Max(0.01f, scale);
    }

    private void LateUpdate()
    {
        if (!IsShopOpen) return;
        FitWindow();
        if (cardsDirty && Time.unscaledTime >= nextRefresh)
        {
            cardsDirty = false; nextRefresh = Time.unscaledTime + .15f; RefreshCards();
        }
        if (purchaseFeedback != null && Time.unscaledTime >= feedbackUntil)
            purchaseFeedback.text = "Cartão iluminado: disponível para comprar.";
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        ApplyLayout();
        foreach (var card in cards)
        {
            string id = card.upgradeId;
            if (card.button != null)
            {
                card.button.onClick.AddListener(() => BuyAndRefresh(id));
            }
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseShop);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGoldChanged += HandleGoldChanged;
            GameManager.Instance.OnUpgradeChanged += RefreshCards;
        }

        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }

        RefreshCards();
    }

    private void OnDestroy()
    {
        foreach (var sprite in croppedSprites.Values)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(sprite);
            else Destroy(sprite);
#else
            Destroy(sprite);
#endif
        }
        croppedSprites.Clear();
        if (GameManager.Instance == null) return;

        GameManager.Instance.OnGoldChanged -= HandleGoldChanged;
        GameManager.Instance.OnUpgradeChanged -= RefreshCards;
    }

    private void HandleGoldChanged(double gold)
    {
        // So vale a pena reconstruir o texto dos cards enquanto a loja
        // esta visivel - com ouro passivo (aprendizes/forja) esse evento
        // dispara a cada frame, e refazer os cards escondidos toda hora
        // era trabalho (e alocacao de string) jogado fora.
        if (IsShopOpen)
        {
            cardsDirty = true;
        }
    }

    public void ToggleShop()
    {
        if (shopPanel == null || (GameManager.Instance != null && GameManager.Instance.CampaignCompleted)) return;

        if (!IsShopOpen && HordeManager.Instance != null && HordeManager.Instance.IsHordeInProgress) return;
        bool newState = !shopPanel.activeSelf;
        shopPanel.SetActive(newState);

        if (newState)
        {
            FitWindow();
            RefreshCards();
        }
    }

    public void CloseShop()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }
    }

    private void BuyAndRefresh(string id)
    {
        if (GameManager.Instance == null) return;
        if (HordeManager.Instance != null && HordeManager.Instance.IsHordeInProgress) return;
        var definition = GameManager.Instance.GetUpgrade(id);
        if (GameManager.Instance.TryBuyUpgrade(id) && purchaseFeedback != null)
        {
            purchaseFeedback.text = "✓ " + definition.displayName + " melhorado!  Nível " + definition.level;
            feedbackUntil = Time.unscaledTime + 2.5f;
        }
        RefreshCards();
    }

    private void RefreshCards()
    {
        if (GameManager.Instance == null) return;
        if (shopGoldText != null) shopGoldText.text = "OURO " + UIManager.FormatNumber(GameManager.Instance.Gold);

        foreach (var card in cards)
        {
            var def = GameManager.Instance.GetUpgrade(card.upgradeId);
            if (def == null || card.infoText == null) continue;

            bool unlocked = GameManager.Instance.IsUnlocked(def);
            bool maxed = def.maxLevel > 0 && def.level >= def.maxLevel;
            if (lockImages.TryGetValue(card.upgradeId, out Image padlock)) padlock.gameObject.SetActive(!unlocked);
            Place(card.infoText.rectTransform, new Vector2(unlocked ? 72f : 47f, 0f), new Vector2(unlocked ? 386f : 336f, 118f));

            bool affordable = GameManager.Instance.CanBuy(def);
            string title = "<size=24><b>" + def.displayName + "</b></size>";
            if (!unlocked)
            {
                var requirement = GameManager.Instance.GetUpgrade(def.requiresUpgradeId);
                string progress = requirement != null ? " (" + requirement.level + "/" + def.requiresUpgradeLevel + ")" : "";
                card.infoText.text = title + "\n<size=19><color=#C9C4D3>" + def.unlockHint + progress + "</color></size>\n<size=20><b>BLOQUEADO</b></size>";
            }
            else if (maxed)
            {
                card.infoText.text = title + "\n<size=18>" + def.description + "</size>\n<size=22><color=#BBF18C><b>✓ MÁXIMO</b></color></size>";
            }
            else
            {
                string price = UIManager.FormatNumber(def.currentCost) + " ouro";
                string status = affordable ? "<color=#BBF18C>COMPRAR • " + price + "</color>"
                    : "<color=#C9C4D3>" + price + " • faltam " + UIManager.FormatNumber(def.currentCost - GameManager.Instance.Gold) + "</color>";
                card.infoText.text = title + "\n<size=19>" + GameManager.Instance.GetNextUpgradePreview(def) + "</size>\n<size=18>Nível " + def.level + " → " + (def.level + 1) + "</size>\n<size=20><b>" + status + "</b></size>";
            }
            if (card.button != null)
            {
                card.button.interactable = affordable;
                var colors = card.button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1, .93f, .72f);
                colors.pressedColor = new Color(.83f, .70f, .42f);
                colors.disabledColor = maxed ? Color.white : new Color(.66f, .63f, .60f, 1);
                card.button.colors = colors;
                Outline border = card.button.GetComponent<Outline>();
                if (border == null) border = card.button.gameObject.AddComponent<Outline>();
                border.effectColor = maxed ? new Color(.6f, .82f, .35f, .8f) : new Color(1, .62f, .14f, .85f);
                border.effectDistance = new Vector2(2, -2);
                border.enabled = affordable || maxed;
            }
        }
    }
}
