using System.Collections.Generic;
using IronKnuckle.Data;
using UnityEngine;

namespace IronKnuckle.Combat
{
    public class EnemyAIController : MonoBehaviour
    {
        public FighterController aiFighter;
        public bool aiEnabled = true;

        private AIStyle style = AIStyle.Swarmer;
        private float difficulty = 0.35f;
        private float thinkTimer = 0.8f;
        private float guardReleaseTimer;
        private readonly Queue<PunchType> comboQueue = new Queue<PunchType>();

        public void Configure(AIStyle aiStyle, float aiDifficulty)
        {
            style = aiStyle;
            difficulty = Mathf.Clamp(aiDifficulty, 0.1f, 1.0f);
            thinkTimer = 0.7f;
            comboQueue.Clear();
        }

        private void Update()
        {
            if (!aiEnabled || aiFighter == null || aiFighter.Opponent == null) return;
            if (aiFighter.State == FighterState.KO || aiFighter.State == FighterState.KnockedDown || aiFighter.State == FighterState.Victory)
                return;

            if (guardReleaseTimer > 0f)
            {
                guardReleaseTimer -= Time.deltaTime;
                if (guardReleaseTimer <= 0f) aiFighter.SetGuardHeld(false);
            }

            if (!aiFighter.CanAct()) return;

            // Reactive defense if player is winding up a heavy punch
            var player = aiFighter.Opponent;
            if (player.State == FighterState.Windup && guardReleaseTimer <= 0f)
            {
                float reactChance = difficulty * (style == AIStyle.CounterPuncher ? 0.75f : style == AIStyle.OutBoxer ? 0.55f : 0.35f);
                if (Random.value < reactChance * Time.deltaTime * 12f)
                {
                    if (style == AIStyle.CounterPuncher || style == AIStyle.OutBoxer)
                    {
                        if (Random.value < 0.55f && aiFighter.TrySlip(Random.value > 0.5f))
                        {
                            thinkTimer = 0.35f;
                            return;
                        }
                    }
                    aiFighter.SetGuardHeld(true);
                    guardReleaseTimer = Random.Range(0.42f, 0.75f);
                    return;
                }
            }

            if (comboQueue.Count > 0)
            {
                var nextPunch = comboQueue.Dequeue();
                float windup = GetReadableWindup(nextPunch);
                aiFighter.TryStartPunch(nextPunch, windup);
                thinkTimer = 0.18f;
                return;
            }

            thinkTimer -= Time.deltaTime;
            if (thinkTimer > 0f) return;

            // Special attack check
            if (aiFighter.Special1 != null && aiFighter.Overdrive >= aiFighter.Special1.overdriveCost && Random.value < 0.75f)
            {
                aiFighter.TryStartPunch(PunchType.Special1, Mathf.Lerp(0.62f, 0.42f, difficulty));
                thinkTimer = Mathf.Lerp(1.3f, 0.75f, difficulty);
                return;
            }

            QueueNextCombo();
            thinkTimer = Mathf.Lerp(1.45f, 0.55f, difficulty);
        }

        private float GetReadableWindup(PunchType punch)
        {
            // Generous readable telegraphs for mobile fairness, faster at high leagues
            float slowFactor = Mathf.Lerp(1.45f, 0.92f, difficulty);
            return CombatCalculator.GetWindupDuration(punch, aiFighter.Stats.speed) * slowFactor;
        }

        private void QueueNextCombo()
        {
            float r = Random.value;
            switch (style)
            {
                case AIStyle.Swarmer:
                    comboQueue.Enqueue(PunchType.LeftJab);
                    comboQueue.Enqueue(PunchType.RightCross);
                    if (r < 0.6f) comboQueue.Enqueue(PunchType.LeftHook);
                    break;
                case AIStyle.OutBoxer:
                    comboQueue.Enqueue(PunchType.LeftJab);
                    if (r < 0.5f) comboQueue.Enqueue(PunchType.RightCross);
                    break;
                case AIStyle.Slugger:
                    if (r < 0.45f) comboQueue.Enqueue(PunchType.Uppercut);
                    else
                    {
                        comboQueue.Enqueue(PunchType.LeftHook);
                        comboQueue.Enqueue(PunchType.RightHook);
                    }
                    break;
                case AIStyle.CounterPuncher:
                    comboQueue.Enqueue(PunchType.RightCross);
                    if (r < 0.65f) comboQueue.Enqueue(PunchType.Uppercut);
                    break;
            }
        }
    }
}
