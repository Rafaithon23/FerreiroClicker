using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Guarda o estado do jogo (ouro, forca do martelo, custo do upgrade),
/// salva/carrega com PlayerPrefs e avisa a UI quando algo muda.
/// Coloque este script em um GameObject vazio chamado "GameManager" na cena.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Economia")]
    [Tooltip("Ouro ganho por clique no comeco do jogo")]
    public int startingGoldPerClick = 1;

    [Tooltip("Custo do primeiro upgrade do martelo")]
    public double startingUpgradeCost = 10;

    [Tooltip("Multiplicador aplicado ao custo a cada upgrade comprado")]
    public float upgradeCostMultiplier = 1.15f;

    private double gold;
    private int goldPerClick;
    private double upgradeCost;

    private const string KEY_GOLD = "bc_gold";
    private const string KEY_GOLD_PER_CLICK = "bc_goldPerClick";
    private const string KEY_UPGRADE_COST = "bc_upgradeCost";

    /// <summary>Disparado sempre que o total de ouro muda. UI escuta este evento.</summary>
    public event Action<double> OnGoldChanged;

    /// <summary>Disparado quando o upgrade e comprado (mudou goldPerClick/upgradeCost).</summary>
    public event Action OnUpgradeChanged;

    public double Gold => gold;
    public int GoldPerClick => goldPerClick;
    public double UpgradeCost => upgradeCost;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadGame();
    }

    /// <summary>Chamado pelo AnvilClicker toda vez que o jogador bate na bigorna.</summary>
    public void AddGold(int amount)
    {
        gold += amount;
        OnGoldChanged?.Invoke(gold);
        SaveGame();
    }

    /// <summary>Chamado pelo botao de upgrade na UI.</summary>
    public bool TryBuyUpgrade()
    {
        if (gold < upgradeCost)
        {
            return false;
        }

        gold -= upgradeCost;
        goldPerClick += 1;
        upgradeCost = Math.Ceiling(upgradeCost * upgradeCostMultiplier);

        OnGoldChanged?.Invoke(gold);
        OnUpgradeChanged?.Invoke();
        SaveGame();
        return true;
    }

    private void SaveGame()
    {
        // Usamos string (InvariantCulture) em vez de float pra nao perder precisao
        // quando o numero de ouro ficar bem grande.
        PlayerPrefs.SetString(KEY_GOLD, gold.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.SetInt(KEY_GOLD_PER_CLICK, goldPerClick);
        PlayerPrefs.SetString(KEY_UPGRADE_COST, upgradeCost.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    private void LoadGame()
    {
        goldPerClick = PlayerPrefs.HasKey(KEY_GOLD_PER_CLICK)
            ? PlayerPrefs.GetInt(KEY_GOLD_PER_CLICK)
            : startingGoldPerClick;

        gold = PlayerPrefs.HasKey(KEY_GOLD)
            ? double.Parse(PlayerPrefs.GetString(KEY_GOLD), CultureInfo.InvariantCulture)
            : 0;

        upgradeCost = PlayerPrefs.HasKey(KEY_UPGRADE_COST)
            ? double.Parse(PlayerPrefs.GetString(KEY_UPGRADE_COST), CultureInfo.InvariantCulture)
            : startingUpgradeCost;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) SaveGame();
    }

    private void OnApplicationQuit()
    {
        SaveGame();
    }

    /// <summary>Util pra zerar o progresso durante testes (nao chamado por nada ainda).</summary>
    [ContextMenu("Resetar progresso")]
    public void ResetProgress()
    {
        PlayerPrefs.DeleteKey(KEY_GOLD);
        PlayerPrefs.DeleteKey(KEY_GOLD_PER_CLICK);
        PlayerPrefs.DeleteKey(KEY_UPGRADE_COST);
        LoadGame();
        OnGoldChanged?.Invoke(gold);
        OnUpgradeChanged?.Invoke();
    }
}
