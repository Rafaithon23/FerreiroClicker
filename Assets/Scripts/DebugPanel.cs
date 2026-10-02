using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FERRAMENTA DE DEBUG - so pra testar mais rapido durante o desenvolvimento
/// (adicionar ouro sem ficar martelando, pra testar as hordas/dificuldade
/// mais rapido). Builda a propria UI em runtime e nao e referenciado por
/// NENHUM outro script do jogo - pra tirar isso do jogo antes de
/// apresentar, basta deletar o GameObject "DebugPanel" da cena (ou
/// desativa-lo) e, se quiser, apagar este arquivo. Nao bugza nem muda
/// nada do que ja existe.
///
/// COMO USAR: cria um GameObject vazio na cena (ex: "DebugPanel") e
/// arrasta este script nele. Nao precisa configurar nada.
/// </summary>
public class DebugPanel : MonoBehaviour
{
    [Tooltip("Valores de ouro que cada botao adiciona de uma vez.")]
    public double[] amounts = { 1000, 10000, 25000, 50000, 80000, 150000 };

    private void Start()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject panelGo = new GameObject("DebugPanelUI", typeof(RectTransform));
        panelGo.transform.SetParent(canvas.transform, false);

        RectTransform panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 1f);
        panelRt.anchorMax = new Vector2(0f, 1f);
        panelRt.pivot = new Vector2(0f, 1f);
        panelRt.anchoredPosition = new Vector2(12f, -12f);
        panelRt.sizeDelta = new Vector2(170f, 34f * (amounts.Length + 1) + 16f);

        Image bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);

        VerticalLayoutGroup layout = panelGo.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateLabel(panelGo.transform, "DEBUG (+gold)");

        foreach (double amount in amounts)
        {
            double amountCopy = amount; // evita captura da variavel de loop
            CreateButton(panelGo.transform, "+" + UIManager.FormatNumber(amountCopy), () =>
            {
                GameManager.Instance?.AddBonusGold(amountCopy);
            });
        }
    }

    private void CreateLabel(Transform parent, string text)
    {
        GameObject go = new GameObject("DebugLabel", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 22f;

        Text t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 14;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(1f, 0.85f, 0.3f);
        t.text = text;
    }

    private void CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject("DebugButton_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 28f;

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);

        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        Text t = textGo.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 14;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = label;
        t.raycastTarget = false;
    }
}
