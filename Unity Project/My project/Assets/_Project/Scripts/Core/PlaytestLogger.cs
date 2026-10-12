using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LanternKeeper
{
    /// <summary>
    /// Playtest telemetry. Each session (one run of the level, from the first frame of play to
    /// a win, a quit or a restart) writes two CSV files to
    /// <c>Application.persistentDataPath/playtests/</c>:
    /// <c>&lt;session&gt;_summary.csv</c> (one row of totals) and <c>&lt;session&gt;_events.csv</c>
    /// (a timestamped log). docs/PLAYTEST_DATA.md describes every column.
    /// The session name is a tester id generated once per launch of the game
    /// (T-yyyyMMdd-XXXX); a restart in the same launch keeps the id and adds a number
    /// (T-20261009-K7QD, T-20261009-K7QD_2, ...), so nothing is ever overwritten.
    /// Setup: one per scene on any object. Nothing to wire: it listens to GameEvents,
    /// GameManager, the player's lantern, and every ghost, plate and gate in the scene.
    /// It works in release builds; it does not depend on the debug overlay.
    /// Times are GameManager.PlayTime (seconds played, not counting the title, pause or win
    /// screens). Section ids must start with S1..S5 to get their own summary columns.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)] // after GameManager (-100) has cleared GameEvents, before everything else
    public class PlaytestLogger : MonoBehaviour
    {
        public const int SectionCount = 5;
        private const string FolderName = "playtests";

        private static string s_testerId = string.Empty;

        public static PlaytestLogger Instance { get; private set; }

        /// <summary>Folder the CSV files are written to.</summary>
        public static string FolderPath => Path.Combine(Application.persistentDataPath, FolderName);

        /// <summary>The generated tester id for this launch of the game. Empty until the first session starts.</summary>
        public static string TesterId => s_testerId;

        [SerializeField, Min(0.5f), Tooltip("Seconds between flushes of the events file to disk.")]
        private float flushInterval = 5f;

        private readonly float[] _sectionTime = new float[SectionCount];
        private readonly int[] _sectionDeaths = new int[SectionCount];
        private readonly int[] _sectionHits = new int[SectionCount];
        private readonly List<string> _pendingRows = new List<string>(8);
        private readonly StringBuilder _row = new StringBuilder(128);

        private GameManager _game;
        private LanternController _lantern;
        private StreamWriter _events;
        private string _session;
        private string _summaryPath;
        private string _eventsPath;
        private DateTime _startedAt;
        private bool _started;
        private bool _ended;
        private bool _completed;
        private bool _ioFailed;
        private bool _dirty;
        private float _flushTimer;
        private float _lastPlayTime;
        private int _sectionIndex;
        private string _sectionId = string.Empty;
        private string _endReason = "in_progress";
        private int _flareCount;
        private float _flareSeconds;
        private float _flareStart = -1f;
        private int _candyThrown;
        private int _candlePickups;
        private int _chases;
        private int _pauses;
        private int _deaths;
        private int _hits;

        /// <summary>True once the session's files have been opened (the game left the title screen).</summary>
        public bool SessionStarted => _started;
        /// <summary>File name prefix of this session: the tester id, plus a number after a restart.</summary>
        public string Session => _session;
        public string SummaryPath => _summaryPath;
        public string EventsPath => _eventsPath;

        /// <summary>Opens the playtest folder in the file browser (creates it if needed).</summary>
        public static void OpenFolder()
        {
            try
            {
                Directory.CreateDirectory(FolderPath);
                Application.OpenURL(new Uri(FolderPath).AbsoluteUri); // file:///C:/Users/...
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlaytestLogger] Could not open {FolderPath}: {e.Message}");
            }
        }

        /// <summary>
        /// Writes the summary now and flushes the events file. Safe to call at any time; the
        /// summary is simply rewritten with the latest numbers.
        /// </summary>
        public void SaveNow()
        {
            if (!_started || _ioFailed) return;
            AccumulateTime();
            try
            {
                if (_events != null) _events.Flush();
                _dirty = false;
                File.WriteAllText(_summaryPath, BuildSummary(), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                FailIo(e);
            }
        }

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PlaytestLogger] Duplicate instance destroyed.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            if (Instance != this) return;
            GameEvents.SectionEntered += HandleSectionEntered;
            GameEvents.CheckpointReached += HandleCheckpointReached;
            GameEvents.PlayerRespawned += HandleRespawned;
            GameEvents.PlayerHit += HandlePlayerHit;
            GameEvents.CandyThrown += HandleCandyThrown;
            GameEvents.PickupCollected += HandlePickupCollected;
            GameEvents.TutorialShown += HandleTutorialShown;
            GameEvents.LevelCompleted += HandleLevelCompleted;
            GameEvents.QuitRequested += HandleQuitRequested;
            GhostAI.AnyStateChanged += HandleGhostStateChanged;
        }

        private void OnDisable()
        {
            if (Instance != this) return;
            GameEvents.SectionEntered -= HandleSectionEntered;
            GameEvents.CheckpointReached -= HandleCheckpointReached;
            GameEvents.PlayerRespawned -= HandleRespawned;
            GameEvents.PlayerHit -= HandlePlayerHit;
            GameEvents.CandyThrown -= HandleCandyThrown;
            GameEvents.PickupCollected -= HandlePickupCollected;
            GameEvents.TutorialShown -= HandleTutorialShown;
            GameEvents.LevelCompleted -= HandleLevelCompleted;
            GameEvents.QuitRequested -= HandleQuitRequested;
            GhostAI.AnyStateChanged -= HandleGhostStateChanged;
        }

        private void Start()
        {
            if (Instance != this) return;

            _game = GameManager.Instance;
            if (_game != null) _game.OnModeChanged += HandleModeChanged;
            else Debug.LogWarning("[PlaytestLogger] No GameManager in the scene; times stay at 0 and the session starts at once.", this);

            if (CandleManager.Instance != null && CandleManager.Instance.TryGetComponent(out _lantern))
            {
                _lantern.OnFlareChanged += HandleFlareChanged;
            }

            IReadOnlyList<PressurePlate> plates = PressurePlate.All;
            for (int i = 0; i < plates.Count; i++) plates[i].OnPressedChanged += HandlePlateChanged;
            IReadOnlyList<Gate> gates = Gate.All;
            for (int i = 0; i < gates.Count; i++) gates[i].OnOpenChanged += HandleGateChanged;

            // A restart skips the title, so the game may already be playing.
            if (_game == null || _game.Mode == GameMode.Playing) BeginSession();
        }

        private void Update()
        {
            if (!_started || _ended) return;
            AccumulateTime();

            _flushTimer += Time.unscaledDeltaTime;
            if (_flushTimer < flushInterval) return;
            _flushTimer = 0f;
            if (_dirty) FlushEvents();
        }

        private void OnApplicationQuit() => EndSession("application_quit");

        private void OnDestroy()
        {
            if (Instance != this) return;
            // The scene is going away without a quit: Restart from the pause or win screen.
            EndSession("restart");
            if (_game != null) _game.OnModeChanged -= HandleModeChanged;
            if (_lantern != null) _lantern.OnFlareChanged -= HandleFlareChanged;
            Instance = null;
        }

        // ------------------------------------------------------------------ session

        private void BeginSession()
        {
            if (_started) return;
            _started = true;
            _startedAt = DateTime.Now;

            if (string.IsNullOrEmpty(s_testerId)) s_testerId = GenerateTesterId();

            try
            {
                Directory.CreateDirectory(FolderPath);
                _session = s_testerId;
                for (int n = 2; SessionExists(_session) && n < 1000; n++) _session = s_testerId + "_" + n.ToString(CultureInfo.InvariantCulture);
                _summaryPath = Path.Combine(FolderPath, _session + "_summary.csv");
                _eventsPath = Path.Combine(FolderPath, _session + "_events.csv");

                // FileShare.ReadWrite: the file can be opened in a spreadsheet while the game runs.
                var stream = new FileStream(_eventsPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                _events = new StreamWriter(stream, new UTF8Encoding(false));
                _events.WriteLine("time_s,section,event,detail");
            }
            catch (Exception e)
            {
                FailIo(e);
            }

            LogEvent("session_start", $"tester={s_testerId};version={Application.version};scene={SceneManager.GetActiveScene().name}");
            for (int i = 0; i < _pendingRows.Count; i++) WriteRow(_pendingRows[i]);
            _pendingRows.Clear();
            SaveNow();
            Debug.Log($"[PlaytestLogger] Session '{_session}' -> {FolderPath}");
        }

        private void EndSession(string reason)
        {
            if (!_started || _ended) return;
            if (_flareStart >= 0f) StopFlareClock();
            if (!_completed) _endReason = reason;
            // "quit" covers closing the window and Pause > Quit; a restart is logged as such.
            LogEvent(reason == "restart" ? "restart" : "quit", reason);
            SaveNow();
            _ended = true;
            if (_events == null) return;
            try
            {
                _events.Dispose();
            }
            catch (Exception e)
            {
                FailIo(e);
            }
            _events = null;
        }

        private static bool SessionExists(string session)
        {
            return File.Exists(Path.Combine(FolderPath, session + "_summary.csv"))
                || File.Exists(Path.Combine(FolderPath, session + "_events.csv"));
        }

        // T-<yyyyMMdd>-<4 characters>, e.g. T-20261009-K7QD. No vowels or look-alike characters.
        private static string GenerateTesterId()
        {
            const string alphabet = "BCDFGHJKMNPQRSTVWXYZ23456789";
            var random = new System.Random();
            var id = new StringBuilder("T-", 16);
            id.Append(DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)).Append('-');
            for (int i = 0; i < 4; i++) id.Append(alphabet[random.Next(alphabet.Length)]);
            return id.ToString();
        }

        private void AccumulateTime()
        {
            if (_game == null) return;
            float now = _game.PlayTime;
            _sectionTime[_sectionIndex] += Mathf.Max(0f, now - _lastPlayTime);
            _lastPlayTime = now;
        }

        // ------------------------------------------------------------------ event handlers

        private void HandleModeChanged(GameMode previous, GameMode next)
        {
            if (next == GameMode.Playing && !_started) BeginSession();

            switch (next)
            {
                case GameMode.Paused:
                    _pauses++;
                    LogEvent("paused", string.Empty);
                    SaveNow(); // the pause screen offers "Open playtest folder"
                    break;

                case GameMode.Extinguished:
                    _deaths++;
                    _sectionDeaths[_sectionIndex]++;
                    LogEvent("extinguished", CauseName(_game.LastExtinguishCause));
                    FlushEvents();
                    break;

                case GameMode.Playing:
                    if (previous == GameMode.Paused) LogEvent("resumed", string.Empty);
                    break;
            }
        }

        private void HandleSectionEntered(string sectionId)
        {
            AccumulateTime(); // close the previous section's clock first
            int index = SectionIndexOf(sectionId);
            if (index >= 0) _sectionIndex = index;
            _sectionId = sectionId ?? string.Empty;
            LogEvent("section_entered", _sectionId);
            FlushEvents();
        }

        private void HandleCheckpointReached(Transform checkpoint)
        {
            LogEvent("checkpoint_reached", checkpoint != null ? checkpoint.name : string.Empty);
        }

        private void HandleRespawned()
        {
            Checkpoint checkpoint = _game != null ? _game.CurrentCheckpoint : null;
            LogEvent("respawned", checkpoint != null ? checkpoint.name : "start");
        }

        private void HandlePlayerHit(Vector3 sourcePosition)
        {
            _hits++;
            _sectionHits[_sectionIndex]++;

            // The event carries the attacker's position; the closest ghost to it is the attacker.
            string ghostName = "unknown";
            float bestSqr = float.MaxValue;
            IReadOnlyList<GhostAI> ghosts = GhostAI.All;
            for (int i = 0; i < ghosts.Count; i++)
            {
                float sqr = (ghosts[i].transform.position - sourcePosition).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                ghostName = ghosts[i].name;
            }
            LogEvent("ghost_hit", ghostName);
        }

        private void HandleFlareChanged(bool flaring)
        {
            if (flaring)
            {
                _flareCount++;
                _flareStart = Time.time;
                LogEvent("flare_start", string.Empty);
            }
            else if (_flareStart >= 0f)
            {
                float seconds = StopFlareClock();
                LogEvent("flare_stop", "seconds=" + seconds.ToString("0.00", CultureInfo.InvariantCulture));
            }
        }

        private float StopFlareClock()
        {
            float seconds = Mathf.Max(0f, Time.time - _flareStart);
            _flareSeconds += seconds;
            _flareStart = -1f;
            return seconds;
        }

        private void HandleCandyThrown(Vector3 origin)
        {
            _candyThrown++;
            LogEvent("candy_thrown", string.Empty);
        }

        private void HandlePickupCollected(Pickup pickup)
        {
            bool candle = pickup is CandlePickup;
            if (candle) _candlePickups++;
            LogEvent(candle ? "candle_pickup" : "candy_pickup", pickup != null ? pickup.name : string.Empty);
        }

        private void HandlePlateChanged(PressurePlate plate, bool pressed)
        {
            LogEvent(pressed ? "plate_pressed" : "plate_released", plate != null ? plate.name : string.Empty);
        }

        private void HandleGateChanged(Gate gate, bool open)
        {
            LogEvent(open ? "gate_opened" : "gate_closed", gate != null ? gate.name : string.Empty);
        }

        private void HandleGhostStateChanged(GhostAI ghost, GhostState previous, GhostState next)
        {
            if (next == GhostState.Chase)
            {
                _chases++;
                LogEvent("ghost_chase", ghost.name);
            }
            else if (next == GhostState.Notice)
            {
                LogEvent("ghost_notice", ghost.name);
            }
        }

        private void HandleTutorialShown(string tutorialId) => LogEvent("tutorial_shown", tutorialId);

        private void HandleLevelCompleted()
        {
            if (!_started) BeginSession();
            if (_flareStart >= 0f) StopFlareClock();
            _completed = true;
            _endReason = "completed";
            AccumulateTime();
            float candle = _game != null ? _game.CandleLeft * 100f : 0f;
            LogEvent("level_completed", "time_s=" + Num(_game != null ? _game.PlayTime : 0f) + ";candle_pct=" + Num(candle));
            SaveNow();
        }

        private void HandleQuitRequested() => EndSession("pause_menu_quit");

        // ------------------------------------------------------------------ writing

        private void LogEvent(string eventName, string detail)
        {
            float time = _game != null ? _game.PlayTime : 0f;
            _row.Clear();
            _row.Append(time.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(_sectionId)).Append(',')
                .Append(Csv(eventName)).Append(',')
                .Append(Csv(detail));
            string row = _row.ToString();

            // Things that happen on the title screen (the first porch lantern lighting) wait for the file.
            if (!_started) _pendingRows.Add(row);
            else if (!_ended) WriteRow(row);
        }

        private void WriteRow(string row)
        {
            if (_events == null || _ioFailed) return;
            try
            {
                _events.WriteLine(row);
                _dirty = true;
            }
            catch (Exception e)
            {
                FailIo(e);
            }
        }

        private void FlushEvents()
        {
            if (_events == null || _ioFailed) return;
            try
            {
                _events.Flush();
                _dirty = false;
            }
            catch (Exception e)
            {
                FailIo(e);
            }
        }

        private string BuildSummary()
        {
            var header = new StringBuilder(512);
            var values = new StringBuilder(512);
            void Add(string name, string value)
            {
                if (header.Length > 0)
                {
                    header.Append(',');
                    values.Append(',');
                }
                header.Append(name);
                values.Append(Csv(value));
            }

            float total = 0f;
            for (int i = 0; i < SectionCount; i++) total += _sectionTime[i];
            float flareSeconds = _flareSeconds + (_flareStart >= 0f ? Mathf.Max(0f, Time.time - _flareStart) : 0f);
            float candle = _game != null ? _game.CandleLeft * 100f : 0f;

            Add("tester_id", s_testerId);
            Add("session", _session);
            Add("date", _startedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Add("time_started", _startedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
            Add("build_version", Application.version);
            Add("completed", _completed ? "yes" : "no");
            Add("end_reason", _endReason);
            Add("last_section", _sectionId);
            Add("total_time_s", Num(total));
            for (int i = 0; i < SectionCount; i++) Add($"S{i + 1}_time_s", Num(_sectionTime[i]));
            for (int i = 0; i < SectionCount; i++) Add($"S{i + 1}_deaths", _sectionDeaths[i].ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < SectionCount; i++) Add($"S{i + 1}_ghost_hits", _sectionHits[i].ToString(CultureInfo.InvariantCulture));
            Add("deaths_total", _deaths.ToString(CultureInfo.InvariantCulture));
            Add("ghost_hits_total", _hits.ToString(CultureInfo.InvariantCulture));
            Add("flare_count", _flareCount.ToString(CultureInfo.InvariantCulture));
            Add("flare_seconds", Num(flareSeconds));
            Add("candy_thrown", _candyThrown.ToString(CultureInfo.InvariantCulture));
            Add("candle_pickups", _candlePickups.ToString(CultureInfo.InvariantCulture));
            Add("final_candle_pct", Num(candle));
            Add("chases", _chases.ToString(CultureInfo.InvariantCulture));
            Add("pauses", _pauses.ToString(CultureInfo.InvariantCulture));

            return header.Append('\n').Append(values).Append('\n').ToString();
        }

        private void FailIo(Exception e)
        {
            if (_ioFailed) return;
            _ioFailed = true;
            Debug.LogWarning($"[PlaytestLogger] Logging stopped, could not write to {FolderPath}: {e.Message}", this);
        }

        // ------------------------------------------------------------------ helpers

        // "S3_Graveyard" -> 2. Ids that do not start with S1..S5 get no summary column of their own.
        private static int SectionIndexOf(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId) || sectionId.Length < 2) return -1;
            if (sectionId[0] != 'S' && sectionId[0] != 's') return -1;
            int number = sectionId[1] - '0';
            return number >= 1 && number <= SectionCount ? number - 1 : -1;
        }

        private static string CauseName(ExtinguishCause cause)
        {
            switch (cause)
            {
                case ExtinguishCause.Drained: return "drained";
                case ExtinguishCause.FlaredOut: return "flared_out";
                case ExtinguishCause.GhostHit: return "ghost_hit";
                case ExtinguishCause.Fell: return "fell";
                default: return "unknown";
            }
        }

        private static string Num(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        // Domain reload is off in this project, so statics are reset by hand for each Play session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            s_testerId = string.Empty;
        }
    }
}
