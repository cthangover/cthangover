namespace Cthangover.Core.Scenes
{
    public class ScenarioDefinition
    {
        public string Name { get; set; }
        public string Scene { get; set; }
        public int Priority { get; set; }
        public string After { get; set; }
        public string Condition { get; set; }
        public bool? LightUseTime { get; set; }
        public bool SaveAllowed { get; set; }
        public bool IsOneRun { get; set; }
        public string ModId { get; set; }
        public string FilePath { get; set; }
    }
}
