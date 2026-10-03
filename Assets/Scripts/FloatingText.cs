using System.Collections;
using UnityEngine;

[RequireComponent(typeof(TextMesh))]
public class FloatingText : MonoBehaviour
{
    public float floatSpeed = 1.2f;
    public float lifetime = .8f;
    private TextMesh textMesh;
    private Color startColor;
    private Vector3 restScale;
    private TextMesh shadow;

    private void Awake()
    {
        textMesh = GetComponent<TextMesh>();
        startColor = textMesh.color;
        restScale = transform.localScale;
    }

    public void Setup(string text, bool critical = false)
    {
        textMesh.text = text;
        textMesh.richText = true;
        textMesh.fontStyle = FontStyle.Bold;
        textMesh.anchor = TextAnchor.MiddleCenter;
        startColor = critical ? new Color(1f, .84f, .30f) : new Color(1f, .97f, .86f);
        textMesh.color = startColor;
        restScale *= critical ? 1.4f : 1.1f;
        if (critical) lifetime = Mathf.Max(lifetime, 1f);
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sortingOrder = 100;
        GameObject outline = new GameObject("PopupShadow");
        outline.transform.SetParent(transform, false);
        outline.transform.localPosition = new Vector3(.025f, -.025f, .01f);
        shadow = outline.AddComponent<TextMesh>();
        shadow.text = text;
        shadow.font = textMesh.font;
        shadow.fontSize = textMesh.fontSize;
        shadow.fontStyle = textMesh.fontStyle;
        shadow.characterSize = textMesh.characterSize;
        shadow.anchor = textMesh.anchor;
        shadow.alignment = textMesh.alignment;
        shadow.richText = true;
        shadow.color = new Color(.04f, .015f, .005f, 1f);
        MeshRenderer shadowRenderer = outline.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            shadowRenderer.sharedMaterial = renderer.sharedMaterial;
            shadowRenderer.sortingLayerID = renderer.sortingLayerID;
            shadowRenderer.sortingOrder = renderer.sortingOrder - 1;
        }
        StartCoroutine(FloatAndFade());
    }

    private IEnumerator FloatAndFade()
    {
        float elapsed = 0f;
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);
            transform.position += Vector3.up * floatSpeed * Time.deltaTime;
            transform.localScale = restScale * (1f + .16f * Mathf.Exp(-elapsed * 18f));
            Color color = startColor;
            color.a = 1f - Mathf.InverseLerp(.35f, 1f, t);
            textMesh.color = color;
            if (shadow != null) shadow.color = new Color(.04f, .015f, .005f, color.a);
            yield return null;
        }
        Destroy(gameObject);
    }
}
