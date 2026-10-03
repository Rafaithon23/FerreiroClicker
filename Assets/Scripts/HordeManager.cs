using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PROTOTIPO (sem sprite de proposito, so pra testar a mecanica): a cada
/// marco do ouro ATUAL exibido na tela (GameManager.Gold - o saldo que
/// sobe e desce quando voce compra upgrade) dispara uma "Horda":
/// escurece a tela, mostra um aviso, faz surgir um bando de inimigos
/// (circulos coloridos, cada um com um efeito de escala/fade "PUF" na
/// posicao final) que o jogador martela um por um antes do tempo acabar.
/// Durante a horda a bigorna fica desativada (ver o check em
/// AnvilClicker.Update()) - o jogador so pode "atacar" os inimigos.
///
/// Os marcos sao uma lista configuravel (ex: 100, 250, 500, 1000, 5000,
/// 10000, 20000, 40000 - nao precisa ser multiplo nem dobrar certinho,
/// e so pra dar uma "sensacao" de progressao gostosa). Depois que a lista
/// configurada no Inspector acaba, a Horda continua sozinha repetindo o
/// mesmo padrao de crescimento entre os ultimos marcos da lista.
///
/// Se limpar todos a tempo: aparece uma tela pra escolher entre 2 bonus
/// PERMANENTES (fica salvo, nunca expira): +X% de ouro por martelada ou
/// +Y% de ouro dos trabalhadores (producao passiva). Se o tempo acabar com
/// inimigo sobrando: perde uma % do ouro atual (failPenaltyPercent) e a
/// horda vai embora sem bonus.
///
/// COMO USAR: cria um GameObject vazio na cena (ex: "HordeManager") e
/// arrasta este script nele. Nao precisa configurar/arrastar mais nada -
/// a UI (escurecido, aviso, timer, tela de escolha) e os inimigos sao
/// criados em runtime. Os numeros abaixo ficam expostos no Inspector pra
/// ajustar sem mexer em codigo.
/// </summary>
public class HordeManager : MonoBehaviour
{
    public static HordeManager Instance { get; private set; }

    public enum State { Idle, Darkening, Warning, Active, Resolving, Cooldown }
    public enum AttackPattern { Standard, Rush, Armored }
    private const string NextHordeKey = "bc_next_horde";
    public AttackPattern CurrentPattern => (AttackPattern)((Mathf.Max(1, hordeNumber) - 1) % 3);
    public double NextThreshold => nextThreshold;
    public int HordeNumber => hordeNumber;

    [Header("Gatilho (marcos de ouro TOTAL ganho pra cada horda)")]
    [Tooltip("Marcos, em ordem. Nao precisa ser multiplo nem progressao exata - depois que a lista acaba, repete o padrao de crescimento dos ultimos marcos.")]
    public double[] thresholds = { 100, 250, 500, 1000, 5000, 10000, 20000, 40000 };

    [Header("Dificuldade")]
    public int baseEnemyCount = 4;
    public int enemiesPerHorde = 1;
    [Tooltip("Inimigos extras por tier de inimigo (0=goblin .. 4=cavaleiro negro) - aumenta a quantidade conforme o ouro sobe, alem do aumento por numero de horda.")]
    public int enemiesPerTier = 2;
    public int maxEnemyCount = 18;
    public int hpPerEnemy = 3;
    [Tooltip("Vida extra por tier de inimigo (0=goblin .. 4=cavaleiro negro) - os primeiros tiers tem menos vida, os ultimos mais, pra pressionar a pessoa a melhorar o martelo.")]
    public int hpIncreasePerTier = 2;
    public float timeLimitSeconds = 20f;
    [Tooltip("Quanto o tempo da horda diminui por horda (em segundos) - fica mais apertado conforme a pessoa avanca.")]
    public float timeReductionPerHorde = 0.5f;
    [Tooltip("Tempo minimo de horda, por mais que a pessoa avance - pra sempre ficar jogavel.")]
    public float minTimeLimitSeconds = 8f;

    [Header("Falha")]
    [Tooltip("Fracao do ouro atual perdida se o tempo acabar com inimigo sobrando (0.1 = 10%)")]
    [Range(0f, 1f)]
    public float failPenaltyPercent = 0.10f;
    [Tooltip("Quanto a penalidade de derrota aumenta por horda (fracao, ex: 0.01 = +1 ponto percentual por horda).")]
    public float penaltyIncreasePerHorde = 0.01f;
    [Tooltip("Penalidade maxima de derrota, por mais que a pessoa avance - pra continuar justo.")]
    [Range(0f, 1f)]
    public float maxFailPenaltyPercent = 0.35f;

