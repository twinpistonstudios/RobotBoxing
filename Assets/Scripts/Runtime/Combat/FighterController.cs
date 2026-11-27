using System;
using IronKnuckle.Data;
using IronKnuckle.Robot;
using UnityEngine;

namespace IronKnuckle.Combat
{
    [RequireComponent(typeof(RobotBuilder))]
    [RequireComponent(typeof(RobotVisualController))]
    public class FighterController : MonoBehaviour
    {
        public string FighterName { get; private set; }
        public bool IsPlayer { get; private set; }
        public RobotStats Stats { get; private set; }

        public float CurrentHp { get; private set; }
        public float MaxHp => Mathf.Max(1f, Stats.maxHp);
        public float CurrentStamina { get; private set; }
        public float MaxStamina => Mathf.Max(20f, Stats.stamina);
        public float GuardStability { get; private set; }
        public float MaxGuardStability => 100f;
        public float Overdrive { get; private set; }
        public int ComboCount { get; private set; }
        public int KnockdownCount { get; private set; }
        public bool IsOverheated { get; private set; }

        public FighterState State { get; private set; } = FighterState.Idle;
        public PunchType ActivePunch { get; private set; }
        public bool HasCounterWindow => counterWindowTimer > 0f;
        public float StateProgress => stateDuration > 0.001f ? Mathf.Clamp01(stateTimer / stateDuration) : 0f;

        public CombatAbilityData Special1 { get; private set; }
        public CombatAbilityData Special2 { get; private set; }
        public CombatAbilityData PassiveMod { get; private set; }

        public RobotBuilder Builder { get; private set; }
        public RobotVisualController Visuals { get; private set; }
        public FighterController Opponent { get; set; }

        public event Action<FighterController, FighterController, PunchType, HitResolution> OnAttackLanded;
        public event Action<FighterController> OnKnockedDown;
        public event Action<FighterController> OnOverheated;

        private float stateTimer;
        private float stateDuration = 0.25f;
        private float guardEnterTime = -10f;
        private float counterWindowTimer;
        private float overheatTimer;
        private bool strikeEvaluated;
        private bool wantGuardHeld;
        private float windupOverride = -1f;

        public void InitializeFighter(
            string fighterName,
            bool isPlayer,
            RobotStats stats,
            CombatAbilityData special1,
            CombatAbilityData special2,
            CombatAbilityData passive)
        {
            FighterName = fighterName;
            IsPlayer = isPlayer;
            Stats = stats;
            Special1 = special1;
            Special2 = special2;
            PassiveMod = passive;

            CurrentHp = MaxHp;
            CurrentStamina = MaxStamina;
            GuardStability = MaxGuardStability;
            Overdrive = 0f;
            ComboCount = 0;
            KnockdownCount = 0;
            IsOverheated = false;
            State = FighterState.Idle;

            Builder = GetComponent<RobotBuilder>();
            Visuals = GetComponent<RobotVisualController>();
            Visuals.Initialize(false);
            Visuals.SetCombatPose(FighterState.Idle, PunchType.LeftJab, 0f, 0.2f);
        }

        public void ResetForNewRound(bool fullHealth = false)
        {
            if (fullHealth) CurrentHp = MaxHp;
            else CurrentHp = Mathf.Min(MaxHp, CurrentHp + MaxHp * 0.15f);

            CurrentStamina = MaxStamina;
            GuardStability = MaxGuardStability;
            IsOverheated = false;
            ComboCount = 0;
            counterWindowTimer = 0f;
            wantGuardHeld = false;
            SetState(FighterState.Idle, 0.2f);
        }

        public void RecoverFromKnockdown(float healthFraction = 0.28f)
        {
            CurrentHp = Mathf.Max(20f, MaxHp * healthFraction);
            CurrentStamina = MaxStamina * 0.85f;
            GuardStability = MaxGuardStability;
            IsOverheated = false;
            wantGuardHeld = false;
            SetState(FighterState.Idle, 0.25f);
        }

        public void SetVictoryPose()
        {
            wantGuardHeld = false;
            SetState(FighterState.Victory, 1.0f);
        }

