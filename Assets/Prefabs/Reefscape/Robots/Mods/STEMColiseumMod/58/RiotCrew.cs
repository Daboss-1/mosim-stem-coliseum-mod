using Games.Reefscape.Enums;
using Games.Reefscape.GamePieceSystem;
using Games.Reefscape.Robots;
using RobotFramework.Components;
using RobotFramework.Controllers.GamePieceSystem;
using RobotFramework.Controllers.PidSystems;
using RobotFramework.Enums;
using RobotFramework.GamePieceSystem;
using UnityEngine;

namespace Prefabs.Reefscape.Robots.Mods.STEMColiseumMod._58
{
    public class RiotCrew : ReefscapeRobotBase
    {
        [SerializeField] private ReefscapeGamePieceIntake coralIntake;
        [SerializeField] private ReefscapeGamePieceIntake algaeIntake;

        [SerializeField] private GamePieceState coralStowState;
        [SerializeField] private GamePieceState algaeStowState;

        private RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>.GamePieceControllerNode _coralController;
        private RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>.GamePieceControllerNode _algaeController;
        [SerializeField] private GenericElevator elevator;
        [SerializeField] private GenericJoint algaeArm;
        [SerializeField] private GenericJoint climberArm;

        [SerializeField] private PidConstants algaeArmPid;
        [SerializeField] private PidConstants funnelFlapPid;
        [SerializeField] private PidConstants climberBarPid;
        [SerializeField] private PidConstants climberFlapPid;
        [SerializeField] private RiotCrewSetpoint stow;
        [SerializeField] private RiotCrewSetpoint intake;
        [SerializeField] private RiotCrewSetpoint l1;
        [SerializeField] private RiotCrewSetpoint processor;

        [SerializeField] private RiotCrewSetpoint l2;
        [SerializeField] private RiotCrewSetpoint l3;
        [SerializeField] private RiotCrewSetpoint l4;
        [SerializeField] private RiotCrewSetpoint l4Place;
        [SerializeField] private RiotCrewSetpoint lowAlgae;
        [SerializeField] private RiotCrewSetpoint highAlgae;
        [SerializeField] private RiotCrewSetpoint bargePrep;
        [SerializeField] private RiotCrewSetpoint bargePlace;

        [Header("Climb Settings")]
        [Tooltip("Angle (degrees) to extend the climb arm out into the cage")]
        [SerializeField] private float climbExtendAngle = -0f;
        [Tooltip("Angle (degrees) to retract the climb arm and pull the robot up")]
        [SerializeField] private float climbRetractAngle = 40f;
        [Tooltip("Angle (degrees) to bring the climber to stow")]
        [SerializeField] private float climbStowAngle = 90f;

        private float _elevatorTargetHeight;
        private float _algaeArmTargetAngle;
        private float _flapTargetAngle;
        private float _climbBarTargetAngle;
        private float _climbFlapTargetAngle;
        private bool _mechanismsReady;
        private bool _isClimbing;
        private bool _climbLocked;

        protected override void Start()
        {
            base.Start();

            if (!ValidateRequiredReferences())
            {
                enabled = false;
                return;
            }

            algaeArm.SetPid(algaeArmPid);
            if (climberArm)
            {
                climberArm.SetPid(climberBarPid);
            }
            RobotGamePieceController.SetPreload(coralStowState);
            _coralController = RobotGamePieceController.GetPieceByName(ReefscapeGamePieceType.Coral.ToString());
            _algaeController = RobotGamePieceController.GetPieceByName(ReefscapeGamePieceType.Algae.ToString());

            if (_coralController == null || _algaeController == null)
            {
                Debug.LogError("RiotCrew is missing Coral/Algae game-piece nodes on ReefscapeRobotGamePieceController.");
                enabled = false;
                return;
            }

            _coralController.gamePieceStates = new[]
            {
                coralStowState
            };
            _coralController.intakes.Add(coralIntake);

            _algaeController.gamePieceStates = new[] { algaeStowState };
            _algaeController.intakes.Add(algaeIntake);
            _mechanismsReady = true;
        }

        private void LateUpdate()
        {
            if (!_mechanismsReady) return;

            algaeArm.UpdatePid(algaeArmPid);
            if (climberArm) climberArm.UpdatePid(climberBarPid);
            _elevatorTargetHeight = 0;
            _algaeArmTargetAngle = 0;
            _flapTargetAngle = 0;
            if (!_isClimbing)
            {
                _climbBarTargetAngle = climbStowAngle;
                _climbFlapTargetAngle = 0;
            }
        }

        private void SetSetpoint(RiotCrewSetpoint setpoint, bool updateArm = true, bool updateElevator = true)
        {
            _elevatorTargetHeight = setpoint.elevatorHeight;
            _algaeArmTargetAngle = setpoint.algaeArmAngle;
        }

