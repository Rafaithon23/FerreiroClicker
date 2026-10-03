using UnityEngine;

/// <summary>
/// Coloque este script no martelo que fica largado do lado da bigorna, no
/// mundo (SpriteRenderer + Collider2D marcado como Trigger). Clicar nele
/// pega o martelo (a luva do cursor passa a segura-lo e some o martelo daqui);
/// clicar de novo devolve ele pro lugar.
///
/// Pra bater na bigorna, o jogador precisa estar SEGURANDO o martelo -
/// o AnvilClicker checa HammerFollowMouse.Instance.IsHolding antes de
/// valer o golpe.
/// </summary>
public class HammerPickup : MonoBehaviour
{
    [Tooltip("SpriteRenderer do suporte onde o martelo fica (o mesmo GameObject, geralmente).")]
    public SpriteRenderer visual;

    [Tooltip("Sprite do suporte COM o martelo em cima (estado inicial / depois de devolver).")]
    public Sprite fullSprite;

    [Tooltip("Sprite do suporte VAZIO (depois que o jogador pega o martelo).")]
    public Sprite emptySprite;

    [Header("Tier 2 - Martelo Reforcado (opcional)")]
    [Tooltip("Suporte com o Martelo Reforcado em cima. Se nao arrastar nada, continua usando o sprite do tier 1.")]
    public Sprite fullSpriteTier2;
    [Tooltip("Suporte vazio (visual do tier 2, se for diferente do suporte base).")]
    public Sprite emptySpriteTier2;

    [Header("Tier 3 - Martelo Lendario (opcional)")]
    [Tooltip("Suporte com o Martelo Lendario em cima. Se nao arrastar nada, cai pro tier 2 ou pro tier 1.")]
    public Sprite fullSpriteTier3;
    [Tooltip("Suporte vazio (visual do tier 3, se for diferente do suporte base).")]
    public Sprite emptySpriteTier3;

    // Guarda se o martelo esta no suporte ou na mao - precisa disso separado
    // do SetVisualState porque, quando o tier muda (upgrade comprado), tem
    // que reaplicar a sprite certa SEM mudar esse estado (ver HandleHammerTierChanged).
    private bool hammerPresent = true;

    private void Start()
    {
        // Assina em Start() (nao em OnEnable) de proposito: Unity so garante
        // que o Awake() de TODOS os objetos da cena ja rodou quando chega no
        // Start - no OnEnable isso nao e garantido, entao GameManager.Instance
        // podia ainda estar null aqui dependendo da ordem dos objetos na cena,
        // e a inscricao no evento simplesmente nao acontecia (bug: sprite so
        // atualizava depois de fechar e abrir o jogo de novo). Mesmo padrao
        // que o ShopManager ja usa pros eventos do GameManager.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnHammerTierChanged += HandleHammerTierChanged;
        }

        // Comeca sincronizado com o estado da luva: se por algum motivo a
        // cena carregar com o martelo ja em maos, o suporte ja nasce vazio.
        bool startsHolding = HammerFollowMouse.Instance != null && HammerFollowMouse.Instance.IsHolding;
        SetVisualState(!startsHolding);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnHammerTierChanged -= HandleHammerTierChanged;
        }
    }

    /// <summary>Chamado pelo GameManager quando o jogador compra um tier novo de martelo - so troca a sprite, sem mexer se o martelo esta na mao ou no suporte agora.</summary>
    private void HandleHammerTierChanged(int tier)
    {
        SetVisualState(hammerPresent);
    }

    // OnMouseDown funciona tanto pra clique de mouse (Editor/PC) quanto
    // pra toque na tela em builds mobile.
    private void OnMouseDown()
    {
        if (HammerFollowMouse.Instance == null) return;

        if (ShopManager.Instance != null && ShopManager.Instance.IsShopOpen)
        {
            return;
        }

        if (HammerFollowMouse.Instance.IsHolding)
        {
            // Durante uma horde o jogador e obrigado a manter o martelo
            // na mao - nao deixa devolver pro suporte nesse meio tempo.
            if (HordeManager.Instance != null && HordeManager.Instance.IsHordeInProgress)
            {
                AnvilClicker.Instance?.SpawnFloatingText("Precisa do martelo na horda!");
                return;
            }

            HammerFollowMouse.Instance.PutDown();
            SetVisualState(true);
        }
        else
        {
            HammerFollowMouse.Instance.PickUp();
            SetVisualState(false);
        }
    }

    // O suporte agora fica sempre visivel - so troca a sprite entre "com
    // martelo" e "vazio", em vez de sumir o objeto inteiro da cena.
    private void SetVisualState(bool present)
    {
        hammerPresent = present;

        if (visual != null)
        {
            Sprite target = present ? GetFullSpriteForCurrentTier() : GetEmptySpriteForCurrentTier();
            if (target != null)
            {
                visual.sprite = target;
            }
            visual.enabled = true;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            // Mantem o collider ativo nos dois estados: assim da pra clicar
            // no mesmo lugar de novo pra devolver o martelo ao suporte.
            col.enabled = true;
        }
    }

    /// <summary>Sprite do suporte COM martelo, conforme o tier comprado (cai pro tier anterior se a sprite nao foi arrastada no Inspector).</summary>
    private Sprite GetFullSpriteForCurrentTier()
    {
        int tier = GameManager.Instance != null ? GameManager.Instance.HammerTier : 1;
        if (tier >= 3 && fullSpriteTier3 != null) return fullSpriteTier3;
        if (tier >= 2 && fullSpriteTier2 != null) return fullSpriteTier2;
        return fullSprite;
    }

    /// <summary>Sprite do suporte VAZIO, conforme o tier comprado (cai pro tier anterior se a sprite nao foi arrastada no Inspector).</summary>
    private Sprite GetEmptySpriteForCurrentTier()
    {
        int tier = GameManager.Instance != null ? GameManager.Instance.HammerTier : 1;
        if (tier >= 3 && emptySpriteTier3 != null) return emptySpriteTier3;
        if (tier >= 2 && emptySpriteTier2 != null) return emptySpriteTier2;
        return emptySprite;
    }
}
