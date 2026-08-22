using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StreamOn.Minigames.TileArena
{
    public sealed class TileArenaController : MonoBehaviour
    {
        private const int Grid = 16;

        [Header("Original HTML Balance")]
        [SerializeField, Range(0f, 1f)] private float blueRatio = 0.10f;
        [SerializeField, Min(1)] private int maximumBlueTiles = 15;
        [SerializeField, Min(0f)] private float transitionSeconds = 2f;
        [SerializeField, Min(1)] private int maximumLives = 5;
        [SerializeField, Min(0.1f)] private float playerSpeedCellsPerSecond = 1000f / 140f;
        [SerializeField, Range(0.01f, 1f)] private float pickupRadius = 0.41f;
        [SerializeField, Min(0.1f)] private float jumpSeconds = 0.76f;
        [SerializeField, Min(0f)] private float invincibleSeconds = 1f;
        [SerializeField, Min(0.001f)] private float stageUpdateInterval = 1f / 30f;
        [Tooltip("0은 무작위, 1~8은 해당 스테이지를 테스트합니다.")]
        [SerializeField, Range(0, 8)] private int debugForcedStage;

        [Header("Scene-authored Board")]
        [SerializeField] private RectTransform board;
        [SerializeField] private Image[] tiles;
        [SerializeField] private Image[] hazards;
        [SerializeField] private RectTransform player;
        [SerializeField] private RectTransform avatar;
        [SerializeField] private RectTransform playerShadow;

        [Header("Scene-authored HUD")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text bestText;
        [SerializeField] private TMP_Text livesText;
        [SerializeField] private GameObject gameOverOverlay;
        [SerializeField] private TMP_Text gameOverScore;
        [SerializeField] private TMP_Text gameOverBest;
        [SerializeField] private GameObject startOverlay;
        [SerializeField] private TileArenaAudioController audioController;
        [SerializeField] private TileArenaChatAdapter chatAdapter;
        [SerializeField] private TileArenaBroadcastSessionController broadcastSession;

        [Header("Original Palette")]
        [SerializeField] private Color floorColor = new Color32(245, 246, 248, 255);
        [SerializeField] private Color alternateFloorColor = new Color32(236, 238, 242, 255);
        [SerializeField] private Color safeColor = new Color32(4, 174, 85, 255);
        [SerializeField] private Color blueColor = new Color32(21, 157, 255, 255);
        [SerializeField] private Color redColor = new Color32(255, 48, 66, 255);

        private readonly HashSet<Vector2Int> _blue = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _red = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _movingRed = new HashSet<Vector2Int>();
        private readonly HashSet<TileArenaDirection> _pointerDirections = new HashSet<TileArenaDirection>();
        private readonly List<int> _stageHistory = new List<int>();
        private Stage[] _stages;
        private Stage _stage;
        private Vector2 _playerPosition;
        private int _score;
        private int _best;
        private int _lives;
        private bool _running;
        private bool _transitioning;
        private float _transitionEndsAt;
        private bool _jumping;
        private float _jumpStartedAt;
        private float _invincibleUntil;
        private float _activeStartedAt;
        private float _lastStageUpdateAt = float.NegativeInfinity;
        private float _hurtUntil;
        private float _shakeUntil;
        private float _focusLostAt = -1f;
        private Vector2 _boardRestPosition;
        private List<Vector2Int[]> _snakeFrames;
        private float _snakeSpacing;
        private Vector2 _joystickVector;
        private float _runStartedAt;
        private float _elapsedAtEnd;
        private int _bestAtRunStart;

        public int Score => _score;
        public int BestScore => _best;
        public int Lives => _lives;
        public bool IsRunning => _running;
        public int MaximumLives => maximumLives;
        public int CurrentStage => _stage != null ? _stage.Id : 0;
        public int BlueTilesRemaining => _blue.Count;
        public float ElapsedSeconds => _running ? Mathf.Max(0f, Time.unscaledTime - _runStartedAt) : _elapsedAtEnd;

        private void Awake()
        {
            if (chatAdapter == null) chatAdapter = GetComponent<TileArenaChatAdapter>();
            if (broadcastSession == null) broadcastSession = GetComponent<TileArenaBroadcastSessionController>();
            if (audioController == null)
            {
                audioController = GetComponent<TileArenaAudioController>();
                if (audioController == null) audioController = gameObject.AddComponent<TileArenaAudioController>();
            }
            _stages = new[] { MakeStage1(), MakeStage2(), MakeStage3(), MakeStage4(), MakeStage5(), MakeStage6(), MakeStage7(), MakeStage8() };
            _best = Mathf.Max(PlayerPrefs.GetInt("tileArenaBest", 0), PlayerPrefs.GetInt("jumpingBattleBest", 0));
            if (board != null) _boardRestPosition = board.anchoredPosition;
            ValidateSceneReferences();
        }

        private void Start()
        {
            RenderHud();
            if (gameOverOverlay != null) gameOverOverlay.SetActive(false);
            if (startOverlay != null)
            {
                _running = false;
                startOverlay.SetActive(true);
                audioController?.StopMusic();
            }
            else StartGame();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                _pointerDirections.Clear();
                _joystickVector = Vector2.zero;
                _focusLostAt = Time.unscaledTime;
            }
            else if (_focusLostAt >= 0f)
            {
                if (_running)
                {
                    float pauseDuration = Mathf.Max(0f, Time.unscaledTime - _focusLostAt);
                    _activeStartedAt += pauseDuration;
                    _runStartedAt += pauseDuration;
                }
                _focusLostAt = -1f;
            }
        }

        private void Update()
        {
            if (!_running) return;
            float now = Time.unscaledTime;
            bool playerMoved = UpdatePlayerMovement(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
            RenderPlayer(now);

            if (_transitioning)
            {
                if (now >= _transitionEndsAt) ActivateStage(_stage, true);
            }
            else
            {
                if (playerMoved && !_jumping) CollectBlueAtPlayer();
                UpdateStage(now);
                CheckCollision(now);
            }
            RenderBoardFeedback(now);
        }

        public void StartGame()
        {
            if (broadcastSession != null && !broadcastSession.TryStartAttempt()) return;
            _running = true;
            _transitioning = false;
            _score = 0;
            _lives = maximumLives;
            _jumping = false;
            _stageHistory.Clear();
            _pointerDirections.Clear();
            _joystickVector = Vector2.zero;
            _runStartedAt = Time.unscaledTime;
            _elapsedAtEnd = 0f;
            _bestAtRunStart = _best;
            if (startOverlay != null) startOverlay.SetActive(false);
            if (gameOverOverlay != null) gameOverOverlay.SetActive(false);
            RenderHud();
            ActivateStage(PickStage(), false);
            audioController?.StartMusic();
            chatAdapter?.OnGameStarted();
        }

        public void TryJump()
        {
            if (!_running || _jumping) return;
            _jumping = true;
            _jumpStartedAt = Time.unscaledTime;
            audioController?.PlayJump();
            chatAdapter?.OnJumped();
        }

        public void SetPointerDirection(TileArenaDirection direction, bool pressed)
        {
            if (pressed) _pointerDirections.Add(direction);
            else _pointerDirections.Remove(direction);
        }

        public void SetJoystickVector(Vector2 value) => _joystickVector = Vector2.ClampMagnitude(value, 1f);

        private bool UpdatePlayerMovement(float deltaSeconds)
        {
            Keyboard keyboard = Keyboard.current;
            bool up = _pointerDirections.Contains(TileArenaDirection.Up);
            bool down = _pointerDirections.Contains(TileArenaDirection.Down);
            bool left = _pointerDirections.Contains(TileArenaDirection.Left);
            bool right = _pointerDirections.Contains(TileArenaDirection.Right);
            if (keyboard != null)
            {
                up |= keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed;
                down |= keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed;
                left |= keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed;
                right |= keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed;
                if (keyboard.spaceKey.wasPressedThisFrame) TryJump();
            }

            Vector2 direction = new Vector2((right ? 1f : 0f) - (left ? 1f : 0f), (down ? 1f : 0f) - (up ? 1f : 0f)) + _joystickVector;
            float length = direction.magnitude;
            if (length <= 0f) return false;
            _playerPosition += direction / length * (playerSpeedCellsPerSecond * deltaSeconds * Mathf.Min(1f, length));
            _playerPosition.x = Mathf.Clamp(_playerPosition.x, 0f, Grid - 1f);
            _playerPosition.y = Mathf.Clamp(_playerPosition.y, 0f, Grid - 1f);
            return true;
        }

        private void ActivateStage(Stage stage, bool preservePosition)
        {
            _stage = stage;
            _stageHistory.Add(stage.Id);
            while (_stageHistory.Count > 2) _stageHistory.RemoveAt(0);
            _transitioning = false;
            _invincibleUntil = 0f;
            if (!preservePosition)
            {
                _playerPosition = stage.Spawn;
                _jumping = false;
            }
            _red.Clear();
            _red.UnionWith(stage.Fixed);
            _movingRed.Clear();
            PrepareRuntime();
            PlaceBlue();
            _activeStartedAt = Time.unscaledTime;
            _lastStageUpdateAt = float.NegativeInfinity;
            UpdateStage(_activeStartedAt, true);
            RenderPlayer(_activeStartedAt);
            RenderRed();
        }

        private void BeginTransition()
        {
            if (_transitioning || !_running) return;
            _transitioning = true;
            _jumping = false;
            chatAdapter?.OnStageCleared();
            _stage = PickStage();
            _blue.Clear();
            _red.Clear();
            _movingRed.Clear();
            RenderTiles(true);
            RenderRed();
            RenderPlayer(Time.unscaledTime);
            _transitionEndsAt = Time.unscaledTime + transitionSeconds;
            audioController?.PlayStageClear();
        }

        private Stage PickStage()
        {
            if (debugForcedStage >= 1 && debugForcedStage <= _stages.Length) return _stages[debugForcedStage - 1];
            Stage[] candidates = _stages.Where(stage => !_stageHistory.Contains(stage.Id)).ToArray();
            return candidates[UnityEngine.Random.Range(0, candidates.Length)];
        }

        private void PlaceBlue()
        {
            List<Vector2Int> blank = new List<Vector2Int>();
            List<Vector2Int> eligible = new List<Vector2Int>();
            Vector2Int occupied = PlayerTile();
            _blue.Clear();
            for (int y = 0; y < Grid; y++)
            for (int x = 0; x < Grid; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (_stage.Safe.Contains(cell) || _stage.Fixed.Contains(cell) || _stage.InitialMoving.Contains(cell)) continue;
                blank.Add(cell);
                if (cell != occupied) eligible.Add(cell);
            }
            Shuffle(eligible);
            int count = Mathf.Min(eligible.Count, Mathf.FloorToInt(blank.Count * blueRatio + 0.5f), maximumBlueTiles);
            for (int i = 0; i < count; i++) _blue.Add(eligible[i]);
            RenderTiles(false);
        }

        private void CollectBlueAtPlayer()
        {
            if (!_running || _transitioning || _jumping) return;
            float cx = _playerPosition.x + 0.5f;
            float cy = _playerPosition.y + 0.5f;
            int minX = Mathf.Clamp(Mathf.FloorToInt(cx - pickupRadius), 0, Grid - 1);
            int maxX = Mathf.Clamp(Mathf.FloorToInt(cx + pickupRadius), 0, Grid - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(cy - pickupRadius), 0, Grid - 1);
            int maxY = Mathf.Clamp(Mathf.FloorToInt(cy + pickupRadius), 0, Grid - 1);
            List<Vector2Int> touched = new List<Vector2Int>();
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!_blue.Contains(cell)) continue;
                float nearestX = Mathf.Clamp(cx, x, x + 1f);
                float nearestY = Mathf.Clamp(cy, y, y + 1f);
                float dx = cx - nearestX;
                float dy = cy - nearestY;
                if (dx * dx + dy * dy <= pickupRadius * pickupRadius) touched.Add(cell);
            }
            if (touched.Count == 0) return;
            foreach (Vector2Int cell in touched) _blue.Remove(cell);
            _score += Mathf.Max(1, Mathf.RoundToInt(touched.Count * (broadcastSession != null ? broadcastSession.ScoreMultiplier : 1f)));
            audioController?.PlayPickup();
            chatAdapter?.OnBluePickedUp(touched.Count);
            if (_score > _best)
            {
                _best = _score;
                PlayerPrefs.SetInt("tileArenaBest", _best);
                PlayerPrefs.Save();
            }
            RenderTiles(false);
            RenderHud();
            if (_blue.Count == 0) BeginTransition();
        }

        private void PrepareRuntime()
        {
            _snakeFrames = null;
            _snakeSpacing = 0f;
            if (_stage.Type != StageType.Snake) return;
            _snakeFrames = new List<Vector2Int[]>();
            for (int index = 0; index < _stage.Segments.Length; index++)
            {
                Segment segment = _stage.Segments[index];
                int direction = segment.To >= segment.From ? 1 : -1;
                for (int value = segment.From;; value += direction)
                {
                    _snakeFrames.Add(BarCells(segment, value));
                    if (value == segment.To) break;
                }
                if (index < _stage.Segments.Length - 1)
                    _snakeFrames.Add(CornerCells(segment, _stage.Segments[index + 1]));
            }
            _snakeSpacing = _snakeFrames.Count / (float)_stage.BarCount;
        }

        private void UpdateStage(float now, bool force = false)
        {
            if (_stage == null || _transitioning) return;
            if (!force && now - _lastStageUpdateAt < stageUpdateInterval) return;
            _lastStageUpdateAt = now;
            float elapsedMs = Mathf.Max(0f, now - _activeStartedAt) * 1000f;
            HashSet<Vector2Int> moving = _stage.Type switch
            {
                StageType.Clock => ClockCells(_stage, elapsedMs),
                StageType.Snake => SnakeCells(_stage, elapsedMs),
                StageType.Cross => CrossCells(_stage, elapsedMs),
                StageType.DiagonalSweep => DiagonalSweepCells(_stage, elapsedMs),
                StageType.Windmill => WindmillCells(_stage, elapsedMs),
                StageType.Pulse => PulseCells(_stage, elapsedMs),
                StageType.CornerDashes => CornerDashCells(_stage, elapsedMs),
                _ => BounceBarCells(_stage, elapsedMs)
            };
            if (!force && _movingRed.SetEquals(moving)) return;
            _movingRed.Clear();
            _movingRed.UnionWith(moving);
            _red.Clear();
            _red.UnionWith(_stage.Fixed);
            _red.UnionWith(_movingRed);
            RenderRed();
        }

        private HashSet<Vector2Int> ClockCells(Stage stage, float elapsedMs)
        {
            int slot = Mathf.FloorToInt(elapsedMs / stage.DirectionMs);
            float slotElapsed = elapsedMs - slot * stage.DirectionMs;
            float before = 0f;
            for (int i = 0; i < slot; i++) before += (i % 2 == 0 ? 1f : -1f) * (stage.DirectionMs / stage.RotationMs) * Mathf.PI * 2f;
            float direction = slot % 2 == 0 ? 1f : -1f;
            float angle = before + direction * (slotElapsed / stage.RotationMs) * Mathf.PI * 2f;
            HashSet<Vector2Int> output = new HashSet<Vector2Int>();
            float center = (Grid - 1f) / 2f;
            float dxLine = Mathf.Sin(angle);
            float dyLine = -Mathf.Cos(angle);
            for (int y = 0; y < Grid; y++)
            for (int x = 0; x < Grid; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (stage.Safe.Contains(cell) || stage.Fixed.Contains(cell)) continue;
                float dx = x - center;
                float dy = y - center;
                if (Mathf.Abs(dx * dyLine - dy * dxLine) <= 0.72f) output.Add(cell);
            }
            return output;
        }

        private HashSet<Vector2Int> SnakeCells(Stage stage, float elapsedMs)
        {
            HashSet<Vector2Int> output = new HashSet<Vector2Int>();
            float phase = elapsedMs / stage.FrameMs % _snakeSpacing;
            for (int bar = 0; bar < stage.BarCount; bar++)
            {
                float position = phase + bar * _snakeSpacing;
                if (position >= _snakeFrames.Count) continue;
                foreach (Vector2Int cell in _snakeFrames[Mathf.FloorToInt(position)])
                    if (!stage.Safe.Contains(cell) && !stage.Fixed.Contains(cell)) output.Add(cell);
            }
            return output;
        }

        private static HashSet<Vector2Int> CrossCells(Stage stage, float elapsedMs)
        {
            float phase = elapsedMs % (stage.TravelMs * 2f) / stage.TravelMs;
            if (phase > 1f) phase = 2f - phase;
            // JavaScript Math.round uses floor(x + .5), unlike Mathf.RoundToInt's midpoint rule.
            int topY = Mathf.FloorToInt(phase * (Grid - 1) + 0.5f);
            int bottomY = Grid - 1 - topY;
            HashSet<Vector2Int> output = new HashSet<Vector2Int>();
            foreach (int x in stage.TopBars) { output.Add(new Vector2Int(x, topY)); output.Add(new Vector2Int(x + 1, topY)); }
            foreach (int x in stage.BottomBars) { output.Add(new Vector2Int(x, bottomY)); output.Add(new Vector2Int(x + 1, bottomY)); }
            return output;
        }

        private static HashSet<Vector2Int> DiagonalSweepCells(Stage stage, float elapsedMs)
        {
            int steps = Mathf.FloorToInt(elapsedMs / stage.FrameMs);
            int phase = (stage.StartSum + steps * 2) % stage.SumSpacing;
            HashSet<Vector2Int> output = new HashSet<Vector2Int>();
            for (int sum = phase; sum <= Grid * 2 - 2; sum += stage.SumSpacing)
            for (int x = 0; x < Grid; x++)
            {
                int y = sum - x;
                Vector2Int cell = new Vector2Int(x, y);
                if (y >= 0 && y < Grid && !stage.Safe.Contains(cell)) output.Add(cell);
            }
            return output;
        }

        private static HashSet<Vector2Int> WindmillCellsAtAngle(HashSet<Vector2Int> safe, float angle, int innerGap)
        {
            HashSet<Vector2Int> output = new HashSet<Vector2Int>();
            float center = (Grid - 1f) / 2f;
            for (int y = 0; y < Grid; y++)
            for (int x = 0; x < Grid; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (safe.Contains(cell)) continue;
                float dx = x - center;
                float dy = y - center;
                for (int arm = 0; arm < 4; arm++)
                {
                    float armAngle = angle + arm * Mathf.PI / 2f;
                    float vx = -Mathf.Sin(armAngle);
                    float vy = -Mathf.Cos(armAngle);
                    float nx = -vy;
                    float ny = vx;
                    float forward = dx * vx + dy * vy;
                    float offset = dx * nx + dy * ny;
                    if (forward > innerGap + 0.5f && Mathf.Abs(offset - 0.5f) <= 0.58f)
                    {
                        output.Add(cell);
                        break;
                    }
                }
            }
            return output;
        }

        private static HashSet<Vector2Int> WindmillCells(Stage stage, float elapsedMs)
        {
            float angle = elapsedMs / stage.RotationMs * Mathf.PI * 2f;
            return WindmillCellsAtAngle(stage.Safe, angle, stage.InnerGap);
        }

        private static int PulseRadius(Stage stage, float elapsedMs)
        {
            int innerSteps = stage.MaxRadius - 1;
            int cycleFrames = stage.MinHoldFrames + innerSteps + stage.MaxHoldFrames + innerSteps;
            int frame = Mathf.FloorToInt(elapsedMs / stage.FrameMs) % cycleFrames;
            if (frame < stage.MinHoldFrames) return 0;
            frame -= stage.MinHoldFrames;
            if (frame < innerSteps) return frame + 1;
            frame -= innerSteps;
            if (frame < stage.MaxHoldFrames) return stage.MaxRadius;
            frame -= stage.MaxHoldFrames;
            return innerSteps - frame;
        }

        private static HashSet<Vector2Int> PulseCells(Stage stage, float elapsedMs)
        {
            int radius = PulseRadius(stage, elapsedMs);
            HashSet<Vector2Int> output = new HashSet<Vector2Int>();
            if (radius <= 0) return output;
            for (int y = 0; y < Grid; y++)
            for (int x = 0; x < Grid; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (stage.Safe.Contains(cell) || stage.Fixed.Contains(cell)) continue;
                int dx = x < 7 ? 7 - x : x > 8 ? x - 8 : 0;
                int dy = y < 7 ? 7 - y : y > 8 ? y - 8 : 0;
                if (dx + dy <= radius) output.Add(cell);
            }
            return output;
        }

        private static void AddDash(HashSet<Vector2Int> output, HashSet<Vector2Int> safe, int centerX, int centerY, Vector2Int[] offsets)
        {
            foreach (Vector2Int offset in offsets)
            {
                Vector2Int cell = new Vector2Int(centerX + offset.x, centerY + offset.y);
                if (cell.x >= 0 && cell.x < Grid && cell.y >= 0 && cell.y < Grid && !safe.Contains(cell)) output.Add(cell);
            }
        }

        private static HashSet<Vector2Int> CornerDashCellsAtPhase(HashSet<Vector2Int> safe, int phase)
        {
            HashSet<Vector2Int> output = new HashSet<Vector2Int>();
            Vector2Int[] slash = { new Vector2Int(-2, 2), new Vector2Int(-1, 1), Vector2Int.zero, new Vector2Int(1, -1), new Vector2Int(2, -2) };
            Vector2Int[] backslash = { new Vector2Int(-2, -2), new Vector2Int(-1, -1), Vector2Int.zero, new Vector2Int(1, 1), new Vector2Int(2, 2) };
            AddDash(output, safe, phase, phase, slash);
            AddDash(output, safe, Grid - 1 - phase, phase, backslash);
            AddDash(output, safe, phase, Grid - 1 - phase, backslash);
            AddDash(output, safe, Grid - 1 - phase, Grid - 1 - phase, slash);
            return output;
        }

        private static HashSet<Vector2Int> CornerDashCells(Stage stage, float elapsedMs)
        {
            int step = Mathf.FloorToInt(elapsedMs / stage.FrameMs) % stage.TravelSteps;
            return CornerDashCellsAtPhase(stage.Safe, stage.StartPhase + step);
        }

        private static HashSet<Vector2Int> BounceBarCells(Stage stage, float elapsedMs)
        {
            int steps = PausedBounceStep(stage, elapsedMs);
            int leftX = ReflectedCell(stage.LeftStart, 1, steps, stage.Min, stage.Max);
            int rightX = ReflectedCell(stage.RightStart, -1, steps, stage.Min, stage.Max);
            int topY = ReflectedCell(stage.TopStart, 1, steps, stage.Min, stage.Max);
            int bottomY = ReflectedCell(stage.BottomStart, -1, steps, stage.Min, stage.Max);
            HashSet<Vector2Int> output = new HashSet<Vector2Int>();
            for (int i = 0; i < Grid; i++)
            {
                Vector2Int[] cells = { new Vector2Int(leftX, i), new Vector2Int(rightX, i), new Vector2Int(i, topY), new Vector2Int(i, bottomY) };
                foreach (Vector2Int cell in cells) if (!stage.Safe.Contains(cell)) output.Add(cell);
            }
            return output;
        }

        private static int PausedBounceStep(Stage stage, float elapsedMs)
        {
            int span = stage.Max - stage.Min;
            int period = span * 2;
            int centerLeft = Mathf.FloorToInt((stage.Min + stage.Max) / 2f);
            int centerRight = Mathf.CeilToInt((stage.Min + stage.Max) / 2f);
            int[] durations = new int[period];
            int cycleFrames = 0;
            for (int step = 0; step < period; step++)
            {
                int left = ReflectedCell(stage.LeftStart, 1, step, stage.Min, stage.Max);
                int right = ReflectedCell(stage.RightStart, -1, step, stage.Min, stage.Max);
                int pairMin = Mathf.Min(left, right);
                int pairMax = Mathf.Max(left, right);
                int duration = 1;
                if (pairMin == centerLeft && pairMax == centerRight)
                {
                    int previousStep = (step - 1 + period) % period;
                    int previousLeft = ReflectedCell(stage.LeftStart, 1, previousStep, stage.Min, stage.Max);
                    int previousRight = ReflectedCell(stage.RightStart, -1, previousStep, stage.Min, stage.Max);
                    bool previousWasCenter = Mathf.Min(previousLeft, previousRight) == centerLeft && Mathf.Max(previousLeft, previousRight) == centerRight;
                    duration = previousWasCenter ? 0 : stage.CenterPauseFrames;
                }
                else if (pairMin == stage.Min && pairMax == stage.Max) duration = stage.EdgePauseFrames;
                durations[step] = duration;
                cycleFrames += duration;
            }
            int frame = Mathf.FloorToInt(elapsedMs / stage.FrameMs) % cycleFrames;
            for (int step = 0; step < period; step++)
            {
                if (frame < durations[step]) return step;
                frame -= durations[step];
            }
            return 0;
        }

        private static int ReflectedCell(int start, int direction, int steps, int min, int max)
        {
            int span = max - min;
            int period = span * 2;
            int value = (start - min + direction * steps) % period;
            if (value < 0) value += period;
            return min + (value <= span ? value : period - value);
        }

        private static Vector2Int[] BarCells(Segment segment, int value) => segment.Axis == Axis.Vertical
            ? new[] { new Vector2Int(segment.Fixed, value), new Vector2Int(segment.Fixed + 1, value) }
            : new[] { new Vector2Int(value, segment.Fixed), new Vector2Int(value, segment.Fixed + 1) };

        private static Vector2Int[] CornerCells(Segment previous, Segment next)
        {
            Vector2Int[] all = BarCells(previous, previous.To).Concat(BarCells(next, next.From)).ToArray();
            int left = all.Min(cell => cell.x);
            int right = all.Max(cell => cell.x);
            int top = all.Min(cell => cell.y);
            int bottom = all.Max(cell => cell.y);
            return left == 0 || left == 8
                ? new[] { new Vector2Int(left, bottom), new Vector2Int(right, top) }
                : new[] { new Vector2Int(left, top), new Vector2Int(right, bottom) };
        }

        private void CheckCollision(float now)
        {
            if (!_running || _transitioning || now < _invincibleUntil || AirborneSafe(now) || !_red.Contains(PlayerTile())) return;
            _lives--;
            _invincibleUntil = now + invincibleSeconds;
            _hurtUntil = now + 0.52f;
            _shakeUntil = now + 0.30f;
            audioController?.PlayHit();
            chatAdapter?.OnPlayerHit(_lives <= 2);
            RenderHud();
            if (_lives <= 0) GameOver();
        }

        private bool AirborneSafe(float now)
        {
            if (!_jumping) return false;
            float progress = (now - _jumpStartedAt) / jumpSeconds;
            return Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI) > 0.38f;
        }

        private Vector2Int PlayerTile() => new Vector2Int(
            Mathf.Clamp(Mathf.FloorToInt(_playerPosition.x + 0.5f), 0, Grid - 1),
            Mathf.Clamp(Mathf.FloorToInt(_playerPosition.y + 0.5f), 0, Grid - 1));

        private void GameOver()
        {
            _elapsedAtEnd = Mathf.Max(0f, Time.unscaledTime - _runStartedAt);
            _running = false;
            _pointerDirections.Clear();
            if (gameOverScore != null) gameOverScore.text = _score.ToString();
            if (gameOverBest != null) gameOverBest.text = _best.ToString();
            if (gameOverOverlay != null) gameOverOverlay.SetActive(true);
            audioController?.StopMusic();
            audioController?.PlayGameOver();
            chatAdapter?.OnGameOver(_score > _bestAtRunStart);
            broadcastSession?.OnAttemptGameOver(_score, maximumLives - _lives);
        }

        private void RenderPlayer(float now)
        {
            if (player == null || board == null) return;
            float cell = board.rect.width / Grid;
            player.anchoredPosition = new Vector2(_playerPosition.x * cell, -_playerPosition.y * cell);
            float height = 0f;
            if (_jumping)
            {
                float progress = (now - _jumpStartedAt) / jumpSeconds;
                if (progress >= 1f)
                {
                    _jumping = false;
                    CollectBlueAtPlayer();
                }
                else if (progress >= 0f) height = Mathf.Sin(progress * Mathf.PI);
            }
            if (avatar != null)
            {
                avatar.anchoredPosition = new Vector2(0f, height * cell * 0.90f);
                avatar.localScale = Vector3.one * (1f + height * 0.16f);
                Graphic avatarGraphic = avatar.GetComponentsInChildren<Graphic>().FirstOrDefault(item => item.name == "Orange Core");
                if (avatarGraphic != null) avatarGraphic.color = now < _hurtUntil ? Color.white : new Color32(255, 157, 34, 255);
                avatar.gameObject.SetActive(now >= _invincibleUntil || Mathf.FloorToInt(now / 0.12f) % 2 == 0);
            }
            if (playerShadow != null)
            {
                playerShadow.localScale = new Vector3(1f - height * 0.58f, 1f - height * 0.58f, 1f);
                Graphic shadowGraphic = playerShadow.GetComponent<Graphic>();
                if (shadowGraphic != null) shadowGraphic.color = new Color(0f, 0f, 0f, 0.68f - height * 0.5f);
            }
        }

        private void RenderBoardFeedback(float now)
        {
            if (board == null) return;
            if (now < _shakeUntil)
            {
                float strength = 5f * ((_shakeUntil - now) / 0.3f);
                board.anchoredPosition = _boardRestPosition + UnityEngine.Random.insideUnitCircle * strength;
            }
            else board.anchoredPosition = _boardRestPosition;
        }

        private void RenderTiles(bool preview)
        {
            if (tiles == null || tiles.Length != Grid * Grid) return;
            for (int y = 0; y < Grid; y++)
            for (int x = 0; x < Grid; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                tiles[y * Grid + x].color = _stage != null && _stage.Safe.Contains(cell)
                    ? safeColor
                    : !preview && _blue.Contains(cell) ? blueColor : ((x + y) % 2 == 0 ? floorColor : alternateFloorColor);
            }
        }

        private void RenderRed()
        {
            if (hazards == null) return;
            int index = 0;
            float cell = board != null ? board.rect.width / Grid : 0f;
            foreach (Vector2Int position in _red)
            {
                if (index >= hazards.Length) break;
                Image hazard = hazards[index++];
                hazard.gameObject.SetActive(true);
                hazard.color = redColor;
                hazard.rectTransform.anchoredPosition = new Vector2(position.x * cell, -position.y * cell);
            }
            while (index < hazards.Length) hazards[index++].gameObject.SetActive(false);
        }

        private void RenderHud()
        {
            if (scoreText != null) scoreText.text = _score.ToString();
            if (bestText != null) bestText.text = _best.ToString();
            if (livesText != null) livesText.text = new string('♥', _lives) + new string('♡', maximumLives - _lives);
        }

        private void ValidateSceneReferences()
        {
            if (tiles == null || tiles.Length != Grid * Grid || hazards == null || hazards.Length != Grid * Grid)
                Debug.LogError("TILE ARENA requires exactly 256 scene-authored tiles and 256 scene-authored hazard images.", this);
        }

        private static void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int swap = UnityEngine.Random.Range(0, i + 1);
                (items[i], items[swap]) = (items[swap], items[i]);
            }
        }

        private static Stage MakeStage1()
        {
            Stage stage = new Stage { Id = 1, Type = StageType.Clock, Spawn = new Vector2(0, 7), RotationMs = 4000f, DirectionMs = 8000f };
            for (int i = 0; i < Grid; i++) { stage.Safe.Add(new Vector2Int(i, 0)); stage.Safe.Add(new Vector2Int(i, 15)); }
            for (int y = 1; y < 15; y++) { stage.Safe.Add(new Vector2Int(0, y)); stage.Safe.Add(new Vector2Int(15, y)); }
            foreach (Vector2Int cell in new[] { new Vector2Int(1, 1), new Vector2Int(14, 1), new Vector2Int(1, 14), new Vector2Int(14, 14) }) stage.Safe.Add(cell);
            foreach (Vector2Int cell in new[] { new Vector2Int(7, 7), new Vector2Int(8, 7), new Vector2Int(7, 8), new Vector2Int(8, 8) }) stage.Fixed.Add(cell);
            for (int y = 1; y < 15; y++) for (int x = 7; x <= 8; x++) if (!stage.Fixed.Contains(new Vector2Int(x, y))) stage.InitialMoving.Add(new Vector2Int(x, y));
            return stage;
        }

        private static Stage MakeStage2()
        {
            Stage stage = new Stage { Id = 2, Type = StageType.Snake, Spawn = Vector2.zero, FrameMs = 77.2f, BarCount = 4 };
            for (int x = 0; x < Grid; x++) for (int y = 0; y < Grid; y++) if (y <= 1 || y >= 14) stage.Safe.Add(new Vector2Int(x, y));
            AddWall(stage.Fixed, 2, 3, 2, 11); AddWall(stage.Fixed, 6, 7, 4, 13); AddWall(stage.Fixed, 10, 11, 2, 11); AddWall(stage.Fixed, 14, 15, 4, 13);
            foreach (int x in new[] { 0, 1, 4, 5, 8, 9, 12, 13 }) stage.InitialMoving.Add(new Vector2Int(x, 7));
            stage.Segments = new[]
            {
                new Segment(Axis.Horizontal, -1, 0, 2), new Segment(Axis.Vertical, 3, 12, 0),
                new Segment(Axis.Horizontal, 0, 4, 12), new Segment(Axis.Vertical, 12, 2, 4),
                new Segment(Axis.Horizontal, 5, 8, 2), new Segment(Axis.Vertical, 3, 12, 8),
                new Segment(Axis.Horizontal, 8, 12, 12), new Segment(Axis.Vertical, 12, 2, 12),
                new Segment(Axis.Horizontal, 13, 15, 2)
            };
            return stage;
        }

        private static Stage MakeStage3()
        {
            Stage stage = new Stage { Id = 3, Type = StageType.Cross, Spawn = new Vector2(0, 7), TopBars = new[] { 2, 6, 10 }, BottomBars = new[] { 4, 8, 12 }, TravelMs = 1680f };
            for (int y = 0; y < Grid; y++)
            foreach (int x in new[] { 0, 1, 14, 15 })
                if (y == 7 || y == 8) stage.Safe.Add(new Vector2Int(x, y)); else stage.Fixed.Add(new Vector2Int(x, y));
            foreach (int x in stage.TopBars) { stage.InitialMoving.Add(new Vector2Int(x, 0)); stage.InitialMoving.Add(new Vector2Int(x + 1, 0)); }
            foreach (int x in stage.BottomBars) { stage.InitialMoving.Add(new Vector2Int(x, 15)); stage.InitialMoving.Add(new Vector2Int(x + 1, 15)); }
            return stage;
        }

        private static Stage MakeStage4()
        {
            Stage stage = new Stage
            {
                Id = 4, Type = StageType.BounceBars, Spawn = Vector2.zero, FrameMs = 300f, Min = 0, Max = 15,
                LeftStart = 2, RightStart = 13, TopStart = 2, BottomStart = 13, CenterPauseFrames = 7, EdgePauseFrames = 7
            };
            foreach (int x in new[] { 0, 1, 14, 15 }) foreach (int y in new[] { 0, 1, 14, 15 }) stage.Safe.Add(new Vector2Int(x, y));
            for (int i = 0; i < Grid; i++)
            {
                stage.InitialMoving.Add(new Vector2Int(2, i)); stage.InitialMoving.Add(new Vector2Int(13, i));
                stage.InitialMoving.Add(new Vector2Int(i, 2)); stage.InitialMoving.Add(new Vector2Int(i, 13));
            }
            return stage;
        }

        private static Stage MakeStage5()
        {
            Stage stage = new Stage
            {
                Id = 5, Type = StageType.DiagonalSweep, Spawn = new Vector2(7, 7), FrameMs = 180f,
                StartSum = 8, GapSteps = 6, SumSpacing = 12
            };
            foreach (Vector2Int cell in new[] { new Vector2Int(7, 7), new Vector2Int(8, 7), new Vector2Int(7, 8), new Vector2Int(8, 8) }) stage.Safe.Add(cell);
            for (int sum = 8; sum <= Grid * 2 - 2; sum += 12)
            for (int x = 0; x < Grid; x++)
            {
                int y = sum - x;
                Vector2Int cell = new Vector2Int(x, y);
                if (y >= 0 && y < Grid && !stage.Safe.Contains(cell)) stage.InitialMoving.Add(cell);
            }
            return stage;
        }

        private static Stage MakeStage6()
        {
            Stage stage = new Stage { Id = 6, Type = StageType.Windmill, Spawn = new Vector2(7, 7), RotationMs = 6000f, InnerGap = 2 };
            foreach (Vector2Int cell in new[] { new Vector2Int(7, 7), new Vector2Int(8, 7), new Vector2Int(7, 8), new Vector2Int(8, 8) }) stage.Safe.Add(cell);
            stage.InitialMoving.UnionWith(WindmillCellsAtAngle(stage.Safe, 0f, stage.InnerGap));
            return stage;
        }

        private static Stage MakeStage7()
        {
            Stage stage = new Stage
            {
                Id = 7, Type = StageType.Pulse, Spawn = new Vector2(7, 0), FrameMs = 180f,
                MaxRadius = 14, MinHoldFrames = 8, MaxHoldFrames = 8
            };
            foreach (Vector2Int cell in new[]
                     {
                         new Vector2Int(7, 0), new Vector2Int(8, 0), new Vector2Int(7, 1), new Vector2Int(8, 1),
                         new Vector2Int(0, 7), new Vector2Int(1, 7), new Vector2Int(0, 8), new Vector2Int(1, 8),
                         new Vector2Int(14, 7), new Vector2Int(15, 7), new Vector2Int(14, 8), new Vector2Int(15, 8),
                         new Vector2Int(7, 14), new Vector2Int(8, 14), new Vector2Int(7, 15), new Vector2Int(8, 15)
                     }) stage.Safe.Add(cell);
            foreach (Vector2Int cell in new[] { new Vector2Int(7, 7), new Vector2Int(8, 7), new Vector2Int(7, 8), new Vector2Int(8, 8) }) stage.Fixed.Add(cell);
            return stage;
        }

        private static Stage MakeStage8()
        {
            Stage stage = new Stage { Id = 8, Type = StageType.CornerDashes, Spawn = Vector2.zero, FrameMs = 180f, StartPhase = 0, TravelSteps = Grid };
            foreach (int x in new[] { 0, 1, 14, 15 }) foreach (int y in new[] { 0, 1, 14, 15 }) stage.Safe.Add(new Vector2Int(x, y));
            stage.InitialMoving.UnionWith(CornerDashCellsAtPhase(stage.Safe, 0));
            return stage;
        }

        private static void AddWall(HashSet<Vector2Int> output, int x1, int x2, int y1, int y2)
        {
            for (int y = y1; y <= y2; y++) for (int x = x1; x <= x2; x++) output.Add(new Vector2Int(x, y));
        }

        private enum StageType { Clock, Snake, Cross, BounceBars, DiagonalSweep, Windmill, Pulse, CornerDashes }
        private enum Axis { Horizontal, Vertical }

        private readonly struct Segment
        {
            public readonly Axis Axis;
            public readonly int From;
            public readonly int To;
            public readonly int Fixed;
            public Segment(Axis axis, int from, int to, int fixedCoordinate) { Axis = axis; From = from; To = to; Fixed = fixedCoordinate; }
        }

        private sealed class Stage
        {
            public int Id;
            public StageType Type;
            public readonly HashSet<Vector2Int> Safe = new HashSet<Vector2Int>();
            public readonly HashSet<Vector2Int> Fixed = new HashSet<Vector2Int>();
            public readonly HashSet<Vector2Int> InitialMoving = new HashSet<Vector2Int>();
            public Vector2 Spawn;
            public float RotationMs;
            public float DirectionMs;
            public float FrameMs;
            public int BarCount;
            public Segment[] Segments;
            public int[] TopBars;
            public int[] BottomBars;
            public float TravelMs;
            public int Min;
            public int Max;
            public int LeftStart;
            public int RightStart;
            public int TopStart;
            public int BottomStart;
            public int CenterPauseFrames;
            public int EdgePauseFrames;
            public int StartSum;
            public int GapSteps;
            public int SumSpacing;
            public int InnerGap;
            public int MaxRadius;
            public int MinHoldFrames;
            public int MaxHoldFrames;
            public int StartPhase;
            public int TravelSteps;
        }
    }
}
