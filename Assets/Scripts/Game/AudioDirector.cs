using System.Collections.Generic;
using MergeResto.Game.Views;
using CrazyGames;
using MergeResto.Sim;
using UnityEngine;

namespace MergeResto.Game
{
    public enum Sfx
    {
        Click,
        Tap,
        Coin,
        Sale,
        Upgrade,
        Manager,
        Merge,
        Spawn,
        Renovate,
        Celebrate,
        Claim,
        Deny,
        Grumpy,
        Whoosh,
        Blip,
    }

    /// <summary>
    /// All game sound: a looping background theme and one-shot effects for shop, board and UI
    /// events, with light pitch variation and a per-sound rate limit so busy moments stay pleasant.
    /// Sound and music can be muted separately; the choice is remembered.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        [System.Serializable]
        public class Entry
        {
            public Sfx sfx;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 0.8f;
            [Tooltip("Random pitch spread, e.g. 0.06 = ±6%.")]
            public float pitchSpread = 0.05f;
            [Tooltip("Shortest gap between two plays of this sound.")]
            public float minInterval = 0.05f;
        }

        [Tooltip("Background tracks, played in turn with a slow crossfade.")]
        [SerializeField] AudioClip[] playlist;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.26f;
        [SerializeField] float crossfadeSeconds = 4f;
        [SerializeField] Entry[] entries;
        [SerializeField] CustomerCrowd crowd;
        [SerializeField] int voices = 10;

        const string SoundKey = "audio_sound";
        const string MusicKey = "audio_music";

        readonly Dictionary<Sfx, Entry> _map = new();
        readonly Dictionary<Sfx, float> _last = new();
        AudioSource[] _pool;
        AudioSource[] _music;
        int _musicFront;
        int _track = -1;
        float _musicFade = 1f;
        int _next;
        ShopSim _sim;
        float _coinStreak;
        int _coinCount;

        // CrazyGames' own mute switch (site settings) silences everything on top of ours.
        bool _platformMuted;

        public bool SoundOn { get; private set; } = true;
        public bool MusicOn { get; private set; } = true;

