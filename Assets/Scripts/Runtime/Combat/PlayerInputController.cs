using IronKnuckle.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace IronKnuckle.Combat
{
    public class PlayerInputController : MonoBehaviour
    {
        public FighterController playerFighter;
        public bool inputEnabled = true;

        private Vector2 pointerDownPos;
        private bool trackingGesture;
        private bool guardHeldFromSwipe;
        private const float MinSwipeDistance = 55f;

        public event System.Action<string> OnPlayerActionPerformed;

        private void Update()
        {
            if (!inputEnabled || playerFighter == null) return;

            HandleKeyboardInput();
            HandlePointerGestures();
        }

        private void HandleKeyboardInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.qKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame) TriggerJab();
            else if (kb.eKey.wasPressedThisFrame || kb.kKey.wasPressedThisFrame) TriggerCross();
            else if (kb.zKey.wasPressedThisFrame || kb.uKey.wasPressedThisFrame) TriggerLeftHook();
            else if (kb.cKey.wasPressedThisFrame || kb.oKey.wasPressedThisFrame) TriggerRightHook();
            else if (kb.wKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame) TriggerUppercut();
            else if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) TriggerSlipLeft();
            else if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) TriggerSlipRight();
            else if (kb.digit1Key.wasPressedThisFrame) TriggerSpecial1();
            else if (kb.digit2Key.wasPressedThisFrame) TriggerSpecial2();

            if (kb.sKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                SetGuardHeld(true);
            else if (kb.sKey.wasReleasedThisFrame || kb.spaceKey.wasReleasedThisFrame)
                SetGuardHeld(false);
        }

        private void HandlePointerGestures()
        {
            var pointer = Pointer.current;
            if (pointer == null) return;

            if (pointer.press.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    trackingGesture = false;
                    return;
                }
                pointerDownPos = pointer.position.ReadValue();
                trackingGesture = true;
            }
            else if (trackingGesture && pointer.press.wasReleasedThisFrame)
            {
                trackingGesture = false;
                if (guardHeldFromSwipe)
                {
                    guardHeldFromSwipe = false;
                    SetGuardHeld(false);
                    return;
                }

                Vector2 endPos = pointer.position.ReadValue();
                Vector2 delta = endPos - pointerDownPos;

                if (delta.magnitude < MinSwipeDistance)
                {
                    // Tap Left half = Jab, Right half = Cross
                    if (endPos.x < Screen.width * 0.5f) TriggerJab();
                    else TriggerCross();
                }
                else
                {
                    if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                    {
                        if (delta.x > 0) TriggerLeftHook();
                        else TriggerRightHook();
                    }
                    else
                    {
                        if (delta.y > 0) TriggerUppercut();
                        else
                        {
                            // Quick down-swipe guard pulse
                            playerFighter.SetGuardHeld(true);
                            Invoke(nameof(ReleaseSwipeGuard), 0.35f);
                            OnPlayerActionPerformed?.Invoke("GUARD");
                        }
                    }
                }
            }
            else if (trackingGesture && pointer.press.isPressed)
            {
                Vector2 curPos = pointer.position.ReadValue();
                Vector2 delta = curPos - pointerDownPos;
                if (!guardHeldFromSwipe && delta.y < -MinSwipeDistance * 1.15f && Mathf.Abs(delta.y) > Mathf.Abs(delta.x))
                {
                    guardHeldFromSwipe = true;
                    SetGuardHeld(true);
                }
            }
        }

        private void ReleaseSwipeGuard()
        {
            if (playerFighter != null) playerFighter.SetGuardHeld(false);
        }

        public void TriggerJab()
        {
            if (inputEnabled && playerFighter != null && playerFighter.TryStartPunch(PunchType.LeftJab))
                OnPlayerActionPerformed?.Invoke("JAB");
        }

        public void TriggerCross()
        {
            if (inputEnabled && playerFighter != null && playerFighter.TryStartPunch(PunchType.RightCross))
                OnPlayerActionPerformed?.Invoke("CROSS");
        }

        public void TriggerLeftHook()
        {
            if (inputEnabled && playerFighter != null && playerFighter.TryStartPunch(PunchType.LeftHook))
                OnPlayerActionPerformed?.Invoke("HOOK");
        }

        public void TriggerRightHook()
        {
            if (inputEnabled && playerFighter != null && playerFighter.TryStartPunch(PunchType.RightHook))
                OnPlayerActionPerformed?.Invoke("HOOK");
        }

        public void TriggerUppercut()
        {
            if (inputEnabled && playerFighter != null && playerFighter.TryStartPunch(PunchType.Uppercut))
                OnPlayerActionPerformed?.Invoke("UPPER");
        }

        public void TriggerSlipLeft()
        {
            if (inputEnabled && playerFighter != null && playerFighter.TrySlip(true))
                OnPlayerActionPerformed?.Invoke("SLIP");
        }

        public void TriggerSlipRight()
        {
            if (inputEnabled && playerFighter != null && playerFighter.TrySlip(false))
                OnPlayerActionPerformed?.Invoke("SLIP");
        }

        public void SetGuardHeld(bool held)
        {
            if (!inputEnabled || playerFighter == null) return;
            playerFighter.SetGuardHeld(held);
            if (held) OnPlayerActionPerformed?.Invoke("GUARD");
        }

        public void TriggerSpecial1()
        {
            if (inputEnabled && playerFighter != null && playerFighter.TryStartPunch(PunchType.Special1))
                OnPlayerActionPerformed?.Invoke("SPECIAL");
        }

        public void TriggerSpecial2()
        {
            if (inputEnabled && playerFighter != null && playerFighter.TryStartPunch(PunchType.Special2))
                OnPlayerActionPerformed?.Invoke("SPECIAL");
        }
    }
}
