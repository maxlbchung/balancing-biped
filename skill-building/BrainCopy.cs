using UnityEngine;

namespace BipedGeneration2
{
    // BrainCopy: identical algorithm to examples/Brain.cs, but every gain / torque
    // is the raw value the SKILL.md formulas produce — no hand-tuning. Use this
    // when you want to see how the formulas perform straight off the page.
    //
    // Recomputed from the default Spawner geometry (1.5×1.5 body, 0.3×1.0 thigh
    // and shin, 1.0×0.2 foot) using the procedures in references/mass-calculation.md
    // and references/inertia-calculation.md:
    //
    //   Masses from area × density (upper body = 1, legs = 2):
    //     body  = 1.5·1.5·1 = 2.25
    //     thigh = 0.3·1.0·2 = 0.6
    //     shin  = 0.3·1.0·2 = 0.6
    //     foot  = 1.0·0.2·2 = 0.4
    //     totalMass = 2.25 + 2·(0.6 + 0.6 + 0.4) = 5.45
    //
    //   Upper-body inertia about the hip-pivot midpoint (body centered at (0, 0),
    //   hip midpoint at (0, −0.75)):
    //     I_centroid = 2.25·(1.5² + 1.5²)/12       = 0.84375
    //     m·d²       = 2.25·0.75²                  = 1.265625
    //     I_upperBody                              = 2.109375
    //
    //   Gains (SKILL.md Step 3.3):
    //     bodyUprightGain = 2 · I_upperBody  ≈ 4.22
    //     hipRestGain     = 1.00 · 4.22      ≈ 4.22
    //     kneeGain        = 0.56 · 4.22      ≈ 2.36
    //     ankleGain       = 0.44 · 4.22      ≈ 1.86
    //
    //   Torques (SKILL.md Step 3.2):
    //     hipTorque   = 20 · 5.45            = 109
    //     kneeTorque  = 0.5  · 109           = 54.5
    //     ankleTorque = 0.43 · 109           ≈ 46.87
    public class BrainCopy : MonoBehaviour, IBipedController
    {
        [Header("Hip controller")]
        public float bodyUprightGain = 4.22f;
        public float hipRestGain = 4.22f;
        public float hipTorque = 109f;

        [Header("Knee / ankle (world-angle P)")]
        public float kneeGain = 2.36f;
        public float ankleGain = 1.86f;
        public float kneeTorque = 54.5f;
        public float ankleTorque = 46.87f;
        public float ankleRestAngle = 1f;

        [Header("Limits")]
        public float maxMotorSpeed = 300f;

        [Header("Tilt resistance")]
        [Tooltip("Tilt magnitude (deg) below which no resistance is applied.")]
        public float tiltDeadzone = 5f;
        [Tooltip("Tilt magnitude (deg) above the deadzone at which resistance reaches full scale.")]
        public float tiltFullScaleRange = 30f;
        [Tooltip("Torque multiplier on the side the body is tilting toward, at full scale.")]
        public float strongSideTorqueMultiplier = 1.25f;
        [Tooltip("Torque multiplier on the opposite side, at full scale.")]
        public float weakSideTorqueMultiplier = 0.25f;
        [Tooltip("Hip joint-angle offset (deg) added outward on the strong side at full scale.")]
        public float hipExtensionDegrees = 45;
        [Tooltip("Ankle world-angle target (deg) pushed toe-down on the strong side at full scale.")]
        public float ankleDownDegrees = 30f;
        [Tooltip("Sign multiplier for the tilt response. Flip to -1 if the wrong leg responds.")]
        public int tiltResponseSign = 1;

        [Header("Velocity gating")]
        [Tooltip("Lerp factor for smoothing body angular velocity (0 = frozen, 1 = no smoothing).")]
        [Range(0f, 1f)] public float angularVelocitySmoothing = 0.1f;
        [Tooltip("Smoothed angular speed (deg/s) below which the body is treated as not actively rotating.")]
        public float angularVelocityDeadzone = 3f;

        Rigidbody2D body;
        LegRig leftLeg;
        LegRig rightLeg;
        float smoothedAngularVelocity;

        public void SetRig(Rigidbody2D bodyRb, LegRig left, LegRig right)
        {
            body = bodyRb;
            leftLeg = left;
            rightLeg = right;
        }

        void FixedUpdate()
        {
            if (body == null || leftLeg == null || rightLeg == null) return;

            smoothedAngularVelocity = Mathf.Lerp(smoothedAngularVelocity, body.angularVelocity, angularVelocitySmoothing);

            float tilt = Mathf.DeltaAngle(0f, body.rotation);
            float tiltMag = Mathf.Abs(tilt);
            float tiltScale = Mathf.Clamp01((tiltMag - tiltDeadzone) / Mathf.Max(0.0001f, tiltFullScaleRange));
            float tiltDir = (tiltScale > 0f) ? Mathf.Sign(tilt) : 0f;

            float angVelDir = (Mathf.Abs(smoothedAngularVelocity) < angularVelocityDeadzone) ? 0f : Mathf.Sign(smoothedAngularVelocity);
            if (angVelDir == -tiltDir)
            {
                tiltScale = 0f;
                tiltDir = 0f;
            }

            UpdateLeg(leftLeg, tiltScale, tiltDir);
            UpdateLeg(rightLeg, tiltScale, tiltDir);
        }

        void UpdateLeg(LegRig leg, float tiltScale, float tiltDir)
        {
            // Body tilts toward the side opposite its rotation sign (CCW rotation -> top tilts left -> falls left).
            // Strong side: leg whose layoutSign matches the fall direction (-tiltDir).
            float sideScore = (tiltDir == 0f) ? 0f : -tiltDir * Mathf.Sign(leg.layoutSign) * Mathf.Sign(tiltResponseSign);
            float strongAmount = Mathf.Max(0f, sideScore) * tiltScale;
            float weakAmount = Mathf.Max(0f, -sideScore) * tiltScale;

            float torqueMultiplier = Mathf.Lerp(1f, strongSideTorqueMultiplier, strongAmount)
                                   * Mathf.Lerp(1f, weakSideTorqueMultiplier, weakAmount);

            float bodyError = Mathf.DeltaAngle(body.rotation, 0f);
            float hipTargetOffset = strongAmount * hipExtensionDegrees * Mathf.Sign(leg.outwardSign);
            float hipSpeed = bodyError * bodyUprightGain - (leg.hip.jointAngle - hipTargetOffset) * hipRestGain;
            SetMotor(leg.hip, hipSpeed, hipTorque * torqueMultiplier);

            DriveToWorldAngle(leg.knee, leg.shin.rotation, 0f, kneeGain, kneeTorque * torqueMultiplier);

            float ankleTarget = (ankleRestAngle + strongAmount * ankleDownDegrees) * Mathf.Sign(leg.outwardSign);
            DriveToWorldAngle(leg.ankle, leg.foot.rotation, ankleTarget, ankleGain, ankleTorque * torqueMultiplier);
        }

        void SetMotor(HingeJoint2D joint, float speed, float maxTorque)
        {
            if (joint == null) return;
            var m = joint.motor;
            m.motorSpeed = Mathf.Clamp(speed, -maxMotorSpeed, maxMotorSpeed);
            m.maxMotorTorque = maxTorque;
            joint.motor = m;
            joint.useMotor = true;
        }

        void DriveToWorldAngle(HingeJoint2D joint, float currentWorld, float targetWorld, float gain, float maxTorque)
        {
            if (joint == null) return;
            float error = Mathf.DeltaAngle(currentWorld, targetWorld);
            SetMotor(joint, -error * gain, maxTorque);
        }

    }
}
