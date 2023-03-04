using Momentum.Casters;
using Momentum.DataTypes;
using Momentum.Kinematics;
using Momentum.Math.Numerics;
using System;
using UnityEngine;
using UnityEngine.Events;

namespace Momentum.Components
{
    public class CharacterHull : MonoBehaviour
    {
        public const float DefaultWidth = 0.6f;
        public const float DefaultHeight = 1.8f;
        public const float DefaultCrouchingHeight = 1.3f;
        public const float DefaultIndirectAccelerationRatio = 0.0f;
        public const float DefaultFeetLiftHeight = 0.3f;
        public const float DefaultGroundSnapDistance = 0.325f;
        public const float DefaultLandingSnapDistance = 0.025f;
        public const int DefaultLayerMask = 1;

        public const float DefaultWalkSpeed = 4.0f;
        public const float DefaultWalkAcceleration = 50.0f;
        public const float DefaultWalkFriction = 12.0f;

        public const float DefaultSprintSpeed = 6.0f;
        public const float DefaultCrouchSpeed = 3.0f;

        public const float DefaultAirSpeed = 0.5f;
        public const float DefaultAirAcceleration = 5.0f;
        public const float DefaultAirFriction = 0.0f;

        public const float DefaultJumpHeight = 1.05f;
        public const float DefaultCrouchJumpHeight = 0.55f;
        public const float DefaultBounce = 0.0f;

        [Min(0.0f)]
        public float width;

        [Min(0.0f)]
        public float height;

        [Min(0.0f)]
        public float crouchHeight;
        public bool allowCrouchInAir;
        public bool useAutoCrouch;

        [Range(0.0f, 90.0f)]
        public float maxStandableAngle;

        [Range(0.0f, 1.0f)]
        public float indirectAccelerationRatio;

        [Min(0.0f)]
        public float feetLiftHeight;

        public bool liftFeetInAir;

        [Min(0.0f)]
        public float groundSnapDistance;

        [Min(0.0f)]
        public float landingSnapDistance;

        [Min(0.0f)]
        public float contactOffsetDistance;

        [Min(0)]
        public int unstuckMaxAttempts;

        public LayerMask layerMask;

        public bool useGravity;

        public CharacterSpeed walkSpeed;
        public CharacterSpeed airSpeed;
        public float sprintSpeed;
        public float crouchSpeed;

        [Min(0.0f)]
        public float jumpVelocity;
        public float crouchJumpVelocity;

        [Min(0.0f)]
        public float groundBounce;

        [Min(0.0f)]
        public float wallBounce;

        [HideInInspector]
        public UnityEvent jumped;

        /// <summary>
        /// The event triggered once the isOnGround flag turns to true with parameter of vertical landing velocity
        /// </summary>
        [HideInInspector]
        public UnityEvent<float> landedOnGround;

        public Vector2 viewAngles { get; set; }
        public Vector3 wishDirection { get; set; }
        public Vector3 velocity { get; set; }
        public float sprintFactor { get; set; }
        public float crouchFactor { get; set; }
        public ClippingPlane groundPlane { get; protected set; }
        public Collider groundCollider { get; protected set; }

        public bool isOnGround => groundCollider != null;
        public bool isCrouching => caster.height < height;
        public float crouchingRatio => Mathf.Clamp01(Mathf.InverseLerp(height, crouchHeight, caster.height));
        public Quaternion localLookRotation => Quaternion.Euler(viewAngles.y, viewAngles.x, 0.0f);
        public Quaternion lookRotation => transform.rotation * localLookRotation;
        public Quaternion moveRotation => transform.rotation * Quaternion.AngleAxis(viewAngles.x, Vector3.up);
        public Vector3 lookDirection => lookRotation * Vector3.forward;

        private AbstractCaster caster;
        private Vector3Lerp positionLerp;
        private Vector3 lastPosition;
        private Vector3 lastVelocity;
        private float availableHeight;
        private float breakInterpolationTime;
        private bool shouldJump;

