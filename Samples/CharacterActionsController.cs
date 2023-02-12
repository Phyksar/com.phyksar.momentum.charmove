using Momentum.Components;
using Momentum.Math.Extensions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Momentum.Samples
{
    [RequireComponent(typeof(CharacterHull))]
    public class CharacterActionsController : MonoBehaviour
    {
        public const float DefaultMaxPitchLookAngle = 90.0f;

        public float maxPitchLookAngle;
        public bool lockCursor;

        protected CharacterHull characterHull;
        protected Vector2 viewAnglesDelta;

        protected virtual Vector2 ClampViewAngles(Vector2 viewAngles)
        {
            viewAngles.x = viewAngles.x.NormalizeDegrees();
            if (maxPitchLookAngle >= 0.0f) {
                viewAngles.y = Mathf.Clamp(viewAngles.y, -maxPitchLookAngle, maxPitchLookAngle);
            }
            return viewAngles;
        }

        protected virtual void ToggleViewMode()
        {
            var currentViewMode = FindCurrentViewMode();
            var nextViewMode = FindNextViewMode(currentViewMode);
            if (nextViewMode != null) {
                currentViewMode.enabled = false;
                nextViewMode.enabled = true;
            }
        }

        protected virtual ViewMode FindCurrentViewMode()
        {
            foreach (var viewMode in GetComponents<ViewMode>()) {
                if (viewMode.enabled) {
                    return viewMode;
                }
            }
            return null;
        }

        protected virtual ViewMode FindNextViewMode(ViewMode currentViewMode)
        {
            var viewModes = GetComponents<ViewMode>();
            if (viewModes.Length == 0) {
                return null;
            }
            ViewMode lastViewMode = null;
            foreach (var viewMode in viewModes) {
                if (lastViewMode == currentViewMode) {
                    return viewMode;
                }
                lastViewMode = viewMode;
            }
            return viewModes[0];
        }

        private void OnMove(InputValue value)
        {
            var direction = value.Get<Vector2>();
            characterHull.wishDirection = new Vector3(direction.x, 0.0f, direction.y);
        }

        private void OnLook(InputValue value)
        {
            viewAnglesDelta = value.Get<Vector2>();
        }

        private void OnJump(InputValue value)
        {
            if (value.isPressed) {
                characterHull.Jump();
            }
        }

        private void OnSprint(InputValue value)
        {
            characterHull.sprintFactor = value.isPressed ? 1.0f : 0.0f;
        }

        private void OnToggleView(InputValue value)
        {
            if (value.isPressed) {
                ToggleViewMode();
            }
        }

        private void OnEnable()
        {
            if (!TryGetComponent<CharacterHull>(out characterHull)) {
                enabled = false;
                throw new MissingComponentException(
                    "CharacterActionsController requires CharacterHull before enabling");
            }
        }

        private void Reset()
        {
            maxPitchLookAngle = DefaultMaxPitchLookAngle;
            lockCursor = true;
        }

        private void Update()
        {
            if (!viewAnglesDelta.Equals(Vector2.zero)) {
                characterHull.viewAngles = ClampViewAngles(characterHull.viewAngles + viewAnglesDelta);
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            SetCursorState(lockCursor);
        }

        private void SetCursorState(bool newState)
        {
            Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
        }
    }
}
