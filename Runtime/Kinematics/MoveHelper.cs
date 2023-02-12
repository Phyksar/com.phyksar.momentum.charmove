using Momentum.Casters;
using UnityEngine;

namespace Momentum.Kinematics
{
    /// <summary>
    /// This is the Half-Life/Quake style movement. If moving from <see cref="position"/> using <see cref="velocity"/>
    /// results in a collision, <see cref="velocity"/> will be changed to slide across the surface where appropriate.
    /// <see cref="position"/> will be updated to the optimal one.
    /// </summary>
    public struct MoveHelper
    {
        public const int DefaultMaxClipPlanes = 5;
        public const float DefaultMaxStandableAngle = 45.0f;
        public const float DefaultRaycastBackoffDistance = 0.003f;
        public const float DefaultBounce = 0.0f;
        public const float DefaultGroundVelocityThreshold = 0.1f;

        public Vector3 position;
        public Vector3 velocity;
        public Vector3 up;
        public ICaster caster;
        public float maxStandableAngle;
        public float contactOffsetDistance;
        public float groundBounce;
        public float wallBounce;

        /// <summary>
        /// Create the MoveHelper and initialize it with the default settings.
        /// <see cref="maxStandableAngle"/> and other fields can be changed after creation.
        /// </summary>
        public MoveHelper(in Vector3 position, in Vector3 velocity, in Vector3 up, in ICaster caster)
        {
            this.position = position;
            this.velocity = velocity;
            this.up = up;
            this.caster = caster;
            maxStandableAngle = DefaultMaxStandableAngle;
            contactOffsetDistance = DefaultRaycastBackoffDistance;
            groundBounce = DefaultBounce;
            wallBounce = DefaultBounce;
        }

        /// <summary>
        /// Move <see cref="position"/> in <paramref name="direction"/> by <paramref name="maxDistance"/>
        /// until it hits a wall.
        /// </summary>
        /// <returns>
        /// True if it hits anything.
        /// </returns>
        public bool SweepMove(in Vector3 direction, out RaycastHit hitInfo, float maxDistance)
        {
            if (caster.SweepTest(position, direction, out hitInfo, maxDistance)) {
                // Due to float point calculation errors sometimes the end position can be starting in solid
                // Push back by a small margin so this should never happen
                position += direction * hitInfo.distance;
                return true;
            }
            position += direction * maxDistance;
            return false;
        }

        /// <summary>
        /// Try to move <see cref="position"/>. <see cref="position"/> and <see cref="velocity"/> will be updated
        /// according to movement.
        /// </summary>
        /// <returns>
        /// Fraction of the desired <see cref="velocity"/> that was traveled.
        /// </returns>
        public float TryMove(bool standingOnGround, float timeDelta, int maxClipPlanes = DefaultMaxClipPlanes)
        {
            var timeLeft = timeDelta;
            var travelFraction = 0.0f;
            using (var clippingPlanes = new ClippingPlanes(maxClipPlanes)) {
                for (int bump = 0; bump < clippingPlanes.maxPlanes; bump++) {
                    if (velocity == Vector3.zero) {
                        break;
                    }
                    var direction = velocity.normalized;
                    var maxDistance = Vector3.Dot(velocity, direction) * timeLeft;
                    if (!SweepMove(direction, out var hitInfo, maxDistance)) {
                        travelFraction += 1.0f;
                        break;
                    }
                    var distanceFraction = hitInfo.distance / maxDistance;
                    travelFraction += distanceFraction;
                    clippingPlanes.StartBump(velocity);
                    timeLeft -= timeLeft * distanceFraction;
                    float bounce;
                    Vector3 normal;
                    if (caster.IsGround(hitInfo, maxStandableAngle, out var groundNormal)) {
                        position += GetContactOffset(groundNormal, isGround: true);
                        bounce = groundBounce;
                        normal = groundNormal;
                    } else {
                        position += GetContactOffset(hitInfo.normal, isGround: false);
                        bounce = wallBounce;
                        normal = standingOnGround
                            ? Vector3.ProjectOnPlane(hitInfo.normal, up).normalized
                            : hitInfo.normal;
                    }
                    if (!clippingPlanes.TryAdd(ClippingPlane.FromRaycastHit(hitInfo, normal), ref velocity, bounce)) {
                        break;
                    }
                }
            }
            if (travelFraction == 0.0f) {
                velocity = Vector3.zero;
            }
            return travelFraction;
        }

