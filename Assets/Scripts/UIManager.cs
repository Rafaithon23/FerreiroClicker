using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Atualiza os textos da HUD (ouro, ouro por martelada, ouro por segundo)
/// e a barra de resistencia do braco, e abre a loja quando o botao "Loja"
/// e clicado. Os upgrades em si moraram para o ShopManager.
/// Coloque este script em um GameObject da Canvas (ex: "UIManager") e
/// arraste as referencias de UI no Inspector.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("HUD - Ouro")]
    public Text goldText;
    public Text goldPerClickText;
    public Text goldPerSecondText;

    [Header("Obstaculo - Resistencia")]
    public Slider staminaSlider;

    [Header("Loja")]
    public Button shopButton;

    [Header("Sair")]
    [Tooltip("Botao de sair direto da tela de jogo, sem precisar voltar pro menu.")]
    public Button quitButton;

    private void Start()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("GameManager nao encontrado na cena.");
            return;
        }

        GameManager.Instance.OnGoldChanged += HandleGoldChanged;
        GameManager.Instance.OnUpgradeChanged += HandleUpgradeChanged;
        GameManager.Instance.OnStaminaChanged += HandleStaminaChanged;

        if (shopButton != null)
        {
            shopButton.onClick.AddListener(OnShopButtonClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(OnQuitButtonClicked);
        }

        if (staminaSlider != null)
        {
            staminaSlider.minValue = 0f;
            staminaSlider.maxValue = 1f;
        }

        RefreshAll();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.OnGoldChanged -= HandleGoldChanged;
        GameManager.Instance.OnUpgradeChanged -= HandleUpgradeChanged;
        GameManager.Instance.OnStaminaChanged -= HandleStaminaChanged;
    }

    /// <summary>
    /// Fecha o jogo direto da tela de jogo (mesma logica do botao Sair do menu,
    /// ver MenuManager.QuitGame()). O progresso salva sozinho - GameManager ja
    /// salva em OnApplicationQuit(), chamado tanto por Application.Quit() quanto
    /// ao sair do Play Mode no Editor.
    /// </summary>
    private void OnQuitButtonClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnShopButtonClicked()
    {
        if (ShopManager.Instance == null) return;

        // So pode ABRIR a loja com a mao vazia (martelo largado no suporte).
        // Fechar sempre pode - nesse ponto o jogador ja esta sem o martelo,
        // ja que ele nao consegue pegar o martelo com a loja aberta.
        bool tryingToOpen = !ShopManager.Instance.IsShopOpen;
        bool isHoldingHammer = HammerFollowMouse.Instance != null && HammerFollowMouse.Instance.IsHolding;

        if (tryingToOpen && isHoldingHammer)
        {
            if (AnvilClicker.Instance != null)
            {
                AnvilClicker.Instance.SpawnFloatingText("Solte o martelo primeiro!");
            }
            return;
        }

        ShopManager.Instance.ToggleShop();
    }

    private void HandleGoldChanged(double gold)
    {
        if (goldText != null)
        {
            goldText.text = FormatNumber(gold);
        }
    }

    private void HandleUpgradeChanged()
    {
        RefreshAll();
    }

    private void HandleStaminaChanged(float current, float max)
    {
        if (staminaSlider != null)
        {
            staminaSlider.value = max > 0 ? current / max : 0f;
        }
    }

    private void RefreshAll()
    {
        if (goldText != null)
        {
            goldText.text = FormatNumber(GameManager.Instance.Gold);
        }

        if (goldPerClickText != null)
        {
            goldPerClickText.text = GameManager.Instance.GoldPerClick + " por martelada";
        }

        if (goldPerSecondText != null)
        {
            goldPerSecondText.text = FormatNumber(GameManager.Instance.GoldPerSecond) + " por segundo";
        }

        if (staminaSlider != null)
        {
            staminaSlider.value = GameManager.Instance.MaxStamina > 0
                ? GameManager.Instance.Stamina / GameManager.Instance.MaxStamina
                : 0f;
        }
    }

    /// <summary>Formata numeros grandes como 1.2K, 3.4M etc. Mantem inteiros pequenos como estao.</summary>
    public static string FormatNumber(double value)
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
