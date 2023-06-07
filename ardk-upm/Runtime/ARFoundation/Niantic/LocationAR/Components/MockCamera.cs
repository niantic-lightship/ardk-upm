#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

namespace Niantic.Lightship.AR.Subsystems
{
    ///This is the mock location camera.  It is so the user can use WASD to navigate their scene as though they were moving around their phone.
    /// If applicable, the trackedposedriver is disabled to stop camera restrictions so this script can run
    /// This script was recyclced from ardk classic
    public class MockCamera : MonoBehaviour
    {
        [Tooltip("The camera move speed")] [SerializeField]
        private float moveSpeed = 5.0f;

        [Tooltip("The camera look speed")] [SerializeField]
        private float lookSpeed = 150.0f;

        [Tooltip("The camera scroll direction")] [SerializeField]
        private float scrollDirection = 1.0f;

#if ENABLE_INPUT_SYSTEM
        private Transform cameraTransform;
        private Mouse currentMouse;
        private Keyboard currentKeyboard;
        private bool startedDragging;

        private void Awake()
        {
            cameraTransform = transform;
            currentMouse = Mouse.current;
            currentKeyboard = Keyboard.current;
        }

        private void Update()
        {
            RotateScroll();
            RotateDrag();
            Move();
        }

        private void Move()
        {
            cameraTransform.position += Time.deltaTime * moveSpeed * GetMoveInput();
        }

        private void RotateDrag()
        {
            var isMouseInGameWindow = IsMouseInGameWindow();
            // If the user has started dragging, allow the mouse to work from outside the game window
            if (!isMouseInGameWindow && !startedDragging)
            {
                return;
            }

            // Only allow a drag to start from within the game window
            if (currentMouse.rightButton.wasPressedThisFrame && isMouseInGameWindow)
            {
                startedDragging = true;
            }

            if (currentMouse.rightButton.isPressed && startedDragging)
            {
                var mouseDeltaDirection =
                    new Vector2
                        (currentMouse.delta.x.ReadValue(), -1 * currentMouse.delta.y.ReadValue());

                Rotate(mouseDeltaDirection, lookSpeed);
            }
            // Once the mouse button is released, stop rotating
            else
            {
                startedDragging = false;
            }
        }

        private void RotateScroll()
        {
            if (!IsMouseInGameWindow())
            {
                return;
            }

            if (Mouse.current.scroll.ReadValue().magnitude >
                0.01f) // not sure why but if this is 0, the camera tracks the mouse cursor.
            {
                var mouseScrollDelta = new Vector2
                    (-currentMouse.scroll.x.ReadValue(), currentMouse.scroll.y.ReadValue());

                Rotate
                (
                    direction: mouseScrollDelta * scrollDirection,
                    speed: lookSpeed / 10
                );
            }
        }

        private void Rotate(Vector2 direction, float speed)
        {
            var pitchVector = Time.deltaTime * speed * direction.y;
            var position = cameraTransform.position;
            cameraTransform.RotateAround(position, cameraTransform.right, pitchVector);

            var yawVector = Time.deltaTime * speed * direction.x;
            cameraTransform.RotateAround(position, Vector3.up, yawVector);
        }

        private Vector3 GetMoveInput()
        {
            var input = Vector3.zero;

            var isWPressed = currentKeyboard.wKey.isPressed ||
                currentKeyboard.wKey.wasPressedThisFrame;

            if (isWPressed)
            {
                input += cameraTransform.forward;
            }

            var isSPressed = currentKeyboard.sKey.isPressed ||
                currentKeyboard.sKey.wasPressedThisFrame;

            if (isSPressed)
            {
                input -= cameraTransform.forward;
            }

            var isAPressed = currentKeyboard.aKey.isPressed ||
                currentKeyboard.aKey.wasPressedThisFrame;

            if (isAPressed)
            {
                input -= cameraTransform.right;
            }

            var isDPressed = currentKeyboard.dKey.isPressed ||
                currentKeyboard.dKey.wasPressedThisFrame;

            if (isDPressed)
            {
                input += cameraTransform.right;
            }

            var isQPressed = currentKeyboard.qKey.isPressed ||
                currentKeyboard.qKey.wasPressedThisFrame;

            if (isQPressed)
            {
                input -= Vector3.up;
            }

            var isEPressed = currentKeyboard.eKey.isPressed ||
                currentKeyboard.eKey.wasPressedThisFrame;

            if (isEPressed)
            {
                input += Vector3.up;
            }

            return input;
        }

        private bool IsMouseInGameWindow()
        {
            if (Input.mousePosition.x <= 0 ||
                Input.mousePosition.x >= Screen.width ||
                Input.mousePosition.y <= 0 ||
                Input.mousePosition.y >= Screen.height)
            {
                return false;
            }

            return true;
        }
#endif
    }
}
#endif
