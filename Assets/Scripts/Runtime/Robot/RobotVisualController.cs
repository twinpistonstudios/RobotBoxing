using IronKnuckle.Data;
using UnityEngine;

namespace IronKnuckle.Robot
{
    [RequireComponent(typeof(RobotBuilder))]
    public class RobotVisualController : MonoBehaviour
    {
        private RobotBuilder builder;

        private FighterState visualState = FighterState.Idle;
        private PunchType activePunch = PunchType.LeftJab;
        private float stateTimer;
        private float stateDuration = 0.25f;
        private float hitFlash;
        private float bouncePop;
        private float idlePhase;
        private bool isShowcaseMode;
        private bool playingDemoCombo;
        private float demoTimer;

        // Telegraph ring around attacking glove
        private GameObject telegraphRing;
        private Renderer telegraphRenderer;

        public void Initialize(bool showcaseMode = false)
        {
            builder = GetComponent<RobotBuilder>();
            isShowcaseMode = showcaseMode;
            idlePhase = Random.Range(0f, 6.28f);

            if (!showcaseMode && telegraphRing == null)
            {
                telegraphRing = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                telegraphRing.name = "TelegraphGlow";
                var col = telegraphRing.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                telegraphRenderer = telegraphRing.GetComponent<Renderer>();
                telegraphRenderer.sharedMaterial = new Material(shader);
                telegraphRing.SetActive(false);
            }
        }

        public void TriggerPartSwapBounce()
        {
            bouncePop = 1f;
        }

        public void TriggerDemoCombo()
        {
            playingDemoCombo = true;
            demoTimer = 0f;
        }

        public void TriggerHitFlash(float intensity = 1f)
        {
            hitFlash = Mathf.Clamp01(intensity);
        }

        public void SetCombatPose(FighterState state, PunchType punch, float normalizedProgress, float duration)
        {
            if (visualState != state)
            {
                visualState = state;
                stateTimer = 0f;
            }
            activePunch = punch;
            stateDuration = Mathf.Max(0.05f, duration);
            stateTimer = normalizedProgress * stateDuration;
            UpdateVisorAndTelegraph(normalizedProgress);
        }

        private void UpdateVisorAndTelegraph(float norm)
        {
            if (builder == null) return;
            var visor = builder.VisorController;
            if (visor != null)
            {
                switch (visualState)
                {
                    case FighterState.Windup:
                    case FighterState.StrikeActive:
                        visor.ApplyExpression(VisorExpression.Angry);
                        break;
                    case FighterState.HitStun:
                    case FighterState.GuardBroken:
                        visor.ApplyExpression(VisorExpression.Hit);
                        break;
                    case FighterState.Stunned:
                    case FighterState.KnockedDown:
                        visor.ApplyExpression(VisorExpression.Stunned);
                        break;
                    case FighterState.KO:
                        visor.ApplyExpression(VisorExpression.KO);
                        break;
                    case FighterState.Victory:
                        visor.ApplyExpression(VisorExpression.Victory);
                        break;
                    default:
                        visor.ApplyExpression(VisorExpression.Neutral);
                        break;
                }
            }

            if (telegraphRing != null)
            {
                bool showTele = visualState == FighterState.Windup;
                telegraphRing.SetActive(showTele);
                if (showTele)
                {
                    bool isLeft = activePunch == PunchType.LeftJab || activePunch == PunchType.LeftHook;
                    Transform tip = isLeft ? builder.LeftGloveTip : builder.RightGloveTip;
                    if (tip != null)
                    {
                        telegraphRing.transform.position = tip.position;
                        float pulse = 0.28f + Mathf.Sin(norm * Mathf.PI) * 0.22f;
                        telegraphRing.transform.localScale = Vector3.one * pulse;
                        Color c = (activePunch == PunchType.Uppercut || activePunch == PunchType.Special1 || activePunch == PunchType.Special2)
                            ? new Color(1f, 0.18f, 0.18f, 0.85f)
                            : (activePunch == PunchType.LeftHook || activePunch == PunchType.RightHook
                                ? new Color(1f, 0.6f, 0.1f, 0.85f)
                                : new Color(1f, 0.92f, 0.2f, 0.8f));
                        telegraphRenderer.sharedMaterial.color = c;
                    }
                }
            }

            bool emitL = visualState == FighterState.StrikeActive && (activePunch == PunchType.LeftJab || activePunch == PunchType.LeftHook || activePunch == PunchType.Special1 || activePunch == PunchType.Special2);
            bool emitR = visualState == FighterState.StrikeActive && (activePunch == PunchType.RightCross || activePunch == PunchType.RightHook || activePunch == PunchType.Uppercut || activePunch == PunchType.Special1 || activePunch == PunchType.Special2);
            builder.SetTrailsEmitting(emitL, emitR);
        }