        void Awake()
        {
            Instance = this;
            foreach (var e in entries)
                if (e.clip != null) _map[e.sfx] = e;
            _pool = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                _pool[i] = gameObject.AddComponent<AudioSource>();
                _pool[i].playOnAwake = false;
                _pool[i].spatialBlend = 0f;
            }
            _music = new AudioSource[2];
            for (int i = 0; i < 2; i++)
            {
                _music[i] = gameObject.AddComponent<AudioSource>();
                _music[i].playOnAwake = false;
                _music[i].loop = false;
                _music[i].volume = 0f;
            }
            SoundOn = PlayerPrefs.GetInt(SoundKey, 1) == 1;
            MusicOn = PlayerPrefs.GetInt(MusicKey, 1) == 1;
        }

        void Start()
        {
            NextTrack();
            if (CrazySDK.IsAvailable && CrazySDK.IsInitialized)
            {
                _platformMuted = CrazySDK.Game.Settings.muteAudio;
                CrazySDK.Game.AddSettingsChangeListener(OnPlatformSettings);
            }
            _sim = GameController.Instance.Sim;
            _sim.UpgradePurchased += OnUpgrade;
            _sim.ManagerHired += OnManager;
            _sim.GoalClaimed += OnGoal;
            GameController.Instance.StationTapped += OnStationTapped;
            if (_sim.Board != null)
            {
                _sim.Board.ItemSpawned += OnSpawn;
                _sim.Board.ItemsMerged += OnMerged;
                _sim.Board.RenovationDone += OnRenovated;
            }
            if (crowd != null)
            {
                crowd.CustomerPaid += OnPaid;
                crowd.CustomerGaveUp += OnGaveUp;
            }
        }

        void OnPlatformSettings(GameSettings settings) => _platformMuted = settings.muteAudio;

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (CrazySDK.IsAvailable && CrazySDK.IsInitialized)
                CrazySDK.Game.RemoveSettingsChangeListener(OnPlatformSettings);
            if (_sim == null)
                return;
            _sim.UpgradePurchased -= OnUpgrade;
            _sim.ManagerHired -= OnManager;
            _sim.GoalClaimed -= OnGoal;
            if (GameController.Instance != null)
                GameController.Instance.StationTapped -= OnStationTapped;
            if (_sim.Board != null)
            {
                _sim.Board.ItemSpawned -= OnSpawn;
                _sim.Board.ItemsMerged -= OnMerged;
                _sim.Board.RenovationDone -= OnRenovated;
            }
            if (crowd != null)
            {
                crowd.CustomerPaid -= OnPaid;
                crowd.CustomerGaveUp -= OnGaveUp;
            }
        }

        void Update()
        {
            UpdateMusic(Time.unscaledDeltaTime);
            _coinStreak = Mathf.Max(0f, _coinStreak - Time.unscaledDeltaTime);
            if (_coinStreak <= 0f)
                _coinCount = 0;
        }

        // ------------------------------------------------------------------ Music

        void NextTrack()
        {
            if (playlist == null || playlist.Length == 0)
                return;
            _track = (_track + 1) % playlist.Length;
            _musicFront = 1 - _musicFront;
            var source = _music[_musicFront];
            source.clip = playlist[_track];
            source.volume = 0f;
            source.Play();
            _musicFade = 0f;
        }

        void UpdateMusic(float dt)
        {
            if (playlist == null || playlist.Length == 0)
                return;
            var front = _music[_musicFront];
            var back = _music[1 - _musicFront];
            // Start the next track a crossfade before this one ends.
            if (front.clip != null && front.isPlaying && front.time >= front.clip.length - crossfadeSeconds)
            {
                NextTrack();
                front = _music[_musicFront];
                back = _music[1 - _musicFront];
            }
            _musicFade = Mathf.Min(1f, _musicFade + dt / crossfadeSeconds);
            bool musicOn = MusicOn && !_platformMuted;
            float level = musicOn ? musicVolume : 0f;
            // Muting fades quickly; the crossfade itself is slow and smooth.
            float mute = musicOn ? 1f : 0f;
            front.volume = Mathf.MoveTowards(front.volume, level * Mathf.SmoothStep(0f, 1f, _musicFade), dt * (mute > 0f ? 1f : 2f));
            back.volume = Mathf.MoveTowards(back.volume, musicOn ? level * (1f - Mathf.SmoothStep(0f, 1f, _musicFade)) : 0f, dt * 2f);
            if (back.isPlaying && back.volume <= 0.001f && _musicFade >= 1f)
                back.Stop();
        }

        // ------------------------------------------------------------------ Playing

        public static void Play(Sfx sfx, float pitch = 1f, float volume = 1f)
        {
            if (Instance != null)
                Instance.PlayInternal(sfx, pitch, volume);
        }

        void PlayInternal(Sfx sfx, float pitch, float volume)
        {
            if (!SoundOn || _platformMuted || !_map.TryGetValue(sfx, out var e))
                return;
            float now = Time.unscaledTime;
            if (_last.TryGetValue(sfx, out float last) && now - last < e.minInterval)
                return;
            _last[sfx] = now;
            var source = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            source.clip = e.clip;
            source.volume = e.volume * volume;
            source.pitch = pitch * (1f + Random.Range(-e.pitchSpread, e.pitchSpread));
            source.Play();
        }

        /// <summary>A coin landing on the counter; quick runs of coins climb in pitch.</summary>
        public static void CoinLanded()
        {
            if (Instance == null)
                return;
            var d = Instance;
            d._coinStreak = 0.35f;
            d._coinCount++;
            Play(Sfx.Coin, 1f + Mathf.Min(d._coinCount, 12) * 0.03f, 0.7f);
        }

        public void SetSound(bool on)
        {
            SoundOn = on;
            PlayerPrefs.SetInt(SoundKey, on ? 1 : 0);
        }

        public void SetMusic(bool on)
        {
            MusicOn = on;
            PlayerPrefs.SetInt(MusicKey, on ? 1 : 0);
        }

        // ------------------------------------------------------------------ Events

        void OnStationTapped(int station) => Play(Sfx.Tap);
        void OnUpgrade(int station, UpgradeTrack track) => Play(Sfx.Upgrade);
        void OnManager(int station) => Play(Sfx.Manager);
        void OnGoal(int goal) => Play(Sfx.Claim);
        void OnSpawn(int from, int to, BoardItem item) => Play(Sfx.Spawn);
        void OnMerged(int cell, BoardItem item) => Play(Sfx.Merge, 1f + item.Tier * 0.06f);
        void OnRenovated(int renovation) => Play(Sfx.Renovate);
        void OnPaid(Vector3 position, double coins) => Play(Sfx.Sale, 1f, 0.55f);
        void OnGaveUp(Vector3 position) => Play(Sfx.Grumpy, 0.9f, 0.6f);
    }
}
