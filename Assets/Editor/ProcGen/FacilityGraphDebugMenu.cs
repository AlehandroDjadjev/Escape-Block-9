using EscapeBlock9.ProcGen.Planning;
using UnityEditor;

namespace EscapeBlock9.ProcGen.Editor
{
    public static class FacilityGraphDebugMenu
    {
        [MenuItem("Tools/ProcGen/Print Seeded Logical Graph")]
        public static void PrintSeededLogicalGraph()
        {
            var config = FacilityGraphPlanConfig.CreateDefault(12345);
            FacilityGraph graph = new FacilityGraphPlanner().Plan(config);
            FacilityGraphDebug.Log(graph);
        }
    }
}
