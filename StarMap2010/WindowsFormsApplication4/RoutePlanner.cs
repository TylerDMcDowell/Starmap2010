// ============================================================
// File: RoutePlanner.cs
// Project: StarMap2010
//
// Purpose:
//   Shared route-finding logic for star travel planning.
//   - Gate links are bidirectional unless specified otherwise
//   - Supports: PreferGates, AvoidGates, DirectOnly, GatesThenDirect
//   - GatesThenDirect: try gates-only first; if none exists, allow direct legs
//   - Uses Dijkstra (non-negative weights)
//   - Computes per-leg distances in LY
//
// Notes:
//   - Direct legs are only added when maxDirectLegLy > 0
//   - StarSystemInfo is expected to have: SystemId, SystemName (or null), XReal/YReal/ZReal
// ============================================================

using System;
using System.Collections.Generic;
using StarMap2010.Models;

namespace StarMap2010
{
    public enum RouteMode
    {
        PreferGates     = 0, // gates are cheaper than direct legs (travel preference)
        AvoidGates      = 1, // gates are allowed but penalized
        DirectOnly      = 2, // ignore gates entirely
        GatesThenDirect = 3  // try gates-only first, otherwise allow direct legs within maxDirectLegLy
    }

    public sealed class GateLink
    {
        public string FromSystemId;
        public string ToSystemId;

        // Larger means "faster" (lower cost). If unknown, leave 1.
        public float SpeedFactor = 1.0f;

        public bool Bidirectional = true;
    }

    public sealed class RouteResult
    {
        public readonly List<string> PathSystemIds = new List<string>();

        // Per-leg LY distances parallel to PathSystemIds (count = PathSystemIds.Count-1)
        public readonly List<float> LegsLy = new List<float>();

        public float TotalLy;
        public float TotalCost; // Dijkstra cost (may differ from TotalLy if gates are weighted)
        public string Error;    // optional

        public bool Found
        {
            get { return PathSystemIds != null && PathSystemIds.Count > 0; }
        }
    }

    public static class RoutePlanner
    {
        // Tuning knobs
        private const float GATE_BONUS = 10.0f;   // PreferGates: gates are effectively faster
        private const float GATE_PENALTY = 10.0f; // AvoidGates: gates are effectively slower

        public static RouteResult FindRoute(
            IList<StarSystemInfo> systems,
            IList<GateLink> gateLinks,
            string startSystemId,
            string endSystemId,
            float maxDirectLegLy)
        {
            return FindRoute(systems, gateLinks, startSystemId, endSystemId, maxDirectLegLy, RouteMode.GatesThenDirect);
        }

        public static RouteResult FindRoute(
            IList<StarSystemInfo> systems,
            IList<GateLink> gateLinks,
            string startSystemId,
            string endSystemId,
            float maxDirectLegLy,
            RouteMode mode)
        {
            if (mode == RouteMode.GatesThenDirect)
            {
                // 1) Try gates-only first.
                var gatesOnly = FindRouteInternal(systems, gateLinks, startSystemId, endSystemId, 0f, RouteMode.PreferGates);
                if (gatesOnly != null && gatesOnly.Found)
                    return gatesOnly;

                // 2) Fall back to allowing direct legs.
                return FindRouteInternal(systems, gateLinks, startSystemId, endSystemId, maxDirectLegLy, RouteMode.PreferGates);
            }

            return FindRouteInternal(systems, gateLinks, startSystemId, endSystemId, maxDirectLegLy, mode);
        }

        private sealed class Edge
        {
            public string ToId;
            public float DistanceLy;
            public float Cost;
            public bool IsGate;
        }

        private static RouteResult FindRouteInternal(
            IList<StarSystemInfo> systems,
            IList<GateLink> gateLinks,
            string startSystemId,
            string endSystemId,
            float maxDirectLegLy,
            RouteMode mode)
        {
            var rr = new RouteResult();

            if (systems == null || systems.Count == 0)
            {
                rr.Error = "No star systems loaded.";
                return rr;
            }
            if (string.IsNullOrEmpty(startSystemId) || string.IsNullOrEmpty(endSystemId))
            {
                rr.Error = "Start or destination system not set.";
                return rr;
            }
            if (string.Equals(startSystemId, endSystemId, StringComparison.OrdinalIgnoreCase))
            {
                rr.PathSystemIds.Add(startSystemId);
                rr.TotalLy = 0f;
                rr.TotalCost = 0f;
                return rr;
            }

            // Index systems by id
            var byId = new Dictionary<string, StarSystemInfo>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < systems.Count; i++)
            {
                var s = systems[i];
                if (s == null) continue;
                if (string.IsNullOrEmpty(s.SystemId)) continue;
                if (!byId.ContainsKey(s.SystemId))
                    byId.Add(s.SystemId, s);
            }

            StarSystemInfo startSys, endSys;
            if (!byId.TryGetValue(startSystemId, out startSys))
            {
                rr.Error = "Start system not found: " + startSystemId;
                return rr;
            }
            if (!byId.TryGetValue(endSystemId, out endSys))
            {
                rr.Error = "Destination system not found: " + endSystemId;
                return rr;
            }

