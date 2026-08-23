using UnityEngine;
using UnityEngine.InputSystem;

namespace StreamOn.Minigames.Runner
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class RunnerRoomPlayerController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 4.5f;
        [SerializeField, Min(0.1f)] private float turnSpeed = 14f;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Animator characterAnimator;
        [SerializeField, Min(0f)] private float animationBlendSeconds = 0.12f;
        private CharacterController _controller;
        private bool _inputLocked;
        private bool _isMoving;
        private static readonly int IdleState = Animator.StringToHash("Base Layer.idle");
        private static readonly int MoveState = Animator.StringToHash("Base Layer.move");
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");

        public bool InputLocked
        {
            get => _inputLocked;
            set
            {
                _inputLocked = value;
                if (value) SetMoving(false);
            }
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>(true);
            if (characterAnimator != null)
            {
                characterAnimator.applyRootMotion = false;
                characterAnimator.SetFloat(SpeedParameter, 1f);
                characterAnimator.Play(IdleState, 0, 0f);
            }
        }

        private void Update()
        {
            if (InputLocked || Keyboard.current == null)
            {
                SetMoving(false);
                return;
            }
            Vector2 input = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (Keyboard.current.dKey.isPressed) input.x += 1f;
            if (Keyboard.current.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            Vector3 movement = forward * input.y + right * input.x;
            _controller.Move((movement * moveSpeed + Physics.gravity) * Time.deltaTime);
            SetMoving(movement.sqrMagnitude > 0.01f);
            if (movement.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(movement);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);
            }
        }

        private void SetMoving(bool moving)
        {
            if (characterAnimator == null || _isMoving == moving) return;
            _isMoving = moving;
            characterAnimator.SetFloat(SpeedParameter, 1f);
            characterAnimator.CrossFadeInFixedTime(moving ? MoveState : IdleState, animationBlendSeconds);
        }
    }
}
