
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public enum RoomType
    {
        Start,
        Combat,
        Treasure,
        Rest,
        Shop,
        Challenge,
        Transition,
        Boss,
        Exit
    }

    public class RoomDefinition : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private RoomType _roomType = RoomType.Combat;
        [SerializeField] private string _biomeTag = "default";

        [Header("Spawn Constraints")]
        [SerializeField] private int _minDepth;
        [SerializeField] private int _maxDepth = 999;
        [SerializeField] private bool _allowOnMainPath = true;
        [SerializeField] private int _weight = 1;

        [Header("Layout")]
        [SerializeField] private Vector2Int _footprint = new Vector2Int(1, 1);
        [SerializeField] private List<DoorSocket> _doorSockets = new List<DoorSocket>();
        [SerializeField] private List<Transform> _spawnAnchors = new List<Transform>();

        public RoomType Type => _roomType;
        public string BiomeTag => _biomeTag;
        public int Weight => Mathf.Max(1, _weight);
        public Vector2Int Footprint => new Vector2Int(Mathf.Max(1, _footprint.x), Mathf.Max(1, _footprint.y));
        public IReadOnlyList<DoorSocket> DoorSockets => _doorSockets;
        public IReadOnlyList<Transform> SpawnAnchors => _spawnAnchors;

        private void Awake()
        {
            if (_doorSockets.Count == 0)
            {
                CacheDoorSockets();
            }
        }

        private void OnValidate()
        {
            _weight = Mathf.Max(1, _weight);
            _maxDepth = Mathf.Max(_minDepth, _maxDepth);

            if (_doorSockets.Count == 0)
            {
                CacheDoorSockets();
            }
        }

        public bool CanSpawnAtDepth(int depth, bool isMainPath)
        {
            if (!_allowOnMainPath && isMainPath)
            {
                return false;
            }

            return depth >= _minDepth && depth <= _maxDepth;
        }

        public bool SupportsConnectionCount(int incomingConnections, int outgoingConnections)
        {
            var required = Mathf.Max(1, incomingConnections + outgoingConnections);
            return _doorSockets.Count >= required;
        }

        private void CacheDoorSockets()
        {
            _doorSockets.Clear();
            GetComponentsInChildren(true, _doorSockets);
        }
    }
}