            // Build adjacency
            var adj = new Dictionary<string, List<Edge>>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in byId.Keys)
                adj[id] = new List<Edge>();

            if (mode != RouteMode.DirectOnly && gateLinks != null)
            {
                for (int i = 0; i < gateLinks.Count; i++)
                {
                    var g = gateLinks[i];
                    if (g == null) continue;
                    if (string.IsNullOrEmpty(g.FromSystemId) || string.IsNullOrEmpty(g.ToSystemId)) continue;

                    StarSystemInfo a, b;
                    if (!byId.TryGetValue(g.FromSystemId, out a)) continue;
                    if (!byId.TryGetValue(g.ToSystemId, out b)) continue;

                    float d = DistanceLy(a, b);
                    float speed = (g.SpeedFactor <= 0.0001f) ? 1.0f : g.SpeedFactor;

                    float cost = GateCost(d, speed, mode);

                    adj[g.FromSystemId].Add(new Edge { ToId = g.ToSystemId, DistanceLy = d, Cost = cost, IsGate = true });
                    if (g.Bidirectional)
                        adj[g.ToSystemId].Add(new Edge { ToId = g.FromSystemId, DistanceLy = d, Cost = cost, IsGate = true });
                }
            }

            if (maxDirectLegLy > 0f)
            {
                // Add all direct edges within range (O(N^2))
                var ids = new List<string>(byId.Keys);
                for (int i = 0; i < ids.Count; i++)
                {
                    var a = byId[ids[i]];
                    for (int j = i + 1; j < ids.Count; j++)
                    {
                        var b = byId[ids[j]];
                        float d = DistanceLy(a, b);
                        if (d <= maxDirectLegLy)
                        {
                            adj[a.SystemId].Add(new Edge { ToId = b.SystemId, DistanceLy = d, Cost = d, IsGate = false });
                            adj[b.SystemId].Add(new Edge { ToId = a.SystemId, DistanceLy = d, Cost = d, IsGate = false });
                        }
                    }
                }
            }

            // Dijkstra (simple O(N^2) selection; OK for modest star counts)
            var dist = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            var prev = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var prevLegLy = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var id in byId.Keys)
                dist[id] = float.PositiveInfinity;
            dist[startSystemId] = 0f;

            while (true)
            {
                string u = null;
                float best = float.PositiveInfinity;

                foreach (var kv in dist)
                {
                    if (visited.Contains(kv.Key)) continue;
                    if (kv.Value < best)
                    {
                        best = kv.Value;
                        u = kv.Key;
                    }
                }

                if (u == null) break;
                if (float.IsInfinity(best)) break;
                if (string.Equals(u, endSystemId, StringComparison.OrdinalIgnoreCase)) break;

                visited.Add(u);

                var edges = adj[u];
                for (int i = 0; i < edges.Count; i++)
                {
                    var e = edges[i];
                    float alt = best + e.Cost;

                    float cur;
                    if (!dist.TryGetValue(e.ToId, out cur) || alt < cur)
                    {
                        dist[e.ToId] = alt;
                        prev[e.ToId] = u;
                        prevLegLy[e.ToId] = e.DistanceLy;
                    }
                }
            }

            float endCost;
            if (!dist.TryGetValue(endSystemId, out endCost) || float.IsInfinity(endCost))
            {
                rr.Error = "No route found.";
                return rr;
            }

            // Reconstruct path
            var rev = new List<string>();
            var revLeg = new List<float>();

            string curId = endSystemId;
            while (!string.IsNullOrEmpty(curId))
            {
                rev.Add(curId);

                string p;
                if (!prev.TryGetValue(curId, out p))
                    break;

                float leg;
                if (!prevLegLy.TryGetValue(curId, out leg))
                    leg = 0f;

                revLeg.Add(leg);
                curId = p;
            }

            rev.Reverse();
            revLeg.Reverse(); // aligns with edges from start to end

            rr.PathSystemIds.AddRange(rev);
            rr.LegsLy.AddRange(revLeg);

            float totalLy = 0f;
            for (int i = 0; i < rr.LegsLy.Count; i++)
                totalLy += rr.LegsLy[i];

            rr.TotalLy = totalLy;
            rr.TotalCost = endCost;
            return rr;
        }

        private static float GateCost(float distanceLy, float speedFactor, RouteMode mode)
        {
            if (distanceLy < 0f) distanceLy = 0f;
            if (speedFactor <= 0.0001f) speedFactor = 1.0f;

            // baseline: faster => cheaper
            float baseCost = distanceLy / speedFactor;

            if (mode == RouteMode.AvoidGates)
                return baseCost * GATE_PENALTY;

            // PreferGates (and the internal run used by GatesThenDirect)
            return baseCost / GATE_BONUS;
        }

        public static float DistanceLy(StarSystemInfo a, StarSystemInfo b)
        {
            if (a == null || b == null) return 0f;
            float dx = (float)(a.XReal - b.XReal);
            float dy = (float)(a.YReal - b.YReal);
            float dz = (float)(a.ZReal - b.ZReal);
            return (float)Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        }
    }
}
