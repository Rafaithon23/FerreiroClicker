using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Tipo de efeito que um upgrade da loja aplica ao jogo.
/// </summary>
public enum UpgradeEffect
{
    GoldPerClick,         // aumenta o ouro ganho por martelada
    CritChance,           // aumenta a chance de golpe critico
    GoldPerSecond,        // aumenta a producao passiva de ouro
    StaminaCostReduction, // reduz o gasto de resistencia por martelada
    MaxStaminaIncrease,   // aumenta a resistencia maxima do braco
    HammerUpgrade         // compra de tier de martelo (nao soma generico - ver GameManager.HammerTier)
}

/// <summary>
/// Definicao de um item da loja de upgrades. O catalogo inteiro e montado
/// em codigo (GameManager.BuildCatalog), entao nao precisa configurar nada
/// no Inspector - so arrastar as referencias de UI nos outros scripts.
/// </summary>
[Serializable]
public class UpgradeDefinition
{
    public string id;
    public string displayName;
    public string description;
    public UpgradeEffect effect;
    public double baseCost;
    public float costMultiplier = 1.15f;
    public double perLevelAmount = 1;
    public int maxLevel = 0; // 0 = sem limite

    [Tooltip("Id de outro upgrade que precisa estar no nivel minimo pra este desbloquear")]
    public string requiresUpgradeId = "";
    public int requiresUpgradeLevel = 0;
    [Tooltip("Texto mostrado na loja enquanto o upgrade esta bloqueado")]
    public string unlockHint = "";

    public int level;
    public double currentCost;
}

