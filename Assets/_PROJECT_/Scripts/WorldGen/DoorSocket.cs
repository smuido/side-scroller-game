
using UnityEngine;

namespace Game
{
    public enum DoorDirection
    {
        Left,
        Right,
        Up,
        Down
    }

    public class DoorSocket : MonoBehaviour
    {
        [SerializeField] private DoorDirection _direction;
        [SerializeField] private Vector3 _localOffset;
        [SerializeField] private bool _optional;

        public DoorDirection Direction => _direction;
        public bool Optional => _optional;
        public Vector3 WorldPosition => transform.TransformPoint(_localOffset);

        public bool IsCompatibleWith(DoorSocket other)
        {
            if (other == null)
            {
                return false;
            }

            return other.Direction == GetOpposite(_direction);
        }

        public static DoorDirection GetOpposite(DoorDirection direction)
        {
            switch (direction)
            {
                case DoorDirection.Left:
                    return DoorDirection.Right;
                case DoorDirection.Right:
                    return DoorDirection.Left;
                case DoorDirection.Up:
                    return DoorDirection.Down;
                default:
                    return DoorDirection.Up;
            }
        }
    }
}
