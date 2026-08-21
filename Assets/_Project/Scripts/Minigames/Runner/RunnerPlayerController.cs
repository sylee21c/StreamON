using UnityEngine;
using UnityEngine.InputSystem;

namespace StreamOn.Minigames.Runner
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class RunnerPlayerController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private RunnerGameManager gameManager;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform groundCheck;
        [SerializeField] private LayerMask groundLayer = 1 << 6;

        [Header("Jump")]
        [SerializeField] private float jumpForce = 12f;
        [SerializeField] private float groundCheckRadius = 0.18f;
        [SerializeField] private float coyoteTime = 0.1f;
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Header("Roll & Attack")]
        [SerializeField] private float rollDuration = 0.72f;
        [SerializeField] private float attackCooldown = 0.72f;
        [SerializeField] private float attackMinimumRange = 0.35f;
        [SerializeField] private float attackMaximumRange = 3.25f;

        [Header("Health")]
        [SerializeField] private int maxHealth = 3;
        [SerializeField] private float invincibilitySeconds = 1f;

        [Header("Campaign Skill Scaling")]
        [SerializeField, Min(1)] private int maximumEffectiveSkillLevel = 10;
        [SerializeField, Min(1)] private int healthStatLevelsPerBonusHealth = 2;
        [SerializeField, Min(0)] private int maximumHealthStatBonusHealth = 4;
        [SerializeField, Min(0.05f)] private float minimumAttackCooldown = 0.38f;
        [SerializeField, Min(0f)] private float maximumAttackRangeBonus = 1.25f;

        public int MaxHealth => _runtimeMaxHealth > 0 ? _runtimeMaxHealth : maxHealth;
        public int CurrentHealth { get; private set; }

        private Rigidbody2D _body;
        private Collider2D _collider;
        private Vector3 _startPosition;
        private float _coyoteCounter;
        private float _jumpBufferCounter;
        private float _invincibleUntil;
        private float _rollUntil;
        private float _attackReadyAt;
        private bool _jumpInProgress;
        private bool _hasLeftGround;
        private bool _isRolling;
        private Vector2 _standingColliderSize;
        private Vector2 _standingColliderOffset;
        private int _runtimeMaxHealth;
        private float _runtimeAttackCooldown;
        private float _runtimeAttackMaximumRange;

        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int JumpStateHash = Animator.StringToHash("Base Layer.Jump");
        private static readonly int HurtHash = Animator.StringToHash("Hurt");
        private static readonly int DeadHash = Animator.StringToHash("Dead");
        private static readonly int RollHash = Animator.StringToHash("Roll");
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        private void Awake()
        {
            // A missing/cleared mask makes every action fail because jump, roll and
            // attack are all gated by the grounded check. Keep the controller usable
            // even when an older scene has serialized this field as Nothing (0).
            if (groundLayer.value == 0)
                groundLayer = 1 << 6;

            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            BoxCollider2D box = (BoxCollider2D)_collider;
            _standingColliderSize = box.size;
            _standingColliderOffset = box.offset;
            _startPosition = transform.position;
            ConfigureForSkill(1, 1);
        }

        private void Update()
        {
            // The collider contact is the primary check. The child Ground Check is a
            // visible/editable fallback, so moving the sprite does not silently break jumping.
            bool rawGrounded = _collider.IsTouchingLayers(groundLayer)
                || (groundCheck != null && Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer));

            // Immediately after applying jump velocity, the collider can still report a
            // ground contact for one or more physics ticks. Do not let that stale contact
            // send the Animator straight back from Jump to Run.
            if (_jumpInProgress)
            {
                if (!rawGrounded)
                    _hasLeftGround = true;

                bool landed = _hasLeftGround && rawGrounded && _body.linearVelocity.y <= 0.01f;
                animator.SetBool(GroundedHash, landed);

                if (landed)
                    _jumpInProgress = false;
            }
            else
            {
                animator.SetBool(GroundedHash, rawGrounded);
            }

            _coyoteCounter = !_jumpInProgress && rawGrounded
                ? coyoteTime
                : _coyoteCounter - Time.deltaTime;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame
                || keyboard.upArrowKey.wasPressedThisFrame
                || keyboard.wKey.wasPressedThisFrame))
                _jumpBufferCounter = jumpBufferTime;
            else
                _jumpBufferCounter -= Time.deltaTime;

            bool canAct = gameManager.State == RunnerGameState.Playing && rawGrounded && !_jumpInProgress;
            if (canAct && !_isRolling && keyboard != null && (keyboard.downArrowKey.wasPressedThisFrame
                || keyboard.cKey.wasPressedThisFrame || keyboard.leftCtrlKey.wasPressedThisFrame))
                StartRoll();

            if (_isRolling && Time.time >= _rollUntil)
                StopRoll();

            Mouse mouse = Mouse.current;
            if (canAct && !_isRolling && mouse != null && mouse.leftButton.wasPressedThisFrame && Time.time >= _attackReadyAt)
                Attack();

            if (gameManager.State == RunnerGameState.Playing && _jumpBufferCounter > 0f && _coyoteCounter > 0f)
            {
                _body.linearVelocity = new Vector2(0f, jumpForce);
                _jumpBufferCounter = 0f;
                _coyoteCounter = 0f;
                _jumpInProgress = true;
                _hasLeftGround = false;
                animator.SetBool(GroundedHash, false);
                animator.ResetTrigger(JumpHash);
                animator.Play(JumpStateHash, 0, 0f);
                gameManager.OnPlayerJumped();
            }

            if (gameManager.State == RunnerGameState.GameOver && keyboard != null && keyboard.rKey.wasPressedThisFrame)
                gameManager.RestartRun();
        }

        public void ResetPlayer()
        {
            transform.position = _startPosition;
            _body.linearVelocity = Vector2.zero;
            CurrentHealth = MaxHealth;
            _invincibleUntil = 0f;
            _attackReadyAt = 0f;
            _jumpInProgress = false;
            _hasLeftGround = false;
            StopRoll();
            animator.Rebind();
            animator.Update(0f);
            animator.SetBool(DeadHash, false);
            animator.SetBool(GroundedHash, true);
        }

        public void ConfigureForSkill(int gameSkill, int healthStat)
        {
            int maximumIndex = Mathf.Max(1, maximumEffectiveSkillLevel - 1);
            int skillIndex = Mathf.Clamp(gameSkill - 1, 0, maximumIndex);
            float progress = skillIndex / (float)maximumIndex;
            int healthBonus = Mathf.Min(maximumHealthStatBonusHealth,
                Mathf.Max(0, healthStat - 1) / Mathf.Max(1, healthStatLevelsPerBonusHealth));
            _runtimeMaxHealth = maxHealth + healthBonus;
            _runtimeAttackCooldown = Mathf.Lerp(attackCooldown, Mathf.Min(attackCooldown, minimumAttackCooldown), progress);
            _runtimeAttackMaximumRange = attackMaximumRange + maximumAttackRangeBonus * progress;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Respawn") || Time.time < _invincibleUntil || gameManager.State != RunnerGameState.Playing) return;
            RunnerObstacle obstacle = other.GetComponent<RunnerObstacle>();
            if (obstacle != null && obstacle.ObstacleType == RunnerObstacleType.Roll && _isRolling) return;
            if (obstacle != null && obstacle.ObstacleType == RunnerObstacleType.Enemy) obstacle.Deactivate();
            ReceiveHit();
        }

        public void ReceiveHit()
        {
            if (Time.time < _invincibleUntil || gameManager.State != RunnerGameState.Playing) return;
            _invincibleUntil = Time.time + invincibilitySeconds;
            CurrentHealth = Mathf.Max(0, CurrentHealth - 1);
            animator.SetTrigger(HurtHash);
            if (CurrentHealth <= 0) animator.SetBool(DeadHash, true);
            gameManager.OnPlayerHit();
        }

        private void StartRoll()
        {
            _isRolling = true;
            _rollUntil = Time.time + rollDuration;
            BoxCollider2D box = (BoxCollider2D)_collider;
            box.size = new Vector2(_standingColliderSize.x, _standingColliderSize.y * 0.46f);
            box.offset = new Vector2(_standingColliderOffset.x, _standingColliderOffset.y - _standingColliderSize.y * 0.27f);
            animator.SetTrigger(RollHash);
        }

        private void StopRoll()
        {
            _isRolling = false;
            BoxCollider2D box = (BoxCollider2D)_collider;
            box.size = _standingColliderSize;
            box.offset = _standingColliderOffset;
        }

        private void Attack()
        {
            _attackReadyAt = Time.time + _runtimeAttackCooldown;
            animator.SetTrigger(AttackHash);

            RunnerObstacle bestTarget = null;
            float bestDistance = float.MaxValue;
            foreach (RunnerObstacle obstacle in FindObjectsByType<RunnerObstacle>(FindObjectsSortMode.None))
            {
                if (obstacle.ObstacleType != RunnerObstacleType.Enemy || obstacle.IsAvailable) continue;
                float distance = obstacle.transform.position.x - transform.position.x;
                if (distance < attackMinimumRange || distance > _runtimeAttackMaximumRange || distance >= bestDistance) continue;
                bestTarget = obstacle;
                bestDistance = distance;
            }

            if (bestTarget == null || !bestTarget.TryDefeat())
                gameManager.OnAttackMissed();
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