    [Header("Recompensa (bonus permanente, escolha 1 de 2)")]
    public float goldPerClickBuffPercent = 2f;
    public float goldPerSecondBuffPercent = 1f;
    [Tooltip("Quanto o bonus de ouro/martelada aumenta por horda vencida (ponto percentual) - recompensa maior pras hordas mais dificeis.")]
    public float goldPerClickBuffIncreasePerHorde = 0.1f;
    [Tooltip("Teto do bonus de ouro/martelada por horda vencida, pra nao sair do controle.")]
    public float maxGoldPerClickBuffPercent = 5f;
    [Tooltip("Quanto o bonus de ouro/trabalhadores aumenta por horda vencida (ponto percentual).")]
    public float goldPerSecondBuffIncreasePerHorde = 0.05f;
    [Tooltip("Teto do bonus de ouro/trabalhadores por horda vencida, pra nao sair do controle.")]
    public float maxGoldPerSecondBuffPercent = 2.5f;

    [Header("Timing")]
    public float darkenDuration = 0.6f;
    public float warningDuration = 1.2f;
    public float resultDisplayDuration = 1.6f;
    public float cooldownAfterHorde = 3f;
    [Tooltip("Duracao do efeito de entrada (escala 0% -> 70% -> 110% -> 100% + fade) de cada inimigo")]
    public float spawnMoveDuration = 0.25f;
    [Tooltip("Atraso aleatorio (0 a isso) antes de cada inimigo comecar a entrar, pra nao aparecerem todos juntos (efeito PUF-PUF-PUF)")]
    public float spawnStagger = 0.12f;

    [Header("Visual (overlay)")]
    [Range(0f, 1f)]
    public float darkenAlpha = 0.85f;

    public State CurrentState { get; private set; } = State.Idle;
    public bool IsHordeActive => CurrentState == State.Active;
    /// <summary>True durante TODO o evento de horde (escurecendo, aviso, luta
    /// e resolucao) - nao so a luta em si. Usado pra travar o martelo e
    /// congelar o contador de ouro assim que o marco e cruzado.</summary>
    public bool IsHordeInProgress => CurrentState != State.Idle && CurrentState != State.Cooldown;

    private double nextThreshold;
    private int hordeNumber; // 1 = primeira horda, 2 = segunda, etc. (gatilho + escala de dificuldade)
    private readonly List<EnemyTarget> activeEnemies = new List<EnemyTarget>();

    private Canvas canvas;
    private Image overlay;
    private RectTransform enemyLayer;
    private Text banner;
    private Text timerText;
    private Sprite placeholderSprite;

    private GameObject choicePanel;
    private Text choiceTitle;
    private Text buffTextA;
    private Text buffTextB;
    private bool choiceMade;
    private RectTransform choiceContent;
    private Image warningPanel;
    private Image timeFill;
    private int spawnedCount;
    private float battleTimeLimit;
    private Sprite rewardCardSprite;

    // Progressao tematica dos inimigos por marco de ouro atingido (baseado
    // na ficha de referencia que o jogador mandou: 10k goblins, 20k orcs,
    // 40k esqueletos, 60k zumbis, 100k+ cavaleiros negros). So cor + nome
    // por enquanto (os sprites de verdade ainda sao os circulos placeholder
    // - troca aqui quando tiver os PNGs com fundo transparente).
    private static readonly string[] TierNames =
        { "GOBLINS", "ORCS", "ESQUELETOS", "ZUMBIS", "CAVALEIROS NEGROS" };

    // Frase de derrota (quando sobra inimigo e perde ouro) - uma por tier,
    // pra combinar com o "jeito" de cada inimigo em vez de uma frase generica.
    private static readonly string[] TierLootMessages =
    {
        "OS GOBLINS SAQUEARAM A FORJA",
        "OS ORCS DESTRUIRAM SEUS SUPRIMENTOS",
        "OS ESQUELETOS ROUBARAM SEU OURO DOS COFRES",
        "OS ZUMBIS DEVORARAM SEUS ESTOQUES",
        "OS CAVALEIROS NEGROS CONQUISTARAM A FORJA",
    };

    // Frase quando o tempo acaba mas nao tinha mais ouro pra roubar (penalty = 0)
    // - tambem uma por tier.
    private static readonly string[] TierEscapeMessages =
    {
        "OS GOBLINS FUGIRAM COM O QUE PUDERAM!",
        "OS ORCS FORAM EMBORA RINDO!",
        "OS ESQUELETOS VOLTARAM PRO TUMULO!",
        "OS ZUMBIS SE ARRASTARAM PARA LONGE!",
        "OS CAVALEIROS NEGROS RECUARAM... POR AGORA!",
    };
    private static readonly Color[] TierColors =
    {
        new Color(0.45f, 0.65f, 0.25f), // goblin - verde claro
        new Color(0.28f, 0.42f, 0.18f), // orc - verde escuro
        new Color(0.80f, 0.75f, 0.60f), // esqueleto - osso
        new Color(0.42f, 0.52f, 0.40f), // zumbi - verde acinzentado
        new Color(0.32f, 0.09f, 0.09f), // cavaleiro negro - vermelho escuro
    };
    private int currentTier;

