using System.Collections;
using UnityEngine;

/// <summary>
/// Texto que sobe e desaparece, usado pro popup "+1" quando o jogador
/// clica na bigorna. Coloque este script no prefab FloatingText (que tem
/// um componente TextMesh).
/// </summary>
[RequireComponent(typeof(TextMesh))]
public class FloatingText : MonoBehaviour
{
    public float floatSpeed = 1.2f;
    public float lifetime = 0.8f;

    private TextMesh textMesh;
    private Color startColor;

    private void Awake()
    {
        textMesh = GetComponent<TextMesh>();
        startColor = textMesh.color;
    }

    /// <summary>Chamado pelo AnvilClicker logo depois de instanciar o prefab.</summary>
    public void Setup(string text)
    {
        textMesh.text = text;
        StartCoroutine(FloatAndFade());
    }

    private IEnumerator FloatAndFade()
    {
        float elapsed = 0f;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;

            transform.position += Vector3.up * floatSpeed * Time.deltaTime;

            Color c = startColor;
            c.a = Mathf.Lerp(1f, 0f, t);
            textMesh.color = c;

            yield return null;
        }

        Destroy(gameObject);
    }
}
