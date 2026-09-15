using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Atualiza os textos da HUD (ouro, ouro por clique) e o botao de upgrade.
/// Coloque este script em um GameObject da Canvas (ex: "UIManager") e
/// arraste as referencias de UI no Inspector.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("HUD")]
    public Text goldText;
    public Text goldPerClickText;

    [Header("Upgrade")]
    public Button upgradeButton;
    public Text upgradeButtonText;

    private void Start()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("GameManager nao encontrado na cena.");
            return;
        }

        GameManager.Instance.OnGoldChanged += HandleGoldChanged;
        GameManager.Instance.OnUpgradeChanged += HandleUpgradeChanged;

        if (upgradeButton != null)
        {
            upgradeButton.onClick.AddListener(OnUpgradeButtonClicked);
        }

        RefreshAll();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.OnGoldChanged -= HandleGoldChanged;
        GameManager.Instance.OnUpgradeChanged -= HandleUpgradeChanged;
    }

    private void OnUpgradeButtonClicked()
    {
        GameManager.Instance.TryBuyUpgrade();
        // RefreshAll() ja vai rodar via eventos (OnGoldChanged / OnUpgradeChanged),
        // mas chamamos aqui tambem pra garantir que o botao atualiza na hora.
        RefreshAll();
    }

    private void HandleGoldChanged(double gold)
    {
        if (goldText != null)
        {
            goldText.text = FormatNumber(gold);
        }

        UpdateUpgradeButtonInteractable();
    }

    private void HandleUpgradeChanged()
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        if (goldText != null)
        {
            goldText.text = FormatNumber(GameManager.Instance.Gold);
        }

        if (goldPerClickText != null)
        {
            goldPerClickText.text = GameManager.Instance.GoldPerClick + " por golpe";
        }

        if (upgradeButtonText != null)
        {
            upgradeButtonText.text = "Melhorar martelo\n(" + FormatNumber(GameManager.Instance.UpgradeCost) + " ouro)";
        }

        UpdateUpgradeButtonInteractable();
    }

    private void UpdateUpgradeButtonInteractable()
    {
        if (upgradeButton == null) return;
        upgradeButton.interactable = GameManager.Instance.Gold >= GameManager.Instance.UpgradeCost;
    }

    /// <summary>Formata numeros grandes como 1.2K, 3.4M etc. Mantem inteiros pequenos como estao.</summary>
    private string FormatNumber(double value)
    {
        if (value < 1000) return value.ToString("0");

        string[] suffixes = { "", "K", "M", "B", "T" };
        int suffixIndex = 0;
        double reduced = value;

        while (reduced >= 1000 && suffixIndex < suffixes.Length - 1)
        {
            reduced /= 1000;
            suffixIndex++;
        }

        return reduced.ToString("0.0") + suffixes[suffixIndex];
    }
}
