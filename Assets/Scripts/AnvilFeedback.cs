using UnityEngine;

/// <summary>Feedback visual e sonoro de golpes confirmados. O collider permanece imóvel.</summary>
public class AnvilFeedback : MonoBehaviour
{
    private const int SparkCount = 48;
    private readonly Spark[] sparks = new Spark[SparkCount];
    private readonly System.Random variation = new System.Random(1973);
    private SpriteRenderer original;
    private SpriteRenderer visual;
    private AudioSource audioSource;
    private AudioClip strike;
    private Texture2D sparkTexture;
    private Sprite sparkSprite;
    private Material sparkMaterial;
    private Color restColor;
    private bool originalWasEnabled;
    private float reaction;
    private float strength;
    private int nextSpark;

    private struct Spark
    {
        public SpriteRenderer renderer;
        public Vector3 position;
        public Vector2 velocity;
        public float remaining;
        public float lifetime;
        public float size;
    }

    private void Awake()
    {
        original = GetComponent<SpriteRenderer>();
        if (original == null) { enabled = false; return; }
        originalWasEnabled = original.enabled;
        restColor = original.color;
        GameObject art = new GameObject("AnvilReaction");
        art.transform.SetParent(transform, false);
        visual = art.AddComponent<SpriteRenderer>();
        visual.sprite = original.sprite;
        visual.sharedMaterial = original.sharedMaterial;
        visual.sortingLayerID = original.sortingLayerID;
        visual.sortingOrder = original.sortingOrder;
        visual.flipX = original.flipX;
        visual.flipY = original.flipY;
        visual.color = restColor;
        original.enabled = false;

        sparkTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        sparkTexture.name = "ForgeSparkPixels";
        sparkTexture.filterMode = FilterMode.Point;
        sparkTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        sparkTexture.Apply();
        sparkSprite = Sprite.Create(sparkTexture, new Rect(0, 0, 2, 2), Vector2.one * .5f, 16f);
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null) sparkMaterial = new Material(shader);
        for (int i = 0; i < SparkCount; i++)
        {
            GameObject particle = new GameObject("ForgeSpark");
            particle.transform.SetParent(transform, false);
            SpriteRenderer renderer = particle.AddComponent<SpriteRenderer>();
            renderer.sprite = sparkSprite;
            if (sparkMaterial != null) renderer.sharedMaterial = sparkMaterial;
            renderer.sortingLayerID = original.sortingLayerID;
            renderer.sortingOrder = original.sortingOrder + 2;
            renderer.enabled = false;
            sparks[i].renderer = renderer;
        }
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = .22f;
        strike = CreateMetalStrike();
    }

    public void PlayHit(Vector3 point, bool critical)
    {
        if (!isActiveAndEnabled || visual == null) return;
        point.z = transform.position.z;
        reaction = .18f;
        strength = critical ? 1.55f : 1f;
        int count = critical ? 18 : 10;
        for (int i = 0; i < count; i++)
        {
            int index = nextSpark++ % SparkCount;
            Spark spark = sparks[index];
            float angle = Mathf.Lerp(20f, 160f, (float)variation.NextDouble()) * Mathf.Deg2Rad;
            float speed = Mathf.Lerp(1.4f, 3.7f, (float)variation.NextDouble()) * strength;
            spark.position = point;
            spark.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            spark.lifetime = spark.remaining = Mathf.Lerp(.18f, .38f, (float)variation.NextDouble());
            spark.size = Mathf.Lerp(.28f, .62f, (float)variation.NextDouble()) * strength;
            spark.renderer.color = critical ? new Color(1f, .94f, .55f) : new Color(1f, .63f, .16f);
            spark.renderer.enabled = true;
            spark.renderer.transform.position = point;
            sparks[index] = spark;
        }
        audioSource.pitch = (critical ? 1.08f : .95f) + (float)variation.NextDouble() * .1f;
        audioSource.PlayOneShot(strike, critical ? 1f : .8f);
    }

    private void Update()
    {
        if (visual == null) return;
        reaction = Mathf.Max(0f, reaction - Time.deltaTime);
        float pulse = Mathf.Sin((reaction / .18f) * Mathf.PI) * strength;
        visual.transform.localScale = new Vector3(1f + pulse * .018f, 1f - pulse * .025f, 1f);
        visual.transform.localPosition = Vector3.down * (pulse * .025f);
        visual.color = Color.Lerp(restColor, new Color(1f, .85f, .58f, restColor.a), reaction / .18f * .42f);
        for (int i = 0; i < SparkCount; i++)
        {
            Spark spark = sparks[i];
            if (spark.remaining <= 0f) continue;
            spark.remaining = Mathf.Max(0f, spark.remaining - Time.deltaTime);
            spark.velocity += Vector2.down * (8f * Time.deltaTime);
            spark.position += (Vector3)spark.velocity * Time.deltaTime;
            spark.renderer.transform.position = spark.position;
            spark.renderer.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(spark.velocity.y, spark.velocity.x) * Mathf.Rad2Deg);
            float scale = spark.size * Mathf.Clamp01(spark.remaining / spark.lifetime);
            // Counteract the parent's scale so sparks keep a consistent world size.
            Vector3 parentScale = transform.lossyScale;
            spark.renderer.transform.localScale = new Vector3(scale / Mathf.Max(.001f, Mathf.Abs(parentScale.x)), scale * .5f / Mathf.Max(.001f, Mathf.Abs(parentScale.y)), 1);
            Color color = spark.renderer.color;
            color.a = Mathf.Clamp01(spark.remaining / .1f);
            spark.renderer.color = color;
            spark.renderer.enabled = spark.remaining > 0f;
            sparks[i] = spark;
        }
    }

    private static AudioClip CreateMetalStrike()
    {
        const int rate = 22050;
        const float duration = .28f;
        float[] samples = new float[Mathf.CeilToInt(rate * duration)];
        System.Random noise = new System.Random(91);
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / rate;
            float attack = Mathf.Min(1f, t / .002f);
            float tone = Mathf.Sin(2f * Mathf.PI * 780f * t) * Mathf.Exp(-t * 22f) * .36f
                + Mathf.Sin(2f * Mathf.PI * 1637f * t) * Mathf.Exp(-t * 28f) * .22f
                + Mathf.Sin(2f * Mathf.PI * 2911f * t) * Mathf.Exp(-t * 36f) * .12f;
            float transient = ((float)noise.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 130f) * .18f;
            samples[i] = (tone + transient) * attack;
        }
        AudioClip clip = AudioClip.Create("BlacksmithMetalStrike", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDisable()
    {
        if (original != null) original.enabled = originalWasEnabled;
        if (visual != null) visual.enabled = false;
        if (audioSource != null) audioSource.Stop();
        for (int i = 0; i < SparkCount; i++)
        {
            if (sparks[i].renderer != null) sparks[i].renderer.enabled = false;
            sparks[i].remaining = 0f;
        }
        reaction = 0;
    }

    private void OnEnable()
    {
        if (visual == null) return;
        original.enabled = false;
        visual.enabled = originalWasEnabled;
    }

    private void OnDestroy()
    {
        if (original != null) original.enabled = originalWasEnabled;
        if (visual != null) Destroy(visual.gameObject);
        foreach (Spark spark in sparks) if (spark.renderer != null) Destroy(spark.renderer.gameObject);
        if (audioSource != null) Destroy(audioSource);
        if (strike != null) Destroy(strike);
        if (sparkSprite != null) Destroy(sparkSprite);
        if (sparkTexture != null) Destroy(sparkTexture);
        if (sparkMaterial != null) Destroy(sparkMaterial);
    }
}