    // Sprites de verdade (Assets/Resources/Enemies/*.png, com fundo
    // removido). Se algum nao for encontrado (ex: ainda nao importado),
    // cai de volta pro circulo placeholder tingido da cor do tier.
    private static readonly string[] TierResourceNames =
        { "Enemies/goblin", "Enemies/orc", "Enemies/skeleton", "Enemies/zombie", "Enemies/dark_knight" };
    private Sprite[] tierSprites;

    /// <summary>Vida de cada inimigo da horda atual - cresce com o tier (goblin tem menos, cavaleiro negro tem mais), pra combinar com os upgrades de martelo da loja.</summary>
    private int GetEffectiveHp()
    {
        int hp = Mathf.Max(1, hpPerEnemy + currentTier * hpIncreasePerTier);
        if (CurrentPattern == AttackPattern.Rush) return Mathf.Max(1, Mathf.CeilToInt(hp * .75f));
        if (CurrentPattern == AttackPattern.Armored) return hp + 2;
        return hp;
    }

    /// <summary>Qual "tipo" de horde deveria aparecer pro valor de ouro atual (ver TierNames/TierColors).</summary>
    private int GetEnemyTier(double currentGold)
    {
        if (currentGold < 20000) return 0;
        if (currentGold < 40000) return 1;
        if (currentGold < 60000) return 2;
        if (currentGold < 100000) return 3;
        return 4;
    }

    // Timer e penalidade ficam mais duros conforme o numero da horda sobe -
    // sempre com piso/teto pra continuar justo (pedido do Rafael: "de um
    // jeito que fique justo ainda").
    private float GetEffectiveTimeLimit()
    {
        float reduced = timeLimitSeconds - (hordeNumber - 1) * timeReductionPerHorde;
        float factor = CurrentPattern == AttackPattern.Rush ? .85f : CurrentPattern == AttackPattern.Armored ? 1.15f : 1;
        return Mathf.Max(minTimeLimitSeconds, reduced * factor);
    }

    private float GetEffectivePenaltyPercent()
    {
        float increased = failPenaltyPercent + (hordeNumber - 1) * penaltyIncreasePerHorde;
        return Mathf.Min(maxFailPenaltyPercent, increased);
    }

    // Recompensa tambem cresce um pouco por horda vencida (sempre com teto,
    // pra nao virar bola de neve ja que o bonus e permanente e acumula).
    private float GetEffectiveGoldPerClickBuffPercent()
    {
        float increased = goldPerClickBuffPercent + (hordeNumber - 1) * goldPerClickBuffIncreasePerHorde;
        return Mathf.Min(maxGoldPerClickBuffPercent, increased);
    }

    private float GetEffectiveGoldPerSecondBuffPercent()
    {
        float increased = goldPerSecondBuffPercent + (hordeNumber - 1) * goldPerSecondBuffIncreasePerHorde;
        return Mathf.Min(maxGoldPerSecondBuffPercent, increased);
    }

    private void LoadTierSprites()
    {
        tierSprites = new Sprite[TierResourceNames.Length];
        for (int i = 0; i < TierResourceNames.Length; i++)
        {
            tierSprites[i] = Resources.Load<Sprite>(TierResourceNames[i]);
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        canvas = UIManager.Instance != null ? UIManager.Instance.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("HordeManager: nenhum Canvas encontrado na cena, a Horda nao tem onde desenhar a UI.");
            return;
        }

        BuildUi();
        LoadTierSprites();

        if (GameManager.Instance != null)
        {
            InitThresholdState(GameManager.Instance.Gold);
            GameManager.Instance.OnGoldChanged += HandleGoldChanged;
            // Confere de cara (ex: save antigo que ja passou de algum marco).
            HandleGoldChanged(GameManager.Instance.Gold);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (rewardCardSprite != null) Destroy(rewardCardSprite);
        if (timeFill != null && timeFill.sprite != null) Destroy(timeFill.sprite);
        if (overlay != null) Destroy(overlay.gameObject);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGoldChanged -= HandleGoldChanged;
        }
    }

    /// <summary>
    /// Calcula a PROXIMA horda (numero + marco) ainda nao atingida a partir
    /// do ouro ATUAL exibido na tela (ex: se o jogador ja tem 600 de ouro e
    /// os marcos sao 100/250/500/1000..., as 3 primeiras ja foram
    /// "passadas" - a proxima e a 4a, em 1000). Isso so decide o PONTO DE
    /// PARTIDA ao carregar o jogo - uma vez disparada, cada marco nunca
    /// volta a disparar de novo, mesmo que o ouro suba e desca depois.
    /// </summary>
    private void InitThresholdState(double currentGold)
    {
        hordeNumber = Mathf.Clamp(PlayerPrefs.GetInt(NextHordeKey, 1), 1, 10000);
        nextThreshold = GetThresholdForHorde(hordeNumber);
        while (nextThreshold <= currentGold)
        {
            hordeNumber++;
            nextThreshold = GetThresholdForHorde(hordeNumber);
        }
    }

