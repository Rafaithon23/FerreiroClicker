using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class CampaignGoalUI : MonoBehaviour
{
    private GameManager game;
    private Canvas canvas;
    private RectTransform victoryContent;
    private GameObject victoryPanel;
    private Font font;
    private bool returningToMenu;

    private void Start()
    {
        game = GameManager.Instance;
        canvas = GetComponentInParent<Canvas>();
        if (game == null || canvas == null) return;
        font = UIManager.Instance.goldText.font;
        BuildVictory();
        game.OnCampaignCompleted += ShowVictory;
        if (game.CampaignCompleted) ShowVictory();
    }

    private void BuildVictory()
    {
        victoryPanel = MakeRect("CampaignVictory", canvas.transform, Vector2.one * .5f, Vector2.zero).gameObject;
        var root = (RectTransform)victoryPanel.transform;
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        Image dim = victoryPanel.AddComponent<Image>(); dim.color = new Color(0, 0, 0, .88f);
        victoryContent = MakeRect("VictoryContent", root, Vector2.one * .5f, new Vector2(950, 490));
        Image panel = victoryContent.gameObject.AddComponent<Image>(); panel.color = new Color(.09f, .05f, .025f, 1);
        Outline edge = victoryContent.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(.95f, .61f, .15f); edge.effectDistance = new Vector2(3, -3);
        MakeText("VictoryTitle", victoryContent, 53, new Vector2(870, 78), new Vector2(0, 155)).text = "VITÓRIA!";
        MakeText("VictoryMessage", victoryContent, 28, new Vector2(860, 150), new Vector2(0, 40)).text =
            "Você se tornou um Mestre Ferreiro!\n\nMartelo Lendário obtido\nHorda de Cavaleiros Negros derrotada";
        RectTransform buttonRect = MakeRect("VictoryMenuButton", victoryContent, Vector2.one * .5f, new Vector2(440, 76));
        buttonRect.anchoredPosition = new Vector2(0, -135);
        Image buttonImage = buttonRect.gameObject.AddComponent<Image>(); buttonImage.color = new Color(.43f, .24f, .06f);
        Button button = buttonRect.gameObject.AddComponent<Button>(); button.targetGraphic = buttonImage;
        buttonRect.gameObject.AddComponent<HammerClickTarget>();
        var colors = button.colors; colors.highlightedColor = new Color(1, .83f, .40f); button.colors = colors;
        MakeText("Label", buttonRect, 25, new Vector2(420, 68), Vector2.zero).text = "VOLTAR AO MENU";
        button.onClick.AddListener(() =>
        {
            if (returningToMenu) return;
            returningToMenu = true;
            if (SceneTransition.Instance != null) SceneTransition.Instance.LoadScene("MainMenu");
            else SceneManager.LoadScene("MainMenu");
        });
        MakeText("VictorySaved", victoryContent, 19, new Vector2(850, 36), new Vector2(0, -205)).text = "Conclusão salva automaticamente.";
        victoryPanel.SetActive(false);
    }

    private void ShowVictory()
    {
        ShopManager.Instance?.CloseShop();
        HammerFollowMouse.Instance?.PutDown();
        victoryPanel.SetActive(true); victoryPanel.transform.SetAsLastSibling();
        if (HammerFollowMouse.Instance != null) HammerFollowMouse.Instance.transform.SetAsLastSibling();
    }

    private void LateUpdate()
    {
        if (canvas == null) return;
        Vector2 size = ((RectTransform)canvas.transform).rect.size;
        if (victoryContent != null) victoryContent.localScale = Vector3.one * Mathf.Clamp(Mathf.Min((size.x - 48) / 950, (size.y - 48) / 490), .1f, 1);
    }

    private Text MakeText(string name, Transform parent, int fontSize, Vector2 size, Vector2 position)
    {
        RectTransform rect = MakeRect(name, parent, Vector2.one * .5f, size); rect.anchoredPosition = position;
        Text text = rect.gameObject.AddComponent<Text>(); text.font = font; text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold; text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1, .91f, .65f); text.raycastTarget = false;
        return text;
    }

    private static RectTransform MakeRect(string name, Transform parent, Vector2 anchor, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = Vector2.one * .5f; rect.sizeDelta = size; return rect;
    }

    private void OnDestroy()
    {
        if (game != null) { game.OnCampaignCompleted -= ShowVictory; }
        if (victoryPanel != null) Destroy(victoryPanel);
    }
}