        private void LateUpdate()
        {
            if (builder == null || builder.PelvisPivot == null) return;

            if (playingDemoCombo)
            {
                demoTimer += Time.deltaTime;
                if (demoTimer < 0.25f)
                    SetCombatPose(FighterState.StrikeActive, PunchType.LeftJab, demoTimer / 0.25f, 0.25f);
                else if (demoTimer < 0.55f)
                    SetCombatPose(FighterState.StrikeActive, PunchType.RightCross, (demoTimer - 0.25f) / 0.30f, 0.30f);
                else if (demoTimer < 0.95f)
                    SetCombatPose(FighterState.StrikeActive, PunchType.Uppercut, (demoTimer - 0.55f) / 0.40f, 0.40f);
                else
                {
                    playingDemoCombo = false;
                    SetCombatPose(FighterState.Idle, PunchType.LeftJab, 0f, 0.2f);
                }
            }

            idlePhase += Time.deltaTime * (isShowcaseMode ? 2.4f : 4.2f);
            float t = stateDuration > 0.001f ? Mathf.Clamp01(stateTimer / stateDuration) : 0f;

            // Decay hit flash & swap bounce
            if (hitFlash > 0f)
            {
                hitFlash = Mathf.Max(0f, hitFlash - Time.deltaTime * 6.5f);
                builder.SetHitFlash(hitFlash);
            }
            if (bouncePop > 0f)
            {
                bouncePop = Mathf.Max(0f, bouncePop - Time.deltaTime * 4.5f);
                float s = 1f + Mathf.Sin(bouncePop * Mathf.PI) * 0.12f;
                transform.localScale = Vector3.one * s;
            }
            else
            {
                transform.localScale = Vector3.one;
            }

            // Base boxing stance targets
            float bob = Mathf.Sin(idlePhase) * 0.035f;
            Vector3 pelvisPos = new Vector3(0f, 0.88f + bob, 0f);
            Vector3 pelvisRot = new Vector3(0f, 18f, 0f);
            Vector3 torsoRot = new Vector3(8f, -12f, 0f);
            Vector3 headRot = new Vector3(-4f, -6f, 0f);
            Vector3 lShoulderRot = new Vector3(-68f, 22f, 10f);
            Vector3 lElbowRot = new Vector3(-72f, 0f, 0f);
            Vector3 rShoulderRot = new Vector3(-68f, -22f, -10f);
            Vector3 rElbowRot = new Vector3(-78f, 0f, 0f);
            Vector3 lLegRot = new Vector3(-6f, 0f, 0f);
            Vector3 rLegRot = new Vector3(8f, 0f, 0f);

            float guardBob = Mathf.Sin(idlePhase * 2f) * 4f;
            lShoulderRot.x += guardBob;
            rShoulderRot.x -= guardBob;

            switch (visualState)
            {
                case FighterState.Guarding:
                    torsoRot = new Vector3(14f, 0f, 0f);
                    pelvisPos.y = 0.82f;
                    lShoulderRot = new Vector3(-92f, 36f, 8f);
                    lElbowRot = new Vector3(-92f, 0f, 0f);
                    rShoulderRot = new Vector3(-92f, -36f, -8f);
                    rElbowRot = new Vector3(-92f, 0f, 0f);
                    break;

                case FighterState.SlipLeft:
                    float sl = Mathf.Sin(t * Mathf.PI);
                    pelvisPos.x = -0.36f * sl;
                    pelvisPos.y = 0.88f - 0.14f * sl;
                    torsoRot = new Vector3(16f, -18f, 24f * sl);
                    headRot = new Vector3(-6f, 14f, -10f * sl);
                    break;

                case FighterState.SlipRight:
                    float sr = Mathf.Sin(t * Mathf.PI);
                    pelvisPos.x = 0.36f * sr;
                    pelvisPos.y = 0.88f - 0.14f * sr;
                    torsoRot = new Vector3(16f, 18f, -24f * sr);
                    headRot = new Vector3(-6f, -14f, 10f * sr);
                    break;

                case FighterState.Windup:
                    ApplyWindupPose(t, ref torsoRot, ref lShoulderRot, ref lElbowRot, ref rShoulderRot, ref rElbowRot, ref pelvisPos);
                    break;

                case FighterState.StrikeActive:
                    ApplyStrikePose(t, ref pelvisPos, ref torsoRot, ref lShoulderRot, ref lElbowRot, ref rShoulderRot, ref rElbowRot);
                    break;

                case FighterState.Recovery:
                    float rec = 1f - t;
                    ApplyStrikePose(rec * 0.45f, ref pelvisPos, ref torsoRot, ref lShoulderRot, ref lElbowRot, ref rShoulderRot, ref rElbowRot);
                    break;

                case FighterState.HitStun:
                    float recoil = Mathf.Sin(t * Mathf.PI);
                    pelvisPos.z = -0.18f * recoil;
                    torsoRot = new Vector3(-24f * recoil, 15f * recoil, 0f);
                    headRot = new Vector3(-32f * recoil, 18f * recoil, 0f);
                    lShoulderRot.x += 28f * recoil;
                    rShoulderRot.x += 28f * recoil;
                    break;

                case FighterState.GuardBroken:
                    float gb = Mathf.Sin(t * Mathf.PI);
                    torsoRot = new Vector3(-18f * gb, 0f, 0f);
                    lShoulderRot = new Vector3(-30f, -45f, 35f);
                    rShoulderRot = new Vector3(-30f, 45f, -35f);
                    break;

                case FighterState.Stunned:
                    float sway = Mathf.Sin(Time.time * 9f);
                    torsoRot = new Vector3(-6f, sway * 14f, sway * 16f);
                    headRot = new Vector3(10f, sway * 22f, -sway * 18f);
                    lShoulderRot = new Vector3(-32f, 10f, 18f);
                    rShoulderRot = new Vector3(-32f, -10f, -18f);
                    break;

                case FighterState.KnockedDown:
                case FighterState.KO:
                    float fall = visualState == FighterState.KO ? Mathf.Clamp01(t * 1.4f) : Mathf.Clamp01(t * 1.5f);
                    pelvisPos = Vector3.Lerp(new Vector3(0f, 0.88f, 0f), new Vector3(0f, 0.24f, -0.55f), fall);
                    pelvisRot = Vector3.Lerp(Vector3.zero, new Vector3(-84f, 0f, 12f), fall);
                    torsoRot = new Vector3(-10f * fall, 0f, 0f);
                    lShoulderRot = new Vector3(-15f, 20f, 40f * fall);
                    rShoulderRot = new Vector3(-15f, -20f, -40f * fall);
                    break;

                case FighterState.Victory:
                    float pump = Mathf.Abs(Mathf.Sin(Time.time * 5.2f));
                    pelvisPos.y = 0.88f + pump * 0.08f;
                    torsoRot = new Vector3(-8f, Mathf.Sin(Time.time * 2.5f) * 14f, 0f);
                    lShoulderRot = new Vector3(-155f + pump * 20f, 10f, 18f);
                    lElbowRot = new Vector3(-25f, 0f, 0f);
                    rShoulderRot = new Vector3(-155f + (1f - pump) * 20f, -10f, -18f);
                    rElbowRot = new Vector3(-25f, 0f, 0f);
                    break;
            }

            float lerpSpeed = Time.deltaTime * 22f;
            builder.PelvisPivot.localPosition = Vector3.Lerp(builder.PelvisPivot.localPosition, pelvisPos, lerpSpeed);
            builder.PelvisPivot.localRotation = Quaternion.Slerp(builder.PelvisPivot.localRotation, Quaternion.Euler(pelvisRot), lerpSpeed);
            builder.TorsoPivot.localRotation = Quaternion.Slerp(builder.TorsoPivot.localRotation, Quaternion.Euler(torsoRot), lerpSpeed);
            builder.HeadPivot.localRotation = Quaternion.Slerp(builder.HeadPivot.localRotation, Quaternion.Euler(headRot), lerpSpeed);
            builder.LeftShoulderPivot.localRotation = Quaternion.Slerp(builder.LeftShoulderPivot.localRotation, Quaternion.Euler(lShoulderRot), lerpSpeed);
            builder.LeftElbowPivot.localRotation = Quaternion.Slerp(builder.LeftElbowPivot.localRotation, Quaternion.Euler(lElbowRot), lerpSpeed);
            builder.RightShoulderPivot.localRotation = Quaternion.Slerp(builder.RightShoulderPivot.localRotation, Quaternion.Euler(rShoulderRot), lerpSpeed);
            builder.RightElbowPivot.localRotation = Quaternion.Slerp(builder.RightElbowPivot.localRotation, Quaternion.Euler(rElbowRot), lerpSpeed);
            builder.LeftLegPivot.localRotation = Quaternion.Slerp(builder.LeftLegPivot.localRotation, Quaternion.Euler(lLegRot), lerpSpeed);
            builder.RightLegPivot.localRotation = Quaternion.Slerp(builder.RightLegPivot.localRotation, Quaternion.Euler(rLegRot), lerpSpeed);
        }

