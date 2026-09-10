
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game
{
    public class RoomPlacementSolver : MonoBehaviour
    {
        [System.Serializable]
        public class PlacedRoomData
        {
            public int NodeId;
            public RoomDefinition Definition;
            public GameObject Instance;
            public Vector3 Position;
            public Vector2 Size;
        }

        [SerializeField] private float _horizontalSpacing = 26f;
        [SerializeField] private float _verticalSpacing = 14f;
        [SerializeField] private int _maxLaneShiftRetries = 2;
        [SerializeField] private bool _logPlacement;

        public List<PlacedRoomData> Place(LevelPlan plan, List<BiomeDefinition> biomes, int seed, Transform rootOverride = null)
        {
            var placedRooms = new List<PlacedRoomData>();
            if (plan == null || plan.Nodes.Count == 0)
            {
                return placedRooms;
            }

            var rng = new System.Random(seed * 31 + 7);
            var nodesByOrder = plan.Nodes.OrderBy(n => n.Depth).ThenBy(n => n.Id).ToList();
            var occupied = new Dictionary<Vector2Int, PlacedRoomData>();
            var root = rootOverride != null ? rootOverride : transform;

            for (var i = 0; i < nodesByOrder.Count; i++)
            {
                var node = nodesByOrder[i];
                if (node.BiomeIndex < 0 || node.BiomeIndex >= biomes.Count || biomes[node.BiomeIndex] == null)
                {
                    continue;
                }

                var biome = biomes[node.BiomeIndex];
                var room = biome.PickRoom(node.RequestedRoomType, node.Depth, node.IsMainPath, rng);
                if (room == null)
                {
                    if (_logPlacement)
                    {
                        Debug.LogWarning($"No room found for node {node.Id} ({node.RequestedRoomType}) in biome {biome.BiomeId}");
                    }

                    continue;
                }

                var lane = node.Lane;
                var grid = new Vector2Int(node.Depth, lane);
                for (var retry = 0; retry < _maxLaneShiftRetries && occupied.ContainsKey(grid); retry++)
                {
                    lane += retry % 2 == 0 ? 1 : -1;
                    grid = new Vector2Int(node.Depth, lane);
                }

                var worldPos = new Vector3(grid.x * _horizontalSpacing, grid.y * _verticalSpacing, 0f);
                var instance = Instantiate(room.gameObject, worldPos, Quaternion.identity, root);

                var placedRoomDefinition = instance.GetComponent<RoomDefinition>();
                if (placedRoomDefinition == null)
                {
                    placedRoomDefinition = room;
                }

                var footprint = placedRoomDefinition.Footprint;
                var data = new PlacedRoomData
                {
                    NodeId = node.Id,
                    Definition = placedRoomDefinition,
                    Instance = instance,
                    Position = worldPos,
                    Size = new Vector2(footprint.x * _horizontalSpacing, footprint.y * _verticalSpacing)
                };

                occupied[grid] = data;
                placedRooms.Add(data);
            }

            return placedRooms;
        }

        public void ClearPlacedRooms(List<PlacedRoomData> rooms)
        {
            if (rooms == null)
            {
                return;
            }

            for (var i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (room?.Instance == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(room.Instance);
                }
                else
                {
                    DestroyImmediate(room.Instance);
                }
            }

            rooms.Clear();
        }
    }
}
