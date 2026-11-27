using IronKnuckle.Data;
using UnityEngine;

namespace IronKnuckle.Robot
{
    public class GarageCameraController : MonoBehaviour
    {
        public Transform robotTarget;
        public float transitionSpeed = 6.5f;

        private Vector3 targetPos = new Vector3(-0.6f, 1.75f, -3.85f);
        private Vector3 targetLook = new Vector3(0.45f, 1.15f, 0f);
        private bool fullViewOverride;

        public void FocusOnFullBody()
        {
            fullViewOverride = false;
            Vector3 root = robotTarget != null ? robotTarget.position : new Vector3(-1.15f, 0f, 0f);
            targetPos = root + new Vector3(0.55f, 1.78f, -3.75f);
            targetLook = root + new Vector3(1.50f, 1.12f, 0f);
        }

        public void FocusOnSlot(PartSlot slot)
        {
            if (fullViewOverride)
            {
                FocusOnFullBody();
                return;
            }
            Vector3 root = robotTarget != null ? robotTarget.position : new Vector3(-1.15f, 0f, 0f);
            switch (slot)
            {
                case PartSlot.Head:
                    targetPos = root + new Vector3(0.32f, 2.08f, -2.05f);
                    targetLook = root + new Vector3(0.75f, 1.85f, 0f);
                    break;
                case PartSlot.Torso:
                    targetPos = root + new Vector3(0.42f, 1.62f, -2.55f);
                    targetLook = root + new Vector3(0.92f, 1.38f, 0f);
                    break;
                case PartSlot.LeftArm:
                    targetPos = root + new Vector3(-0.75f, 1.52f, -2.25f);
                    targetLook = root + new Vector3(0.35f, 1.25f, 0f);
                    break;
                case PartSlot.RightArm:
                    targetPos = root + new Vector3(0.95f, 1.52f, -2.25f);
                    targetLook = root + new Vector3(0.85f, 1.25f, 0f);
                    break;
                case PartSlot.Legs:
                    targetPos = root + new Vector3(0.48f, 0.82f, -2.75f);
                    targetLook = root + new Vector3(0.98f, 0.55f, 0f);
                    break;
                case PartSlot.Core:
                    // Swing behind robot to inspect the Power Core
                    targetPos = root + new Vector3(-0.55f, 1.72f, 2.45f);
                    targetLook = root + new Vector3(-0.75f, 1.40f, 0f);
                    break;
                default:
                    FocusOnFullBody();
                    break;
            }
        }

        public void ToggleFullView(PartSlot currentSlot)
        {
            fullViewOverride = !fullViewOverride;
            if (fullViewOverride) FocusOnFullBody();
            else FocusOnSlot(currentSlot);
        }

        private void LateUpdate()
        {
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * transitionSpeed);
            Quaternion desiredRot = Quaternion.LookRotation(targetLook - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, Time.deltaTime * transitionSpeed);
        }
    }
}
