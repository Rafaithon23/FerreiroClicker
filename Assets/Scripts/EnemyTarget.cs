using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class EnemyTarget : MonoBehaviour
{
    private int currentHp;
    private Image image;
    private Button button;
    private HordeManager horde;
    private RectTransform rectTransform;
    private Vector2 basePosition;
    private bool dying;
    private Coroutine shakeRoutine;

    public void Setup(HordeManager owner, int hp, Image img, Button btn, RectTransform rt, Vector2 targetPos, float spawnDuration, float startDelay)
    {
        horde = owner;
        currentHp = hp;
        image = img;
        button = btn;
        rectTransform = rt;
        basePosition = targetPos;
        button.onClick.AddListener(HandleClicked);

        StartCoroutine(SpawnRoutine(spawnDuration, startDelay));
    }

    /// <summary>
    /// Entrada "PUF": nasce direto na posicao final, com escala
    /// 0% -> 70% -> 110% -> 100% + fade, em vez de deslizar de fora da
    /// tela. Mais barato visualmente e da aquele efeito de "a horda
    /// estourou na tela".
    /// </summary>
    private IEnumerator SpawnRoutine(float duration, float delay)
    {
        rectTransform.anchoredPosition = basePosition;
        rectTransform.localScale = Vector3.zero;
        SetAlpha(0f);

        if (delay > 0f) yield return new WaitForSeconds(delay);

        float t = 0f;
        float d = Mathf.Max(0.05f, duration);
        while (t < d)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / d);
            rectTransform.localScale = Vector3.one * EvaluatePunchScale(p);
            SetAlpha(Mathf.Clamp01(p / 0.6f));
            yield return null;
        }

        rectTransform.localScale = Vector3.one;
        SetAlpha(1f);
    }

    /// <summary>Curva simples (sem AnimationCurve, feita em runtime): 0 -> 0.7 -> 1.1 -> 1.0.</summary>
    private static float EvaluatePunchScale(float p)
    {
        if (p < 0.45f) return Mathf.Lerp(0f, 0.7f, p / 0.45f);
        if (p < 0.75f) return Mathf.Lerp(0.7f, 1.1f, (p - 0.45f) / 0.30f);
        return Mathf.Lerp(1.1f, 1.0f, (p - 0.75f) / 0.25f);
    }

    private void HandleClicked()
    {
        if (dying) return;
        if (horde == null || !horde.IsHordeActive) return;

        if (HammerFollowMouse.Instance == null || !HammerFollowMouse.Instance.IsHolding)
        {
            AnvilClicker.Instance?.SpawnFloatingText("Pegue o martelo!");
            return;
        }

        currentHp--;

        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine());
        StartCoroutine(FlashRoutine());

        AnvilClicker.Instance?.SpawnFloatingText("-1");

        if (currentHp <= 0)
        {
            dying = true;
            button.interactable = false;
            horde.OnEnemyDefeated(this);
            StartCoroutine(DeathRoutine());
        }
    }

    /// <summary>Tremidinha rapida na posicao ao levar um golpe (nao-letal ou nao).</summary>
    private IEnumerator ShakeRoutine()
    {
        float duration = 0.14f;
        float magnitude = 9f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float falloff = 1f - (t / duration);
            float offsetX = Random.Range(-magnitude, magnitude) * falloff;
            float offsetY = Random.Range(-magnitude, magnitude) * falloff;
            rectTransform.anchoredPosition = basePosition + new Vector2(offsetX, offsetY);
            yield return null;
        }

        rectTransform.anchoredPosition = basePosition;
    }

    private IEnumerator FlashRoutine()
    {
        if (image == null) yield break;
        Color original = image.color;
        Color flash = original;
        flash.r = 1f; flash.g = 1f; flash.b = 1f;
        image.color = flash;
        yield return new WaitForSeconds(0.08f);
        if (image != null) image.color = original;
    }

    /// <summary>Encolhe + desaparece ao morrer, em vez de sumir instantaneo.</summary>
    private IEnumerator DeathRoutine()
    {
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        rectTransform.anchoredPosition = basePosition;

        float duration = 0.22f;
        float t = 0f;
        Vector3 startScale = rectTransform.localScale;

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            rectTransform.localScale = Vector3.Lerp(startScale, Vector3.one * 0.15f, p);
            SetAlpha(1f - p);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void SetAlpha(float a)
    {
        if (image == null) return;
        Color c = image.color;
        c.a = a;
        image.color = c;
    }
}
