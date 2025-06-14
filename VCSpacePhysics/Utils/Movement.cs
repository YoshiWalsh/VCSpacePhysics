using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VCSpacePhysics.Utils
{
    internal class Movement
    {
        // All provided values must use consistent distance & time units. E.g. if the relativeVelocity is specified in m/s, the maxAccelerationMagnitude should be specified as m/s/s
        public static Vector3 ApproximateRequiredAcceleration(Vector3 desiredDisplacement, Vector3 currentVelocity, float maxAccelerationMagnitude)
        {
            // TODO: If I can figure out a way to factor in turnover time, this could serve as a starting point for autopilot

            var distanceToTarget = desiredDisplacement.magnitude;
            var directionToTarget = desiredDisplacement.normalized;

            // Calculate theoretical total amount of acceleration required to cancel perpendicular movement if that's all we were doing
            var requiredPerpendicularAcceleration = -Vector3.ProjectOnPlane(currentVelocity, directionToTarget);

            // Estimate theoretical total amount of acceleration required to reach the target point if there were no perpendicular movement
            var currentVelocityTowardsTarget = Vector3.Dot(directionToTarget, currentVelocity) / directionToTarget.magnitude;
            if(Single.IsNaN(currentVelocityTowardsTarget))
            {
                currentVelocityTowardsTarget = 0;
            }
            var currentVelocityTowardsTargetSquare = Math.Pow(currentVelocityTowardsTarget, 2);
            var stoppingTimeAtCurrentVelocity = currentVelocityTowardsTarget * Math.Sign(currentVelocityTowardsTarget) / maxAccelerationMagnitude;
            var stoppingDistanceAtCurrentVelocity = currentVelocityTowardsTarget * stoppingTimeAtCurrentVelocity / 2;

            var turnoverReached = stoppingDistanceAtCurrentVelocity >= distanceToTarget;
            double requiredColinearAcceleration;
            double timeBeforeTurnover;
            double timeAfterTurnover;
            if(!turnoverReached) // The two paths here are very similar and could be merged, but I think it's easier to read this way
            {
                // We can afford to keep accelerating, calculate how much and find total acceleration
                var remainingAccelerationDistance = (distanceToTarget - stoppingDistanceAtCurrentVelocity) / 2f;
                var maximumVelocity = Math.Sqrt(currentVelocityTowardsTargetSquare + 2 * maxAccelerationMagnitude * remainingAccelerationDistance);
                var accelerationTime = (maximumVelocity - currentVelocityTowardsTarget) / maxAccelerationMagnitude;
                var decelerationTime = maximumVelocity / maxAccelerationMagnitude;
                requiredColinearAcceleration = accelerationTime + decelerationTime;
                timeBeforeTurnover = accelerationTime;
                timeAfterTurnover = decelerationTime;
            } else
            {
                // We need to decelerate now, find total acceleration (including overshoot correction if required)
                var overshootAccelerationDistance = (stoppingDistanceAtCurrentVelocity - distanceToTarget) / 2f;
                var correctionMaxVelocity = Math.Sqrt(2 * maxAccelerationMagnitude * overshootAccelerationDistance);
                var initialDecelerationTime = currentVelocityTowardsTarget / maxAccelerationMagnitude;
                var overshootCorrectionTime = correctionMaxVelocity * 2 / maxAccelerationMagnitude;
                requiredColinearAcceleration = -(initialDecelerationTime + overshootCorrectionTime);
                timeBeforeTurnover = 0;
                timeAfterTurnover = requiredColinearAcceleration;
            }

            // We need to figure out a balance between cancelling our perpendicular momentum & moving towards the goal.
            // I'm not 100% happy with the way I'm balancing this at the moment, but it's good enough for now.
            var perpendicularPriority = Mathf.Clamp01((float)(timeAfterTurnover * 1.5f / (timeBeforeTurnover + timeAfterTurnover))); // Figuring out a nicer way to do this would yield smoother results
            
            var accelerationForPerpendicular = Mathf.Min(requiredPerpendicularAcceleration.magnitude, perpendicularPriority * maxAccelerationMagnitude);
            var accelerationForColinear = Mathf.Min(Mathf.Abs((float) requiredColinearAcceleration), maxAccelerationMagnitude - accelerationForPerpendicular);
            var totalAcceleration = accelerationForPerpendicular + accelerationForColinear;
            var accelerationRatio = totalAcceleration > 0 ? accelerationForColinear / totalAcceleration : 0;

            var blendedAcceleration = Vector3.Lerp(requiredPerpendicularAcceleration, directionToTarget * (float)requiredColinearAcceleration, accelerationRatio);

            return blendedAcceleration.normalized * totalAcceleration;
        }
    }
}
