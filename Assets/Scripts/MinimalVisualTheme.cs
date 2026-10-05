using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Visual styling only. Existing buttons, colliders, saves and gameplay remain owned by their controllers.
public class MinimalVisualTheme : MonoBehaviour
{
    public static bool IsReady { get; private set; }
    public static readonly Color Ink = new Color(.157f, .145f, .20f);
    public static readonly Color Panel = new Color(.21f, .20f, .26f);
    public static readonly Color Steel = new Color(.435f, .47f, .514f);
    public static readonly Color Cream = new Color(.875f, .773f, .631f);
    public static readonly Color Wood = new Color(.50f, .329f, .239f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetHook() { IsReady = false; SceneManager.sceneLoaded -= OnSceneLoaded; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenu" || scene.name == "SampleScene")
        {
            IsReady = false;
            new GameObject("MinimalVisualTheme").AddComponent<MinimalVisualTheme>();
        }
    }
    private IEnumerator Start()
    {
        // Runtime shop, tutorial and combat panels are built in their controllers' Start methods.
        yield return null;
        var scene = gameObject.scene;
        bool menu = scene.name == "MainMenu";
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                string n = image.gameObject.name;
                if (menu && (n == "TitleLogo" || n == "TitleEmblem" || n == "AnvilProp" || n == "HammerProp"))
                    image.enabled = false;
                if (n == "HudPanel" || n == "ShopWindow" || n == "VictoryContent" || n == "Introduction" || n == "HordeWarningPanel")
                    Skin(image, Panel);
                if (n.EndsWith("_Plaque")) Skin(image, n == "ShopTitle_Plaque" ? Parchment : Green);
                if (n == "ShopGoldBadge" || n == "IconFrame") Solid(image, Ink, false);
                if (menu && n == "TitlePlaque")
                {
                    image.gameObject.SetActive(true); image.enabled = true; Skin(image, Parchment);
                    var r = image.rectTransform; r.anchorMin = r.anchorMax = new Vector2(.5f, .80f);
                    r.pivot = Vector2.one * .5f; r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(760, 235);
                    r.SetAsFirstSibling();
                }
                if (n == "Background" && image.GetComponentInParent<Slider>() != null) Solid(image, Ink, true);
                if (n == "HudContrast") Solid(image, Ink, false);
                if (n == "HudDivider") Solid(image, Steel, false);
                if (n == "Fill" && image.GetComponentInParent<Slider>() != null) Solid(image, Steel, false);
            }
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.GetComponent<EnemyTarget>() != null) continue;
                Image image = button.targetGraphic as Image;
                if (image != null) Skin(image, button.name.StartsWith("Card_") ? Panel : Green);
                var c = button.colors;
                c.normalColor = Color.white; c.highlightedColor = new Color(1, .94f, .82f);
                c.pressedColor = new Color(.77f, .74f, .69f); c.disabledColor = new Color(.64f, .63f, .67f);
                button.colors = c;
                if (button.name.StartsWith("Card_"))
                {
                    Outline edge = button.GetComponent<Outline>();
                    Text label = button.GetComponentInChildren<Text>();
                    if (edge != null) edge.enabled = button.interactable || (label != null && label.text.Contains("MÁXIMO"));
                }
            }
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.name == "TitleText" && menu)
                {
                    text.gameObject.SetActive(true); text.enabled = true;
                    text.text = "FERREIRO\nCLICKER";
                    var art = Resources.Load<ShopArtCatalog>("ShopArtCatalog");
                    if (art != null && art.titleFont != null) text.font = art.titleFont;
                    text.fontSize = 70; text.resizeTextForBestFit = true;
                    text.resizeTextMinSize = 32; text.resizeTextMaxSize = 70;
                    text.alignment = TextAnchor.MiddleCenter;
                    RectTransform r = text.rectTransform;
                    r.anchorMin = r.anchorMax = new Vector2(.5f, .80f);
                    r.pivot = Vector2.one * .5f; r.anchoredPosition = Vector2.zero;
                    r.sizeDelta = new Vector2(1100, 210);
                }
                Button parentButton = text.GetComponentInParent<Button>(true);
                if (menu && parentButton != null && (parentButton.name == "StartButton" || parentButton.name == "QuitButton"))
                { text.gameObject.SetActive(true); text.enabled = true; text.raycastTarget = false; text.text = parentButton.name == "StartButton" ? "JOGAR" : "SAIR"; }
                if (text.name == "CloseButtonText") { text.enabled = true; text.text = "X"; }
                if (text.name.StartsWith("Header"))
                {
                    text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    text.fontStyle = FontStyle.Bold; text.fontSize = 28;
                    text.resizeTextForBestFit = false; text.horizontalOverflow = HorizontalWrapMode.Overflow;
                    text.verticalOverflow = VerticalWrapMode.Truncate;
                    text.rectTransform.sizeDelta = new Vector2(520, 54);
                }
                if (parentButton != null && parentButton.name.StartsWith("Card_"))
                { text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.lineSpacing = 1f; }
                if (text.GetComponentInParent<Button>() != null && !text.GetComponentInParent<Button>().name.StartsWith("Card_"))
                { var art = Resources.Load<ShopArtCatalog>("ShopArtCatalog"); if (art != null && art.titleFont != null) text.font = art.titleFont;
                  text.resizeTextForBestFit = true; text.resizeTextMinSize = 12; text.resizeTextMaxSize = text.fontSize; }
                text.color = (menu && text.name == "TitleText") || text.name == "ShopTitle" || (parentButton != null && !parentButton.name.StartsWith("Card_")) ? WoodInk : Cream;
                foreach (Outline outline in text.GetComponents<Outline>())
                { outline.effectColor = Ink; outline.effectDistance = new Vector2(1, -1);
                  if ((menu && text.name == "TitleText") || text.name == "ShopTitle" || (parentButton != null && !parentButton.name.StartsWith("Card_"))) outline.enabled = false; }
            }
        }
        IsReady = true;
    }
    private static readonly Color Green = new Color(.34f, .58f, .42f);
    private static readonly Color Parchment = new Color(.88f, .78f, .57f);
    private static readonly Color WoodInk = new Color(.24f, .16f, .11f);
    private readonly System.Collections.Generic.Dictionary<Color, Sprite> skins = new System.Collections.Generic.Dictionary<Color, Sprite>();

    // A small sliced sprite keeps the frame corners crisp at every panel/button size.
    private void Skin(Image image, Color fill)
    {
        // Os painéis atuais não dependem das antigas texturas de fundo.
        image.enabled = true;
        if (!skins.TryGetValue(fill, out Sprite sprite))
        {
            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            Color rim = new Color(.35f, .24f, .15f);
            Color shine = Color.Lerp(fill, new Color(1f, .92f, .69f), .30f);
            Color shade = Color.Lerp(fill, WoodInk, .32f);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int edge = Mathf.Min(x, y, size - 1 - x, size - 1 - y);
                Color c = edge == 0 ? WoodInk : edge <= 2 ? rim : fill;
                if (edge >= 3 && edge <= 5) c = y > size / 2 || x < size / 2 ? shine : shade;
                if ((x < 2 || x >= size - 2) && (y < 2 || y >= size - 2)) c = Color.clear;
                pixels[y * size + x] = c;
            }
            texture.SetPixels(pixels); texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100f, 0,
                SpriteMeshType.FullRect, new Vector4(6, 6, 6, 6));
            skins.Add(fill, sprite);
        }
        image.sprite = sprite; image.type = Image.Type.Sliced; image.color = Color.white; image.pixelsPerUnitMultiplier = .5f;
        Shadow shadow = null;
        foreach (Shadow candidate in image.GetComponents<Shadow>())
            if (candidate.GetType() == typeof(Shadow)) shadow = candidate;
        if (shadow == null) shadow = image.gameObject.AddComponent<Shadow>();
        shadow.enabled = true; shadow.effectColor = new Color(.12f, .09f, .08f, .7f); shadow.effectDistance = new Vector2(3, -5);
        Outline outline = image.GetComponent<Outline>(); if (outline != null) outline.enabled = false;
    }
    private void OnDestroy()
    {
        foreach (Sprite sprite in skins.Values) { Destroy(sprite.texture); Destroy(sprite); }
        skins.Clear();
    }
    private static void Solid(Image image, Color color, bool border)
    {
        image.enabled = true;
        image.sprite = null; image.type = Image.Type.Simple; image.color = color;
        if (!border) return;
        Outline edge = image.GetComponent<Outline>();
        if (edge == null) edge = image.gameObject.AddComponent<Outline>();
        edge.enabled = true; edge.effectColor = Steel; edge.effectDistance = new Vector2(2, -2);
    }
}

