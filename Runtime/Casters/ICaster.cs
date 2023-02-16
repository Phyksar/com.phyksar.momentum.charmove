using UnityEngine;

namespace Momentum.Casters
{
    public interface ICaster
    {
        bool SweepTest(
            Vector3 start,
            Vector3 direction,
            out RaycastHit hitInfo,
            float maxDistance,
            float contactOffsetDistance,
            float heightReduction = 0.0f);
        bool CheckOverlaping(Vector3 start, float contactOffsetDistance, float heightReduction = 0.0f);
        bool IsGround(RaycastHit raycastHit, float maxStandableAngle, out Vector3 normal);
        bool GetFarthestPenetration(Vector3 start, out Vector3 direction, out float distance);
    }
}
