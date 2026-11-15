using System;
using System.Collections.Generic;
using OutOfReach.Contracts;

namespace OutOfReach.Tests.ChapterUi
{
    /// <summary>Test double of the mission clock and game state (one elapsed time for both, like Core).</summary>
    public sealed class FakeMission : IGameState, ISimulationClock
    {
        public GamePhase Phase { get; set; } = GamePhase.Planning;
        public MissionOutcome Outcome { get; set; }
        public double ElapsedSimulationSeconds { get; set; }
        public bool IsPaused { get; set; }
        public double TimeScale { get; set; } = 60d;
        public CommandResult TryTransition(GamePhase next) { Phase = next; return new CommandResult(true, CommandFailure.None, string.Empty); }
        public void AdvanceTime(double simulationSeconds) => ElapsedSimulationSeconds += simulationSeconds;

        public static double At(int day, int hour, int minute = 0, double second = 0d) =>
            ((day - 1) * 86400d) + (hour * 3600d) + (minute * 60d) + second;
    }

    public sealed class FakeResources : IResourceSystem
    {
        public ResourceSnapshot Snapshot { get; set; } = new ResourceSnapshot(42d, 120d, 40d, 4.2d, 3, 900d);
        public double GetQuantity(ResourceId resource) => 0d;
        public CommandResult TryConsume(IReadOnlyList<ResourceAmount> costs) => new CommandResult(false, CommandFailure.InvalidInput, "fake");
        public void AdvanceTime(double simulationSeconds) { }
    }

    public sealed class FakeRecovery : ISurvivalRecovery
    {
        public bool IsResting { get; set; }
        public double RestRemainingSimulationSeconds { get; set; }
        public RestStopReason LastRestStop { get; set; }
        public int MissionDay { get; set; } = 1;
        public RecoveryStage Stage { get; set; }
        public double? ArrivalAtSimulationSeconds { get; set; }
        public double ArrivalRemainingSimulationSeconds { get; set; }
        public double? BeaconWindowEndsAtSimulationSeconds { get; set; }
        public CommandResult PowerCheck { get; set; } = new CommandResult(true, CommandFailure.None, "Power the beacon: 0.4 kWh from the battery");
        public CommandResult CheckRest(double hours) => new CommandResult(true, CommandFailure.None, string.Empty);
        public CommandResult TryBeginRest(double hours) => CheckRest(hours);
        public CommandResult TryWake() => new CommandResult(true, CommandFailure.None, string.Empty);
        public CommandResult CheckPowerBeacon() => PowerCheck;
        public CommandResult TryPowerBeacon() => PowerCheck;
        public event Action Changed { add { } remove { } }
    }

    public sealed class FakeSurvival : ISurvivalExtension
    {
        public bool Enabled { get; set; } = true;
        public bool IsActive { get; set; }
        public double? ContactRestoredAtSimulationSeconds { get; set; }
        public double Hunger01 { get; set; }
        public double Thirst01 { get; set; }
        public GrowModuleStage GrowStage { get; set; }
        public double CropAgeSimulationDays { get; set; }
        public double CropRemainingSimulationSeconds { get; set; }
        public CommandResult TryConsumeRation() => Ok();
        public CommandResult TryDrinkWater() => Ok();
        public CommandResult TryBuildGrowModule() => Ok();
        public CommandResult TryPlant() => Ok();
        public CommandResult TryHarvest() => Ok();
        public event Action Changed { add { } remove { } }
        private static CommandResult Ok() => new CommandResult(true, CommandFailure.None, string.Empty);
    }

    /// <summary>Scriptable IMissionChapterFlow: records every command the UI issues; tests set the state Core would commit.</summary>
    public sealed class FakeChapterFlow : IMissionChapterFlow
    {
        private readonly FakeMission mission;

        public FakeChapterFlow(FakeMission mission) { this.mission = mission; }

        public bool Enabled { get; set; } = true;
        public MissionChapter Chapter { get; set; } = MissionChapter.Accident;
        public MissionChapter NextChapter { get; set; } = MissionChapter.Consequences;
        public double? NextChapterAtSimulationSeconds { get; set; } = FakeMission.At(5, 8);
        public bool IsRoutineRunning { get; set; }
        public double? RoutineStartedAtSimulationSeconds { get; set; }
        public double? RoutineStoppedAtSimulationSeconds { get; set; }
        public RoutineStopReason LastRoutineStop { get; set; }
        public string LastRoutineStopText { get; set; } = string.Empty;
        public int RoutineRations { get; set; }
        public int RoutineDrinks { get; set; }
        public int RoutineRests { get; set; }
        public ChapterDecision PendingDecision { get; set; }
        public RescuePlan Rescue { get; set; }
        public MissionDispatch LatestDispatch { get; set; }
        public int Revision { get; set; }

        public CommandResult ContinueCheck { get; set; } = new CommandResult(true, CommandFailure.None, "Routine days until the inspection");
        public Func<ChapterChoice, CommandResult> ChooseCheck { get; set; } = choice => new CommandResult(true, CommandFailure.None, "Core explains " + choice);
        public int ContinueCalls { get; private set; }
        public int CancelCalls { get; private set; }
        public List<ChapterChoice> Chosen { get; } = new List<ChapterChoice>();

        public CommandResult CheckContinueRoutine() => ContinueCheck;

        public CommandResult TryContinueRoutine()
        {
            ContinueCalls++;
            if (!ContinueCheck.Succeeded) return ContinueCheck;
            StartRoutine();
            return ContinueCheck;
        }

        public CommandResult TryCancelRoutine()
        {
            CancelCalls++;
            if (!IsRoutineRunning) return new CommandResult(false, CommandFailure.PreconditionsNotMet, "No routine is running.");
            StopRoutine(RoutineStopReason.PlayerCancelled, "the crew stopped the routine");
            return new CommandResult(true, CommandFailure.None, "Routine stopped at the current time.");
        }

        public CommandResult CheckChoose(ChapterChoice choice) => ChooseCheck(choice);

        public CommandResult TryChoose(ChapterChoice choice)
        {
            CommandResult check = ChooseCheck(choice);
            if (!check.Succeeded) return check;
            Chosen.Add(choice);
            PendingDecision = ChapterDecision.None;
            Revision++;
            return check;
        }

        public event Action Changed { add { } remove { } }

        /// <summary>What Core commits when a routine starts at the current mission time.</summary>
        public void StartRoutine()
        {
            IsRoutineRunning = true;
            RoutineStartedAtSimulationSeconds = mission.ElapsedSimulationSeconds;
            RoutineStoppedAtSimulationSeconds = null;
            LastRoutineStop = RoutineStopReason.None;
            LastRoutineStopText = string.Empty;
            RoutineRations = RoutineDrinks = RoutineRests = 0;
            Revision++;
        }

        /// <summary>What Core commits when a routine stops at the current (actual) mission time.</summary>
        public void StopRoutine(RoutineStopReason reason, string text)
        {
            IsRoutineRunning = false;
            RoutineStoppedAtSimulationSeconds = mission.ElapsedSimulationSeconds;
            LastRoutineStop = reason;
            LastRoutineStopText = text;
            Revision++;
        }
    }
}
