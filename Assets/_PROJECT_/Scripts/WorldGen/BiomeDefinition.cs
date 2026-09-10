
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game
{
    public class BiomeDefinition : MonoBehaviour
    {
        [Header("Biome Identity")]
        [SerializeField] private string _biomeId = "biome";
        [SerializeField] private int _orderIndex;

        [Header("Graph Flow")]
        [SerializeField] private int _mainPathRoomBudget = 5;
        [SerializeField] [Range(0f, 1f)] private float _branchChance = 0.3f;
        [SerializeField] private int _maxBranchLength = 3;

        [Header("Content Pools")]
        [SerializeField] private List<RoomDefinition> _roomPool = new List<RoomDefinition>();
        [SerializeField] private List<GameObject> _encounterPrefabs = new List<GameObject>();

        public string BiomeId => _biomeId;
        public int OrderIndex => _orderIndex;
        public int MainPathRoomBudget => Mathf.Max(1, _mainPathRoomBudget);
        public float BranchChance => Mathf.Clamp01(_branchChance);
        public int MaxBranchLength => Mathf.Max(1, _maxBranchLength);
        public IReadOnlyList<GameObject> EncounterPrefabs => _encounterPrefabs;

        private void OnValidate()
        {
            _mainPathRoomBudget = Mathf.Max(1, _mainPathRoomBudget);
            _maxBranchLength = Mathf.Max(1, _maxBranchLength);
            _branchChance = Mathf.Clamp01(_branchChance);
        }

        public List<RoomDefinition> GetCandidates(RoomType roomType, int depth, bool isMainPath)
        {
            return _roomPool
                .Where(room => room != null)
                .Where(room => room.Type == roomType)
                .Where(room => room.CanSpawnAtDepth(depth, isMainPath))
                .ToList();
        }

        public RoomDefinition PickRoom(RoomType roomType, int depth, bool isMainPath, System.Random rng)
        {
            var candidates = GetCandidates(roomType, depth, isMainPath);
            if (candidates.Count == 0)
            {
                candidates = _roomPool
                    .Where(room => room != null)
                    .Where(room => room.CanSpawnAtDepth(depth, isMainPath))
                    .ToList();
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            var totalWeight = 0;
            for (var i = 0; i < candidates.Count; i++)
            {
                totalWeight += candidates[i].Weight;
            }

            var pick = rng.Next(0, totalWeight);
            var cumulative = 0;
            for (var i = 0; i < candidates.Count; i++)
            {
                cumulative += candidates[i].Weight;
                if (pick < cumulative)
                {
                    return candidates[i];
                }
            }

            return candidates[candidates.Count - 1];
        }

        public GameObject PickEncounterPrefab(System.Random rng)
        {
            if (_encounterPrefabs.Count == 0)
            {
                return null;
            }

            var index = rng.Next(0, _encounterPrefabs.Count);
            return _encounterPrefabs[index];
        }
    }
}
