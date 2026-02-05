// ============================================================
// File: MainForm.OrbitText.cs
// Project: StarMap2010
//
// Human-friendly orbital phrasing derived from system_objects:
// - Planets: "Fifth planet from the Sun"
// - Moons: "Moon of Jupiter"
// - Heliocentric stations/facilities: "between Earth and Mars", etc.
//
// IMPORTANT: We never show radial_order directly.
// ============================================================

using System;
using System.Collections.Generic;
using StarMap2010.Models;

namespace StarMap2010
{
    //MainForm.OrbitText
    public partial class MainForm
    {
        private string GetOrbitDescription(SystemObjectInfo obj)
        {
            if (obj == null) return "-";

            // If it doesn't orbit anything, it's probably the root or a star
            if (string.IsNullOrWhiteSpace(obj.OrbitHostObjectId))
            {
                // Give nicer output for obvious cases
                string k0 = (obj.ObjectKind ?? "").Trim();
                if (string.Equals(k0, "system_root", StringComparison.OrdinalIgnoreCase))
                    return "-";
                if (string.Equals(k0, "star", StringComparison.OrdinalIgnoreCase))
                    return "-";
                return "-";
            }

            // Resolve orbit host
            SystemObjectInfo host = GetObjectByIdCached(obj.OrbitHostObjectId);
            string hostKind = (host != null ? (host.ObjectKind ?? "") : "").Trim();

            // If orbiting a planet -> moon / station / ring phrasing
            if (host != null && string.Equals(hostKind, "planet", StringComparison.OrdinalIgnoreCase))
            {
                string hostName = SafeName(host);

                string kind = (obj.ObjectKind ?? "").Trim();

                if (string.Equals(kind, "moon", StringComparison.OrdinalIgnoreCase))
                    return "Moon of " + hostName;

                if (string.Equals(kind, "ring_system", StringComparison.OrdinalIgnoreCase))
                    return "Ring system of " + hostName;

                // Facilities / stations / installations around a planet
                if (IsFacilityLike(kind))
                    return "In orbit around " + hostName;

                // Generic
                return "Orbiting " + hostName;
            }

            // If orbiting a star or the system root -> heliocentric phrasing
            // Treat "system_root" or "star" as the same concept here.
            if (host != null &&
                (string.Equals(hostKind, "star", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(hostKind, "system_root", StringComparison.OrdinalIgnoreCase)))
            {
                return DescribeHeliocentric(obj, host.ObjectId);
            }

            // Orbiting something else (moon, station, etc.) - keep it simple for now
            if (host != null)
                return "Orbiting " + SafeName(host);

            return "-";
        }

        private string DescribeHeliocentric(SystemObjectInfo obj, string hostId)
        {
            string kind = (obj.ObjectKind ?? "").Trim();
            string sunName = SafeName(GetObjectByIdCached(hostId));

            // Planets get special encyclopedia-style phrasing:
            if (string.Equals(kind, "planet", StringComparison.OrdinalIgnoreCase))
            {
                int ordinal = GetPlanetOrdinalFromSun(obj, hostId);
                if (ordinal > 0)
                    return OrdinalWord(ordinal) + " planet from " + sunName;

                // fallback if something odd
                return "Planet orbiting " + sunName;
            }

            // Dwarf planets – typically not "9th planet"; use region phrasing if possible
            if (string.Equals(kind, "dwarf_planet", StringComparison.OrdinalIgnoreCase))
            {
                string between = BetweenPlanetsPhrase(obj, hostId);
                if (!string.IsNullOrEmpty(between)) return "Beyond Neptune (Kuiper Belt)"; // decent default for Pluto-like cases
                string beyond = BeyondLastPlanetPhrase(obj, hostId);
                if (!string.IsNullOrEmpty(beyond)) return beyond;
                string interior = InteriorToFirstPlanetPhrase(obj, hostId);
                if (!string.IsNullOrEmpty(interior)) return interior;

                return "Dwarf planet orbiting " + sunName;
            }

            // Belts / clouds: use landmark phrasing
            if (string.Equals(kind, "belt", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "asteroid_belt", StringComparison.OrdinalIgnoreCase))
            {
                string between = BetweenPlanetsPhrase(obj, hostId);
                if (!string.IsNullOrEmpty(between)) return "Between " + between;
                return "Belt orbiting " + sunName;
            }

            if (string.Equals(kind, "kuiper_belt", StringComparison.OrdinalIgnoreCase))
            {
                // In your data you currently use "belt" for Kuiper belt; keeping this for future
                return "Beyond Neptune";
            }

            if (string.Equals(kind, "comet_cloud", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "oort_cloud", StringComparison.OrdinalIgnoreCase))
            {
                return "Far beyond the outer planets";
            }

            // Artificial things (stations, gate facilities, installations) in heliocentric orbit:
            if (IsFacilityLike(kind) || string.Equals(kind, "gate_facility", StringComparison.OrdinalIgnoreCase))
            {
                string between = BetweenPlanetsPhrase(obj, hostId);
                if (!string.IsNullOrEmpty(between)) return "Between " + between;

                string interior = InteriorToFirstPlanetPhrase(obj, hostId);
                if (!string.IsNullOrEmpty(interior)) return interior;

                string beyond = BeyondLastPlanetPhrase(obj, hostId);
                if (!string.IsNullOrEmpty(beyond)) return beyond;

                return "Orbiting " + sunName;
            }

            // Generic heliocentric object: try between planets first
            {
                string between = BetweenPlanetsPhrase(obj, hostId);
                if (!string.IsNullOrEmpty(between)) return "Between " + between;

                string interior = InteriorToFirstPlanetPhrase(obj, hostId);
                if (!string.IsNullOrEmpty(interior)) return interior;

                string beyond = BeyondLastPlanetPhrase(obj, hostId);
                if (!string.IsNullOrEmpty(beyond)) return beyond;
            }

            return "Orbiting " + sunName;
        }

        private int GetPlanetOrdinalFromSun(SystemObjectInfo planet, string hostId)
        {
            // Among heliocentric planets, sorted by radial_order
            List<SystemObjectInfo> planets = GetHeliocentricPlanets(hostId);
            if (planets == null || planets.Count == 0) return 0;

            string id = planet.ObjectId;
            for (int i = 0; i < planets.Count; i++)
            {
                if (planets[i] != null && string.Equals(planets[i].ObjectId, id, StringComparison.Ordinal))
                    return i + 1;
            }
            return 0;
        }

        private string BetweenPlanetsPhrase(SystemObjectInfo obj, string hostId)
        {
            // Find nearest inner and outer PLANET by radial_order
            List<SystemObjectInfo> planets = GetHeliocentricPlanets(hostId);
            if (planets == null || planets.Count == 0) return null;

            int ro = obj.RadialOrder;

            SystemObjectInfo inner = null;
            SystemObjectInfo outer = null;

            for (int i = 0; i < planets.Count; i++)
            {
                var p = planets[i];
                if (p == null) continue;

                if (p.RadialOrder < ro) inner = p;
                if (p.RadialOrder > ro) { outer = p; break; }
            }

            if (inner != null && outer != null)
                return SafeName(inner) + " and " + SafeName(outer);

            return null;
        }

        private string InteriorToFirstPlanetPhrase(SystemObjectInfo obj, string hostId)
        {
            List<SystemObjectInfo> planets = GetHeliocentricPlanets(hostId);
            if (planets == null || planets.Count == 0) return null;

            var first = planets[0];
            if (first == null) return null;

            if (obj.RadialOrder < first.RadialOrder)
                return "Closer to the Sun than " + SafeName(first);

            return null;
        }

        private string BeyondLastPlanetPhrase(SystemObjectInfo obj, string hostId)
        {
            List<SystemObjectInfo> planets = GetHeliocentricPlanets(hostId);
            if (planets == null || planets.Count == 0) return null;

            var last = planets[planets.Count - 1];
            if (last == null) return null;

            if (obj.RadialOrder > last.RadialOrder)
                return "Beyond " + SafeName(last);

            return null;
        }

        private List<SystemObjectInfo> GetHeliocentricPlanets(string hostId)
        {
            // HostId = star or system_root object id.
            // We define heliocentric planets as: orbit_host_object_id == hostId and object_kind == "planet"
            var list = new List<SystemObjectInfo>();

            var all = GetCurrentSystemObjectsCached();
            if (all == null) return list;

            for (int i = 0; i < all.Count; i++)
            {
                var o = all[i];
                if (o == null) continue;

                if (!string.Equals(o.OrbitHostObjectId ?? "", hostId ?? "", StringComparison.Ordinal))
                    continue;

                string k = (o.ObjectKind ?? "").Trim();
                if (!string.Equals(k, "planet", StringComparison.OrdinalIgnoreCase))
                    continue;

                list.Add(o);
            }

            list.Sort((a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return -1;
                if (b == null) return 1;
                return a.RadialOrder.CompareTo(b.RadialOrder);
            });

            return list;
        }

        private static bool IsFacilityLike(string kind)
        {
            kind = (kind ?? "").Trim().ToLowerInvariant();

            return
                kind == "installation" ||
                kind == "station" ||
                kind == "facility" ||
                kind == "military_station" ||
                kind == "outpost";
        }

        private static string SafeName(SystemObjectInfo o)
        {
            if (o == null) return "(unknown)";
            if (!string.IsNullOrWhiteSpace(o.DisplayName)) return o.DisplayName.Trim();
            return "(unnamed)";
        }

        private static string OrdinalWord(int n)
        {
            switch (n)
            {
                case 1: return "First";
                case 2: return "Second";
                case 3: return "Third";
                case 4: return "Fourth";
                case 5: return "Fifth";
                case 6: return "Sixth";
                case 7: return "Seventh";
                case 8: return "Eighth";
                case 9: return "Ninth";
                case 10: return "Tenth";
                case 11: return "Eleventh";
                case 12: return "Twelfth";
                default: return n.ToString() + "th";
            }
        }
    }
}