        public void InvalidateCollider()
        {
            var collider = GetComponent<Collider>();
            if (collider == null) {
                enabled = false;
                throw new MissingComponentException(
                    "CharacterHull requires a CapsuleCollider component before enabling");
            }
            caster = CreateCaster(collider, layerMask.value);
            caster.width = width;
            caster.height = height;
        }

        public void BreakInterpolation()
        {
            breakInterpolationTime = Time.fixedTime;
        }

        public void Jump()
        {
            shouldJump = true;
        }

        public bool TryUnstuck()
        {
            CreateMoveHelper(out var moveHelper);
            var unstuckResult = moveHelper.TryUnstuck(out var attempts, unstuckMaxAttempts);
            if (attempts > 0) {
                var unstuckResultText = unstuckResult ? "succeeded" : "failed";
                Debug.LogWarning(
                    $"character {gameObject.name} is stuck, unstuck {unstuckResultText} in {attempts} attempts");
            }
            if (!unstuckResult) {
                // Character is stuck and unstuck algorithm has failed after max attempts
                // Freeze velocity, giving a chance to unstuck in next fixed update
                transform.position = moveHelper.position;
                velocity = Vector3.zero;
                return false;
            }
            transform.position = moveHelper.position;
            velocity = moveHelper.velocity;
            return true;
        }

        public static float ComputeJumpVelocity(float jumpHeight, float opposingGravity)
        {
            return Mathf.Sqrt(Mathf.Abs(2.0f * opposingGravity * jumpHeight));
        }

        protected virtual CharacterSpeed GetSpeedOnGround()
        {
            var speed = walkSpeed;
            speed.maxSpeed = Mathf.Lerp(
                Mathf.Lerp(walkSpeed.maxSpeed, sprintSpeed, sprintFactor),
                crouchSpeed,
                crouchingRatio);
            return speed;
        }

        protected virtual CharacterSpeed GetSpeedInAir()
        {
            return airSpeed;
        }

        protected float GetAvailableHeight(float targetHeight, float heightReduction)
        {
            if (targetHeight < caster.height) {
                return targetHeight;
            }
            var hitResult = caster.SweepTest(
                transform.position,
                transform.up,
                out var casterHit,
                targetHeight - heightReduction + contactOffsetDistance,
                contactOffsetDistance,
                caster.height - heightReduction);
            if (!hitResult) {
                return targetHeight;
            }
            return Mathf.Max(casterHit.distance + heightReduction - contactOffsetDistance, 0.0f);
        }

        protected void UpdateHeight()
        {
            availableHeight = GetAvailableHeight(height, crouchHeight);
            var targetHeight = Mathf.Lerp(height, crouchHeight, crouchFactor);
            if (!isOnGround && !allowCrouchInAir) {
                targetHeight = height;
            }
            if (useAutoCrouch) {
                caster.height = Mathf.Min(availableHeight, targetHeight);
            } else if (targetHeight <= availableHeight) {
                caster.height = targetHeight;
            }
        }

        protected void MoveInDirection(in Vector3 wishDirection, float deltaTime)
        {
            UpdateHeight();
            lastVelocity = velocity;
            if (!TryUnstuck()) {
                // Check if character is stuck, if true do not allow movement until unstuck algorithm will find
                // a way to do its job
                velocity = Vector3.zero;
                return;
            }
            UpdateGroundState();
            if (useGravity) {
                velocity += Physics.gravity * deltaTime;
            }
            if (isOnGround) {
                velocity = groundPlane.WithNormal(transform.up).ClipVelocity(velocity);
                MoveOnGround(wishDirection, GetSpeedOnGround(), deltaTime);
            } else {
                MoveInAir(wishDirection, GetSpeedInAir(), deltaTime);
            }
            UpdateGroundState();
        }

