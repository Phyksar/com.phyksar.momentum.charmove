using UnityEngine;

namespace Momentum.Components
{
    public class CharacterEyes : MonoBehaviour
    {
        public const float DefaultHeightOffset = -0.2f;
        public const float DefaultHeightDampingTime = 0.05f;

        public float heightOffset;
        public float heightDampingTime;

        private float lastHeight;
        private float heightVelocity;
        private float lastUpdateTime;

        public float referenceHeight {
            get {
                if (TryGetComponent<CapsuleCollider>(out var capsule)) {
                    return capsule.height;
                }
                return 0.0f;
            }
        }

        public Vector3 localPosition { get; set; }
        public Quaternion localRotation { get; set; }

        public Vector3 position {
            get => transform.TransformPoint(localPosition);
            set => localPosition = transform.InverseTransformPoint(value);
        }

        public Quaternion rotation {
            get => transform.rotation * localRotation;
            set => localRotation = Quaternion.Inverse(transform.rotation) * value;
        }

        public void OnEnable()
        {
            lastHeight = referenceHeight + heightOffset;
        }

        public void UpdateTransform(in Quaternion localLookRotation, in Vector3 localUp, float availableHeight)
        {
            var targetHeight = referenceHeight + heightOffset;
            if (heightDampingTime > 0.0f) {
                lastHeight = Mathf.SmoothDamp(
                    lastHeight,
                    targetHeight,
                    ref heightVelocity,
                    heightDampingTime,
                    float.PositiveInfinity,
                    Mathf.Clamp(Time.time - lastUpdateTime, 0.0f, Time.maximumDeltaTime));
                if (lastHeight > availableHeight) {
                    lastHeight = availableHeight;
                }
            } else {
                lastHeight = targetHeight;
            }
            localRotation = localLookRotation;
            localPosition = localUp * lastHeight;
            lastUpdateTime = Time.time;
        }

        private void Reset()
        {
            heightOffset = DefaultHeightOffset;
            heightDampingTime = DefaultHeightDampingTime;
        }
    }
}
