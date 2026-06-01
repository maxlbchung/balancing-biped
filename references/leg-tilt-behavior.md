### Behavior of each leg when the creature is falling over

All offsets and multipliers scale linearly with tilt magnitude: zero below the tilt deadzone, reaching full value at `tiltFullScaleRange` degrees above the deadzone (see Step 3.4 of SKILL.md).

Strong side — leg on the side the body is tilting toward:
- Torque is multiplied by `strongSideTorqueMultiplier` (default 1.25) at full tilt.
- Hip target angle shifts outward by `hipExtensionDegrees` (default 45) at full tilt, driving the hip to extend.
- Knee target is unchanged (shin still driven to world-angle 0, pointing straight down).
- Ankle target shifts toe-down by `ankleDownDegrees` (default 30) at full tilt.

Weak side — opposite leg:
- Torque is multiplied by `weakSideTorqueMultiplier` (default 0.25) at full tilt.
- No positional offsets — hip, knee, and ankle targets are the same as during natural stance.
