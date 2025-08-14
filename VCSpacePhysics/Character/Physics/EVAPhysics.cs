using CG.Game.Player;
using Cinemachine.Utility;
using Opsive.UltimateCharacterController.Character.Abilities;
using UnityEngine;
using VCSpacePhysics.Utils;

namespace VCSpacePhysics.Character.Physics
{
    public class EVAPhysics : MonoBehaviour
    {
        public CustomCharacterLocomotion _locomotion;
        public Player _player;
        public CustomFirstPersonCombat _firstPersonView;

        public float RollInput = 0f;
        public float RollDeadzone = 0.05f;
        public Vector3? GravityDirection = null;
        public float MaximumThrust = 0.1f;

        public Quaternion? PreviousRotation = null;

        public void Awake()
        {
            _locomotion = gameObject.GetComponent<CustomCharacterLocomotion>();
            _player = gameObject.GetComponent<Player>();
        }

        public void Update()
        {
            _firstPersonView.useLockedRelativeRotation = CharacterUtils.IsPlayerSpaceborne(_locomotion);
        }

        public void FixedUpdate()
        {
            if(!_locomotion.Rigidbody.isKinematic)
            {
                _firstPersonView.m_BaseRotation = _locomotion.transform.rotation;
            }
            if (CharacterUtils.IsPlayerFlying(_locomotion))
            {
                if (_firstPersonView != null)
                {
                    if (PreviousRotation is Quaternion previousRotation)
                    {
                        // Adjust camera facing direction based on rotation from last frame
                        var cameraDirectionOldLocal = Quaternion.Euler(_firstPersonView.m_Pitch, _firstPersonView.m_Yaw, 0f) * Vector3.forward;
                        var cameraDirectionLastFrameWorldspace = previousRotation * cameraDirectionOldLocal;
                        var cameraDirectionNewLocal = _locomotion.Rigidbody.transform.InverseTransformDirection(cameraDirectionLastFrameWorldspace);
                        var cameraAnglesNewLocal = Quaternion.LookRotation(cameraDirectionNewLocal, Vector3.up).eulerAngles;
                        _firstPersonView.m_Pitch = Mathf.Clamp(cameraAnglesNewLocal.x, _firstPersonView.PitchLimit.MinValue, _firstPersonView.PitchLimit.MaxValue);
                        _firstPersonView.m_Yaw = Mathf.Clamp(cameraAnglesNewLocal.y, -_firstPersonView.YawLimit, _firstPersonView.YawLimit);
                    }

                    // Apply thrust
                    var rolling = Mathf.Abs(RollInput) > RollDeadzone;
                    Vector3 requiredRotation;
                    if(GravityDirection is Vector3 downDirection && !rolling)
                    {
                        // If we're under the influence of a gravity field, we should assist the player into a logical orientation.
                        // The desired final orientation for the character is as follows:
                        // 1. Player's pitch & roll are upright within gravity field
                        // 2. Player's yaw is pointing towards the camera direction
                        var cameraDirectionLocal = new Vector3(_firstPersonView.m_Pitch, _firstPersonView.m_Yaw, 0f);
                        var upDirectionLocal = _locomotion.Rigidbody.transform.InverseTransformDirection(downDirection * -1);
                        var forwardDirectionLocal = Vector3.ProjectOnPlane(cameraDirectionLocal, upDirectionLocal);
                        var requiredRotationQuaternion = Quaternion.LookRotation(forwardDirectionLocal, upDirectionLocal);
                        requiredRotation = requiredRotationQuaternion.eulerAngles;
                    } else
                    {
                        // If we're in free space (or the player is currently rolling), we should just adjust pitch & yaw to point
                        // the player where the camera is facing.
                        requiredRotation = new Vector3(_firstPersonView.m_Pitch, _firstPersonView.m_Yaw, 0f);
                    }

                    var currentAngularVelocity = _locomotion.Rigidbody.angularVelocity;
                    Vector3 attitudeInput = Movement.ApproximateRequiredAcceleration(requiredRotation, currentAngularVelocity, MaximumThrust) / MaximumThrust;
                    if (rolling)
                    {
                        attitudeInput.z = 0;
                        attitudeInput.Normalize();
                    }
                    var attitudeThrust = attitudeInput * MaximumThrust;

                    _locomotion.Rigidbody.AddTorque(attitudeThrust, ForceMode.Acceleration);

                    //AddRotation(new Vector3(_firstPersonView.m_Pitch, 0f, 0f));
                    //_firstPersonView.m_Pitch = 0f;

                    //var pitchRotation = Quaternion.AngleAxis(PendingRotation.x, _locomotion.transform.right);
                    //var yawRotation = Quaternion.AngleAxis(PendingRotation.y, _locomotion.transform.up);
                    //var rollRotation = Quaternion.AngleAxis(PendingRotation.z, _locomotion.transform.forward);
                    //var worldspaceRotation = rollRotation * pitchRotation * yawRotation;
                    //RotatePositionAroundCenterOfGravity(worldspaceRotation);

                    //_firstPersonView.m_BaseRotation = worldspaceRotation * _firstPersonView.m_BaseRotation;

                    PreviousRotation = _locomotion.Rigidbody.transform.rotation;
                }
            } else
            {
                PreviousRotation = null;
            }
        }
    }
}
