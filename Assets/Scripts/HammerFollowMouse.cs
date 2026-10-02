using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Cursor customizado do jogo: por padrao e uma luva vazia (m_ao aberta,
/// pronta pra pegar o martelo). Quando o jogador pega o martelo (ver
/// HammerPickup.cs), troca pra sprite da luva segurando o martelo e passa
/// a poder bater na bigorna (AnvilClicker chama PlaySwing() a cada golpe
/// valido). Clicar de novo no "slot" do martelo devolve pro estado de
/// luva vazia.
///
/// Mantive o nome do arquivo/classe (HammerFollowMouse) pra nao quebrar a
/// referencia ja existente no GameObject "hammer" da cena - por dentro o
/// script agora e o controlador do cursor "luva".
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class HammerFollowMouse : MonoBehaviour
{
    public static HammerFollowMouse Instance { get; private set; }

    [Header("Sprites da luva")]
    public Sprite emptyGloveSprite;
    public Sprite heldHammerSprite;

    [Tooltip("Sprite mostrada quando o cursor passa em cima de um botao/UI clicavel (com a mao vazia), tipo o ponteiro do Windows. Opcional - se nao arrastar nada, so continua com a luva vazia.")]
    public Sprite pointerSprite;

    [Header("Colisao da martelada")]
    [Tooltip("Filho vazio (RectTransform) posicionado em cima da cabeca do martelo na sprite. Arraste ele na Scene View se a colisao parecer deslocada - ele gira e se move junto com a luva.")]
    public RectTransform hammerHeadPoint;

    [Tooltip("Ajuste fino de onde o 'dedo/cabeca do martelo' fica em relacao ao cursor real. O pivot do RectTransform fica no punho/braçadeira (pra girar certo quando bate); esse offset empurra o desenho de volta pra ficar em cima do cursor de verdade.")]
    public Vector2 offset = new Vector2(85f, -70f);

    [Tooltip("Se marcado, esconde o cursor do mouse do Windows enquanto o jogo roda.")]
    public bool hideSystemCursor = true;

    [Header("Animacao de batida (so roda quando esta segurando o martelo)")]
    public float swingAngle = 45f;
    public float swingDuration = 0.09f;
    public Vector2 swingPunch = new Vector2(10f, -12f);

    [Header("Impacto (efeito separado, nao troca a sprite da luva)")]
    public Image impactBurst;
    public Vector2 impactBurstOffset = new Vector2(0f, 20f);
    public float impactBurstDuration = 0.18f;
    public float impactBurstStartScale = 0.6f;
    public float impactBurstEndScale = 1.15f;
    public float impactHoldTime = 0.05f;

    /// <summary>Verdadeiro quando o jogador esta com o martelo na mao.</summary>
    public bool IsHolding { get; private set; }

    // Marca em que frame o martelo foi pego. O clique que pega o martelo
    // (OnMouseDown) roda antes do Update() deste mesmo frame - sem isso,
    // IsHolding vira true e o "wasPressedThisFrame" ainda esta valendo,
    // entao o proprio clique de pegar disparava uma martelada/hit instantanea.
    private int pickupFrame = -1;

    /// <summary>True so no exato frame em que o jogador pegou o martelo - usado pra ignorar esse clique na hora de martelar.</summary>
    public bool JustPickedUpThisFrame => pickupFrame == Time.frameCount;

    private RectTransform rectTransform;
    private Image image;
    private Quaternion restRotation;
    private Vector2 punchOffsetCurrent;
    private Coroutine swingRoutine;
    private Coroutine impactBurstRoutine;

    private PointerEventData pointerEventData;
    private readonly List<RaycastResult> raycastResultsBuffer = new List<RaycastResult>();

    private void Awake()
    {
        Instance = this;
        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        restRotation = rectTransform.localRotation;

        if (impactBurst != null)
        {
            impactBurst.gameObject.SetActive(false);
        }

        ApplySprite();
    }

    private void Start()
    {
        if (hideSystemCursor)
        {
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (Mouse.current == null || rectTransform == null) return;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

        // Numa Canvas em Screen Space - Overlay, a posicao (em pixels da tela)
        // bate direto com a posicao do RectTransform, sem precisar de camera.
        Vector2 finalPos = mouseScreenPos + offset + punchOffsetCurrent;
        rectTransform.position = new Vector3(finalPos.x, finalPos.y, rectTransform.position.z);

        UpdateHoverSprite();

        // Martela em QUALQUER clique, em qualquer lugar da tela (inclusive
        // em cima de botoes/loja) - e so um feedback visual. O AnvilClicker
        // e quem decide, separadamente, se aquele golpe realmente encostou
        // na bigorna e deve valer ouro.
        if (IsHolding && Mouse.current.leftButton.wasPressedThisFrame && !JustPickedUpThisFrame)
        {
            PlaySwing();
        }
    }

    /// <summary>Chamado pelo HammerPickup ao clicar no martelo largado perto da bigorna.</summary>
    public void PickUp()
    {
        if (IsHolding) return;
        IsHolding = true;
        pickupFrame = Time.frameCount;
        ApplySprite();
    }

    /// <summary>Chamado pelo HammerPickup pra largar o martelo de volta no lugar.</summary>
    public void PutDown()
    {
        if (!IsHolding) return;
        IsHolding = false;
        ApplySprite();
    }

    private void ApplySprite()
    {
        if (image == null) return;
        Sprite target = IsHolding ? heldHammerSprite : emptyGloveSprite;
        if (target != null)
        {
            image.sprite = target;
        }
    }

    /// <summary>
    /// Roda todo frame (so com a mao vazia): se o cursor estiver em cima de
    /// algum botao/UI clicavel, troca pra sprite "de ponteiro" - igual o
    /// cursor do Windows vira uma maozinha em cima de um link. Enquanto
    /// segura o martelo isso nao roda, a sprite fica por conta do
    /// PickUp/PutDown (ver ApplySprite).
    /// </summary>
    private void UpdateHoverSprite()
    {
        if (image == null || IsHolding) return;

        bool hovering = pointerSprite != null && IsPointerOverClickable();
        Sprite target = hovering ? pointerSprite : emptyGloveSprite;
        if (target != null && image.sprite != target)
        {
            image.sprite = target;
        }
    }

    /// <summary>
    /// True se o cursor estiver em cima de algum elemento de UI clicavel
    /// (Button, etc - qualquer Selectable interativo). Usa o EventSystem
    /// normal do Unity, entao funciona em QUALQUER botao da cena sem
    /// precisar configurar nada nele.
    /// </summary>
    private bool IsPointerOverClickable()
    {
        if (EventSystem.current == null || Mouse.current == null) return false;

        if (pointerEventData == null)
        {
            pointerEventData = new PointerEventData(EventSystem.current);
        }
        pointerEventData.position = Mouse.current.position.ReadValue();

        raycastResultsBuffer.Clear();
        EventSystem.current.RaycastAll(pointerEventData, raycastResultsBuffer);

        for (int i = 0; i < raycastResultsBuffer.Count; i++)
        {
            Selectable selectable = raycastResultsBuffer[i].gameObject.GetComponentInParent<Selectable>();
            if (selectable != null && selectable.interactable)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Posicao em PIXELS DE TELA de onde a cabeca do martelo esta agora (ou
    /// da propria luva, se nao houver um ponto de cabeca definido - caso do
    /// cursor do menu, que nao segura martelo). Usada tanto pra colisao no
    /// mundo (bigorna) quanto pra "clicar" em botoes de UI (HammerClickTarget).
    /// </summary>
    public Vector2 GetHammerHeadScreenPosition()
    {
        return hammerHeadPoint != null ? (Vector2)hammerHeadPoint.position : (Vector2)rectTransform.position;
    }

    /// <summary>
    /// Posicao no MUNDO (2D) de onde a cabeca do martelo esta agora na tela.
    /// O AnvilClicker usa isso pra saber se a martelada "colidiu" com a bigorna,
    /// em vez de depender de clicar exatamente em cima dela.
    /// </summary>
    public Vector3 GetHammerHeadWorldPosition()
    {
        if (Camera.main == null) return Vector3.zero;

        Vector2 screenPos = GetHammerHeadScreenPosition();
        float distanceFromCamera = -Camera.main.transform.position.z;
        return Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, distanceFromCamera));
    }

    /// <summary>Chamado pelo AnvilClicker a cada golpe valido (so faz sentido com o martelo na mao).</summary>
    public void PlaySwing()
    {
        if (!IsHolding) return;

        if (swingRoutine != null)
        {
            StopCoroutine(swingRoutine);
            punchOffsetCurrent = Vector2.zero;
        }
        swingRoutine = StartCoroutine(SwingRoutine());
    }

    private IEnumerator SwingRoutine()
    {
        // Espera 1 frame antes de girar QUALQUER coisa. Sem isso, como uma
        // coroutine roda de forma sincrona ate o primeiro "yield", a
        // rotacao/punch comecavam a mudar ainda dentro do MESMO Update() do
        // clique - ou seja, no instante exato em que HammerClickTarget (ou
        // AnvilClicker) lia a posicao da cabeca do martelo pra decidir se
        // acertou, ela ja tinha pulado pra um angulo diferente de onde o
        // jogador mirou. Isso fazia a martelada "errar" botoes pequenos
        // (a bigorna, bem maior, disfarçava o problema).
        yield return null;

        Quaternion down = restRotation * Quaternion.Euler(0f, 0f, swingAngle);
        float t = 0f;

        while (t < swingDuration)
        {
            t += Time.deltaTime;
            float progress = t / swingDuration;
            rectTransform.localRotation = Quaternion.Lerp(restRotation, down, progress);
            punchOffsetCurrent = Vector2.Lerp(Vector2.zero, swingPunch, progress);
            yield return null;
        }
        rectTransform.localRotation = down;
        punchOffsetCurrent = swingPunch;

        if (impactBurst != null)
        {
            if (impactBurstRoutine != null) StopCoroutine(impactBurstRoutine);
            impactBurstRoutine = StartCoroutine(ImpactBurstRoutine());
        }
        if (impactHoldTime > 0f)
        {
            yield return new WaitForSeconds(impactHoldTime);
        }

        t = 0f;
        while (t < swingDuration)
        {
            t += Time.deltaTime;
            float progress = t / swingDuration;
            rectTransform.localRotation = Quaternion.Lerp(down, restRotation, progress);
            punchOffsetCurrent = Vector2.Lerp(swingPunch, Vector2.zero, progress);
            yield return null;
        }

        rectTransform.localRotation = restRotation;
        punchOffsetCurrent = Vector2.zero;
    }

    private IEnumerator ImpactBurstRoutine()
    {
        Vector3 headPos = hammerHeadPoint != null ? (Vector3)hammerHeadPoint.position : rectTransform.position;
        impactBurst.rectTransform.position = headPos + (Vector3)impactBurstOffset;
        impactBurst.gameObject.SetActive(true);

        Color c = impactBurst.color;
        c.a = 1f;
        impactBurst.color = c;
        impactBurst.rectTransform.localScale = Vector3.one * impactBurstStartScale;

        float t = 0f;
        while (t < impactBurstDuration)
        {
            t += Time.deltaTime;
            float p = t / impactBurstDuration;
            impactBurst.rectTransform.localScale = Vector3.Lerp(Vector3.one * impactBurstStartScale, Vector3.one * impactBurstEndScale, p);
            c.a = Mathf.Lerp(1f, 0f, p);
            impactBurst.color = c;
            yield return null;
        }

        impactBurst.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        Cursor.visible = true;
    }
}
