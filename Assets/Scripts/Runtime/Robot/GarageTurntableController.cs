using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace IronKnuckle.Robot
{
    public class GarageTurntableController : MonoBehaviour
    {
        public Transform targetRoot;
        public float idleSpinSpeed = 12f;
        public float dragSensitivity = 0.45f;

        private bool dragging;
        private float lastPointerX;
        private float resumeIdleTimer;

        private void Update()
        {
            if (targetRoot == null) return;

            var pointer = Pointer.current;
            if (pointer != null)
            {
                if (pointer.press.wasPressedThisFrame)
                {
                    if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                    {
                        dragging = true;
                        lastPointerX = pointer.position.ReadValue().x;
                    }
                }
                else if (dragging && pointer.press.isPressed)
                {
                    float curX = pointer.position.ReadValue().x;
                    float dx = curX - lastPointerX;
                    lastPointerX = curX;
                    targetRoot.Rotate(Vector3.up, -dx * dragSensitivity, Space.World);
                    resumeIdleTimer = 1.5f;
                }
                else if (pointer.press.wasReleasedThisFrame)
                {
                    dragging = false;
                }
            }

            if (!dragging)
            {
                if (resumeIdleTimer > 0f) resumeIdleTimer -= Time.deltaTime;
                else targetRoot.Rotate(Vector3.up, idleSpinSpeed * Time.deltaTime, Space.World);
            }
        }
    }
}