    /// <summary>
    /// Marco de ouro total ganho necessario pra disparar a horda numero n
    /// (1-based). Os primeiros vem direto do array "thresholds" (editavel
    /// no Inspector). Depois que a lista acaba, continua a progressao
    /// sozinha repetindo, em ciclo, as MESMAS proporcoes entre os marcos
    /// configurados (ex: se os ultimos saltos foram x2, continua dobrando).
    /// </summary>
    private double GetThresholdForHorde(int n)
    {
        if (thresholds == null || thresholds.Length == 0)
        {
            return 10000 * Math.Pow(2, n - 1); // fallback de seguranca
        }

        if (n <= thresholds.Length)
        {
            return thresholds[n - 1];
        }

        double value = thresholds[thresholds.Length - 1];

        if (thresholds.Length < 2)
        {
            // so 1 marco configurado - nao da pra extrair um padrao, dobra.
            return value * Math.Pow(2, n - thresholds.Length);
        }

        int ratioCount = thresholds.Length - 1;
        int extra = n - thresholds.Length;
        for (int i = 0; i < extra; i++)
        {
            int idx = i % ratioCount;
            double ratio = thresholds[idx + 1] / thresholds[idx];
            value *= ratio;
        }
        return value;
    }

    /// <summary>
    /// Dispara a horda no EXATO instante em que o ouro exibido na tela
    /// cruza o marco - e o primeiro passo do RunHorde() ja marca o estado
    /// como "nao mais Idle" antes de qualquer frame seguinte, entao o
    /// contador de ouro (GameManager.Update) e o martelo na bigorna
    /// congelam a partir desse exato valor.
    /// </summary>
    private void HandleGoldChanged(double currentGold)
    {
        if (CurrentState != State.Idle || (GameManager.Instance != null && GameManager.Instance.CampaignCompleted)) return;
        if (currentGold >= nextThreshold)
        {
            StartCoroutine(RunHorde());
        }
    }

    private IEnumerator RunHorde()
    {
        ShopManager.Instance?.CloseShop();
        CurrentState = State.Darkening;
        currentTier = GetEnemyTier(GameManager.Instance != null ? GameManager.Instance.Gold : 0);
        yield return StartCoroutine(FadeOverlay(0f, darkenAlpha, darkenDuration));

        CurrentState = State.Warning;
        float countdown = Mathf.Max(3f, warningDuration);
        while (countdown > 0)
        {
            bool holding = HammerFollowMouse.Instance != null && HammerFollowMouse.Instance.IsHolding;
            SetBanner("HORDA " + hordeNumber + " • " + PatternName() + "\n" +
                TierNames[currentTier] + " EM " + Mathf.CeilToInt(countdown) + "...\n<size=22>" +
                (holding ? PatternHint() : "PEGUE O MARTELO NO SUPORTE!") + "</size>");
            countdown -= Time.deltaTime;
            yield return null;
        }
        SetBanner("");

        CurrentState = State.Active;
        SpawnEnemies();

        yield return new WaitForSeconds(Mathf.Max(.05f, spawnMoveDuration) + Mathf.Max(0, spawnStagger));
        battleTimeLimit = GetEffectiveTimeLimit();
        float timeLeft = battleTimeLimit;
        while (timeLeft > 0f && activeEnemies.Count > 0)
        {
            timeLeft -= Time.deltaTime;
            SetTimerText(Mathf.Max(0f, timeLeft));
            yield return null;
        }

        CurrentState = State.Resolving;
        bool success = activeEnemies.Count == 0;
        ClearRemainingEnemies();
        SetTimerText(-1f);

        if (success)
        {
            SetBanner($"<color=#8FE08F>HORDA DE {TierNames[currentTier]} DERROTADA!</color>");
            yield return new WaitForSeconds(1f);
            SetBanner("");

            if (GameManager.Instance != null && GameManager.Instance.TryCompleteCampaign(currentTier))
                yield break;
            yield return StartCoroutine(ShowBuffChoice());
        }
        else
        {
            double penalty = GameManager.Instance != null ? GameManager.Instance.Gold * GetEffectivePenaltyPercent() : 0;
            GameManager.Instance?.RemoveGold(penalty);
            SetBanner(penalty > 0
                ? $"<color=#E08F8F>{TierLootMessages[currentTier]}! -{UIManager.FormatNumber(penalty)} OURO</color>"
                : $"<color=#E08F8F>{TierEscapeMessages[currentTier]}</color>");

            yield return new WaitForSeconds(resultDisplayDuration);
            SetBanner("");
        }

        yield return StartCoroutine(FadeOverlay(darkenAlpha, 0f, darkenDuration));

        CurrentState = State.Cooldown;

        // Avanca pro proximo marco da lista (ou da progressao estendida),
        // independente de vitoria ou derrota.
        hordeNumber++;
        nextThreshold = GetThresholdForHorde(hordeNumber);
        PlayerPrefs.SetInt(NextHordeKey, hordeNumber);
        PlayerPrefs.Save();

        yield return new WaitForSeconds(Mathf.Max(0, cooldownAfterHorde));
        CurrentState = State.Idle;

        // Caso o jogador ja tenha passado de mais de um marco de uma vez
        // (ex: ouro passivo alto), encadeia a proxima horda.
        if (GameManager.Instance != null)
        {
            HandleGoldChanged(GameManager.Instance.Gold);
        }
    }