        protected void UpdateGroundState()
        {
            var up = transform.up;
            var wasOnGround = isOnGround;
            var groundResult = caster.SweepTest(
                transform.position + up * feetLiftHeight,
                -up,
                out var groundHit,
                feetLiftHeight + landingSnapDistance,
                contactOffsetDistance,
                feetLiftHeight);
            if (!groundResult || !caster.IsGround(groundHit, maxStandableAngle, out var groundNormal)) {
                ClearGroundState();
                return;
            }
            groundPlane = ClippingPlane.FromRaycastHit(groundHit, groundNormal);
            groundCollider = groundHit.collider;
            if (Vector3.Dot(velocity - groundPlane.velocity, groundPlane.normal) <= 0.0f) {
                transform.position += up * (feetLiftHeight - groundHit.distance + contactOffsetDistance);
            }
            velocity = groundPlane.WithNormal(up).ClipVelocity(velocity);
            if (!wasOnGround) {
                landedOnGround?.Invoke(Mathf.Max(Vector3.Dot(velocity - lastVelocity, groundPlane.normal), 0.0f));
            }
        }

        protected void ClearGroundState()
        {
            groundCollider = null;
            groundPlane = new ClippingPlane(Vector3.zero, Vector3.zero);
        }

        protected void MoveOnGround(in Vector3 wishDirection, in CharacterSpeed specs, float deltaTime)
        {
            ApplyFriction(groundPlane.velocity, specs.friction, deltaTime);
            if (wishDirection != Vector3.zero && specs.maxSpeed != 0.0f) {
                Accelerate(wishDirection, groundPlane.velocity, specs.maxSpeed, specs.acceleration, deltaTime);
            }
            var jumpVelocity = Mathf.Lerp(this.jumpVelocity, crouchJumpVelocity, crouchingRatio);
            if (shouldJump && jumpVelocity > 0.0f) {
                ClearGroundState();
                velocity += transform.up * jumpVelocity;
                MoveInAir(wishDirection, airSpeed, deltaTime);
                jumped?.Invoke();
                return;
            }
            MovePosition(standingOnGround: true, allowFeetLift: true, deltaTime);
        }

        protected void MoveInAir(in Vector3 wishDirection, in CharacterSpeed specs, float deltaTime)
        {
            ApplyFriction(Vector3.zero, specs.friction, deltaTime);
            if (wishDirection != Vector3.zero && specs.maxSpeed != 0.0f) {
                Accelerate(wishDirection, Vector3.zero, specs.maxSpeed, specs.acceleration, deltaTime);
            }
            MovePosition(standingOnGround: false, liftFeetInAir, deltaTime);
        }

        protected void Accelerate(
            in Vector3 wishDirection,
            in Vector3 contactVelocity,
            float maxSpeed,
            float acceleration,
            float deltaTime)
        {
            var wishVelocity = wishDirection * maxSpeed;
            var grouldLocalVelocity = velocity - contactVelocity;
            var currentSpeed = Vector3.Dot(grouldLocalVelocity, wishDirection);
            // This fixes speed going above max while wall-running, zig-zaging and other Quake engine related stuff
            if (currentSpeed > 0.0f) {
                currentSpeed = Mathf.Lerp(grouldLocalVelocity.magnitude, currentSpeed, indirectAccelerationRatio);
            }
            var extraSpeed = MathF.Min(acceleration * deltaTime, maxSpeed - currentSpeed);
            velocity += wishDirection * MathF.Max(extraSpeed, 0.0f);
        }

        protected void ApplyFriction(in Vector3 contactVelocity, float frictionAmount, float timeDelta)
        {
            var frictionVelocity = velocity - contactVelocity;
            var frictionSpeed = frictionAmount * timeDelta;
            if (frictionVelocity.sqrMagnitude > frictionSpeed * frictionSpeed) {
                frictionVelocity = frictionVelocity.normalized * frictionSpeed;
            }
            velocity -= frictionVelocity;
        }

