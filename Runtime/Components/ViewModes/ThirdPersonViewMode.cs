using UnityEngine;

namespace Momentum.Components.ViewModes
{
    [RequireComponent(typeof(CharacterEyes))]
    public class ThirdPersonViewMode : ViewMode
    {
        private const float DefaultFieldOfView = 70.0f;
        private const float DefaultDistance = 2.5f;

        public float fieldOfView;
        public float distance;

        protected override void UpdateView(Camera camera)
        {
            if (TryGetComponent<CharacterEyes>(out var eyes)) {
                var eyesRotation = eyes.rotation;
                camera.transform.position = eyes.position + eyesRotation * (Vector3.back * distance);
                camera.transform.rotation = eyesRotation;
                camera.fieldOfView = fieldOfView;
            }
        }

        private void Reset()
        {
            fieldOfView = DefaultFieldOfView;
            distance = DefaultDistance;
        }
    }
}