        public bool CanAct()
        {
            return State == FighterState.Idle || State == FighterState.Guarding || State == FighterState.Recovery;
        }

        public void SetGuardHeld(bool held)
        {
            wantGuardHeld = held;
            if (held && (State == FighterState.Idle || State == FighterState.Recovery))
            {
                guardEnterTime = Time.time;
                SetState(FighterState.Guarding, 0.2f);
            }
            else if (!held && State == FighterState.Guarding)
            {
                SetState(FighterState.Idle, 0.15f);
            }
        }

        public bool TrySlip(bool left)
        {
            if (IsOverheated || (!CanAct() && State != FighterState.Guarding)) return false;
            if (CurrentStamina < 10f) return false;

            wantGuardHeld = false;
            CurrentStamina -= 10f;
            SetState(left ? FighterState.SlipLeft : FighterState.SlipRight, 0.34f);
            return true;
        }

        public bool TryStartPunch(PunchType punch, float customWindup = -1f)
        {
            if (!CanAct()) return false;

            if (punch == PunchType.Special1 || punch == PunchType.Special2)
            {
                var ab = punch == PunchType.Special1 ? Special1 : Special2;
                float cost = ab != null ? ab.overdriveCost : 50f;
                if (Overdrive < cost) return false;
                Overdrive -= cost;
            }
            else
            {
                if (IsOverheated) return false;
                float stamCost = CombatCalculator.GetPunchStaminaCost(punch);
                CurrentStamina -= stamCost;
                if (CurrentStamina <= 0f)
                {
                    CurrentStamina = 0f;
                    TriggerOverheat();
                }
            }

            wantGuardHeld = false;
            ActivePunch = punch;
            strikeEvaluated = false;
            windupOverride = customWindup;
            float windup = customWindup > 0f ? customWindup : CombatCalculator.GetWindupDuration(punch, Stats.speed);
            SetState(FighterState.Windup, windup);
            return true;
        }

        private void TriggerOverheat()
        {
            IsOverheated = true;
            overheatTimer = 1.65f;
            OnOverheated?.Invoke(this);
        }

        public void TickCombat(float dt)
        {
            if (State == FighterState.KO || State == FighterState.Victory || State == FighterState.KnockedDown)
            {
                stateTimer += dt;
                Visuals?.SetCombatPose(State, ActivePunch, StateProgress, stateDuration);
                return;
            }

            if (counterWindowTimer > 0f) counterWindowTimer = Mathf.Max(0f, counterWindowTimer - dt);

            if (IsOverheated)
            {
                overheatTimer -= dt;
                if (overheatTimer <= 0f)
                {
                    IsOverheated = false;
                    CurrentStamina = MaxStamina * 0.5f;
                }
            }
            else if (State == FighterState.Idle || State == FighterState.Guarding)
            {
                float regen = (18f + (PassiveMod != null ? PassiveMod.passiveStaminaRegenBonus : 0f)) * (State == FighterState.Guarding ? 0.35f : 1f);
                CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + regen * dt);
                if (State == FighterState.Idle)
                {
                    GuardStability = Mathf.Min(MaxGuardStability, GuardStability + 14f * dt);
                }
            }

            stateTimer += dt;

            if (State == FighterState.Windup && stateTimer >= stateDuration)
            {
                SetState(FighterState.StrikeActive, 0.14f);
            }
            else if (State == FighterState.StrikeActive)
            {
                if (!strikeEvaluated && stateTimer >= stateDuration * 0.45f)
                {
                    strikeEvaluated = true;
                    EvaluateStrikeImpact();
                }
                if (stateTimer >= stateDuration)
                {
                    SetState(FighterState.Recovery, 0.16f);
                }
            }
            else if (State == FighterState.Recovery || State == FighterState.SlipLeft || State == FighterState.SlipRight || State == FighterState.HitStun || State == FighterState.GuardBroken || State == FighterState.Stunned)
            {
                if (stateTimer >= stateDuration)
                {
                    if (wantGuardHeld)
                    {
                        guardEnterTime = Time.time;
                        SetState(FighterState.Guarding, 0.2f);
                    }
                    else
                    {
                        SetState(FighterState.Idle, 0.2f);
                    }
                }
            }

