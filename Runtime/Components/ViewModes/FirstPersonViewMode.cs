using UnityEngine;

namespace Momentum.Components.ViewModes
{
    [RequireComponent(typeof(CharacterEyes))]
    public class FirstPersonViewMode : ViewMode
    {
        private const float DefaultFieldOfView = 90.0f;

        public float fieldOfView;

        protected override void UpdateView(Camera camera)
        {
            if (TryGetComponent<CharacterEyes>(out var eyes)) {
                camera.transform.position = eyes.position;
                camera.transform.rotation = eyes.rotation;
                camera.fieldOfView = fieldOfView;
            }
        }

        private void Reset()
        {
            fieldOfView = DefaultFieldOfView;
        }
    }
}