    private IEnumerator ShowBuffChoice()
    {
        choiceMade = false;
        if (choiceTitle != null) choiceTitle.text = "VITÓRIA! ESCOLHA SUA RECOMPENSA";
        if (buffTextA != null) buffTextA.text = ClickRewardPreview();
        if (buffTextB != null) buffTextB.text = WorkerRewardPreview();
        if (choicePanel != null) choicePanel.SetActive(true);

        yield return new WaitUntil(() => choiceMade);

        if (choicePanel != null) choicePanel.SetActive(false);
    }

    private void SpawnEnemies()
    {
        activeEnemies.Clear();
        if (enemyLayer == null) return;

        int count = GetEnemyCount();
        spawnedCount = count;
        Vector2 area = enemyLayer.rect.size;
        int columns = Mathf.Max(1, Mathf.Min(6, Mathf.FloorToInt(area.x * .78f / 150f)));
        int rows = Mathf.CeilToInt((float)count / columns);

        for (int i = 0; i < count; i++)
        {
            int column = i % columns;
            int row = i / columns;
            int rowCount = Mathf.Min(columns, count - row * columns);
            float spacingX = Mathf.Min(230f, area.x * .78f / Mathf.Max(1, columns));
            float spacingY = Mathf.Min(145f, area.y * .42f / Mathf.Max(1, rows));
            Vector2 targetPos = new Vector2((column - (rowCount - 1) * .5f) * spacingX,
                -area.y * .08f + ((rows - 1) * .5f - row) * spacingY);

            GameObject go = new GameObject("HordeEnemy_" + i, typeof(RectTransform));
            go.transform.SetParent(enemyLayer, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

            Sprite tierSprite = tierSprites != null ? tierSprites[currentTier] : null;

            Image img = go.AddComponent<Image>();
            if (tierSprite != null)
            {
                img.sprite = tierSprite;
                img.color = Color.white;
                rt.sizeDelta = GetSpriteDisplaySize(tierSprite, 110f);
            }
            else
            {
                // fallback: ainda nao tem o PNG desse tier em Resources/Enemies
                img.sprite = GetPlaceholderSprite();
                img.color = TierColors[currentTier];
                rt.sizeDelta = new Vector2(90, 90);
            }

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            // Faz os golpes reagirem a posicao VISUAL da cabeca do martelo
            // (em vez do cursor real, escondido e deslocado) - o mesmo
            // sistema que os outros botoes do jogo ja usam.
            go.AddComponent<HammerClickTarget>();

            float targetHeight = Mathf.Min(110f, Mathf.Max(36f, Mathf.Min(spacingX, spacingY) * .72f));
            if (tierSprite != null) rt.sizeDelta = GetSpriteDisplaySize(tierSprite, targetHeight);
            else rt.sizeDelta = Vector2.one * targetHeight;
            EnemyTarget enemy = go.AddComponent<EnemyTarget>();
            enemy.Setup(this, GetEffectiveHp(), img, btn, rt, targetPos,
                spawnMoveDuration, UnityEngine.Random.Range(0f, spawnStagger));

            activeEnemies.Add(enemy);
        }
    }

    public void OnEnemyDefeated(EnemyTarget enemy)
    {
        activeEnemies.Remove(enemy);
    }

    private void ClearRemainingEnemies()
    {
        foreach (var e in activeEnemies)
        {
            if (e != null) Destroy(e.gameObject);
        }
        activeEnemies.Clear();
    }

    private void BuildUi()
    {
        GameObject overlayGo = new GameObject("HordeOverlay", typeof(RectTransform));
        overlayGo.transform.SetParent(canvas.transform, false);
        RectTransform overlayRt = overlayGo.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        overlayRt.localScale = Vector3.one;

        overlay = overlayGo.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0f);
        overlay.raycastTarget = false;

        GameObject enemyLayerGo = new GameObject("HordeEnemyLayer", typeof(RectTransform));
        enemyLayerGo.transform.SetParent(overlayGo.transform, false);
        enemyLayer = enemyLayerGo.GetComponent<RectTransform>();
        enemyLayer.anchorMin = Vector2.zero;
        enemyLayer.anchorMax = Vector2.one;
        enemyLayer.offsetMin = Vector2.zero;
        enemyLayer.offsetMax = Vector2.zero;

        GameObject bannerGo = new GameObject("HordeBanner", typeof(RectTransform));
        bannerGo.transform.SetParent(overlayGo.transform, false);
        RectTransform bannerRt = bannerGo.GetComponent<RectTransform>();
        bannerRt.anchorMin = new Vector2(0.5f, 0.78f);
        bannerRt.anchorMax = new Vector2(0.5f, 0.78f);
        bannerRt.sizeDelta = new Vector2(1100, 110);
        banner = bannerGo.AddComponent<Text>();
        banner.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        banner.fontSize = 36;
        banner.fontStyle = FontStyle.Bold;
        banner.alignment = TextAnchor.MiddleCenter;
        banner.color = new Color(1f, 0.85f, 0.3f);
        banner.supportRichText = true;
        // Frases maiores (ex: as novas mensagens por tier + valor de ouro) podem
        // quebrar em 2 linhas - sem isso o Text corta a segunda linha (era o bug
        // relatado: a frase aparecia cortada no meio).
        banner.horizontalOverflow = HorizontalWrapMode.Wrap;
        banner.verticalOverflow = VerticalWrapMode.Overflow;
        banner.text = "";
        banner.raycastTarget = false;

        GameObject timerGo = new GameObject("HordeTimer", typeof(RectTransform));
        timerGo.transform.SetParent(overlayGo.transform, false);
        RectTransform timerRt = timerGo.GetComponent<RectTransform>();
        timerRt.anchorMin = new Vector2(0.5f, 0.68f);
        timerRt.anchorMax = new Vector2(0.5f, 0.68f);
        timerRt.sizeDelta = new Vector2(700, 50);
        timerText = timerGo.AddComponent<Text>();
        timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timerText.fontSize = 26;
        timerText.alignment = TextAnchor.MiddleCenter;
        timerText.color = Color.white;
        timerText.text = "";
        timerText.raycastTarget = false;

        BuildCombatPresentation(overlayGo.transform);
        BuildChoiceUi(overlayGo.transform);

        // O cursor (martelo/luva) e filho direto do Canvas tambem - sem isso
        // ele ficaria por baixo do escurecido e pareceria "apagado" durante
        // a horda. Forca ele a ficar sempre por cima de tudo (inclusive da
        // tela de escolha de bonus).
        if (HammerFollowMouse.Instance != null)
        {
            HammerFollowMouse.Instance.transform.SetAsLastSibling();
        }
    }

