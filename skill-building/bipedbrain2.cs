using UnityEngine;

namespace BipedGen2
{
    // Gains and torques below are pre-computed from the default Spawner rig:
    //   Upper body (rigid): body circle r=0.75 m=3.53, 2 ears m=0.07 each,
    //   2 upper arms 0.18x0.65 m=0.23 each, 2 forearms 0.18x0.55 m=0.20 each.
    //   Combined upper body mass = 4.52, COM ~(0, -0.02), I_combined ~= 2.36.
    //   Hip pivots at y=-0.6 -> h = 0.58.
    //     bodyUprightGain = 3 * 2.36 / 0.58^2 ~= 21
    //     hipRestGain = 0.33 * 21 ~= 7
    //     kneeGain    = 0.56 * 21 ~= 11.8
    //     ankleGain   = 0.44 * 21 ~= 9.2
    //   Lower body adds 2*(thigh 0.33 + shin 0.33 + foot 0.165) = 1.65.
    //   Total mass ~= 6.17 -> hipTorque = 20 * 6.17 ~= 125.
    //     kneeTorque  = 0.50 * 125 = 62.5
    //     ankleTorque = 0.43 * 125 ~= 54
    public class bipedbrain2 : MonoBehaviour
    {
        [Header("Hip controller")]
        public float bodyUprightGain = 21f;
        public float hipRestGain = 7f;
        public float hipTorque = 125f;

        [Header("Knee / ankle (world-angle P)")]
        public float kneeGain = 11.8f;
        public float ankleGain = 9.2f;
        public float kneeTorque = 62.5f;
        public float ankleTorque = 54f;
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
        LegRig2 leftLeg;
        LegRig2 rightLeg;
        float smoothedAngularVelocity;

        public void SetRig(Rigidbody2D bodyRb, LegRig2 left, LegRig2 right)
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

        void UpdateLeg(LegRig2 leg, float tiltScale, float tiltDir)
        {
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
