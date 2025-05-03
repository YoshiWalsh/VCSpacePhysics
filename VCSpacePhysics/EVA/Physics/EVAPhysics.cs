using CG.Game.Player;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.FirstPersonController.Camera.ViewTypes;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VCSpacePhysics.EVA.Physics
{
    public class EVAPhysics : MonoBehaviour
    {
        public CustomCharacterLocomotion _locomotion;
        public Player _player;
        public FirstPerson _firstPersonView;

        public Vector3 PendingRotation = Vector3.zero;

        public void Awake()
        {
            _locomotion = gameObject.GetComponent<CustomCharacterLocomotion>();
            _player = gameObject.GetComponent<Player>();
        }

        private static Vector3 CenterOfGravityOffset = new Vector3(0, 1.2f, 0f);

        public void AddRotation(Vector3 rotation)
        {
            PendingRotation += rotation;
        }

        private void RotatePositionAroundCenterOfGravity(Quaternion rotation)
        {
            Vector3 COGWorldspace = _locomotion.gameObject.transform.TransformPoint(CenterOfGravityOffset);
            Vector3 currentRootPositionWorldspace = _locomotion.gameObject.transform.position;
            Vector3 currentRootPositionRelativeToCOG = currentRootPositionWorldspace - COGWorldspace;
            Vector3 newRootPositionRelativeToCOG = rotation * currentRootPositionRelativeToCOG;
            Vector3 newRootPositionWorldspace = newRootPositionRelativeToCOG + COGWorldspace;

            _locomotion.Rigidbody.position = _locomotion.gameObject.transform.position = newRootPositionWorldspace;
        }

        public void FixedUpdate()
        {
            if (EVAUtils.IsPlayerFlying(_locomotion))
            {
                if(_firstPersonView != null)
                {
                    AddRotation(new Vector3(_firstPersonView.m_Pitch, 0f, 0f));
                    _firstPersonView.m_Pitch = 0f;

                    var pitchRotation = Quaternion.AngleAxis(PendingRotation.x, _locomotion.transform.right);
                    var yawRotation = Quaternion.AngleAxis(PendingRotation.y, _locomotion.transform.up);
                    var rollRotation = Quaternion.AngleAxis(PendingRotation.z, _locomotion.transform.forward);
                    var worldspaceRotation = rollRotation * pitchRotation * yawRotation;
                    RotatePositionAroundCenterOfGravity(worldspaceRotation);

                    _firstPersonView.m_BaseRotation = worldspaceRotation * _firstPersonView.m_BaseRotation;
                }
            }
            PendingRotation = Vector3.zero;
        }
    }
}
