using UnityEngine;

namespace IronKnuckle.Combat
{
    public class CombatCameraController : MonoBehaviour
    {
        public Transform playerTarget;
        public Transform opponentTarget;

        private Vector3 basePosition = new Vector3(-1.35f, 2.25f, -3.55f);
        private Quaternion baseRotation;
        private float baseFov = 46f;
        private float trauma;
        private float fovPunch;
        private bool koOrbitActive;
        private float koOrbitAngle;
        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam != null) baseFov = cam.fieldOfView;
            basePosition = transform.position;
            baseRotation = transform.rotation;
        }

        public void AddTrauma(float amount, float fovKick = 0f)
        {
            trauma = Mathf.Clamp01(trauma + amount);
            fovPunch = Mathf.Max(fovPunch, fovKick);
        }

        public void SetKOOrbit(bool active)
        {
            koOrbitActive = active;
            koOrbitAngle = 0f;
        }

        private void LateUpdate()
        {
            if (koOrbitActive && opponentTarget != null)
            {
                koOrbitAngle += Time.unscaledDeltaTime * 32f;
                Vector3 center = (playerTarget != null ? (playerTarget.position + opponentTarget.position) * 0.5f : opponentTarget.position) + Vector3.up * 1.1f;
                Vector3 offset = Quaternion.Euler(14f, koOrbitAngle - 20f, 0f) * new Vector3(0f, 0.8f, -4.2f);
                transform.position = Vector3.Lerp(transform.position, center + offset, Time.unscaledDeltaTime * 5f);
                transform.LookAt(center);
                return;
            }

            Vector3 targetPos = basePosition;
            if (playerTarget != null && opponentTarget != null)
            {
                Vector3 lookTarget = Vector3.Lerp(playerTarget.position, opponentTarget.position, 0.65f) + Vector3.up * 1.35f;
                baseRotation = Quaternion.LookRotation(lookTarget - basePosition);
            }

            if (trauma > 0f)
            {
                float shake = trauma * trauma;
                Vector3 jitter = new Vector3(
                    (Mathf.PerlinNoise(Time.unscaledTime * 35f, 0f) - 0.5f) * 0.45f * shake,
                    (Mathf.PerlinNoise(0f, Time.unscaledTime * 35f) - 0.5f) * 0.45f * shake,
                    0f);
                transform.position = targetPos + jitter;
                transform.rotation = baseRotation * Quaternion.Euler(jitter.y * 12f, jitter.x * 12f, 0f);
                trauma = Mathf.Max(0f, trauma - Time.unscaledDeltaTime * 2.4f);
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.unscaledDeltaTime * 10f);
                transform.rotation = Quaternion.Slerp(transform.rotation, baseRotation, Time.unscaledDeltaTime * 10f);
            }

            if (cam != null)
            {
                fovPunch = Mathf.Max(0f, fovPunch - Time.unscaledDeltaTime * 28f);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFov + fovPunch, Time.unscaledDeltaTime * 14f);
            }
        }
    }
}