        private void ApplyWindupPose(float t, ref Vector3 torsoRot, ref Vector3 lSh, ref Vector3 lEl, ref Vector3 rSh, ref Vector3 rEl, ref Vector3 pelvisPos)
        {
            switch (activePunch)
            {
                case PunchType.LeftJab:
                    torsoRot = new Vector3(8f, -22f * t, 0f);
                    lSh = new Vector3(-62f, 10f, 22f * t);
                    lEl = new Vector3(-98f, 0f, 0f);
                    break;
                case PunchType.RightCross:
                    torsoRot = new Vector3(10f, 28f * t, 0f);
                    rSh = new Vector3(-55f, -35f * t, -25f * t);
                    rEl = new Vector3(-102f, 0f, 0f);
                    break;
                case PunchType.LeftHook:
                    torsoRot = new Vector3(12f, -38f * t, 10f * t);
                    lSh = new Vector3(-76f, -55f * t, 60f * t);
                    lEl = new Vector3(-82f, 0f, 0f);
                    break;
                case PunchType.RightHook:
                    torsoRot = new Vector3(12f, 38f * t, -10f * t);
                    rSh = new Vector3(-76f, 55f * t, -60f * t);
                    rEl = new Vector3(-82f, 0f, 0f);
                    break;
                case PunchType.Uppercut:
                case PunchType.Special1:
                    pelvisPos.y = 0.88f - 0.16f * t;
                    torsoRot = new Vector3(22f * t, 24f * t, 0f);
                    rSh = new Vector3(-15f, -15f, -22f);
                    rEl = new Vector3(-105f, 0f, 0f);
                    break;
                case PunchType.Special2:
                    pelvisPos.y = 0.88f - 0.14f * t;
                    torsoRot = new Vector3(18f * t, 0f, 0f);
                    lSh = new Vector3(-35f, 40f * t, 45f * t);
                    rSh = new Vector3(-35f, -40f * t, -45f * t);
                    break;
            }
        }

