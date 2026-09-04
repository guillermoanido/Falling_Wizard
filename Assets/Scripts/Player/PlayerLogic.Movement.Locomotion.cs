using UnityEngine;

namespace FallingWizard.Player
{
    public partial class PlayerLogic
    {
        // What actually moves the wizard, in the order FixedTick runs it: which way they face,
        // running (along a ramp where there is one), the step assist, the jump, and gravity.
        public partial class Movement
        {
            void UpdateFacing(float steer)
            {
                if (Mathf.Abs(steer) > steerDeadzone)
                    Facing = steer < 0f ? -1 : 1;

                if (sprite != null)
                    sprite.flipX = Facing < 0;
            }

            // Walking up a low lip, because a BoxCollider2D cannot do it on its own.
            //
            // The wizard is a box with square corners, frozen rotation and - deliberately - no
            // friction, so a lip is a flat vertical face meeting a flat vertical face. The
            // solver's only answer to that is to delete the sideways speed, and Run puts it
            // straight back on the next step. That is what "stuck on the scenery" feels like:
            // the stick is forward, the wizard is not moving, and nothing is actually broken.
            //
            // Kept far below a whole box on purpose. This is for tile seams and the pixel-high
            // teeth along a ramp's edge, NOT for real steps - a wizard who climbs a box without
            // jumping is a wizard for whom jumping has stopped mattering.
            void TryStepUp(Command command, Modifiers stats)
            {
                if (stepHeight <= 0f || body == null || hull == null)
                    return;

                if (lockout > 0f || stats.Rooted)
                    return;

                // The same window a jump is allowed in, so a step taken just after walking off a
                // lip is no more generous than the jump they could have had instead. Anyone
                // genuinely on their way up - a jump, a slime, a fling - is left alone.
                if (coyoteTimer <= 0f || body.linearVelocityY > 0f)
                    return;

                // The STICK, not the speed. Pressed against a lip, Run only ever lands one
                // step's worth of acceleration before the solver takes it away again, so a test
                // on how fast the wizard is really travelling would be false at exactly the
                // moment it matters.
                float steer = command.Steer;

                if (Mathf.Abs(steer) <= steerDeadzone)
                    return;

                int direction = steer < 0f ? -1 : 1;

                if (!TryFindLip(direction, out float lipTop))
                    return;

                Bounds box = hull.bounds;
                float rise = lipTop - box.min.y;

                // Under the skin there is nothing to climb and the contact rides over it on its
                // own; over stepHeight it is a wall, and walls are what the jump is for.
                if (rise <= groundCheckSkin || rise > stepHeight)
                    return;

                Vector2 offset = body.position - (Vector2)box.center;
                var landing = new Vector2(
                    box.center.x + direction * stepReach,
                    box.min.y + rise + StepClearance + box.extents.y);

                // The whole wizard has to fit where they are going. This is the headroom check
                // and the "is that lip actually the top of a wall" check in one query, and it is
                // also what makes the move below safe: the destination is proved empty before
                // the body is ever put there, so it cannot be shoved back out.
                if (Physics2D.OverlapBox(landing, box.size, 0f, GroundFilter, Overlaps) > 0)
                    return;

                // Written straight to Rigidbody2D.position rather than added as upward speed.
                // An impulse IS a jump - it leaves the ground, counts as airtime, fights the
                // short hop, and goes as high as the number of steps the player stayed pressed
                // against the lip. This is a move of a known, already-checked distance.
                body.position = landing + offset;
            }

            void Run(Command command, Modifiers stats, float fixedDeltaTime)
            {
                if (lockout > 0f)
                    return;

                float steer = stats.Rooted ? 0f : command.Steer;
                float topSpeed = command.Walk ? walkSpeed : runSpeed;
                float targetSpeed = steer * topSpeed * stats.MoveSpeedMultiplier;

                // After the move multiply and BEFORE the wind is added, so a canopy carries the
                // wizard further under their own steam without also amplifying a gale.
                if (!IsGrounded)
                    targetSpeed *= stats.AirSpeedMultiplier;

                if (command.Walk && IsGrounded && IsAtEdge && Mathf.Abs(steer) > steerDeadzone)
                    targetSpeed = 0f;

                targetSpeed += wind.x;

                bool steering = Mathf.Abs(steer) > steerDeadzone;
                float rate = steering ? acceleration : groundFriction;

                // Grip scales BOTH branches above, because ice is two things at once and one of
                // them alone is not ice: you keep going after you let go (groundFriction) and you
                // cannot turn round in a hurry (acceleration). Scaling only the first gives a
                // wizard who slides but corners like a car; only the second gives one who feels
                // heavy but stops dead, which reads as mud.
                //
                // ON THE GROUND ONLY, and deliberately. The air rate is already airControl of
                // what it was; putting sheet ice through it as well is not "slippery" but "no
                // air control", and it would mean a trigger box taller than the patch robs the
                // steering of anyone sailing through the top of it for no visible reason.
                if (!IsGrounded)
                    rate *= airControl *
                            (steering ? stats.AirControlMultiplier : stats.AirDragMultiplier);
                else
                    rate *= grip;

                if (TryRunAlongRamp(targetSpeed, topSpeed * stats.MoveSpeedMultiplier,
                        rate * fixedDeltaTime))
                    return;

                body.linearVelocityX =
                    Mathf.MoveTowards(body.linearVelocityX, targetSpeed, rate * fixedDeltaTime);
            }

