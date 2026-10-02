using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Faz este botao "clicar" quando a cabeca do martelo (ou a luva, se estiver
/// vazia - ex: no menu) encostar nele no instante do clique, em vez de
/// depender da posicao real (escondida) do cursor do Windows, que fica
/// deslocada da luva desenhada na tela. Mesmo principio usado na bigorna
/// (ver AnvilClicker.cs).
///
/// Coloque este script em QUALQUER GameObject que tenha um Button - ele
/// pega o RectTransform e o Button sozinho, nao precisa arrastar nada no
/// Inspector. Ele tambem desliga o "Raycast Target" da imagem do botao,
/// pra o clique normal de UI (baseado no cursor real) nao disparar de novo
/// junto com este script e acabar clicando 2x.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Button))]
public class HammerClickTarget : MonoBehaviour
{
    [Tooltip("Margem extra (em pixels de tela) ao redor do botao, pra nao exigir que a cabeca do martelo acerte pixel perfeito.")]
    public float hitPadding = 12f;

    private RectTransform rectTransform;
    private Button button;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        button = GetComponent<Button>();

        // Evita clique duplicado: o clique normal de UI (via EventSystem,
        // baseado no cursor real escondido) fica desligado - so este script
        // decide se o botao foi "encostado" pela luva/martelo.
        Graphic graphic = GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.raycastTarget = false;
        }
    }

    private void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        if (button == null || !button.interactable) return;
        if (HammerFollowMouse.Instance == null) return;
        if (HammerFollowMouse.Instance.JustPickedUpThisFrame) return;

        Vector2 screenPos = HammerFollowMouse.Instance.GetHammerHeadScreenPosition();
        if (IsPointOverButton(screenPos))
        {
            button.onClick.Invoke();
        }
    }

    private bool IsPointOverButton(Vector2 screenPos)
    {
        if (hitPadding <= 0f)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPos, null);
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPos, null, out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = rectTransform.rect;
        return localPoint.x >= rect.xMin - hitPadding && localPoint.x <= rect.xMax + hitPadding &&
               localPoint.y >= rect.yMin - hitPadding && localPoint.y <= rect.yMax + hitPadding;
    }
}
