using System;
using UnityEngine;

namespace Momentum.Kinematics
{
    [Serializable]
    public struct ClippingPlane
    {
        public Vector3 normal;
        public Vector3 velocity;

        public ClippingPlane(in Vector3 normal, in Vector3 velocity)
        {
            this.normal = normal;
            this.velocity = velocity;
        }

        public Vector3 ClipVelocity(in Vector3 velocity, float overbounce = 1.0f)
        {
            return velocity + normal * Mathf.Max(Vector3.Dot(this.velocity - velocity, normal) * overbounce, 0.0f);
        }

        public ClippingPlane WithNormal(in Vector3 normal)
        {
            return new ClippingPlane {
                normal = normal,
                velocity = velocity
            };
        }

        public static ClippingPlane FromRaycastHit(in RaycastHit raycastHit)
        {
            return FromRaycastHit(raycastHit, raycastHit.normal);
        }

        public static ClippingPlane FromRaycastHit(in RaycastHit raycastHit, in Vector3 normal)
        {
            var velocity = Vector3.zero;
            if (raycastHit.rigidbody != null) {
                velocity = raycastHit.rigidbody.GetPointVelocity(raycastHit.point);
            }
            return new ClippingPlane(normal, velocity);
        }
    }
}
