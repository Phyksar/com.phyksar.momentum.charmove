using Momentum.Kinematics;
using System;
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

        public CapsuleCaster(CapsuleCollider collider, int layerMask, int maxHits = DefaultMaxHits)
        {
            this.collider = collider;
            this.layerMask = layerMask;
            this.maxHits = maxHits;
        }

        public override void Resize(float width, float height)
        {
            if (collider.direction != CapsuleDirection) {
                throw new NotSupportedException("The only supported direction is Y-Axis");
            }
            collider.height = height;
            collider.radius = Mathf.Min(width, height) * 0.5f;
            collider.center = Vector3.up * (height * 0.5f);
        }

        public override bool SweepTest(
            Vector3 start,
            Vector3 direction,
            out RaycastHit hitInfo,
            float maxDistance,
            float heightReduction = 0.0f)
        {
            GetCapsulePoints(start, out var pointA, out var pointB, heightReduction);
            var raycastHits = ArrayPool<RaycastHit>.Shared.Rent(maxHits);
            var totalHits = Physics.CapsuleCastNonAlloc(
                pointA,
                pointB,
                collider.radius,
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

        public override bool CheckOverlaping(Vector3 start, float heightReduction = 0.0f)
        {
            GetCapsulePoints(start, out var pointA, out var pointB, heightReduction);
            var colliders = ArrayPool<Collider>.Shared.Rent(maxHits);
            var totalColliders = Physics.OverlapCapsuleNonAlloc(
                pointA,
                pointB,
                collider.radius,
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

        public override void DrawGroundPlaneGizmo(ClippingPlane groundPlane)
        {
            var center = collider.transform.position + collider.transform.up * collider.radius;
            Gizmos.color = GroundGizmoColor;
            Gizmos.DrawLine(center, center - groundPlane.normal * collider.radius);
        }

        private void GetCapsulePoints(
            in Vector3 origin,
            out Vector3 pointA,
            out Vector3 pointB,
            float heightReduction = 0.0f)
        {
            var up = collider.transform.up;
            pointA = origin + up * collider.radius;
            pointB = origin + up * Mathf.Max(collider.height - collider.radius - heightReduction, collider.radius);
        }
    }
}
