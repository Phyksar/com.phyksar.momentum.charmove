using Momentum.Kinematics;
using UnityEngine;

namespace Momentum.Casters
{
    public abstract class AbstractCaster : ICaster
    {
        public abstract void Resize(float width, float height);
        public abstract bool SweepTest(
            Vector3 start,
            Vector3 direction,
            out RaycastHit hitInfo,
            float maxDistance,
            float contactOffsetDistance,
            float heightReduction = 0.0f);
        public abstract bool CheckOverlaping(Vector3 start, float contactOffsetDistance, float heightReduction = 0.0f);
        public abstract bool IsGround(RaycastHit raycastHit, float maxStandableAngle, out Vector3 normal);
        public abstract bool GetFarthestPenetration(Vector3 start, out Vector3 direction, out float distance);

        public virtual void DrawGroundPlaneGizmo(ClippingPlane groundPlane)
        { }

        protected bool IsGroundNormal(in Vector3 normal, in Vector3 up, float maxStandableAngle)
        {
            return Vector3.Angle(normal, up) <= maxStandableAngle;
        }
    }
}