        protected void MovePosition(bool standingOnGround, bool allowFeetLift, float deltaTime)
        {
            if (velocity.Equals(Vector3.zero)) {
                return;
            }
            CreateMoveHelper(out var moveHelper);
            var heightReduction = useAutoCrouch ? (caster.height - crouchHeight) : 0.0f;
            if (allowFeetLift && feetLiftHeight > 0.0f) {
                moveHelper.TryMoveWithFeetLift(
                    standingOnGround,
                    feetLiftHeight,
                    landingSnapDistance,
                    deltaTime,
                    heightReduction);
            } else {
                moveHelper.TryMove(standingOnGround, deltaTime, heightReduction);
            }
            if (standingOnGround) {
                moveHelper.SnapToGround(feetLiftHeight, groundSnapDistance);
            }
            UpdateHeight();
            TryUnstuck();
            transform.position = moveHelper.position;
            velocity = moveHelper.velocity;
        }

        private AbstractCaster CreateCaster(Collider collider, int layerMask)
        {
            if (collider is CapsuleCollider capsuleCollider) {
                return new CapsuleCaster(capsuleCollider, layerMask);
            } else {
                throw new NotSupportedException($"Collider of type {collider.GetType().Name} is not supported");
            }
        }

        private void CreateMoveHelper(out MoveHelper moveHelper)
        {
            moveHelper = new MoveHelper(transform.position, velocity, transform.up, caster) {
                maxStandableAngle = maxStandableAngle,
                contactOffsetDistance = contactOffsetDistance,
                groundBounce = groundBounce,
                wallBounce = wallBounce
            };
        }

        private void OnEnable()
        {
            InvalidateCollider();
            BreakInterpolation();
        }

        private void Update()
        {
            GetComponent<CharacterEyes>()?.UpdateTransform(localLookRotation, Vector3.up, availableHeight);
            if (breakInterpolationTime <= Time.fixedTime) {
                lastPosition = positionLerp.Evaluate();
                transform.position = lastPosition;
            }
        }

        private void FixedUpdate()
        {
            if (breakInterpolationTime < Time.fixedTime && transform.position.Equals(lastPosition)) {
                transform.position = positionLerp.current;
            } else {
                positionLerp.Update(transform.position);
            }
            MoveInDirection(moveRotation * wishDirection, Time.deltaTime);
            positionLerp.Update(transform.position);
            lastPosition = transform.position;
            shouldJump = false;
        }

        private void Reset()
        {
            width = DefaultWidth;
            height = DefaultHeight;
            crouchHeight = DefaultCrouchingHeight;
            allowCrouchInAir = false;
            useAutoCrouch = false;
            maxStandableAngle = MoveHelper.DefaultMaxStandableAngle;
            indirectAccelerationRatio = DefaultIndirectAccelerationRatio;
            feetLiftHeight = DefaultFeetLiftHeight;
            liftFeetInAir = false;
            groundSnapDistance = DefaultGroundSnapDistance;
            landingSnapDistance = DefaultLandingSnapDistance;
            contactOffsetDistance = MoveHelper.DefaultRaycastBackoffDistance;
            unstuckMaxAttempts = MoveHelper.DefaultMaxUnstuckAttempts;
            layerMask.value = DefaultLayerMask;
            useGravity = true;
            walkSpeed = new CharacterSpeed {
                maxSpeed = DefaultWalkSpeed,
                acceleration = DefaultWalkAcceleration,
                friction = DefaultWalkFriction
            };
            sprintSpeed = DefaultSprintSpeed;
            crouchSpeed = DefaultCrouchSpeed;
            airSpeed = new CharacterSpeed {
                maxSpeed = DefaultAirSpeed,
                acceleration = DefaultAirAcceleration,
                friction = DefaultAirFriction
            };
            jumpVelocity = ComputeJumpVelocity(DefaultJumpHeight, Vector3.Dot(Physics.gravity, Vector3.up));
            crouchJumpVelocity = ComputeJumpVelocity(DefaultCrouchJumpHeight, Vector3.Dot(Physics.gravity, Vector3.up));
            groundBounce = DefaultBounce;
            wallBounce = DefaultBounce;
        }

        private void OnDrawGizmosSelected()
        {
            if (isOnGround) {
                caster.DrawGroundPlaneGizmo(groundPlane);
            }
        }
    }
}