    /// <summary>Tela (escondida ate ser usada) com 2 botoes pra escolher o bonus permanente ao vencer uma horda.</summary>
    private void BuildChoiceUi(Transform parent)
    {
        choicePanel = new GameObject("HordeChoicePanel", typeof(RectTransform));
        choicePanel.transform.SetParent(parent, false);
        RectTransform panelRt = choicePanel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        var blocker = choicePanel.AddComponent<Image>();
        blocker.color = new Color(.025f, .018f, .012f, .75f);
        choiceContent = new GameObject("RewardContent", typeof(RectTransform)).GetComponent<RectTransform>();
        choiceContent.SetParent(choicePanel.transform, false);
        choiceContent.anchorMin = choiceContent.anchorMax = new Vector2(.5f, .5f);
        choiceContent.sizeDelta = new Vector2(1040, 420);

        GameObject titleGo = new GameObject("HordeChoiceTitle", typeof(RectTransform));
        titleGo.transform.SetParent(choiceContent, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.anchoredPosition = new Vector2(0, 160);
        titleRt.sizeDelta = new Vector2(1000, 70);
        choiceTitle = titleGo.AddComponent<Text>();
        choiceTitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        choiceTitle.fontSize = 34;
        choiceTitle.fontStyle = FontStyle.Bold;
        choiceTitle.alignment = TextAnchor.MiddleCenter;
        choiceTitle.color = new Color(1f, 0.85f, 0.3f);
        choiceTitle.text = "ESCOLHA UM BONUS PERMANENTE";
        choiceTitle.raycastTarget = false;

        float clickBuff = GetEffectiveGoldPerClickBuffPercent();
        float secondBuff = GetEffectiveGoldPerSecondBuffPercent();

        Button buttonA = CreateChoiceButton(choiceContent, "HordeChoiceButtonA", new Vector2(-260, 0),
            "+" + clickBuff.ToString("0.#") + "% ouro\npor martelada\n(permanente)", out buffTextA);
        buttonA.onClick.AddListener(() =>
        {
            if (choiceMade) return;
            float percent = GetEffectiveGoldPerClickBuffPercent();
            GameManager.Instance?.AddGoldPerClickBonusPercent(percent);
            AnvilClicker.Instance?.SpawnFloatingText("+" + percent.ToString("0.#") + "% OURO/MARTELADA!");
            choiceMade = true;
        });

        Button buttonB = CreateChoiceButton(choiceContent, "HordeChoiceButtonB", new Vector2(260, 0),
            "+" + secondBuff.ToString("0.#") + "% ouro\ndos trabalhadores\n(permanente)", out buffTextB);
        buttonB.onClick.AddListener(() =>
        {
            if (choiceMade) return;
            float percent = GetEffectiveGoldPerSecondBuffPercent();
            GameManager.Instance?.AddGoldPerSecondBonusPercent(percent);
            AnvilClicker.Instance?.SpawnFloatingText("+" + percent.ToString("0.#") + "% OURO/TRABALHADORES!");
            choiceMade = true;
        });

        choicePanel.SetActive(false);
    }

    private Button CreateChoiceButton(Transform parent, string name, Vector2 anchoredPos, string label, out Text labelText)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(480, 220);
        rt.anchoredPosition = anchoredPos;

        Image img = go.AddComponent<Image>();
        img.color = new Color(.12f, .075f, .04f, .98f);
        var shop = ShopManager.Instance;
        if (shop != null && shop.cardSprite != null)
        {
            if (rewardCardSprite == null)
            {
                var texture = shop.cardSprite.texture;
                Vector4 crop = shop.cardCrop;
                rewardCardSprite = Sprite.Create(texture, new Rect(crop.x * texture.width, crop.y * texture.height,
                    crop.z * texture.width, crop.w * texture.height), new Vector2(.5f, .5f), shop.cardSprite.pixelsPerUnit);
            }
            img.sprite = rewardCardSprite; img.color = Color.white;
        }
        Outline border = go.AddComponent<Outline>();
        border.effectColor = new Color(.95f, .6f, .12f); border.effectDistance = new Vector2(2, -2);

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors; colors.highlightedColor = new Color(1, .88f, .55f); btn.colors = colors;
        go.AddComponent<HammerClickTarget>();

        GameObject textGo = new GameObject(name + "_Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(14, 14);
        textRt.offsetMax = new Vector2(-14, -14);

        Text text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 25;
        text.fontStyle = FontStyle.Bold;
        text.supportRichText = true;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;
        text.raycastTarget = false;
        labelText = text;

        return btn;
    }

    private IEnumerator FadeOverlay(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetOverlayAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        SetOverlayAlpha(to);
    }

    private void SetOverlayAlpha(float a)
    {
        if (overlay == null) return;
        Color c = overlay.color;
        c.a = a;
        overlay.color = c;
    }

    private void SetBanner(string text)
    {
        if (banner != null) banner.text = text;
        if (warningPanel != null) warningPanel.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    private void SetTimerText(float secondsLeft)
    {
        if (timerText == null) return;
        timerText.gameObject.SetActive(secondsLeft >= 0);
        if (timeFill != null)
        {
            timeFill.transform.parent.gameObject.SetActive(secondsLeft >= 0);
            timeFill.fillAmount = battleTimeLimit > 0 ? Mathf.Clamp01(secondsLeft / battleTimeLimit) : 0;
            timeFill.color = secondsLeft <= 5 ? new Color(1, .28f, .15f) : new Color(1, .70f, .2f);
        }
        timerText.color = secondsLeft <= 5 ? new Color(1, .5f, .35f) : Color.white;
        timerText.text = secondsLeft >= 0 ? "HORDA " + hordeNumber + " • " + Mathf.CeilToInt(secondsLeft) + "s • " +
            (spawnedCount - activeEnemies.Count) + "/" + spawnedCount + " derrotados" : "";
    }

    private int GetEnemyCount()
    {
        int count = baseEnemyCount + (hordeNumber - 1) * enemiesPerHorde + currentTier * enemiesPerTier;
        if (CurrentPattern == AttackPattern.Rush) count += 2;
        else if (CurrentPattern == AttackPattern.Armored) count = Mathf.CeilToInt(count * .8f);
        return Mathf.Clamp(count, 1, Mathf.Max(1, maxEnemyCount));
    }

    private string PatternName() => CurrentPattern == AttackPattern.Rush ? "ATAQUE RÁPIDO" :
        CurrentPattern == AttackPattern.Armored ? "VANGUARDA BLINDADA" : "INVASÃO";
    private string PatternHint() => CurrentPattern == AttackPattern.Rush ? "Mais inimigos, menos vida. Acerte rápido!" :
        CurrentPattern == AttackPattern.Armored ? "Menos inimigos, mais resistentes. Use um martelo melhor!" : "Derrote todos antes do tempo acabar para ganhar um bônus permanente.";

    private string ClickRewardPreview()
    {
        float current = GameManager.Instance != null ? GameManager.Instance.GoldPerClickBonusPercent : 0;
        return "FORÇA DO FERREIRO\n<color=#F9E77E>+" + GetEffectiveGoldPerClickBuffPercent().ToString("0.#") +
            "% ouro por golpe</color>\n<size=21>Bônus total: " + current.ToString("0.#") + "% → " +
            (current + GetEffectiveGoldPerClickBuffPercent()).ToString("0.#") + "%\nPERMANENTE • CLIQUE PARA ESCOLHER</size>";
    }

    private string WorkerRewardPreview()
    {
        float current = GameManager.Instance != null ? GameManager.Instance.GoldPerSecondBonusPercent : 0;
        return "OFICINA EFICIENTE\n<color=#F9E77E>+" + GetEffectiveGoldPerSecondBuffPercent().ToString("0.#") +
            "% ouro por segundo</color>\n<size=21>Bônus total: " + current.ToString("0.#") + "% → " +
            (current + GetEffectiveGoldPerSecondBuffPercent()).ToString("0.#") + "%\nPERMANENTE • CLIQUE PARA ESCOLHER</size>";
    }

    private void BuildCombatPresentation(Transform parent)
    {
        warningPanel = new GameObject("HordeWarningPanel", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        warningPanel.transform.SetParent(parent, false);
        RectTransform rect = warningPanel.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .82f);
        rect.sizeDelta = new Vector2(1120, 164);
        warningPanel.color = new Color(.08f, .045f, .025f, .97f); warningPanel.raycastTarget = false;
        var edge = warningPanel.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(.8f, .41f, .13f); edge.effectDistance = new Vector2(2, -2);
        banner.transform.SetParent(rect, false);
        banner.rectTransform.anchorMin = Vector2.zero; banner.rectTransform.anchorMax = Vector2.one;
        banner.rectTransform.offsetMin = new Vector2(18, 8); banner.rectTransform.offsetMax = new Vector2(-18, -8);
        banner.fontSize = 29; banner.verticalOverflow = VerticalWrapMode.Truncate;
        warningPanel.gameObject.SetActive(false);
        var track = new GameObject("HordeTimeTrack", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        track.transform.SetParent(parent, false);
        track.rectTransform.anchorMin = track.rectTransform.anchorMax = new Vector2(.5f, .64f);
        track.rectTransform.sizeDelta = new Vector2(600, 10);
        track.color = new Color(.15f, .10f, .06f); track.raycastTarget = false;
        timeFill = new GameObject("HordeTimeFill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        timeFill.transform.SetParent(track.transform, false);
        timeFill.rectTransform.anchorMin = Vector2.zero; timeFill.rectTransform.anchorMax = Vector2.one;
        timeFill.rectTransform.offsetMin = timeFill.rectTransform.offsetMax = Vector2.zero;
        timeFill.type = Image.Type.Filled;
        timeFill.fillMethod = Image.FillMethod.Horizontal; timeFill.fillOrigin = 0; timeFill.raycastTarget = false;
        // A full solid rect is used for the timer instead of the circular fallback sprite.
        timeFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(.5f, .5f));
        track.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (canvas == null) return;
        Vector2 area = ((RectTransform)canvas.transform).rect.size;
        if (choiceContent != null)
            choiceContent.localScale = Vector3.one * Mathf.Clamp(Mathf.Min((area.x - 48) / 1040, (area.y - 48) / 420), .1f, 1);
        if (warningPanel != null) warningPanel.rectTransform.localScale = Vector3.one * Mathf.Clamp((area.x - 48) / 1120, .1f, 1);
        if (timerText != null) timerText.rectTransform.localScale = Vector3.one * Mathf.Clamp((area.x - 48) / 700, .1f, 1);
        if (timeFill != null) timeFill.transform.parent.localScale = Vector3.one * Mathf.Clamp((area.x - 48) / 600, .1f, 1);
    }

    /// <summary>Mantem a proporcao original do sprite, encaixando na altura alvo (em pixels de UI).</summary>
    private Vector2 GetSpriteDisplaySize(Sprite sprite, float targetHeight)
    {
        float ratio = sprite.rect.width / sprite.rect.height;
        return new Vector2(targetHeight * ratio, targetHeight);
    }

    private Sprite GetPlaceholderSprite()
    {
        if (placeholderSprite != null) return placeholderSprite;

        int size = 64;
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                pixels[y * size + x] = dist <= radius ? Color.white : new Color(0, 0, 0, 0);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        placeholderSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return placeholderSprite;
    }
}
