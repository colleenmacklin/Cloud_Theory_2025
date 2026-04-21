using UnityEngine;
using UnityEngine.InputSystem;

namespace Synthic
{
    /// <summary>
    /// Player paddle — movement only.
    /// Ball spawning is handled entirely by PongDuelingSequencer.
    /// Player 1 (left)  : W / S
    /// Player 2 (right) : Up / Down arrows
    /// </summary>
    public class PongPaddle : MonoBehaviour
    {
        public enum PlayerSide { Left, Right }

        [Header("Player")]
        [SerializeField] public PlayerSide side = PlayerSide.Left;
        [SerializeField, Range(2f, 20f)] private float moveSpeed = 8f;

        [Header("Arena Bounds")]
        [SerializeField] private float arenaHalfHeight  = 4.5f;
        [SerializeField] private float paddleHalfHeight = 1f;

        private void Update()
        {
            if (Keyboard.current == null) return;
            HandleMovement();
        }

        private void HandleMovement()
        {
            float input = 0f;

            if (side == PlayerSide.Left)
            {
                if (Keyboard.current.wKey.isPressed) input =  1f;
                if (Keyboard.current.sKey.isPressed) input = -1f;
            }
            else
            {
                if (Keyboard.current.upArrowKey.isPressed)   input =  1f;
                if (Keyboard.current.downArrowKey.isPressed) input = -1f;
            }

            Vector3 pos = transform.position;
            pos.y = Mathf.Clamp(
                pos.y + input * moveSpeed * Time.deltaTime,
                -arenaHalfHeight + paddleHalfHeight,
                 arenaHalfHeight - paddleHalfHeight);
            transform.position = pos;
        }
    }
}
