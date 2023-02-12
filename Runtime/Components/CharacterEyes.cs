using UnityEngine;

namespace Momentum.Components
{
    public class CharacterEyes : MonoBehaviour
    {
        public const float DefaultHeight = 1.6f;

        [Min(0.0f)]
        public float height;

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

        public void UpdateTransform(in Quaternion localLookRotation, in Vector3 localUp)
        {
            localRotation = localLookRotation;
            localPosition = localUp * height;
        }

        private void Reset()
        {
            height = DefaultHeight;
        }
    }
}
