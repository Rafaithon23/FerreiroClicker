using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Trilha persistente com volumes por contexto e sobreposição nos loops.</summary>
public sealed class ForgeMusic : MonoBehaviour
{
    public static ForgeMusic Instance { get; private set; }

    [Range(0f, 1f)] public float menuVolume = .32f;
    [Range(0f, 1f)] public float gameVolume = .13f;
    [Range(0f, 1f)] public float battleVolume = .23f;
    [Min(.1f)] public float battleTransitionSeconds = 2.8f;
    [Min(.1f)] public float volumeTransitionSeconds = 1.8f;
    [Min(.1f)] public float loopOverlapSeconds = 2.5f;

    private MusicLoop daybreak;
    private MusicLoop valor;
    private float battleMix;
    private float daybreakLevel;
    private float introduction;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() { Instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null) new GameObject("ForgeMusic").AddComponent<ForgeMusic>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        daybreak = new MusicLoop(transform, "Daybreak", Resources.Load<AudioClip>("Music/Forge of Daybreak"), loopOverlapSeconds, .900f, 118.45f);
        valor = new MusicLoop(transform, "Valor", Resources.Load<AudioClip>("Music/Forge of Valor"), loopOverlapSeconds, .906f, 119.45f);
        daybreakLevel = SceneManager.GetActiveScene().name == "MainMenu" ? menuVolume : gameVolume;
    }

    private void Update()
    {
        bool menu = SceneManager.GetActiveScene().name == "MainMenu";
        HordeManager horde = HordeManager.Instance;
        bool combat = !menu && horde != null &&
            (GameManager.Instance == null || !GameManager.Instance.CampaignCompleted) &&
            (horde.CurrentState == HordeManager.State.Darkening ||
             horde.CurrentState == HordeManager.State.Warning ||
             horde.CurrentState == HordeManager.State.Active);
        if (!valor.Available) combat = false;

        float dt = Time.unscaledDeltaTime;
        introduction = Mathf.MoveTowards(introduction, 1f, dt / 2f);
        battleMix = Mathf.MoveTowards(battleMix, combat ? 1f : 0f, dt / Mathf.Max(.1f, battleTransitionSeconds));
        daybreakLevel = Mathf.MoveTowards(daybreakLevel, menu ? menuVolume : gameVolume,
            dt * Mathf.Max(menuVolume, gameVolume) / Mathf.Max(.1f, volumeTransitionSeconds));
        // Curva suave nas extremidades, sem somar os dois volumes máximos no meio.
        float blend = Mathf.SmoothStep(0f, 1f, battleMix);
        daybreak.Tick(daybreakLevel * (1f - blend) * introduction, true);
        valor.Tick(battleVolume * blend * introduction, combat || battleMix > 0f);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private sealed class MusicLoop
    {
        private readonly AudioSource[] sources = new AudioSource[2];
        private readonly float start;
        private readonly float end;
        private readonly float overlap;
        private readonly float gain;
        private readonly AudioClip clip;
        private int current;
        private bool crossfading;
        private bool started;
        private bool paused;
        public bool Available { get; }

        public MusicLoop(Transform parent, string name, AudioClip clip, float fade, float normalization, float loopEnd)
        {
            Available = clip != null;
            if (!Available) { Debug.LogWarning("Música não encontrada: " + name); return; }
            this.clip = clip;
            start = 0f;
            end = Mathf.Min(clip.length, loopEnd);
            // Valores medidos antes da otimização: não percorre milhões de
            // amostras nem descomprime as duas músicas na abertura.
            gain = normalization;
            overlap = Mathf.Min(Mathf.Max(.1f, fade), (end - start) / 4f);
            for (int i = 0; i < sources.Length; i++)
            {
                GameObject child = new GameObject(name + " " + (i + 1));
                child.transform.SetParent(parent, false);
                sources[i] = child.AddComponent<AudioSource>();
                sources[i].clip = clip;
                sources[i].playOnAwake = false;
                sources[i].loop = false;
                sources[i].spatialBlend = 0f;
                sources[i].volume = 0f;
                sources[i].priority = 64;
            }
            Debug.Log("ForgeMusic " + name + ": " + clip.length.ToString("F1") +
                "s, trecho " + start.ToString("F2") + "–" + end.ToString("F2") +
                "s, ganho " + gain.ToString("F2"));
        }

        public void Tick(float volume, bool running)
        {
            if (!Available || clip.loadState == AudioDataLoadState.Failed) return;
            if (!running)
            {
                foreach (AudioSource source in sources) { source.volume = 0f; if (!paused) source.Pause(); }
                paused = true;
                return;
            }
            if (!started)
            {
                sources[current].time = start;
                sources[current].Play();
                started = true;
                paused = false;
                return; // Deixa o stream iniciar antes de verificar o fim do loop.
            }
            else if (paused)
            {
                sources[current].UnPause();
                if (crossfading) sources[1 - current].UnPause();
                paused = false;
            }

            AudioSource outgoing = sources[current];
            AudioSource incoming = sources[1 - current];
            if (!crossfading && (outgoing.time >= end - overlap || !outgoing.isPlaying))
            {
                incoming.time = start;
                incoming.volume = 0f;
                incoming.Play();
                crossfading = true;
            }
            float mix = crossfading ? Mathf.Clamp01((incoming.time - start) / overlap) : 0f;
            mix = Mathf.SmoothStep(0f, 1f, mix);
            outgoing.volume = Mathf.Clamp01(volume * gain * (1f - mix));
            incoming.volume = Mathf.Clamp01(volume * gain * mix);
            if (crossfading && mix >= 1f)
            {
                outgoing.Stop();
                current = 1 - current;
                crossfading = false;
            }
        }

    }
}