        /// <summary>
        /// Like <see cref="TryMove"/> but will also try to step up if it hits a wall.
        /// </summary>
        /// <returns>
        /// Fraction of the desired <see cref="velocity"/> that was traveled.
        /// </returns>
        public float TryMoveWithFeetLift(
            bool standingOnGround,
            float feetLiftHeight,
            float snapDistance,
            float timeDelta,
            int maxClipPlanes = DefaultMaxClipPlanes)
        {
            if (feetLiftHeight <= 0.0f) {
                return TryMove(standingOnGround, timeDelta, maxClipPlanes);
            }
            var startPosition = position;
            // Make a copy of current MoveHelper
            var feetLiftMoveHelper = this;
            // Do a regular move
            var fraction = TryMove(standingOnGround, timeDelta, maxClipPlanes);
            // Move up as much as possible
            if (feetLiftMoveHelper.SweepMove(up, out var upHit, feetLiftHeight)) {
                feetLiftMoveHelper.position += feetLiftMoveHelper.GetContactOffset(upHit.normal, isGround: false);
            }
            // Move across using copied MoveHelper
            var stepFraction = feetLiftMoveHelper.TryMove(standingOnGround, timeDelta, maxClipPlanes);
            // Move back down. if it didn't land on anything, landed on a wall or the stepless sweep moved further away,
            // then keep original results of TryMove
            var downHitResult = feetLiftMoveHelper.SweepMove(-up, out var downHit, feetLiftHeight + snapDistance);
            var downHitNormal = downHit.normal;
            var isOnGround = downHitResult && caster.IsGround(downHit, maxStandableAngle, out downHitNormal);
            if (!downHitResult
                || !isOnGround
                || Vector3.ProjectOnPlane(position - startPosition, up).sqrMagnitude
                    > Vector3.ProjectOnPlane(feetLiftMoveHelper.position - startPosition, up).sqrMagnitude) {
                return fraction;
            }
            // Step MoveHelper moved further, use its results
            position = feetLiftMoveHelper.position + feetLiftMoveHelper.GetContactOffset(downHitNormal, isOnGround);
            velocity = feetLiftMoveHelper.velocity;
            return stepFraction;
        }

        /// <summary>
        /// Snap <see cref="position"/> to ground so the character would stay on it when moving down the slopes.
        /// or stairs.
        /// </summary>
        /// <returns>
        /// True if snapping was successfull.
        /// </returns>
        public bool SnapToGround(
            float feetLiftHeight,
            float snapDistance,
            float groundVelocityThreshold = DefaultGroundVelocityThreshold)
        {
            var snapResult = caster.SweepTest(
                position + up * feetLiftHeight,
                -up,
                out var hitInfo,
                feetLiftHeight + snapDistance,
                feetLiftHeight);
            if (!snapResult) {
                return false;
            }
            var snapPlane = ClippingPlane.FromRaycastHit(hitInfo);
            if (hitInfo.distance == 0.0f
                || Vector3.Dot(velocity - snapPlane.velocity, up) > groundVelocityThreshold
                || !caster.IsGround(hitInfo, maxStandableAngle, out var _)) {
                return false;
            }
            var snapPosition = position
                - up * (hitInfo.distance - feetLiftHeight)
                + GetContactOffset(hitInfo.normal, isGround: true);
            if (caster.CheckOverlaping(snapPosition)) {
                return false;
            }
            position = snapPosition;
            velocity += Vector3.Project(snapPlane.velocity - velocity, up);
            return true;
        }

        private Vector3 GetContactOffset(in Vector3 hitNormal, bool isGround)
        {
            if (isGround) {
                return up * (contactOffsetDistance / Mathf.Abs(Vector3.Dot(up, hitNormal)));
            }
            return hitNormal * contactOffsetDistance;
        }
    }
}
