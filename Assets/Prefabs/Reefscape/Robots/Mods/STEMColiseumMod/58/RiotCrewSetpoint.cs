
using UnityEngine;

namespace Prefabs.Reefscape.Robots.Mods.STEMColiseumMod._58
{
   [CreateAssetMenu(fileName = "Setpoint", menuName = "Robot/RiotCrew Setpoint", order = 0)]
   public class RiotCrewSetpoint : ScriptableObject
   {
       [Tooltip("Inches")] public float elevatorHeight;
       [Tooltip("Degrees")] public float algaeArmAngle;
   }
}