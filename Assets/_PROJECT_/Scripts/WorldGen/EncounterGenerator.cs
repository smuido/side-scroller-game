
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game
{
    public class EncounterRuntimeData : MonoBehaviour
    {
        [SerializeField] private int _nodeId;
        [SerializeField] private float _difficulty;

        public int NodeId => _nodeId;
        public float Difficulty => _difficulty;

        public void Initialize(int nodeId, float difficulty)
        {
            _nodeId = nodeId;
            _difficulty = difficulty;
        }
    }

    public class EncounterGenerator : MonoBehaviour
    {
        [SerializeField] private int _minSpawnsPerRoom = 1;
        [SerializeField] private int _maxSpawnsPerRoom = 5;
        [SerializeField] private bool _populateMainPathOnly;
        [SerializeField] private bool _logPopulation;

        public void Populate(LevelPlan plan, List<RoomPlacementSolver.PlacedRoomData> placedRooms, List<BiomeDefinition> biomes, int seed)
        {
            if (plan == null || placedRooms == null)
            {
                return;
            }

            var rng = new System.Random(seed * 73 + 11);
            var nodeMap = plan.Nodes.ToDictionary(node => node.Id, node => node);

            for (var i = 0; i < placedRooms.Count; i++)
            {
                var placed = placedRooms[i];
                if (placed?.Definition == null || placed.Instance == null)
                {
                    continue;
                }

                if (!nodeMap.TryGetValue(placed.NodeId, out var node))
                {
                    continue;
                }

                if (_populateMainPathOnly && !node.IsMainPath)
                {
                    continue;
                }

                if (node.BiomeIndex < 0 || node.BiomeIndex >= biomes.Count)
                {
                    continue;
                }

                var biome = biomes[node.BiomeIndex];
                if (biome == null)
                {
                    continue;
                }

                var encounterPrefab = biome.PickEncounterPrefab(rng);
                if (encounterPrefab == null)
                {
                    continue;
                }

                var difficultyMultiplier = Mathf.Lerp(0.6f, 1.25f, node.Difficulty);
                if (!node.IsMainPath)
                {
                    difficultyMultiplier *= 0.9f;
                }

                var rawSpawns = Mathf.RoundToInt(Mathf.Lerp(_minSpawnsPerRoom, _maxSpawnsPerRoom, difficultyMultiplier));
                var spawnCount = Mathf.Clamp(rawSpawns + rng.Next(0, 2), _minSpawnsPerRoom, _maxSpawnsPerRoom);
                var anchors = placed.Definition.SpawnAnchors;

                if (anchors.Count == 0)
                {
                    var fallback = new GameObject("SpawnAnchor_Auto");
                    fallback.transform.SetParent(placed.Instance.transform, false);
                    fallback.transform.localPosition = Vector3.zero;
                    SpawnEncounterAtAnchor(encounterPrefab, fallback.transform, placed.NodeId, difficultyMultiplier);
                    continue;
                }

                var anchorOrder = anchors.OrderBy(_ => rng.Next()).ToList();
                var maxSpawnable = Mathf.Min(spawnCount, anchorOrder.Count);
                for (var spawnIndex = 0; spawnIndex < maxSpawnable; spawnIndex++)
                {
                    SpawnEncounterAtAnchor(encounterPrefab, anchorOrder[spawnIndex], placed.NodeId, difficultyMultiplier);
                }

                if (_logPopulation)
                {
                    Debug.Log($"Populated node {placed.NodeId} with {maxSpawnable} encounters.");
                }
            }
        }

        private static void SpawnEncounterAtAnchor(GameObject prefab, Transform anchor, int nodeId, float difficulty)
        {
            var instance = Instantiate(prefab, anchor.position, anchor.rotation, anchor);
            var runtimeData = instance.GetComponent<EncounterRuntimeData>();
            if (runtimeData == null)
            {
                runtimeData = instance.AddComponent<EncounterRuntimeData>();
            }

            runtimeData.Initialize(nodeId, difficulty);
        }
    }
}
