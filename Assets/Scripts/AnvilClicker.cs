using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Detecta o clique/toque na bigorna, respeita a resistencia do braco
/// (obstaculo), manda o GameManager somar ouro, anima o martelo batendo
/// e mostra o texto flutuante ("+N" ou "Cansado!") nascendo em cima do
/// mouse/martelo, em vez de um ponto fixo na bigorna.
/// Coloque este script no GameObject da Bigorna (precisa ter um
/// Collider2D marcado, ex: BoxCollider2D ou PolygonCollider2D).
/// </summary>
public class AnvilClicker : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Prefab do FloatingText (+1, +2, Cansado!...)")]
    public GameObject floatingTextPrefab;

    [Tooltip("Onde o texto flutuante nasce, caso o mouse nao seja detectado (fallback)")]
    public Transform floatingTextSpawnPoint;

    [Header("Texto flutuante")]
    [Tooltip("Deslocamento a partir da posicao do mouse, em unidades do mundo")]
    public Vector3 floatingTextOffset = new Vector3(0f, 0.3f, 0f);

    [Header("Colisao da martelada")]
    [Tooltip("Raio (em unidades do mundo) de tolerancia ao redor da cabeca do martelo pra considerar que ela encostou na bigorna. 0 = exige que o ponto da cabeca esteja exatamente dentro do collider da bigorna.")]
    public float hammerHeadHitRadius = 0.4f;

    /// <summary>Usado por outros scripts (ex: UIManager) pra mostrar um aviso com o mesmo estilo de texto flutuante.</summary>
    public static AnvilClicker Instance { get; private set; }

    private Collider2D anvilCollider;
    private AnvilFeedback feedback;

    private void Awake()
    {
        Instance = this;
        anvilCollider = GetComponent<Collider2D>();
        feedback = GetComponent<AnvilFeedback>();
        if (feedback == null) feedback = gameObject.AddComponent<AnvilFeedback>();
    }

    // OnMouseDown so serve mais de dica ("Pegue o martelo!") quando o jogador
    // clica direto em cima da bigorna sem o martelo na mao.
    private void OnMouseDown()
    {
        if (HammerFollowMouse.Instance != null && HammerFollowMouse.Instance.IsHolding) return;
        if (ShopManager.Instance != null && ShopManager.Instance.IsShopOpen) return;

        SpawnFloatingText("Pegue o martelo!");
    }

    // O jogador martela em QUALQUER clique, em qualquer lugar da tela (o
    // HammerFollowMouse cuida da animacao independente disso). Aqui a gente
    // so decide se aquele golpe realmente vale ouro: conta toda vez que a
    // CABECA do martelo estiver encostando na bigorna no instante do clique -
    // nao depende mais de acertar o clique exatamente em cima dela (o cursor
    // de verdade fica escondido e deslocado da luva desenhada na tela).
    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CampaignCompleted) return;
        if (Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        if (HammerFollowMouse.Instance == null || !HammerFollowMouse.Instance.IsHolding) return;
        if (HammerFollowMouse.Instance.JustPickedUpThisFrame) return;
        if (ShopManager.Instance != null && ShopManager.Instance.IsShopOpen) return;
        // Trava a bigorna durante TODO o evento de horde (nao so a luta) -
        // assim o ouro por martelada tambem para de contar a partir do
        // instante exato em que o marco e cruzado.
        if (HordeManager.Instance != null && HordeManager.Instance.IsHordeInProgress) return;

        Vector3 headWorldPos = HammerFollowMouse.Instance.GetHammerHeadWorldPosition();
        if (IsHammerHeadTouchingAnvil(headWorldPos))
        {
            HandleHit();
        }
    }

    /// <summary>Testa se a cabeca do martelo (ponto no mundo) esta encostando no collider da bigorna.</summary>
    private bool IsHammerHeadTouchingAnvil(Vector3 headWorldPos)
    {
        if (anvilCollider == null) return false;

        if (hammerHeadHitRadius <= 0f)
        {
            return anvilCollider.OverlapPoint(headWorldPos);
        }

        Collider2D hit = Physics2D.OverlapCircle(headWorldPos, hammerHeadHitRadius);
        return hit != null && hit.gameObject == gameObject;
    }

    private void HandleHit()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("GameManager nao encontrado na cena.");
            return;
        }

        // Obstaculo: sem resistencia suficiente, a martelada nao rende ouro.
        if (!GameManager.Instance.HasStamina())
        {
            SpawnFloatingText("Cansado!");
            return;
        }

        GameManager.Instance.ConsumeStaminaForHit();

        int amount = GameManager.Instance.RegisterHit();
        bool isCrit = GameManager.Instance.LastHitWasCrit;

        Vector3 contact = HammerFollowMouse.Instance != null ? HammerFollowMouse.Instance.GetHammerHeadWorldPosition() : transform.position;
        if (anvilCollider != null) contact = anvilCollider.ClosestPoint(contact);
        feedback?.PlayHit(contact, isCrit);
        UIManager.Instance?.NotifyHit(isCrit);
        WorkshopPresentation.Instance?.NotifyAnvilHit();
        SpawnFloatingText(isCrit ? "+" + amount + " CRÍTICO!" : "+" + amount, isCrit);
        // A animacao de martelada agora roda em QUALQUER clique (ver
        // HammerFollowMouse.Update()), entao nao precisa disparar de novo aqui.
    }

    /// <summary>Publico pra outros scripts (ex: aviso de "solte o martelo" da loja) poderem usar o mesmo popup de texto.</summary>
    public void SpawnFloatingText(string text, bool critical = false)
    {
        if (floatingTextPrefab == null) return;

        Vector3 spawnPos = GetFloatingTextSpawnPosition() + floatingTextOffset;

        GameObject go = Instantiate(floatingTextPrefab, spawnPos, Quaternion.identity);
        FloatingText ft = go.GetComponent<FloatingText>();
        if (ft != null)
        {
            ft.Setup(text, critical);
        }
    }

    /// <summary>
    /// De preferencia nasce em cima da cabeca do martelo (com ele na mao, e
    /// onde a martelada de fato aconteceu). Sem o martelo, cai pro mouse real
    /// - caso da dica "Pegue o martelo!".
    /// </summary>
    private Vector3 GetFloatingTextSpawnPosition()
    {
        if (HammerFollowMouse.Instance != null && HammerFollowMouse.Instance.IsHolding)
        {
            Vector3 headPos = HammerFollowMouse.Instance.GetHammerHeadWorldPosition();
            headPos.z = transform.position.z;
            return headPos;
        }

        return GetMouseWorldPosition();
    }

    /// <summary>Converte a posicao real do mouse (tela) pra posicao no mundo 2D, na mesma profundidade da bigorna.</summary>
    private Vector3 GetMouseWorldPosition()
    {
        if (Camera.main != null && Mouse.current != null)
        {
            Vector2 screenPos = Mouse.current.position.ReadValue();
            float distanceFromCamera = -Camera.main.transform.position.z;
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, distanceFromCamera));
            worldPos.z = transform.position.z;
            return worldPos;
        }

        // Fallback, caso nao consiga ler o mouse por algum motivo.
        return floatingTextSpawnPoint != null
            ? floatingTextSpawnPoint.position
            : transform.position + Vector3.up * 0.5f;
    }
}