            // Steering ALONG a ramp instead of across it.
            //
            // Driving purely sideways into a 45-degree face cannot work here and the numbers say
            // why: acceleration is 20 boxes a second squared, of which only cos(45) - about 14 -
            // pushes up the face, while gravity pulls 9.81 x gravityScale x sin(45) - about 21 -
            // straight back down it, and there is no friction to make up the difference. The
            // wizard loses that argument every time and slides, which is what "he glides down
            // instead of going up" is. Worse, Run rewrites the sideways speed absolutely every
            // step, so the up-the-slope velocity the contact solver correctly hands back is
            // thrown away before it can ever add up.
            //
            // So on a ramp the wizard is steered along the surface, both components at once, and
            // gravity is simply not part of the sum.
            bool TryRunAlongRamp(float targetSpeed, float topSpeed, float change)
            {
                bool wasClimbing = climbedLastStep;
                climbedLastStep = false;

                if (!OnRamp)
                {
                    // The ramp has just run out. Whatever upward speed carried the wizard up it
                    // is a hop nobody asked for now the ground is level again, so it is taken
                    // back - but only if walking could plausibly have produced it. A jump or a
                    // slime is faster than any ramp can push and is left alone.
                    if (wasClimbing && IsGrounded && !rising &&
                        body.linearVelocityY > 0f && body.linearVelocityY <= topSpeed)
                        body.linearVelocityY = 0f;

                    return false;
                }

                // Already going up faster than walking ever could, so something else - a jump, a
                // bounce, a fling - owns the wizard this step and the ramp keeps out of it.
                if (rising || body.linearVelocityY > topSpeed)
                    return false;

                // Tangent to the surface, always pointing the way x grows, so a positive target
                // speed means "that way along the floor" exactly as it does on the flat.
                var along = new Vector2(groundNormal.y, -groundNormal.x);

                if (along.x < 0f)
                    along = -along;

                // How fast they are already going along the face - CLAMPED to what walking could
                // have produced. Dropping onto a ramp otherwise arrives with the whole fall
                // pointing down the slope, and a 16 b/s landing would fire the wizard away
                // downhill faster than they can ever run back up.
                float carried = Mathf.Clamp(
                    Vector2.Dot(body.linearVelocity, along), -topSpeed, topSpeed);

                float speed = Mathf.MoveTowards(carried, targetSpeed, change);

                // Both components written together, and gravity left out of the sum entirely.
                // That is the whole trick: with nothing pulling them down the face, letting go
                // of the stick leaves the wizard stood on the ramp instead of sliding off it.
                body.linearVelocity = along * speed;
                climbedLastStep = speed * along.y > 0f;
                return true;
            }

            void TryJump(Modifiers stats)
            {
                // ABOVE the air-jump count, not folded into the test below it. Spellbook.Rebuild
                // resets and re-applies every equipped spell each fixed step, so a spell granting
                // ExtraJumps hands them out continuously - and a wizard who cannot jump off the
                // floor but can still jump in mid-air is the worst of both.
                //
                // This is the ONLY thing switched off. Launch is a separate entry point that
                // nothing here reaches, so a slime, a fling and a ramp all carry on unchanged,
                // and `rising` simply never becomes true, which leaves ApplyShortHop inert rather
                // than clipping a bounce when the button comes up.
                if (!canJump)
                    return;

                bool onGroundOrCoyote = coyoteTimer > 0f;
                bool hasAirJump = airJumpsUsed < stats.ExtraJumps;

                if (bufferTimer <= 0f || (!onGroundOrCoyote && !hasAirJump))
                    return;

                if (!onGroundOrCoyote)
                    airJumpsUsed++;

                bufferTimer = 0f;
                coyoteTimer = 0f;
                rising = true;

                body.linearVelocityY =
                    Mathf.Sqrt(2f * BaseGravity * jumpHeight * stats.JumpHeightMultiplier);
            }

            void ApplyShortHop(bool jumpHeld)
            {
                if (!rising || jumpHeld)
                    return;

                if (body.linearVelocityY > 0f)
                    body.linearVelocityY *= shortHopMultiplier;

                rising = false;
            }

            void ApplyFallGravity(Modifiers stats)
            {
                float floatiness = stats.FallSpeedMultiplier;

                // Not while stood on something. A wizard easing down a ramp has a negative
                // vertical speed without falling at all, and putting the fall multiplier under
                // them there turns every ramp into a slide they cannot walk back up.
                bool falling = !IsGrounded && body.linearVelocityY < 0f;

                body.gravityScale = falling
                    ? baseGravityScale * fallGravityMultiplier * floatiness
                    : baseGravityScale;

                float terminalSpeed = maxFallSpeed * floatiness;
                if (body.linearVelocityY < -terminalSpeed)
                    body.linearVelocityY = -terminalSpeed;
            }
        }
    }
}
