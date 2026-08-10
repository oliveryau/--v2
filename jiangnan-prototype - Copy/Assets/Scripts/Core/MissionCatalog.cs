using System;
using UnityEngine;

public enum MissionTaskKind
{
    Build = 0,
    Hire = 1,
    OpenBusiness = 2
}

[Serializable]
public class MissionTaskDefinition
{
    public MissionTaskKind taskKind = MissionTaskKind.Build;
    [TextArea]
    public string description = "任务";
    public PlaceableType requiredType;
    public WorkerType requiredWorkerType;
    public int requiredCount = 1;
}

[Serializable]
public class MissionPartDefinition
{
    public string title = "任务";
    public MissionTaskDefinition[] tasks = Array.Empty<MissionTaskDefinition>();
    public bool revealSecondFloorWhenComplete;
    [Tooltip("When this mission part completes, start the hiring phase (hire spots appear).")]
    public bool startHiringWhenComplete;
}

[CreateAssetMenu(fileName = "MissionCatalog", menuName = "Jiangnan/Mission Catalog")]
public class MissionCatalog : ScriptableObject
{
    public const int MaxTasksPerPart = 3;
    public const int StarterBuildMissionPartIndex = 0;
    public const int HireMissionPartIndex = 1;
    public const int TableBuildMissionPartIndex = 2;
    public const int OpenBusinessMissionPartIndex = 3; // Stairs mission — unlocks auto-open after tables.

    [SerializeField] private MissionPartDefinition[] _parts = CreateDefaultParts();

    public MissionPartDefinition[] Parts => _parts;

    public bool TryGetPart(int partIndex, out MissionPartDefinition part)
    {
        part = null;

        if (_parts == null || partIndex < 0 || partIndex >= _parts.Length)
            return false;

        part = _parts[partIndex];
        return part != null;
    }

    public int PartCount => _parts != null ? _parts.Length : 0;

    public static MissionCatalog LoadOrCreateDefault()
    {
        MissionCatalog catalog = Resources.Load<MissionCatalog>("MissionCatalog");

        if (catalog != null)
            return catalog;

        catalog = CreateInstance<MissionCatalog>();
        catalog._parts = CreateDefaultParts();
        return catalog;
    }

    private void OnValidate()
    {
        if (_parts == null || _parts.Length == 0)
            _parts = CreateDefaultParts();

        for (int i = 0; i < _parts.Length; i++)
        {
            MissionPartDefinition part = _parts[i];

            if (part == null)
                continue;

            if (part.tasks == null)
                part.tasks = Array.Empty<MissionTaskDefinition>();

            if (part.tasks.Length > MaxTasksPerPart)
                Array.Resize(ref part.tasks, MaxTasksPerPart);
        }
    }

    private static MissionPartDefinition[] CreateDefaultParts()
    {
        return new[]
        {
            new MissionPartDefinition
            {
                title = "准备建造",
                revealSecondFloorWhenComplete = false,
                startHiringWhenComplete = true,
                tasks = new[]
                {
                    new MissionTaskDefinition
                    {
                        taskKind = MissionTaskKind.Build,
                        description = "建造柜台",
                        requiredType = PlaceableType.Reception,
                        requiredCount = 1
                    },
                    new MissionTaskDefinition
                    {
                        taskKind = MissionTaskKind.Build,
                        description = "建造灶台",
                        requiredType = PlaceableType.Stove,
                        requiredCount = 1
                    }
                }
            },
            new MissionPartDefinition
            {
                title = "招聘员工",
                revealSecondFloorWhenComplete = false,
                startHiringWhenComplete = false,
                tasks = new[]
                {
                    new MissionTaskDefinition
                    {
                        taskKind = MissionTaskKind.Hire,
                        description = "招厨师",
                        requiredWorkerType = WorkerType.Chef,
                        requiredCount = 1
                    },
                    new MissionTaskDefinition
                    {
                        taskKind = MissionTaskKind.Hire,
                        description = "招小二",
                        requiredWorkerType = WorkerType.Waiter,
                        requiredCount = 1
                    }
                }
            },
            new MissionPartDefinition
            {
                title = "建造桌子",
                revealSecondFloorWhenComplete = false,
                startHiringWhenComplete = false,
                tasks = new[]
                {
                    new MissionTaskDefinition
                    {
                        taskKind = MissionTaskKind.Build,
                        description = "建造桌子",
                        requiredType = PlaceableType.Table,
                        requiredCount = 2
                    }
                }
            },
            new MissionPartDefinition
            {
                title = "升级饭店",
                revealSecondFloorWhenComplete = false,
                startHiringWhenComplete = false,
                tasks = new[]
                {
                    new MissionTaskDefinition
                    {
                        taskKind = MissionTaskKind.Build,
                        description = "建造楼梯",
                        requiredType = PlaceableType.Stairs,
                        requiredCount = 1
                    }
                }
            },
            new MissionPartDefinition
            {
                title = "准备邀请VIP",
                revealSecondFloorWhenComplete = false,
                startHiringWhenComplete = false,
                tasks = new[]
                {
                    new MissionTaskDefinition
                    {
                        taskKind = MissionTaskKind.Build,
                        description = "建造VIP桌子",
                        requiredType = PlaceableType.VipTable,
                        requiredCount = 1
                    },
                    new MissionTaskDefinition
                    {
                        taskKind = MissionTaskKind.Hire,
                        description = "招二楼小儿队",
                        requiredWorkerType = WorkerType.Waiter,
                        // Ground waiter (mission 2) + second-floor hire spot (waiter/call ladies/performer).
                        requiredCount = 2
                    }
                }
            }
        };
    }
}
