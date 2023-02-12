using System;
using System.Buffers;
using UnityEngine;

namespace Momentum.Kinematics
{
    /// <summary>
    /// Used to store a list of planes that an object is going to hit, and then remove velocity from them so the object
    /// can slide over the surface without going through any of the planes.
    /// </summary>
    public struct ClippingPlanes : IDisposable
    {
        private Vector3 originalVelocity;
        private Vector3 bumpVelocity;
        private ClippingPlane[] planes;

        /// <summary>
        /// Maximum number of planes that can be added
        /// </summary>
        public int maxPlanes { get; private set; }

        /// <summary>
        /// Number of planes currently in use
        /// </summary>
        public int count { get; private set; }

        public ClippingPlanes(int max)
        {
            maxPlanes = max;
            originalVelocity = Vector3.zero;
            bumpVelocity = Vector3.zero;
            planes = ArrayPool<ClippingPlane>.Shared.Rent(maxPlanes);
            count = 0;
        }

        public void Dispose()
        {
            ArrayPool<ClippingPlane>.Shared.Return(planes);
        }

        /// <summary>
        /// Start a new bump. Clears planes and resets <see cref="bumpVelocity"/>
        /// </summary>
        public void StartBump(in Vector3 velocity)
        {
            bumpVelocity = velocity;
            count = 0;
        }

        /// <summary>
        /// Try to add <paramref name="plane"/> and restrain <paramref name="velocity"/> to it and all previously
        /// added planes
        /// </summary>
        /// <returns>
        /// False if no more planes can be added
        /// </returns>
        public bool TryAdd(in ClippingPlane plane, ref Vector3 velocity, float bounce = 0.0f)
        {
            if (count == maxPlanes) {
                velocity = Vector3.zero;
                return false;
            }
            planes[count++] = plane;
            // if only one plane was added then apply the bounce
            if (count == 1) {
                bumpVelocity = plane.ClipVelocity(bumpVelocity, 1.0f + bounce);
                velocity = bumpVelocity;
                return true;
            }
            // clip to all of the planes that were added
            velocity = bumpVelocity;
            if (TryClip(ref velocity)) {
                if (count == 2) {
                    var direction = Vector3.Cross(planes[0].normal, planes[1].normal);
                    velocity = direction.normalized * Vector3.Dot(velocity, direction);
                } else {
                    velocity = Vector3.zero;
                    return true;
                }
            }
            // Velocity results in the opposite direction to the original one, so just stop right there
            if (Vector3.Dot(velocity, originalVelocity) < 0.0f) {
                velocity = Vector3.zero;
            }
            return true;
        }

        /// <summary>
        /// Try to clip the <paramref name="velocity"/> to all the planes.
        /// </summary>
        /// <returns>
        /// True if clipping was successfull.
        /// </returns>
        private bool TryClip(ref Vector3 velocity)
        {
            for (int i = 0; i < count; i++) {
                velocity = planes[i].ClipVelocity(bumpVelocity);
                if (IsMovingTowardsAnyPlane(velocity, i)) {
                    return false;
                }
            }
            return true;
        }

        /// <returns>
        /// True if moving towards any of added planes, except for skipped one.
        /// </returns>
        private bool IsMovingTowardsAnyPlane(in Vector3 velocity, int skipIndex)
        {
            for (int i = 0; i < count; i++) {
                if (i == skipIndex) {
                    continue;
                }
                if (Vector3.Dot(velocity, planes[i].normal) < 0) {
                    return false;
                }
            }
            return true;
        }
    }
}
