namespace Cthangover.Core.Scenes
{
    public class SceneEventInfo
    {
        public string Id { get; set; }
        public string ClassName { get; set; }
        public string ScenarioPath { get; set; }
        public int Priority { get; set; }
        public string After { get; set; }
        public string Condition { get; set; }
        public bool? LightUseTime { get; set; }
        public bool IsOneRun { get; set; }
    }
}