        private void ApplyStrikePose(float t, ref Vector3 pelvisPos, ref Vector3 torsoRot, ref Vector3 lSh, ref Vector3 lEl, ref Vector3 rSh, ref Vector3 rEl)
        {
            float snap = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI * 0.5f);
            pelvisPos.z = 0.22f * snap;

            switch (activePunch)
            {
                case PunchType.LeftJab:
                    torsoRot = new Vector3(12f, 22f * snap, 0f);
                    lSh = new Vector3(-88f, 6f, 4f);
                    lEl = new Vector3(-6f, 0f, 0f);
                    break;
                case PunchType.RightCross:
                    torsoRot = new Vector3(14f, -34f * snap, 0f);
                    rSh = new Vector3(-90f, -6f, -4f);
                    rEl = new Vector3(-5f, 0f, 0f);
                    break;
                case PunchType.LeftHook:
                    torsoRot = new Vector3(10f, 42f * snap, -8f * snap);
                    lSh = new Vector3(-86f, 48f * snap, 25f);
                    lEl = new Vector3(-65f, 0f, 0f);
                    break;
                case PunchType.RightHook:
                    torsoRot = new Vector3(10f, -42f * snap, 8f * snap);
                    rSh = new Vector3(-86f, -48f * snap, -25f);
                    rEl = new Vector3(-65f, 0f, 0f);
                    break;
                case PunchType.Uppercut:
                case PunchType.Special1:
                    pelvisPos.y = 0.96f;
                    torsoRot = new Vector3(-16f * snap, -22f * snap, 0f);
                    rSh = new Vector3(-135f * snap, -12f, -6f);
                    rEl = new Vector3(-45f, 0f, 0f);
                    break;
                case PunchType.Special2:
                    float flurry = Mathf.Sin(t * Mathf.PI * 4f);
                    torsoRot = new Vector3(14f, flurry * 28f, 0f);
                    lSh = new Vector3(-92f, 12f, 6f);
                    lEl = new Vector3(flurry > 0 ? -6f : -75f, 0f, 0f);
                    rSh = new Vector3(-92f, -12f, -6f);
                    rEl = new Vector3(flurry <= 0 ? -6f : -75f, 0f, 0f);
                    break;
            }
        }
    }
}
