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
    }

    public UpgradeCardUI[] cards;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
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
            RefreshCards();
        }
    }

    public void ToggleShop()
    {
        if (shopPanel == null) return;

        bool newState = !shopPanel.activeSelf;
        shopPanel.SetActive(newState);

        if (newState)
        {
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
        GameManager.Instance.TryBuyUpgrade(id);
        RefreshCards();
    }

    private void RefreshCards()
    {
        if (GameManager.Instance == null) return;

        foreach (var card in cards)
        {
            var def = GameManager.Instance.GetUpgrade(card.upgradeId);
            if (def == null || card.infoText == null) continue;

            bool unlocked = GameManager.Instance.IsUnlocked(def);
            bool maxed = def.maxLevel > 0 && def.level >= def.maxLevel;

            if (!unlocked)
            {
                card.infoText.text = "<b>" + def.displayName + "</b>\n<color=#9A9A9A>Bloqueado - " + def.unlockHint + "</color>";
                if (card.button != null) card.button.interactable = false;
            }
            else if (maxed)
            {
                card.infoText.text = "<b>" + def.displayName + "</b>\n" + def.description + "\n<color=#8FBF6B>Nivel maximo!</color>";
                if (card.button != null) card.button.interactable = false;
            }
            else
            {
                card.infoText.text = "<b>" + def.displayName + "</b>\n" + def.description +
                    "\nNivel " + def.level + " - " + UIManager.FormatNumber(def.currentCost) + " ouro";

                if (card.button != null)
                {
                    card.button.interactable = GameManager.Instance.Gold >= def.currentCost;
                }
            }
        }
    }
}
