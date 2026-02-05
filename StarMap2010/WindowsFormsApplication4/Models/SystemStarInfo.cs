namespace StarMap2010.Models
{
    public sealed class SystemStarInfo
    {
        public int StarId;
        public string SystemId;

        public string StarName;        // "Sol"
        public string RealStarName;    // "Sun"
        public string StarType;        // G2V
        public string SpectralClass;   // G
        public string OrbitalRole;     // primary / secondary / tertiary

        public double? SemiMajorAxisAu;
        public double? Eccentricity;
    }
}
