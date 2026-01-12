namespace StarMap2010.Models
{
    public class StarSystemInfo
    {
        public string SystemId;
        public string SystemName;
        public string RealSystemName;
        public string PrimaryStarName;
        public string PrimaryStarType;
        public string primaryStarColor;
        public string GovernmentId;
        public string GovernmentName;
        public string FactionColor;
        public float XReal, YReal, ZReal;
        public float RaDeg, DecDeg, DistanceLy;
        public int ScreenX, ScreenY;   // <-- use these for drawing
        public string SystemType;
        public string StrategicValue;
        public string HabitabilityClass;
        public string Notes;
    }
}
