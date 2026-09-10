
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game
{
    public class LevelValidator : MonoBehaviour
    {
        [SerializeField] private int _minimumRooms = 6;
        [SerializeField] private int _maximumLaneJump = 2;
        [SerializeField] [Range(0.2f, 1f)] private float _overlapTolerance = 0.85f;
        [SerializeField] private bool _logFailures = true;

        public bool ValidatePlan(LevelPlan plan, out string issue)
        {
            issue = string.Empty;

            if (plan == null)
            {
                issue = "Plan is null.";
                return false;
            }

            if (plan.Nodes.Count < _minimumRooms)
            {
                issue = $"Plan has too few rooms ({plan.Nodes.Count}).";
                return Fail(issue);
            }

            var nodesById = plan.Nodes.ToDictionary(node => node.Id, node => node);
            if (!nodesById.ContainsKey(plan.StartNodeId) || !nodesById.ContainsKey(plan.ExitNodeId))
            {
                issue = "Start or exit node is missing.";
                return Fail(issue);
            }

            var incomingCounts = plan.Nodes.ToDictionary(node => node.Id, _ => 0);
            var adjacency = plan.Nodes.ToDictionary(node => node.Id, _ => new List<int>());

            for (var i = 0; i < plan.Edges.Count; i++)
            {
                var edge = plan.Edges[i];
                if (!nodesById.TryGetValue(edge.FromNodeId, out var fromNode) || !nodesById.TryGetValue(edge.ToNodeId, out var toNode))
                {
                    issue = $"Edge {i} references unknown node IDs.";
                    return Fail(issue);
                }

                if (toNode.Depth <= fromNode.Depth)
                {
                    issue = $"Edge {fromNode.Id}->{toNode.Id} breaks forward progression.";
                    return Fail(issue);
                }

                if (Mathf.Abs(toNode.Lane - fromNode.Lane) > _maximumLaneJump)
                {
                    issue = $"Edge {fromNode.Id}->{toNode.Id} lane jump is too large.";
                    return Fail(issue);
                }

                adjacency[fromNode.Id].Add(toNode.Id);
                incomingCounts[toNode.Id]++;
            }

            foreach (var node in plan.Nodes)
            {
                if (node.Id == plan.StartNodeId)
                {
                    continue;
                }

                if (incomingCounts[node.Id] == 0)
                {
                    issue = $"Node {node.Id} is unreachable.";
                    return Fail(issue);
                }
            }

            var reachable = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(plan.StartNodeId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!reachable.Add(current))
                {
                    continue;
                }

                var outgoing = adjacency[current];
                for (var i = 0; i < outgoing.Count; i++)
                {
                    queue.Enqueue(outgoing[i]);
                }
            }

            if (!reachable.Contains(plan.ExitNodeId))
            {
                issue = "Exit cannot be reached from start.";
                return Fail(issue);
            }

            return true;
        }

        public bool ValidatePlacement(LevelPlan plan, List<RoomPlacementSolver.PlacedRoomData> placedRooms, out string issue)
        {
            issue = string.Empty;

            if (plan == null)
            {
                issue = "Plan is null for placement validation.";
                return Fail(issue);
            }

            if (placedRooms == null)
            {
                issue = "Placed room list is null.";
                return Fail(issue);
            }

            var placedByNode = placedRooms
                .Where(room => room != null)
                .GroupBy(room => room.NodeId)
                .ToDictionary(group => group.Key, group => group.First());

            for (var i = 0; i < plan.Nodes.Count; i++)
            {
                var node = plan.Nodes[i];
                if (!placedByNode.ContainsKey(node.Id))
                {
                    issue = $"Node {node.Id} was not placed.";
                    return Fail(issue);
                }
            }

            for (var i = 0; i < placedRooms.Count; i++)
            {
                var a = placedRooms[i];
                if (a == null)
                {
                    continue;
                }

                var boundsA = new Bounds(a.Position, new Vector3(a.Size.x * _overlapTolerance, a.Size.y * _overlapTolerance, 1f));
                for (var j = i + 1; j < placedRooms.Count; j++)
                {
                    var b = placedRooms[j];
                    if (b == null)
                    {
                        continue;
                    }

                    var boundsB = new Bounds(b.Position, new Vector3(b.Size.x * _overlapTolerance, b.Size.y * _overlapTolerance, 1f));
                    if (boundsA.Intersects(boundsB))
                    {
                        issue = $"Rooms for nodes {a.NodeId} and {b.NodeId} overlap.";
                        return Fail(issue);
                    }
                }
            }

            return true;
        }

        private bool Fail(string message)
        {
            if (_logFailures)
            {
                Debug.LogWarning(message);
            }

            return false;
        }
    }
}