/// <summary>
/// Guarda todo o estado do jogo: ouro, resistencia do braco (obstaculo) e
/// o catalogo de upgrades da loja (martelo, golpe critico, aprendizes,
/// forja automatica, fole e luvas). Salva/carrega com PlayerPrefs e avisa
/// a UI quando algo muda.
/// Coloque este script em um GameObject vazio chamado "GameManager".
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Economia base")]
    [Tooltip("Ouro ganho por martelada antes de qualquer upgrade")]
    public int startingGoldPerClick = 1;

    [Header("Golpe critico")]
    [Tooltip("Multiplicador de ouro aplicado quando sai um golpe critico")]
    public double critMultiplier = 3.0;
    [Tooltip("Chance maxima de critico (0 a 1), mesmo com upgrades no talo")]
    public float maxCritChance = 0.5f;

    [Header("Obstaculo - Resistencia do braco")]
    public float baseMaxStamina = 100f;
    public float baseStaminaCostPerHit = 10f;
    [Tooltip("O Fole nao deixa o gasto por martelada cair abaixo disso")]
    public float minStaminaCostPerHit = 5f;
    public float staminaRegenPerSecond = 18f;

    /// <summary>Catalogo completo da loja. Montado em Awake.</summary>
    public List<UpgradeDefinition> Upgrades { get; private set; }

    public bool CampaignCompleted { get; private set; }
    public event Action OnCampaignCompleted;
    private const string KEY_CAMPAIGN_COMPLETED = "bc_campaign_completed";

    private double gold;
    private double totalGoldEarned;
    private float currentStamina;
    private bool lastHitWasCrit;

    // Multiplicadores temporarios (nao usados pela Horda atualmente, mas
    // ficam disponiveis pra outros efeitos futuros). Ficam em 1 (sem efeito)
    // o tempo todo, exceto durante a janela de um buff.
    private float tempGoldPerClickMultiplier = 1f;
    private float tempGoldPerSecondMultiplier = 1f;
    private float tempStaminaCostMultiplier = 1f;

    // Bonus PERMANENTE (em %) escolhido pelo jogador ao vencer uma Horda -
    // ver HordeManager. Diferente dos multiplicadores temporarios acima,
    // esse nunca reseta sozinho e e salvo no jogo.
    private float goldPerClickBonusPercent;
    private float goldPerSecondBonusPercent;

    // Em vez de gravar no PlayerPrefs a cada martelada (o que forca uma escrita
    // em disco por clique e pode engasgar o jogo), guardamos as mudancas na
    // memoria e salvamos de tempos em tempos (ver Update/autosave abaixo), alem
    // de sempre salvar ao comprar upgrade, pausar ou fechar o jogo.
    private bool hasUnsavedChanges;
    private float autosaveTimer;
    private const float AUTOSAVE_INTERVAL = 10f;

    private const string KEY_GOLD = "bc_gold";
    private const string KEY_TOTAL_GOLD_EARNED = "bc_total_gold_earned";
    private const string KEY_HORDE_CLICK_BONUS = "bc_horde_click_bonus_pct";
    private const string KEY_HORDE_SECOND_BONUS = "bc_horde_second_bonus_pct";

    /// <summary>Disparado sempre que o total de ouro muda. UI escuta este evento.</summary>
    public event Action<double> OnGoldChanged;

    /// <summary>
    /// Disparado sempre que o ouro TOTAL ganho na partida (nunca diminui, mesmo
    /// comprando upgrade) muda. A HordeManager escuta isso pra saber quando
    /// cruzar um marco de 10 mil e disparar uma horda.
    /// </summary>
    public event Action<double> OnTotalGoldEarnedChanged;

    /// <summary>Disparado quando algum upgrade da loja e comprado.</summary>
    public event Action OnUpgradeChanged;

    /// <summary>
    /// Disparado quando o tier do martelo muda (compra do Martelo Reforcado ou
    /// do Martelo Lendario). HammerPickup/HammerFollowMouse escutam isso pra
    /// trocar a sprite do martelo (suporte e luva) na hora.
    /// </summary>
    public event Action<int> OnHammerTierChanged;

    /// <summary>Disparado quando a resistencia do braco muda (current, max).</summary>
    public event Action<float, float> OnStaminaChanged;

    public double Gold => gold;
    public double TotalGoldEarned => totalGoldEarned;
    public float GoldPerClickBonusPercent => goldPerClickBonusPercent;
    public float GoldPerSecondBonusPercent => goldPerSecondBonusPercent;
    public float Stamina => currentStamina;
    public bool LastHitWasCrit => lastHitWasCrit;

    public float MaxStamina => baseMaxStamina + (float)GetEffectTotal(UpgradeEffect.MaxStaminaIncrease);
    public float StaminaCostPerHit => Mathf.Max(minStaminaCostPerHit, baseStaminaCostPerHit - (float)GetEffectTotal(UpgradeEffect.StaminaCostReduction)) * tempStaminaCostMultiplier;
    public int GoldPerClick => Mathf.Max(1, Mathf.RoundToInt((startingGoldPerClick + (int)GetEffectTotal(UpgradeEffect.GoldPerClick)) * tempGoldPerClickMultiplier * (1f + goldPerClickBonusPercent / 100f)));
    public float CritChance => Mathf.Min(maxCritChance, (float)GetEffectTotal(UpgradeEffect.CritChance) + HammerCritBonusPercent);

    /// <summary>
    /// Tier atual do martelo: 1 = martelo base (sempre), 2 = Martelo Reforcado,
    /// 3 = Martelo Lendario. Usado pelo dano contra hordas, pelo bonus de
    /// critico acima e pela troca de sprite (HammerPickup/HammerFollowMouse).
    /// </summary>
    public int HammerTier
    {
        get
        {
            var tier3 = GetUpgrade("hammer_tier3");
            if (tier3 != null && tier3.level > 0) return 3;

            var tier2 = GetUpgrade("hammer_tier2");
            if (tier2 != null && tier2.level > 0) return 2;

            return 1;
        }
    }

    /// <summary>Dano (em "vida") que cada martelada tira de um inimigo de horda, conforme o tier do martelo.</summary>
    public int HammerDamagePerHit
    {
        get
        {
            switch (HammerTier)
            {
                case 3: return 4;
                case 2: return 2;
                default: return 1;
            }
        }
    }

    /// <summary>Bonus de chance de critico (fracao, ex: 0.03 = +3%) concedido pelo tier do martelo.</summary>
    private float HammerCritBonusPercent
    {
        get
        {
            switch (HammerTier)
            {
                case 3: return 0.08f;
                case 2: return 0.03f;
                default: return 0f;
            }
        }
    }

    public double GoldPerSecond => GetEffectTotal(UpgradeEffect.GoldPerSecond) * tempGoldPerSecondMultiplier * (1f + goldPerSecondBonusPercent / 100f);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildCatalog();
        LoadGame();
        currentStamina = MaxStamina;
    }

    private void Update()
    {
        if (CampaignCompleted) return;
        // Regenera a resistencia do braco aos poucos.
        if (currentStamina < MaxStamina)
        {
            currentStamina = Mathf.Min(MaxStamina, currentStamina + staminaRegenPerSecond * Time.deltaTime);
            OnStaminaChanged?.Invoke(currentStamina, MaxStamina);
        }

        // Producao passiva de ouro (aprendizes + forja automatica).
        // Pausa durante uma horde ativa: o jogador fica martelando os
        // inimigos nesse meio tempo, nao faz sentido o ouro continuar
        // subindo sozinho (e isso tambem evita estourar o proximo
        // limiar de horde por causa so do tempo passado na luta).
        // Congela a partir do EXATO instante em que a horda e disparada
        // (nao so durante a luta): e isso que mantem o numero na tela
        // parado no valor certo enquanto a horda acontece.
        bool hordeInProgress = HordeManager.Instance != null && HordeManager.Instance.IsHordeInProgress;
        double perSecond = GoldPerSecond;
        if (perSecond > 0 && !hordeInProgress)
        {
            double amount = perSecond * Time.deltaTime;
            gold += amount;
            totalGoldEarned += amount;
            OnGoldChanged?.Invoke(gold);
            OnTotalGoldEarnedChanged?.Invoke(totalGoldEarned);
            hasUnsavedChanges = true;
        }

        // Autosave: grava no PlayerPrefs de tempos em tempos em vez de a
        // cada martelada/tick de ouro passivo (evita travadinha por excesso
        // de escrita em disco). Tambem salva ao pausar/fechar o jogo.
        if (hasUnsavedChanges)
        {
            autosaveTimer += Time.deltaTime;
            if (autosaveTimer >= AUTOSAVE_INTERVAL)
            {
                autosaveTimer = 0f;
                SaveGame();
            }
        }
    }

    /// <summary>Monta o catalogo de upgrades da loja (regras e valores do jogo).</summary>
    private void BuildCatalog()
    {
        Upgrades = new List<UpgradeDefinition>
        {
            new UpgradeDefinition
            {
                id = "hammer",
                displayName = "Melhorar Martelo",
                description = "+1 ouro por martelada",
                effect = UpgradeEffect.GoldPerClick,
                baseCost = 10,
                costMultiplier = 1.15f,
                perLevelAmount = 1,
            },
            new UpgradeDefinition
            {
                id = "crit",
                displayName = "Golpe Certeiro",
                description = "+5% de chance de critico (x3 ouro)",
                effect = UpgradeEffect.CritChance,
                baseCost = 40,
                costMultiplier = 1.20f,
                perLevelAmount = 0.05,
                maxLevel = 10,
            },
            new UpgradeDefinition
            {
                id = "assistant",
                displayName = "Aprendiz Ferreiro",
                description = "+1 ouro por segundo",
                effect = UpgradeEffect.GoldPerSecond,
                baseCost = 25,
                costMultiplier = 1.22f,
                perLevelAmount = 1,
            },
            new UpgradeDefinition
            {
                id = "forge",
                displayName = "Forja Automatica",
                description = "+8 ouro por segundo",
                effect = UpgradeEffect.GoldPerSecond,
                baseCost = 500,
                costMultiplier = 1.20f,
                perLevelAmount = 8,
                requiresUpgradeId = "assistant",
                requiresUpgradeLevel = 5,
                unlockHint = "Contrate 5 Aprendizes",
            },
            new UpgradeDefinition
            {
                id = "bellows",
                displayName = "Fole de Ferreiro",
                description = "-0.3 de gasto de resistencia por martelada",
                effect = UpgradeEffect.StaminaCostReduction,
                baseCost = 30,
                costMultiplier = 1.22f,
                perLevelAmount = 0.3,
                maxLevel = 17,
            },
            new UpgradeDefinition
            {
                id = "gloves",
                displayName = "Luvas de Couro",
                description = "+8 de resistencia maxima",
                effect = UpgradeEffect.MaxStaminaIncrease,
                baseCost = 35,
                costMultiplier = 1.20f,
                perLevelAmount = 8,
                maxLevel = 15,
            },
            new UpgradeDefinition
            {
                id = "hammer_tier2",
                displayName = "Martelo Reforcado",
                description = "Dobra o dano contra as hordas, +3% de critico",
                effect = UpgradeEffect.HammerUpgrade,
                baseCost = 3000,
                costMultiplier = 1f,
                perLevelAmount = 0,
                maxLevel = 1,
            },
            new UpgradeDefinition
            {
                id = "hammer_tier3",
                displayName = "Martelo Lendario",
                description = "Dano devastador contra as hordas, +8% de critico",
                effect = UpgradeEffect.HammerUpgrade,
                baseCost = 25000,
                costMultiplier = 1f,
                perLevelAmount = 0,
                maxLevel = 1,
                requiresUpgradeId = "hammer_tier2",
                requiresUpgradeLevel = 1,
                unlockHint = "Compre o Martelo Reforcado",
            },
        };

        foreach (var u in Upgrades)
        {
            u.currentCost = u.baseCost;
        }
    }

    private double GetEffectTotal(UpgradeEffect effect)
    {
        double total = 0;
        foreach (var u in Upgrades)
        {
            if (u.effect == effect) total += u.level * u.perLevelAmount;
        }
        return total;
    }

    public UpgradeDefinition GetUpgrade(string id)
    {
        return Upgrades.Find(u => u.id == id);
    }

    /// <summary>Se um upgrade tem pre-requisito, verifica se ja foi cumprido.</summary>
    public bool IsUnlocked(UpgradeDefinition u)
    {
        if (string.IsNullOrEmpty(u.requiresUpgradeId)) return true;
        var req = GetUpgrade(u.requiresUpgradeId);
        return req != null && req.level >= u.requiresUpgradeLevel;
    }

    /// <summary>Actual before/after values for one purchase, including caps and horde bonuses.</summary>
    public string GetNextUpgradePreview(UpgradeDefinition u)
    {
        if (u == null) return "";
        switch (u.effect)
        {
            case UpgradeEffect.GoldPerClick:
                int nextClick = Mathf.Max(1, Mathf.RoundToInt((startingGoldPerClick + (int)(GetEffectTotal(u.effect) + u.perLevelAmount)) * tempGoldPerClickMultiplier * (1 + goldPerClickBonusPercent / 100f)));
                return GoldPerClick + " → " + nextClick + " ouro / golpe";
            case UpgradeEffect.CritChance:
                float nextCrit = Mathf.Min(maxCritChance, (float)(GetEffectTotal(u.effect) + u.perLevelAmount) + HammerCritBonusPercent);
                return (CritChance * 100).ToString("0.#") + "% → " + (nextCrit * 100).ToString("0.#") + "% de crítico";
            case UpgradeEffect.GoldPerSecond:
                double nextSecond = GoldPerSecond + u.perLevelAmount * tempGoldPerSecondMultiplier * (1 + goldPerSecondBonusPercent / 100f);
                return UIManager.FormatNumber(GoldPerSecond) + " → " + UIManager.FormatNumber(nextSecond) + " ouro / segundo";
            case UpgradeEffect.StaminaCostReduction:
                float nextCost = Mathf.Max(minStaminaCostPerHit, baseStaminaCostPerHit - (float)(GetEffectTotal(u.effect) + u.perLevelAmount)) * tempStaminaCostMultiplier;
                return StaminaCostPerHit.ToString("0.#") + " → " + nextCost.ToString("0.#") + " resistência / golpe";
            case UpgradeEffect.MaxStaminaIncrease:
                return MaxStamina.ToString("0") + " → " + (MaxStamina + u.perLevelAmount).ToString("0") + " resistência máxima";
            case UpgradeEffect.HammerUpgrade:
                int tier = u.id == "hammer_tier3" ? 3 : 2;
                int damage = tier == 3 ? 4 : 2;
                float bonus = tier == 3 ? .08f : .03f;
                float crit = Mathf.Min(maxCritChance, (float)GetEffectTotal(UpgradeEffect.CritChance) + bonus);
                return HammerDamagePerHit + " → " + damage + " dano • " + (crit * 100).ToString("0.#") + "% crítico";
            default: return u.description;
        }
    }

    public bool CanBuy(UpgradeDefinition u)
    {
        if (CampaignCompleted || u == null) return false;
        if (!IsUnlocked(u)) return false;
        if (u.maxLevel > 0 && u.level >= u.maxLevel) return false;
        return gold >= u.currentCost;
    }

    /// <summary>Chamado pelos botoes da loja.</summary>
    public bool TryBuyUpgrade(string id)
    {
        var u = GetUpgrade(id);
        if (!CanBuy(u))
        {
            return false;
        }

        gold -= u.currentCost;
        u.level += 1;
        u.currentCost = Math.Ceiling(u.currentCost * u.costMultiplier);

        OnGoldChanged?.Invoke(gold);
        OnUpgradeChanged?.Invoke();

        if (id == "hammer_tier2" || id == "hammer_tier3")
        {
            OnHammerTierChanged?.Invoke(HammerTier);
        }

        SaveGame();
        hasUnsavedChanges = false;
        autosaveTimer = 0f;
        return true;
    }

    /// <summary>Verifica se tem resistencia suficiente pra bater de novo (regra/obstaculo).</summary>
    public bool HasStamina()
    {
        return currentStamina >= StaminaCostPerHit;
    }

    /// <summary>Gasta a resistencia de uma martelada. So chamar quando HasStamina() for true.</summary>
    public void ConsumeStaminaForHit()
    {
        currentStamina = Mathf.Max(0f, currentStamina - StaminaCostPerHit);
        OnStaminaChanged?.Invoke(currentStamina, MaxStamina);
    }

    /// <summary>
    /// Chamado pelo AnvilClicker toda vez que uma martelada acerta com sucesso.
    /// Sorteia o golpe critico, soma o ouro e retorna o valor ganho (ja com o
    /// bonus de critico aplicado, se for o caso). Consulte LastHitWasCrit logo
    /// em seguida pra saber se foi critico.
    /// </summary>
    public int RegisterHit()
    {
        int amount = GoldPerClick;
        lastHitWasCrit = UnityEngine.Random.value < CritChance;

        if (lastHitWasCrit)
        {
            amount = Mathf.RoundToInt((float)(amount * critMultiplier));
        }

        gold += amount;
        totalGoldEarned += amount;
        OnGoldChanged?.Invoke(gold);
        OnTotalGoldEarnedChanged?.Invoke(totalGoldEarned);
        hasUnsavedChanges = true;
        return amount;
    }

    /// <summary>
    /// Multiplicadores temporarios usados pelos buffs de recompensa da Horda
    /// (ver HordeManager). Passe 1 pra remover o efeito quando o buff acabar.
    /// </summary>
    public void SetGoldPerClickMultiplier(float multiplier) => tempGoldPerClickMultiplier = multiplier;
    public void SetGoldPerSecondMultiplier(float multiplier) => tempGoldPerSecondMultiplier = multiplier;
    public void SetStaminaCostMultiplier(float multiplier) => tempStaminaCostMultiplier = multiplier;

    /// <summary>Concede permanentemente +percent% de ouro por martelada (escolha da tela pos-Horda).</summary>
    public void AddGoldPerClickBonusPercent(float percent)
    {
        goldPerClickBonusPercent += percent;
        hasUnsavedChanges = true;
        OnUpgradeChanged?.Invoke();
        SaveGame();
    }

    /// <summary>Concede permanentemente +percent% de ouro por segundo (escolha da tela pos-Horda).</summary>
    public void AddGoldPerSecondBonusPercent(float percent)
    {
        goldPerSecondBonusPercent += percent;
        hasUnsavedChanges = true;
        OnUpgradeChanged?.Invoke();
        SaveGame();
    }

    /// <summary>Concede ouro bonus (ex: recompensa por limpar uma Horda). Conta como ouro total ganho.</summary>
    public void AddBonusGold(double amount)
    {
        if (amount <= 0) return;

        gold += amount;
        totalGoldEarned += amount;
        OnGoldChanged?.Invoke(gold);
        OnTotalGoldEarnedChanged?.Invoke(totalGoldEarned);
        hasUnsavedChanges = true;
    }

    /// <summary>Remove ouro (ex: penalidade por falhar uma Horda). Nao mexe no total ganho (historico).</summary>
    public void RemoveGold(double amount)
    {
        if (amount <= 0) return;

        gold = Math.Max(0, gold - amount);
        OnGoldChanged?.Invoke(gold);
        hasUnsavedChanges = true;
    }

    public bool TryCompleteCampaign(int defeatedEnemyTier)
    {
        if (CampaignCompleted || !CampaignRules.IsVictory(HammerTier, defeatedEnemyTier, true)) return false;
        CampaignCompleted = true;
        SaveGame();
        hasUnsavedChanges = false;
        OnCampaignCompleted?.Invoke();
        return true;
    }

    private void SaveGame()
    {
        PlayerPrefs.SetInt(KEY_CAMPAIGN_COMPLETED, CampaignCompleted ? 1 : 0);
        // Usamos string (InvariantCulture) em vez de float pra nao perder precisao
        // quando o numero de ouro ficar bem grande.
        PlayerPrefs.SetString(KEY_GOLD, gold.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.SetString(KEY_TOTAL_GOLD_EARNED, totalGoldEarned.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.SetString(KEY_HORDE_CLICK_BONUS, goldPerClickBonusPercent.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.SetString(KEY_HORDE_SECOND_BONUS, goldPerSecondBonusPercent.ToString(CultureInfo.InvariantCulture));

        foreach (var u in Upgrades)
        {
            PlayerPrefs.SetInt("up_" + u.id + "_level", u.level);
            PlayerPrefs.SetString("up_" + u.id + "_cost", u.currentCost.ToString(CultureInfo.InvariantCulture));
        }

        PlayerPrefs.Save();
    }

    private void LoadGame()
    {
        CampaignCompleted = PlayerPrefs.GetInt(KEY_CAMPAIGN_COMPLETED, 0) == 1;
        gold = PlayerPrefs.HasKey(KEY_GOLD)
            ? double.Parse(PlayerPrefs.GetString(KEY_GOLD), CultureInfo.InvariantCulture)
            : 0;

        // Jogos salvos antes da Horda existir nao tem esse valor gravado -
        // nesse caso comeca do ouro atual (nao do zero), pra nao forcar o
        // jogador a esperar 10k de novo so porque atualizou o jogo.
        totalGoldEarned = PlayerPrefs.HasKey(KEY_TOTAL_GOLD_EARNED)
            ? double.Parse(PlayerPrefs.GetString(KEY_TOTAL_GOLD_EARNED), CultureInfo.InvariantCulture)
            : gold;

        goldPerClickBonusPercent = PlayerPrefs.HasKey(KEY_HORDE_CLICK_BONUS)
            ? float.Parse(PlayerPrefs.GetString(KEY_HORDE_CLICK_BONUS), CultureInfo.InvariantCulture)
            : 0f;
        goldPerSecondBonusPercent = PlayerPrefs.HasKey(KEY_HORDE_SECOND_BONUS)
            ? float.Parse(PlayerPrefs.GetString(KEY_HORDE_SECOND_BONUS), CultureInfo.InvariantCulture)
            : 0f;

        foreach (var u in Upgrades)
        {
            string levelKey = "up_" + u.id + "_level";
            string costKey = "up_" + u.id + "_cost";

            u.level = PlayerPrefs.HasKey(levelKey) ? PlayerPrefs.GetInt(levelKey) : 0;
            u.currentCost = PlayerPrefs.HasKey(costKey)
                ? double.Parse(PlayerPrefs.GetString(costKey), CultureInfo.InvariantCulture)
                : u.baseCost;
        }
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
        PlayerPrefs.DeleteAll();
        CampaignCompleted = false;
        BuildCatalog();
        gold = 0;
        totalGoldEarned = 0;
        tempGoldPerClickMultiplier = 1f;
        tempGoldPerSecondMultiplier = 1f;
        tempStaminaCostMultiplier = 1f;
        goldPerClickBonusPercent = 0f;
        goldPerSecondBonusPercent = 0f;
        currentStamina = MaxStamina;
        OnGoldChanged?.Invoke(gold);
        OnTotalGoldEarnedChanged?.Invoke(totalGoldEarned);
        OnUpgradeChanged?.Invoke();
        OnStaminaChanged?.Invoke(currentStamina, MaxStamina);
    }
}
