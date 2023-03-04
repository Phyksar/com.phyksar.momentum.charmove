using Momentum.Kinematics;
using System.Buffers;
using UnityEngine;

namespace Momentum.Casters
{
    public class CapsuleCaster : AbstractCaster
    {
        public const int CapsuleDirection = 1;
        public const int DefaultMaxHits = 32;
        public const float GroundPointOffsetDistance = 0.002f;
        public const float GroundPointRaycastDistance = 0.02f;

        public static readonly Color GroundGizmoColor = new Color(0.2f, 1.0f, 1.0f);

        private CapsuleCollider collider;
        private int layerMask;
        private int maxHits;

        public override float width {
            get {
                return collider.radius * 2.0f;
            }
            set {
                collider.radius = value * 0.5f;
                UpdateHeight(collider.height);
            }
        }

        public override float height {
            get {
                return collider.height;
            }
            set {
                UpdateHeight(value);
            }
        }

        public CapsuleCaster(CapsuleCollider collider, int layerMask, int maxHits = DefaultMaxHits)
        {
            this.collider = collider;
            this.layerMask = layerMask;
            this.maxHits = maxHits;
        }

        public override bool SweepTest(
            Vector3 start,
            Vector3 direction,
            out RaycastHit hitInfo,
            float maxDistance,
            float contactOffsetDistance,
            float heightReduction = 0.0f)
        {
            GetCapsulePoints(start, out var pointA, out var pointB, heightReduction);
            var raycastHits = ArrayPool<RaycastHit>.Shared.Rent(maxHits);
            var totalHits = Physics.CapsuleCastNonAlloc(
                pointA,
                pointB,
                collider.radius - contactOffsetDistance,
                direction,
                raycastHits,
                maxDistance,
                layerMask,
                QueryTriggerInteraction.Ignore);
            var up = collider.transform.up;
            var closestDistance = maxDistance;
            int? closestId = null;
            for (var i = 0; i < totalHits; i++) {
                var hitCollider = raycastHits[i].collider;
                // Ignore any colliders related to the original one
                if (hitCollider == collider || hitCollider.transform.IsChildOf(collider.transform)) {
                    continue;
                }
                var distance = raycastHits[i].distance;
                // TODO: Bypassing a Unity bug with an invalid result when capsule hits a vertex, potentially buggy
                // and dangerous code, redo when fixed or found a better solution
                if (raycastHits[i].point.Equals(Vector3.zero) && distance == 0.0f && totalHits > 1) {
                    continue;
                }
                if (distance < closestDistance) {
                    closestDistance = distance;
                    closestId = i;
                }
            }
            hitInfo = closestId.HasValue ? raycastHits[closestId.Value] : new RaycastHit { };
            ArrayPool<RaycastHit>.Shared.Return(raycastHits);
            return closestId.HasValue;
        }

        public override bool CheckOverlaping(Vector3 start, float contactOffsetDistance, float heightReduction = 0.0f)
        {
            GetCapsulePoints(start, out var pointA, out var pointB, heightReduction);
            var colliders = ArrayPool<Collider>.Shared.Rent(maxHits);
            var totalColliders = Physics.OverlapCapsuleNonAlloc(
                pointA,
                pointB,
                collider.radius - contactOffsetDistance,
                colliders,
                layerMask,
                QueryTriggerInteraction.Ignore);
            var isOverlapping = false;
            for (var i = 0; i < totalColliders; i++) {
                if (colliders[i] != collider && !colliders[i].transform.IsChildOf(collider.transform)) {
                    isOverlapping = true;
                    break;
                }
            }
            ArrayPool<Collider>.Shared.Return(colliders);
            return isOverlapping;
        }

        public override bool IsGround(RaycastHit capsuleHit, float maxStandableAngle, out Vector3 normal)
        {
            var up = collider.transform.up;
            var groundPointDirection = -Vector3.ProjectOnPlane(capsuleHit.normal, up);
            var groundPointOffset = groundPointDirection * (collider.radius + GroundPointOffsetDistance);
            if (groundPointOffset.sqrMagnitude >= collider.radius * collider.radius) {
                normal = capsuleHit.normal;
                return IsGroundNormal(capsuleHit.normal, up, maxStandableAngle);
            }
            var groundPoint = capsuleHit.point + groundPointDirection * GroundPointOffsetDistance;
            var raycastResult = capsuleHit.collider.Raycast(
                new Ray(groundPoint + capsuleHit.normal * (GroundPointRaycastDistance * 0.5f), -capsuleHit.normal),
                out var raycastHit,
                GroundPointRaycastDistance);
            if (!raycastResult) {
                normal = capsuleHit.normal;
                return IsGroundNormal(capsuleHit.normal, up, maxStandableAngle);
            }
            normal = raycastHit.normal;
            return IsGroundNormal(raycastHit.normal, up, maxStandableAngle);
        }

        public override bool GetFarthestPenetration(Vector3 start, out Vector3 direction, out float distance)
        {
            GetCapsulePoints(start, out var pointA, out var pointB);
            var colliders = ArrayPool<Collider>.Shared.Rent(maxHits);
            var totalColliders = Physics.OverlapCapsuleNonAlloc(
                pointA,
                pointB,
                collider.radius,
                colliders,
                layerMask,
                QueryTriggerInteraction.Ignore);
            direction = Vector3.zero;
            distance = 0.0f;
            for (var i = 0; i < totalColliders; i++) {
                var otherCollider = colliders[i];
                if (otherCollider == collider || otherCollider.transform.IsChildOf(collider.transform)) {
                    continue;
                }
                var penetrationResult = Physics.ComputePenetration(
                    collider,
                    start,
                    collider.transform.rotation,
                    otherCollider,
                    otherCollider.transform.position,
                    otherCollider.transform.rotation,
                    out var penetrationDirection,
                    out var penetrationDistance);
                if (!penetrationResult) {
                    continue;
                }
                if (penetrationDistance > distance) {
                    distance = penetrationDistance;
                    direction = penetrationDirection;
                }
            }
            ArrayPool<Collider>.Shared.Return(colliders);
            return distance != 0.0f;
        }

        public override void DrawGroundPlaneGizmo(ClippingPlane groundPlane)
        {
            var center = collider.transform.position + collider.transform.up * collider.radius;
            Gizmos.color = GroundGizmoColor;
            Gizmos.DrawLine(center, center - groundPlane.normal * collider.radius);
        }

        private void GetCapsulePoints(
            in Vector3 position,
            out Vector3 pointA,
            out Vector3 pointB,
            float heightReduction = 0.0f)
        {
            var up = collider.transform.up;
            var centerOffset = Mathf.Max(collider.height * 0.5f - collider.radius, 0.0f);
            pointA = position + collider.transform.rotation * collider.center - up * centerOffset;
            pointB = pointA + up * Mathf.Max(collider.height - 2.0f * collider.radius - heightReduction, 0.0f);
        }

        private void UpdateHeight(float height)
        {
            height = Mathf.Max(height, collider.radius * 2.0f);
            collider.center = Vector3.up * (height * 0.5f);
            collider.height = height;
        }
    }
}
