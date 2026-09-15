using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Faz o martelo seguir o cursor do mouse, virando ele mesmo o "cursor" do jogo.
/// Esconde o cursor padrao do sistema enquanto este objeto estiver ativo.
/// Usa o pacote novo (Input System), que e o que este projeto tem ativado.
/// Coloque este script no GameObject "Hammer".
/// </summary>
public class HammerFollowMouse : MonoBehaviour
{
    [Tooltip("Distancia da camera usada pra converter a posicao do mouse (tela) em posicao no mundo. Nao precisa mexer.")]
    public float zDistanceFromCamera = 10f;

    [Tooltip("Ajuste fino de onde a 'ponta' do martelo fica em relacao ao cursor real. " +
             "Se o pivo do sprite estiver no topo do cabo, um offset pequeno pra baixo/direita costuma ficar melhor.")]
    public Vector2 offset = Vector2.zero;

    [Tooltip("Se marcado, esconde o cursor do mouse do Windows enquanto o jogo roda.")]
    public bool hideSystemCursor = true;

    private Camera cam;

    private void Start()
    {
        cam = Camera.main;

        if (hideSystemCursor)
        {
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (cam == null || Mouse.current == null) return;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 screenPoint = new Vector3(mouseScreenPos.x, mouseScreenPos.y, zDistanceFromCamera);

        Vector3 worldPos = cam.ScreenToWorldPoint(screenPoint);

        // Mantem o Z original do martelo (importante pro Order in Layer/profundidade).
        transform.position = new Vector3(worldPos.x + offset.x, worldPos.y + offset.y, transform.position.z);
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
