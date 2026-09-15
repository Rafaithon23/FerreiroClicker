using System.Collections;
using UnityEngine;

/// <summary>
/// Detecta o clique/toque na bigorna, manda o GameManager somar ouro,
/// anima o martelo batendo e mostra o texto "+N" flutuante.
/// Coloque este script no GameObject da Bigorna (precisa ter um
/// Collider2D marcado, ex: BoxCollider2D ou PolygonCollider2D).
/// </summary>
public class AnvilClicker : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Transform do martelo, pra animar a batida")]
    public Transform hammer;

    [Tooltip("Prefab do FloatingText (+1, +2...)")]
    public GameObject floatingTextPrefab;

    [Tooltip("Onde o texto flutuante nasce (opcional, usa a propria bigorna se vazio)")]
    public Transform floatingTextSpawnPoint;

    [Header("Animacao")]
    public float swingAngle = 55f;
    public float swingDuration = 0.12f;
    public float punchScale = 0.08f;

    private bool isSwinging;
    private Vector3 anvilBaseScale;
    private Quaternion hammerRestRotation;

    private void Start()
    {
        anvilBaseScale = transform.localScale;

        if (hammer != null)
        {
            hammerRestRotation = hammer.localRotation;
        }
    }

    // OnMouseDown funciona tanto pra clique de mouse (Editor/PC)
    // quanto pra toque na tela em builds mobile.
    private void OnMouseDown()
    {
        HandleHit();
    }

    private void HandleHit()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("GameManager nao encontrado na cena.");
            return;
        }

        int amount = GameManager.Instance.GoldPerClick;
        GameManager.Instance.AddGold(amount);

        SpawnFloatingText("+" + amount);
        PunchAnvil();

        if (hammer != null && !isSwinging)
        {
            StartCoroutine(SwingHammer());
        }
    }

    private void SpawnFloatingText(string text)
    {
        if (floatingTextPrefab == null) return;

        Vector3 spawnPos = floatingTextSpawnPoint != null
            ? floatingTextSpawnPoint.position
            : transform.position + Vector3.up * 0.5f;

        GameObject go = Instantiate(floatingTextPrefab, spawnPos, Quaternion.identity);
        FloatingText ft = go.GetComponent<FloatingText>();
        if (ft != null)
        {
            ft.Setup(text);
        }
    }

    private void PunchAnvil()
    {
        StopCoroutine(nameof(PunchScaleRoutine));
        StartCoroutine(PunchScaleRoutine());
    }

    private IEnumerator PunchScaleRoutine()
    {
        Vector3 target = anvilBaseScale * (1f - punchScale);
        float half = 0.05f;
        float t = 0f;

        while (t < half)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(anvilBaseScale, target, t / half);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(target, anvilBaseScale, t / half);
            yield return null;
        }

        transform.localScale = anvilBaseScale;
    }

    private IEnumerator SwingHammer()
    {
        isSwinging = true;

        Quaternion down = hammerRestRotation * Quaternion.Euler(0f, 0f, -swingAngle);
        float t = 0f;

        while (t < swingDuration)
        {
            t += Time.deltaTime;
            hammer.localRotation = Quaternion.Lerp(hammerRestRotation, down, t / swingDuration);
            yield return null;
        }

        t = 0f;
        while (t < swingDuration)
        {
            t += Time.deltaTime;
            hammer.localRotation = Quaternion.Lerp(down, hammerRestRotation, t / swingDuration);
            yield return null;
        }

        hammer.localRotation = hammerRestRotation;
        isSwinging = false;
    }
}
