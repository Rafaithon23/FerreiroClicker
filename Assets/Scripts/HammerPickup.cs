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

    private void Start()
    {
        // Comeca sincronizado com o estado da luva: se por algum motivo a
        // cena carregar com o martelo ja em maos, o suporte ja nasce vazio.
        if (HammerFollowMouse.Instance != null && HammerFollowMouse.Instance.IsHolding)
        {
            SetVisualState(false);
        }
        else
        {
            SetVisualState(true);
        }
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
    private void SetVisualState(bool hammerPresent)
    {
        if (visual != null)
        {
            Sprite target = hammerPresent ? fullSprite : emptySprite;
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
}
