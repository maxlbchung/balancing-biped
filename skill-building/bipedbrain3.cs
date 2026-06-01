using UnityEngine;

namespace BipedGen3
{
    // Gains and torques are pre-computed from the default bipedconstructor3 rig:
    //   Upper body (rigid weld): body circle r=0.75 m=3.53 I=0.99
    //                           + head triangle 0.65x0.7 m=0.46 I=0.023 centroid y=0.98.
    //   Combined upper-body COM y ~= 0.11, I_combined ~= 1.41.
    //   Hip pivots at y = -0.6  ->  h (hip -> COM) ~= 0.71.
    //     bodyUprightGain = 3 * 1.41 / 0.71^2  ~= 8.4  -> tuned up to 9
    //     hipRestGain     = 0.33 * 9  ~= 3
    //     kneeGain        = 0.56 * 9  ~= 5
    //     ankleGain       = 0.44 * 9  ~= 3.9
    //   Lower body adds 2 * (thigh 0.33 + shin 0.33 + foot 0.165) = 1.65.
    //   Total mass ~= 5.64  ->  hipTorque = 20 * 5.64 ~= 115.
    //     kneeTorque  = 0.50 * 115 ~= 58
    //     ankleTorque = 0.43 * 115 ~= 50
    public class bipedbrain3 : MonoBehaviour
    {
        [Header("Hip controller")]
        public float bodyUprightGain = 9f;
        public float hipRestGain = 3f;
        public float hipTorque = 115f;

        [Header("Knee / ankle (world-angle P)")]
        public float kneeGain = 5f;
        public float ankleGain = 3.9f;
        public float kneeTorque = 58f;
        public float ankleTorque = 50f;
        public float ankleRestAngle = 1f;

        [Header("Limits")]
        public float maxMotorSpeed = 300f;

        [Header("Tilt resistance")]
        public float tiltDeadzone = 5f;
        public float tiltFullScaleRange = 30f;
        public float strongSideTorqueMultiplier = 1.25f;
        public float weakSideTorqueMultiplier = 0.25f;
        public float hipExtensionDegrees = 45f;
        public float ankleDownDegrees = 30f;
        public int tiltResponseSign = 1;

        [Header("Velocity gating")]
        [Range(0f, 1f)] public float angularVelocitySmoothing = 0.1f;
        public float angularVelocityDeadzone = 3f;

        Rigidbody2D body;
        LegRig3 leftLeg;
        LegRig3 rightLeg;
        float smoothedAngularVelocity;

        public void SetRig(Rigidbody2D bodyRb, LegRig3 left, LegRig3 right)
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

        void UpdateLeg(LegRig3 leg, float tiltScale, float tiltDir)
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
