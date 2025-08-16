using CG;
using CG.Game.Player;
using CG.Input;
using Cinemachine.Utility;
using Opsive.UltimateCharacterController.Character.Abilities;
using UnityEngine;
using UnityEngine.InputSystem;
using VCSpacePhysics.Utils;

namespace VCSpacePhysics.Character.Physics
{
    public class EVAPhysics : MonoBehaviour
    {
        public CustomCharacterLocomotion _locomotion;
        public Player _player;
        public CustomFirstPersonCombat _firstPersonView;
        private InputActionReferences InputActionReferences => ServiceBase<InputService>.Instance.InputActionReferences;

        public float RollInput => rollInputPositive - rollInputNegative;
        private float rollInputNegative = 0f;
        private float rollInputPositive = 0f;
        public float RollDeadzone = 0.05f;
        public Vector3? GravityDirection = null;
        public float MaximumThrust = 1f; // radians / second / second

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

        public void EnableInput()
        {
            InputActionReferences.StrafeLeft.action.performed += RollCCW;
            InputActionReferences.StrafeLeft.action.canceled += RollCCW;
            InputActionReferences.StrafeRight.action.performed += RollCW;
            InputActionReferences.StrafeRight.action.canceled += RollCW;
        }
        public void DisableInput()
        {
            InputActionReferences.StrafeLeft.action.performed -= RollCCW;
            InputActionReferences.StrafeLeft.action.canceled -= RollCCW;
            InputActionReferences.StrafeRight.action.performed -= RollCW;
            InputActionReferences.StrafeRight.action.canceled -= RollCW;
        }

        public void RollCCW(InputAction.CallbackContext obj)
        {
            rollInputNegative = obj.ReadValue<float>();
            Plugin.logger.LogError("Roll left " + rollInputNegative);
        }

        public void RollCW(InputAction.CallbackContext obj)
        {
            rollInputPositive = obj.ReadValue<float>();
            Plugin.logger.LogError("Roll right " + rollInputNegative);
        }

        private float DegreesAroundZero(float d)
        {
            if(d > 180f)
            {
                return d - 360f;
            }
            return d;
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
                    var currentAngularVelocity = _locomotion.Rigidbody.transform.InverseTransformDirection(_locomotion.Rigidbody.angularVelocity); // radians / second
                    if (PreviousRotation is Quaternion previousRotation)
                    {
                        // Adjust camera facing direction based on rotation from last frame
                        /*var cameraDirectionOldLocal = Quaternion.Euler(_firstPersonView.m_Pitch, _firstPersonView.m_Yaw, 0f) * Vector3.forward;
                        var cameraDirectionLastFrameWorldspace = previousRotation * cameraDirectionOldLocal;
                        var cameraDirectionNewLocal = _locomotion.Rigidbody.transform.InverseTransformDirection(cameraDirectionLastFrameWorldspace);
                        var cameraAnglesNewLocal = Quaternion.LookRotation(cameraDirectionNewLocal, Vector3.up).eulerAngles;
                        _firstPersonView.m_Pitch = Mathf.Clamp(DegreesAroundZero(cameraAnglesNewLocal.x), _firstPersonView.PitchLimit.MinValue, _firstPersonView.PitchLimit.MaxValue);
                        _firstPersonView.m_Yaw = Mathf.Clamp(DegreesAroundZero(cameraAnglesNewLocal.y), -_firstPersonView.YawLimit, _firstPersonView.YawLimit);*/
                        _firstPersonView.m_Pitch = Mathf.Clamp(DegreesAroundZero(_firstPersonView.m_Pitch  - currentAngularVelocity.x), _firstPersonView.PitchLimit.MinValue, _firstPersonView.PitchLimit.MaxValue);
                        _firstPersonView.m_Yaw = Mathf.Clamp(DegreesAroundZero(_firstPersonView.m_Yaw - currentAngularVelocity.y), -_firstPersonView.YawLimit, _firstPersonView.YawLimit);
                    }

                    var cameraForwardWorldspace = _firstPersonView.FirstPersonCameraTransform.forward;
                    var cameraUpWorldspace = _firstPersonView.FirstPersonCameraTransform.up;
                    var cameraForwardLocal = _locomotion.Rigidbody.transform.InverseTransformDirection(cameraForwardWorldspace);
                    var cameraUpLocal = _locomotion.Rigidbody.transform.InverseTransformDirection(cameraUpWorldspace);
                    var cameraLocalRotation = Quaternion.LookRotation(cameraForwardLocal, cameraUpLocal).eulerAngles;
                    var lookDirection = new Vector2(DegreesAroundZero(cameraLocalRotation.x), DegreesAroundZero(cameraLocalRotation.y));

                    Plugin.logger.LogMessage(lookDirection);


                    // Apply thrust
                    var rolling = Mathf.Abs(RollInput) > RollDeadzone;
                    Vector3 requiredRotation;
                    //requiredRotation = Vector3.zero;
                    if (GravityDirection is Vector3 downDirection && !rolling)
                    {
                        // If we're under the influence of a gravity field, we should assist the player into a logical orientation.
                        // The desired final orientation for the character is as follows:
                        // 1. Player's pitch & roll are upright within gravity field
                        // 2. Player's yaw is pointing towards the camera direction
                        var cameraDirectionLocal = new Vector3(lookDirection.x, lookDirection.y, 0f);
                        var upDirectionLocal = _locomotion.Rigidbody.transform.InverseTransformDirection(downDirection * -1);
                        var forwardDirectionLocal = Vector3.ProjectOnPlane(cameraDirectionLocal, upDirectionLocal);
                        var requiredRotationQuaternion = Quaternion.LookRotation(forwardDirectionLocal, upDirectionLocal);
                        requiredRotation = requiredRotationQuaternion.eulerAngles;
                    }
                    else
                    {
                        // If we're in free space (or the player is currently rolling), we should just adjust pitch & yaw to point
                        // the player where the camera is facing.
                        requiredRotation = new Vector3(lookDirection.x, lookDirection.y, 0f);
                    }

                    Vector3 attitudeInput = Movement.ApproximateRequiredAcceleration(requiredRotation * Mathf.Deg2Rad, currentAngularVelocity, MaximumThrust) / MaximumThrust;
                    if (rolling)
                    {
                        attitudeInput.z = -RollInput;
                    }
                    var attitudeThrust = attitudeInput * MaximumThrust;

                    _locomotion.Rigidbody.AddRelativeTorque(attitudeThrust, ForceMode.Acceleration);

                    PreviousRotation = _locomotion.Rigidbody.transform.rotation;
                }
            } else
            {
                PreviousRotation = null;
            }
        }
    }
}
