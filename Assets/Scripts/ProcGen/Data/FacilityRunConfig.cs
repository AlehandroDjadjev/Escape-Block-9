using EscapeBlock9.ProcGen.Planning;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Data
{
    [CreateAssetMenu(menuName = "Escape Block 9/ProcGen/Facility Run Config", fileName = "FacilityRunConfig")]
    public sealed class FacilityRunConfig : ScriptableObject
    {
        [SerializeField] private FacilityGraphPlanConfig graphPlan = new FacilityGraphPlanConfig();

        public FacilityGraphPlanConfig GraphPlan => graphPlan;
    }
}