        private void UpdateSetpoints()
        {
            elevator.SetTarget(_elevatorTargetHeight);
            algaeArm.SetTargetAngle(_algaeArmTargetAngle).withAxis(JointAxis.X);
            if (climberArm)
            {
                if (!_climbLocked)
                {
                    climberArm.SetTargetAngle(_climbBarTargetAngle).withAxis(JointAxis.X);
                }

                // Lock the arm once it reaches the retract angle so the robot hangs on the cage
                if (CurrentSetpoint == ReefscapeSetpoints.Climbed && !_climbLocked)
                {
                    float currentAngle = climberArm.GetSingleAxisAngle(JointAxis.X);
                    if (MoSimLib.Utils.InAngularRange(currentAngle, climbRetractAngle, 3f))
                    {
                        climberArm.lockAllAxis();
                        _climbLocked = true;
                        Debug.Log("RiotCrew: Climb arm locked — robot is hanging.");
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            bool hasAlgae = _algaeController.HasPiece();
            bool hasCoral = _coralController.HasPiece();
            _algaeController.SetTargetState(algaeStowState);
            _coralController.SetTargetState(coralStowState);
            _algaeController.RequestIntake(algaeIntake, !hasAlgae && IntakeAction.IsPressed());
            _coralController.RequestIntake(coralIntake, !hasCoral && IntakeAction.IsPressed());


            switch (CurrentSetpoint)
            {
                case ReefscapeSetpoints.Stow:
                    _isClimbing = false;
                    _climbLocked = false;
                    if (climberArm) climberArm.freeAngularAxis(JointAxis.X);
                    SetSetpoint(stow, true, true);
                    break;
                case ReefscapeSetpoints.Intake:
                    if (!hasCoral && CurrentRobotMode == ReefscapeRobotMode.Coral)
                    {
                        SetSetpoint(intake, true, true);
                    }
                    else if (CurrentRobotMode == ReefscapeRobotMode.Coral)
                    {
                        SetSetpoint(intake, true, false);
                    }
                    _coralController.RequestIntake(coralIntake, !hasCoral);
                    break;
                case ReefscapeSetpoints.Place:
                    if (LastSetpoint == ReefscapeSetpoints.Barge)
                    {
                        SetSetpoint(bargePlace, false, false);
                    }
                    else if (LastSetpoint == ReefscapeSetpoints.L4)
                    {
                        SetSetpoint(l4Place, false, false);
                    }
                    else
                    {
                        SetState(LastSetpoint);
                    }
                    if (OuttakeAction.IsPressed())
                        PlacePiece();
                    break;
                case ReefscapeSetpoints.L1:
                    SetSetpoint(l1);
                    break;
                case ReefscapeSetpoints.Stack:
                    SetSetpoint(processor);
                    _coralController.RequestIntake(coralIntake, false);
                    break;
                case ReefscapeSetpoints.L2:
                    SetSetpoint(l2);
                    break;
                case ReefscapeSetpoints.LowAlgae:
                    SetSetpoint(lowAlgae);
                    _coralController.RequestIntake(coralIntake, false);
                    break;
                case ReefscapeSetpoints.L3:
                    SetSetpoint(l3);
                    break;
                case ReefscapeSetpoints.HighAlgae:
                    SetSetpoint(highAlgae);
                    _coralController.RequestIntake(coralIntake, false);
                    break;
                case ReefscapeSetpoints.L4:
                    SetSetpoint(l4);
                    break;
                case ReefscapeSetpoints.Processor:
                    SetSetpoint(processor);
                    break;
                case ReefscapeSetpoints.Barge:
                    SetSetpoint(bargePrep);
                    break;
                case ReefscapeSetpoints.RobotSpecial:
                    SetState(ReefscapeSetpoints.Stow);
                    break;
                case ReefscapeSetpoints.Climb:
                    _isClimbing = true;
                    _climbLocked = false;
                    if (climberArm) climberArm.freeAngularAxis(JointAxis.X);
                    _climbBarTargetAngle = climbExtendAngle;
                    break;
                case ReefscapeSetpoints.Climbed:
                    _isClimbing = true;
                    if (!_climbLocked && climberArm) climberArm.freeAngularAxis(JointAxis.X);
                    _climbBarTargetAngle = climbRetractAngle;
                    break;
            }
            UpdateSetpoints();


        }

        private bool ValidateRequiredReferences()
        {
            bool valid = true;

            if (!coralIntake)
            {
                Debug.LogError("RiotCrew is missing coralIntake reference.");
                valid = false;
            }

            if (!algaeIntake)
            {
                Debug.LogError("RiotCrew is missing algaeIntake reference.");
                valid = false;
            }

            if (coralStowState == null || algaeStowState == null)
            {
                Debug.LogError("RiotCrew is missing stow state references.");
                valid = false;
            }

            if (!elevator || !algaeArm)
            {
                Debug.LogError("RiotCrew requires elevator and algaeArm references.");
                valid = false;
            }

            if (!RobotGamePieceController)
            {
                Debug.LogError("RiotCrew requires ReefscapeRobotGamePieceController on the robot root.");
                valid = false;
            }

            return valid;
        }

        private void PlacePiece()
        {
            if (CurrentRobotMode == ReefscapeRobotMode.Algae)
            {
                if (LastSetpoint == ReefscapeSetpoints.Barge)
                {
                    _algaeController.ReleaseGamePieceWithForce(new Vector3(0, 6, 1.5f));
                }
                else
                {
                    _algaeController.ReleaseGamePieceWithForce(new Vector3(0, 0, 1.5f));
                }
                //SetState(ReefscapeSetpoints.Stow);
            }
            else
            {
                if (LastSetpoint == ReefscapeSetpoints.L4)
                {
                    _coralController.ReleaseGamePieceWithContinuedForce(new Vector3(0, 0, 5.5f), 1f, 0.5f);
                }
                else if (LastSetpoint == ReefscapeSetpoints.L1)
                {
                    _coralController.ReleaseGamePieceWithForce(new Vector3(0, 0, 2f));
                }
                else
                {
                    _coralController.ReleaseGamePieceWithForce(new Vector3(0, 0, 6f));
                }
            }
        }
    }

}