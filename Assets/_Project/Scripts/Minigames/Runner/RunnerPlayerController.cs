using System.Collections;
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
        [Header("Attack Timing")]
        [Tooltip("다음 공격 입력이 허용되기까지의 프레임 수입니다.")]
        [SerializeField, Min(1)] private int attackCooldownFrames = 12;
        [Tooltip("공격 입력을 몇 애니메이션 프레임 동안 기억할지 정합니다. 좌클릭 씹힘을 줄여줍니다.")]
        [SerializeField, Min(0)] private int attackInputBufferFrames = 4;
        [Tooltip("공격 쿨다운과 입력 버퍼 계산에 사용하는 기준 FPS입니다.")]
        [SerializeField, Min(1f)] private float attackAnimationFramesPerSecond = 16f;
        [SerializeField] private BoxCollider2D attackHitbox;
        [SerializeField] private float attackMinimumRange = 0.35f;
        [SerializeField] private float attackMaximumRange = 3.25f;

        [Header("Health")]
        [SerializeField] private int maxHealth = 5;
        [SerializeField] private float invincibilitySeconds = 1f;

        [Header("Campaign Skill Scaling")]
        [SerializeField, Min(1)] private int maximumEffectiveSkillLevel = 10;
        [SerializeField, Min(1)] private int healthStatLevelsPerBonusHealth = 2;
        [SerializeField, Min(0)] private int maximumHealthStatBonusHealth = 4;
        [SerializeField, Min(1)] private int minimumAttackCooldownFrames = 6;
        [SerializeField, Min(0f)] private float maximumAttackRangeBonus = 1.25f;

        public int MaxHealth => _runtimeMaxHealth > 0 ? _runtimeMaxHealth : maxHealth;
        public int CurrentHealth { get; private set; }

        public IEnumerator WaitForDeathAnimationComplete(float fallbackSeconds)
        {
            fallbackSeconds = Mathf.Max(0f, fallbackSeconds);
            if (animator == null || !animator.isActiveAndEnabled)
            {
                if (fallbackSeconds > 0f) yield return new WaitForSecondsRealtime(fallbackSeconds);
                yield break;
            }

            bool enteredDeathState = false;
            float timeoutAt = Time.realtimeSinceStartup + Mathf.Max(2f, fallbackSeconds + 1f);
            while (Time.realtimeSinceStartup < timeoutAt)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash == DeathStateHash)
                {
                    enteredDeathState = true;
                    if (!animator.IsInTransition(0) && state.normalizedTime >= 1f)
                    {
                        // Let the final death sprite render for one full frame before UI covers it.
                        yield return new WaitForEndOfFrame();
                        yield break;
                    }
                }
                else if (enteredDeathState && !animator.IsInTransition(0))
                {
                    yield break;
                }

                yield return null;
            }
        }

        private Rigidbody2D _body;
        private Collider2D _collider;
        private Vector3 _startPosition;
        private float _coyoteCounter;
        private float _jumpBufferCounter;
        private float _invincibleUntil;
        private float _rollUntil;
        private float _attackReadyAt;
        private float _attackBufferedUntil = -1f;
        private bool _jumpInProgress;
        private bool _hasLeftGround;
        private bool _isRolling;
        private bool _isAttacking;
        private Vector2 _standingColliderSize;
        private Vector2 _standingColliderOffset;
        private int _runtimeMaxHealth;
        private float _runtimeAttackCooldown;
        private float _runtimeAttackMaximumRange;
        private Vector2 _baseAttackHitboxSize;
        private Vector2 _baseAttackHitboxOffset;
        private readonly ContactPoint2D[] _groundContacts = new ContactPoint2D[8];

        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int JumpStateHash = Animator.StringToHash("Base Layer.Jump");
        private static readonly int HurtHash = Animator.StringToHash("Hurt");
        private static readonly int DeadHash = Animator.StringToHash("Dead");
        private static readonly int RollHash = Animator.StringToHash("Roll");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int AttackStateHash = Animator.StringToHash("Base Layer.Attack");
        private static readonly int DeathStateHash = Animator.StringToHash("Base Layer.Dead");

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
            if (attackHitbox == null) attackHitbox = transform.Find("Attack Hitbox")?.GetComponent<BoxCollider2D>();
            if (attackHitbox == null) attackHitbox = CreateFallbackAttackHitbox();
            attackHitbox.isTrigger = true;
            attackHitbox.enabled = false;
            _baseAttackHitboxSize = attackHitbox.size;
            _baseAttackHitboxOffset = attackHitbox.offset;
            _startPosition = transform.position;
            ConfigureForSkill(1, 1);
        }

        private void Update()
        {
            // Only a real collider contact whose normal points upward counts as landing.
            // An overlap-circle probe reaches the floor slightly before the body actually lands,
            // which previously reopened jumping while the character was still visibly airborne.
            bool rawGrounded = HasGroundContact();

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
                {
                    _jumpInProgress = false;
                    gameManager.OnPlayerLanded();
                }
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
            if (mouse != null && mouse.leftButton.wasPressedThisFrame
                && gameManager.State == RunnerGameState.Playing)
                _attackBufferedUntil = Time.time + AttackInputBufferSeconds;

            if (canAct && !_isRolling && !_isAttacking
                && Time.time >= _attackReadyAt && Time.time <= _attackBufferedUntil)
            {
                _attackBufferedUntil = -1f;
                StartCoroutine(AttackRoutine());
            }

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
            _attackBufferedUntil = -1f;
            _jumpInProgress = false;
            _hasLeftGround = false;
            _isAttacking = false;
            StopAllCoroutines();
            if (attackHitbox != null) attackHitbox.enabled = false;
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
            float baseAttackCooldown = attackCooldownFrames / Mathf.Max(1f, attackAnimationFramesPerSecond);
            float minimumAttackCooldown = Mathf.Min(attackCooldownFrames, minimumAttackCooldownFrames)
                / Mathf.Max(1f, attackAnimationFramesPerSecond);
            _runtimeAttackCooldown = Mathf.Lerp(baseAttackCooldown, minimumAttackCooldown, progress);
            _runtimeAttackMaximumRange = attackMaximumRange + maximumAttackRangeBonus * progress;
            if (attackHitbox != null)
            {
                float rangeBonus = Mathf.Max(0f, _runtimeAttackMaximumRange - attackMaximumRange);
                attackHitbox.size = new Vector2(_baseAttackHitboxSize.x + rangeBonus, _baseAttackHitboxSize.y);
                attackHitbox.offset = new Vector2(_baseAttackHitboxOffset.x + rangeBonus * 0.5f, _baseAttackHitboxOffset.y);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Respawn") || Time.time < _invincibleUntil || gameManager.State != RunnerGameState.Playing) return;
            RunnerObstacle obstacle = other.GetComponent<RunnerObstacle>();
            if (obstacle != null && obstacle.ObstacleType == RunnerObstacleType.Enemy
                && attackHitbox != null && attackHitbox.enabled) return;
            if (obstacle != null && obstacle.ObstacleType == RunnerObstacleType.Roll && _isRolling) return;
            if (obstacle != null && obstacle.ObstacleType == RunnerObstacleType.Enemy) obstacle.Deactivate();
            ReceiveHit();
        }

        public void ReceiveHit()
        {
            if (Time.time < _invincibleUntil || gameManager.State != RunnerGameState.Playing) return;
            _invincibleUntil = Time.time + invincibilitySeconds;
            CurrentHealth = Mathf.Max(0, CurrentHealth - 1);
            if (CurrentHealth <= 0)
            {
                _isAttacking = false;
                if (attackHitbox != null) attackHitbox.enabled = false;
                animator.ResetTrigger(HurtHash);
                animator.ResetTrigger(AttackHash);
                animator.ResetTrigger(RollHash);
                animator.SetBool(DeadHash, true);
                animator.Play(DeathStateHash, 0, 0f);
            }
            else animator.SetTrigger(HurtHash);
            gameManager.OnPlayerHit();
        }

        private void StartRoll()
        {
            _isRolling = true;
            gameManager.OnPlayerRolled();
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

        private IEnumerator AttackRoutine()
        {
            _isAttacking = true;
            gameManager.OnPlayerAttacked();
            _attackReadyAt = Time.time + _runtimeAttackCooldown;
            animator.SetTrigger(AttackHash);
            bool hit = false;
            bool enteredAttackState = false;
            float waitUntil = Time.time + 0.25f;
            while (gameManager.State == RunnerGameState.Playing && CurrentHealth > 0)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                bool inAttackState = state.fullPathHash == AttackStateHash;
                if (!enteredAttackState)
                {
                    if (!inAttackState)
                    {
                        if (Time.time >= waitUntil) break;
                        yield return null;
                        continue;
                    }
                    enteredAttackState = true;
                    attackHitbox.enabled = true;
                }

                if (!inAttackState) break;
                // The hitbox stays active for the complete Attack animator state.
                // This follows the authored animation duration and does not depend on
                // a manually entered sprite-frame count.
                EvaluateAttackOverlaps(ref hit);
                if (state.normalizedTime >= 1f) break;
                yield return null;
            }
            attackHitbox.enabled = false;
            _isAttacking = false;
            if (!hit && gameManager.State == RunnerGameState.Playing) gameManager.OnAttackMissed();
        }

        private float AttackInputBufferSeconds => attackInputBufferFrames / Mathf.Max(1f, attackAnimationFramesPerSecond);

        private void EvaluateAttackOverlaps(ref bool hit)
        {
            Collider2D[] overlaps = Physics2D.OverlapBoxAll(attackHitbox.bounds.center,
                attackHitbox.bounds.size, attackHitbox.transform.eulerAngles.z);
            foreach (Collider2D overlap in overlaps)
            {
                RunnerObstacle obstacle = overlap != null ? overlap.GetComponentInParent<RunnerObstacle>() : null;
                if (obstacle == null || obstacle.ObstacleType != RunnerObstacleType.Enemy || obstacle.IsAvailable) continue;
                float distance = obstacle.transform.position.x - transform.position.x;
                if (distance < attackMinimumRange || distance > _runtimeAttackMaximumRange) continue;
                if (obstacle.TryDefeat()) hit = true;
            }
        }

        private BoxCollider2D CreateFallbackAttackHitbox()
        {
            GameObject hitboxObject = new GameObject("Attack Hitbox");
            hitboxObject.transform.SetParent(transform, false);
            BoxCollider2D hitbox = hitboxObject.AddComponent<BoxCollider2D>();
            hitbox.isTrigger = true;
            hitbox.size = new Vector2(3f, 1.4f);
            hitbox.offset = new Vector2(1.8f, 0.15f);
            return hitbox;
        }

        private bool HasGroundContact()
        {
            int count = _collider.GetContacts(_groundContacts);
            for (int i = 0; i < count; i++)
            {
                ContactPoint2D contact = _groundContacts[i];
                Collider2D other = contact.collider == _collider ? contact.otherCollider : contact.collider;
                if (other == null || other.isTrigger
                    || (groundLayer.value & (1 << other.gameObject.layer)) == 0)
                    continue;
                // The contact must be on the lower edge of the player's body, not its side.
                if (contact.point.y <= _collider.bounds.min.y + 0.08f) return true;
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        private void OnValidate()
        {
            attackCooldownFrames = Mathf.Max(1, attackCooldownFrames);
            attackInputBufferFrames = Mathf.Max(0, attackInputBufferFrames);
            attackAnimationFramesPerSecond = Mathf.Max(1f, attackAnimationFramesPerSecond);
            minimumAttackCooldownFrames = Mathf.Clamp(minimumAttackCooldownFrames, 1, attackCooldownFrames);
            maxHealth = Mathf.Max(1, maxHealth);
        }
    }
}