            Visuals?.SetCombatPose(State, ActivePunch, StateProgress, stateDuration);
        }

        private void EvaluateStrikeImpact()
        {
            if (Opponent == null || Opponent.State == FighterState.KO || Opponent.State == FighterState.KnockedDown) return;

            CombatAbilityData ab = null;
            if (ActivePunch == PunchType.Special1) ab = Special1;
            else if (ActivePunch == PunchType.Special2) ab = Special2;

            bool parryWindow = Opponent.State == FighterState.Guarding && (Time.time - Opponent.guardEnterTime <= 0.25f);
            float passiveCounter = PassiveMod != null ? PassiveMod.passiveCounterDamageBonus : 0f;

            var res = CombatCalculator.ResolveHit(
                Stats,
                Opponent.Stats,
                ActivePunch,
                Opponent.State,
                parryWindow,
                HasCounterWindow,
                ComboCount,
                Opponent.GuardStability,
                passiveCounter,
                UnityEngine.Random.value,
                ab);

            if (res.wasDodged)
            {
                Opponent.GrantCounterWindow(1.5f);
                Opponent.AddOverdrive(14f);
                ComboCount = 0;
                OnAttackLanded?.Invoke(this, Opponent, ActivePunch, res);
                return;
            }

            if (res.wasParried)
            {
                Opponent.GrantCounterWindow(1.6f);
                Opponent.AddOverdrive(24f);
                ComboCount = 0;
                SetState(FighterState.Stunned, 0.95f);
                OnAttackLanded?.Invoke(this, Opponent, ActivePunch, res);
                return;
            }

            if (HasCounterWindow) counterWindowTimer = 0f;

            ComboCount++;
            float odGain = (ActivePunch == PunchType.Special1 || ActivePunch == PunchType.Special2) ? 0f : (8f + Stats.overdriveGain * 0.25f);
            AddOverdrive(odGain);

            Opponent.ReceiveHit(res, ab);
            OnAttackLanded?.Invoke(this, Opponent, ActivePunch, res);
        }

        public void GrantCounterWindow(float duration)
        {
            counterWindowTimer = Mathf.Max(counterWindowTimer, duration);
        }

        public void AddOverdrive(float amount)
        {
            Overdrive = Mathf.Clamp(Overdrive + amount, 0f, 100f);
        }

        public void ReceiveHit(HitResolution res, CombatAbilityData incomingAbility)
        {
            CurrentHp = Mathf.Max(0f, CurrentHp - res.finalDamage);
            GuardStability = Mathf.Max(0f, GuardStability - res.guardDamage);
            AddOverdrive(res.wasBlocked ? 4f : 7f);
            ComboCount = 0;

            Visuals?.TriggerHitFlash(res.wasBlocked ? 0.35f : 1.0f);

            if (CurrentHp <= 0f)
            {
                KnockdownCount++;
                wantGuardHeld = false;
                if (KnockdownCount >= 3 || (!IsPlayer && KnockdownCount >= 2))
                {
                    SetState(FighterState.KO, 1.2f);
                }
                else
                {
                    SetState(FighterState.KnockedDown, 1.0f);
                }
                OnKnockedDown?.Invoke(this);
                return;
            }

            if (res.brokeGuard)
            {
                wantGuardHeld = false;
                GuardStability = 0f;
                SetState(FighterState.GuardBroken, 0.9f);
            }
            else if (!res.wasBlocked)
            {
                if (incomingAbility != null && incomingAbility.stunDuration > 0.2f)
                {
                    SetState(FighterState.Stunned, incomingAbility.stunDuration);
                }
                else
                {
                    SetState(FighterState.HitStun, res.wasCrit || res.wasCounter ? 0.34f : 0.22f);
                }
            }
        }

        private void SetState(FighterState newState, float duration)
        {
            State = newState;
            stateTimer = 0f;
            stateDuration = Mathf.Max(0.05f, duration);
            Visuals?.SetCombatPose(State, ActivePunch, 0f, stateDuration);
        }
    }
}
