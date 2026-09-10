
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game
{
    [System.Serializable]
    public class LevelNodeData
    {
        public int Id;
        public int Depth;
        public int Lane;
        public bool IsMainPath;
        public int BiomeIndex;
        public RoomType RequestedRoomType;
        public float Difficulty;
    }

    [System.Serializable]
    public class LevelEdgeData
    {
        public int FromNodeId;
        public int ToNodeId;
        public bool IsMainPath;
    }

    [System.Serializable]
    public class LevelPlan
    {
        public int Seed;
        public int StartNodeId;
        public int ExitNodeId;
        public List<LevelNodeData> Nodes = new List<LevelNodeData>();
        public List<LevelEdgeData> Edges = new List<LevelEdgeData>();
    }

    [System.Serializable]
    public class LevelGenerationResult
    {
        public LevelPlan Plan;
        public List<RoomPlacementSolver.PlacedRoomData> PlacedRooms;
    }

    [System.Serializable]
    public class GenerationProgress
    {
        public float Percent;
        public string Step;
    }

    public class LevelGraphGenerator : MonoBehaviour
    {
        [Header("World Inputs")]
        [SerializeField] private List<BiomeDefinition> _biomes = new List<BiomeDefinition>();
        [SerializeField] private RoomPlacementSolver _placementSolver;
        [SerializeField] private EncounterGenerator _encounterGenerator;
        [SerializeField] private LevelValidator _levelValidator;
        [SerializeField] private Transform _levelRoot;

        [Header("Flow Tuning")]
        [SerializeField] private int _minMainPathRooms = 8;
        [SerializeField] private int _maxMainPathRooms = 22;
        [SerializeField] private int _maxLaneDrift = 3;
        [SerializeField] private int _maxGenerationAttempts = 8;
        [SerializeField] private bool _generateOnStart;

        [Header("Debug")]
        [SerializeField] private bool _logGeneration;

        public bool IsGenerating { get; private set; }
        public LevelGenerationResult LastResult { get; private set; }

        private void Start()
        {
            if (_generateOnStart)
            {
                GenerateWorldAsync().Forget();
            }
        }

        public async UniTask<LevelGenerationResult> GenerateWorldAsync(
            int? forcedSeed = null,
            System.Action<GenerationProgress> onProgress = null,
            CancellationToken cancellationToken = default)
        {
            if (IsGenerating)
            {
                return LastResult;
            }

            IsGenerating = true;

            var seed = forcedSeed ?? Random.Range(int.MinValue, int.MaxValue);
            var attemptSeed = seed;

            try
            {
                if (_biomes.Count == 0 || _placementSolver == null || _encounterGenerator == null || _levelValidator == null)
                {
                    Debug.LogError("World generation is missing required references.");
                    return null;
                }

                var sortedBiomes = GetSortedBiomes();
                if (sortedBiomes.Count == 0)
                {
                    Debug.LogError("No valid biome definitions were provided.");
                    return null;
                }

                for (var attempt = 0; attempt < _maxGenerationAttempts; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    onProgress?.Invoke(new GenerationProgress { Percent = 0.1f, Step = "Generating graph" });
                    var plan = GenerateGraph(attemptSeed, sortedBiomes);
                    await UniTask.Yield(cancellationToken);

                    if (!_levelValidator.ValidatePlan(plan, out var planIssue))
                    {
                        if (_logGeneration)
                        {
                            Debug.LogWarning($"Graph attempt {attempt + 1} failed validation: {planIssue}");
                        }

                        attemptSeed += 911;
                        continue;
                    }

                    onProgress?.Invoke(new GenerationProgress { Percent = 0.45f, Step = "Placing rooms" });
                    var placedRooms = _placementSolver.Place(plan, sortedBiomes, attemptSeed, _levelRoot);
                    await UniTask.Yield(cancellationToken);

                    if (!_levelValidator.ValidatePlacement(plan, placedRooms, out var placementIssue))
                    {
                        if (_logGeneration)
                        {
                            Debug.LogWarning($"Placement attempt {attempt + 1} failed validation: {placementIssue}");
                        }

                        _placementSolver.ClearPlacedRooms(placedRooms);
                        attemptSeed += 911;
                        continue;
                    }

                    onProgress?.Invoke(new GenerationProgress { Percent = 0.75f, Step = "Populating encounters" });
                    _encounterGenerator.Populate(plan, placedRooms, sortedBiomes, attemptSeed);
                    await UniTask.Yield(cancellationToken);

                    var result = new LevelGenerationResult
                    {
                        Plan = plan,
                        PlacedRooms = placedRooms
                    };

                    LastResult = result;
                    onProgress?.Invoke(new GenerationProgress { Percent = 1f, Step = "Generation complete" });

                    if (_logGeneration)
                    {
                        Debug.Log($"World generated. Seed: {attemptSeed}, Nodes: {plan.Nodes.Count}, Edges: {plan.Edges.Count}");
                    }

                    return result;
                }

                Debug.LogError("World generation failed after all retry attempts.");
                return null;
            }
            finally
            {
                IsGenerating = false;
            }
        }

        public LevelPlan GenerateGraph(int seed)
        {
            return GenerateGraph(seed, GetSortedBiomes());
        }

        private LevelPlan GenerateGraph(int seed, List<BiomeDefinition> sortedBiomes)
        {
            var rng = new System.Random(seed);

            var plan = new LevelPlan
            {
                Seed = seed
            };

            if (sortedBiomes.Count == 0)
            {
                return plan;
            }

            var mainPathCount = Mathf.Clamp(sortedBiomes.Sum(b => b.MainPathRoomBudget), _minMainPathRooms, _maxMainPathRooms);
            var nextNodeId = 0;

            var startNode = CreateNode(nextNodeId++, 0, 0, true, 0, RoomType.Start, 0f);
            plan.Nodes.Add(startNode);
            plan.StartNodeId = startNode.Id;

            var previousNode = startNode;
            var previousBiomeIndex = 0;

            for (var depth = 1; depth < mainPathCount - 1; depth++)
            {
                var biomeIndex = ResolveBiomeIndex(depth, mainPathCount, sortedBiomes.Count);
                var lane = Mathf.Clamp(previousNode.Lane + rng.Next(-1, 2), -_maxLaneDrift, _maxLaneDrift);

                RoomType requestedType;
                if (biomeIndex != previousBiomeIndex)
                {
                    requestedType = RoomType.Transition;
                }
                else
                {
                    requestedType = PickMainPathRoomType(rng, depth, mainPathCount);
                }

                var normalized = mainPathCount <= 1 ? 0f : (float)depth / (mainPathCount - 1);
                var node = CreateNode(nextNodeId++, depth, lane, true, biomeIndex, requestedType, normalized);

                plan.Nodes.Add(node);
                plan.Edges.Add(new LevelEdgeData
                {
                    FromNodeId = previousNode.Id,
                    ToNodeId = node.Id,
                    IsMainPath = true
                });

                previousNode = node;
                previousBiomeIndex = biomeIndex;
            }

            var exitDepth = mainPathCount - 1;
            var exitBiome = ResolveBiomeIndex(exitDepth, mainPathCount, sortedBiomes.Count);
            var exitNode = CreateNode(nextNodeId++, exitDepth, previousNode.Lane, true, exitBiome, RoomType.Exit, 1f);

            plan.Nodes.Add(exitNode);
            plan.Edges.Add(new LevelEdgeData
            {
                FromNodeId = previousNode.Id,
                ToNodeId = exitNode.Id,
                IsMainPath = true
            });
            plan.ExitNodeId = exitNode.Id;

            // Add side branches that reconnect forward to preserve directional flow.
            var mainNodes = plan.Nodes.Where(n => n.IsMainPath).OrderBy(n => n.Depth).ToList();
            for (var i = 1; i < mainNodes.Count - 2; i++)
            {
                var mainNode = mainNodes[i];
                var biome = sortedBiomes[mainNode.BiomeIndex];
                if (rng.NextDouble() > biome.BranchChance)
                {
                    continue;
                }

                var branchLength = rng.Next(1, biome.MaxBranchLength + 1);
                var branchParent = mainNode;
                var laneDirection = rng.NextDouble() < 0.5 ? -1 : 1;
                LevelNodeData firstBranchNode = null;

                for (var branchDepthStep = 1; branchDepthStep <= branchLength; branchDepthStep++)
                {
                    var branchDepth = mainNode.Depth + branchDepthStep;
                    if (branchDepth >= exitDepth)
                    {
                        break;
                    }

                    var branchLane = Mathf.Clamp(
                        branchParent.Lane + laneDirection,
                        -_maxLaneDrift,
                        _maxLaneDrift);

                    var nodeBiomeIndex = ResolveBiomeIndex(branchDepth, mainPathCount, sortedBiomes.Count);
                    var branchType = PickBranchRoomType(rng);
                    var branchNode = CreateNode(
                        nextNodeId++,
                        branchDepth,
                        branchLane,
                        false,
                        nodeBiomeIndex,
                        branchType,
                        Mathf.Clamp01(mainPathCount <= 1 ? 0f : (float)branchDepth / (mainPathCount - 1)));

                    plan.Nodes.Add(branchNode);
                    plan.Edges.Add(new LevelEdgeData
                    {
                        FromNodeId = branchParent.Id,
                        ToNodeId = branchNode.Id,
                        IsMainPath = false
                    });

                    if (firstBranchNode == null)
                    {
                        firstBranchNode = branchNode;
                    }

                    branchParent = branchNode;
                }

                if (firstBranchNode == null)
                {
                    continue;
                }

                var mergeCandidates = mainNodes.Where(n => n.Depth > branchParent.Depth).OrderBy(n => n.Depth).ToList();
                if (mergeCandidates.Count == 0)
                {
                    continue;
                }

                var mergeNode = mergeCandidates[rng.Next(0, mergeCandidates.Count)];
                plan.Edges.Add(new LevelEdgeData
                {
                    FromNodeId = branchParent.Id,
                    ToNodeId = mergeNode.Id,
                    IsMainPath = false
                });
            }

            return plan;
        }

        private List<BiomeDefinition> GetSortedBiomes()
        {
            return _biomes
                .Where(b => b != null)
                .OrderBy(b => b.OrderIndex)
                .ToList();
        }

        private static LevelNodeData CreateNode(
            int id,
            int depth,
            int lane,
            bool isMainPath,
            int biomeIndex,
            RoomType requestedRoomType,
            float difficulty)
        {
            return new LevelNodeData
            {
                Id = id,
                Depth = depth,
                Lane = lane,
                IsMainPath = isMainPath,
                BiomeIndex = biomeIndex,
                RequestedRoomType = requestedRoomType,
                Difficulty = difficulty
            };
        }

        private static int ResolveBiomeIndex(int depth, int mainPathCount, int biomeCount)
        {
            if (biomeCount <= 1 || mainPathCount <= 1)
            {
                return 0;
            }

            var t = Mathf.Clamp01((float)depth / (mainPathCount - 1));
            var scaled = Mathf.FloorToInt(t * biomeCount);
            return Mathf.Clamp(scaled, 0, biomeCount - 1);
        }

        private static RoomType PickMainPathRoomType(System.Random rng, int depth, int mainPathCount)
        {
            var lateGameStartDepth = Mathf.RoundToInt(mainPathCount * 0.8f);
            if (depth >= lateGameStartDepth && rng.NextDouble() < 0.3)
            {
                return RoomType.Challenge;
            }

            var roll = rng.NextDouble();
            if (roll < 0.55)
            {
                return RoomType.Combat;
            }

            if (roll < 0.7)
            {
                return RoomType.Treasure;
            }

            if (roll < 0.82)
            {
                return RoomType.Rest;
            }

            if (roll < 0.92)
            {
                return RoomType.Shop;
            }

            return RoomType.Challenge;
        }

        private static RoomType PickBranchRoomType(System.Random rng)
        {
            var roll = rng.NextDouble();
            if (roll < 0.55)
            {
                return RoomType.Combat;
            }

            if (roll < 0.85)
            {
                return RoomType.Treasure;
            }

            return RoomType.Challenge;
        }
    }
}
