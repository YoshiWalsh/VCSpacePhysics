using CG.Game.Player;
using UnityEngine;

namespace VCSpacePhysics.Character.Physics
{
    public class EVAPhysics : MonoBehaviour
    {
        public CustomCharacterLocomotion _locomotion;
        public Player _player;
        public CustomFirstPersonCombat _firstPersonView;

        public Vector3 PendingRotation = Vector3.zero;

        public void Awake()
        {
            _locomotion = gameObject.GetComponent<CustomCharacterLocomotion>();
            _player = gameObject.GetComponent<Player>();
        }

        public void AddRotation(Vector3 rotation)
        {
            PendingRotation += rotation;
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
            //if (CharacterUtils.IsPlayerFlying(_locomotion))
            //{
            //    if (_firstPersonView != null)
            //    {
            //        AddRotation(new Vector3(_firstPersonView.m_Pitch, 0f, 0f));
            //        _firstPersonView.m_Pitch = 0f;

            //        var pitchRotation = Quaternion.AngleAxis(PendingRotation.x, _locomotion.transform.right);
            //        var yawRotation = Quaternion.AngleAxis(PendingRotation.y, _locomotion.transform.up);
            //        var rollRotation = Quaternion.AngleAxis(PendingRotation.z, _locomotion.transform.forward);
            //        var worldspaceRotation = rollRotation * pitchRotation * yawRotation;
            //        RotatePositionAroundCenterOfGravity(worldspaceRotation);

            //        _firstPersonView.m_BaseRotation = worldspaceRotation * _firstPersonView.m_BaseRotation;
            //    }
            //}
            PendingRotation = Vector3.zero;
        }
    }
}
