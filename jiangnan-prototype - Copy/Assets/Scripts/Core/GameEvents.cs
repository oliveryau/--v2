using System;

public static class GameEvents
{
    public static event Action<GameState> StateChanged;
    public static event Action<BuildSpot, BuildSpotState> BuildSpotStateChanged;
    public static event Action<HireSpot, HireSpotState> HireSpotStateChanged;
    public static event Action<Customer, CustomerState> CustomerStateChanged;
    public static event Action<Worker, WorkerState> WorkerStateChanged;
    public static event Action<Worker, int, int> WorkerEnergyChanged;
    public static event Action<int> GoldChanged;
    public static event Action HiringCompleted;
    public static event Action TableBuildInfoRequested;
    public static event Action<int> MissionPartChanged;
    public static event Action<int> MissionPartCompleted;
    public static event Action MainSceneSecondFloorRevealRequested;
    public static event Action SecondFloorUnlocked;
    public static event Action<int> RestaurantFloorChanged;
    public static event Action BusinessSessionStarted;
    public static event Action BusinessSessionEnded;
    public static event Action BusinessFloorCleared;
    public static event Action BusinessDowntimeStarted;
    public static event Action<DiningTable> TableClicked;
    public static event Action<DiningTable> TableUpgraded;
    public static event Action<DiningTable> TableStatusChanged;
    public static event Action CustomerSpawned;
    public static event Action BagInventoryChanged;

    public static void RaiseStateChanged(GameState state)
    {
        StateChanged?.Invoke(state);
    }

    public static void RaiseBuildSpotStateChanged(BuildSpot spot, BuildSpotState state)
    {
        BuildSpotStateChanged?.Invoke(spot, state);
    }

    public static void RaiseHireSpotStateChanged(HireSpot spot, HireSpotState state)
    {
        HireSpotStateChanged?.Invoke(spot, state);
    }

    public static void RaiseCustomerStateChanged(Customer customer, CustomerState state)
    {
        CustomerStateChanged?.Invoke(customer, state);
    }

    public static void RaiseWorkerStateChanged(Worker worker, WorkerState state)
    {
        WorkerStateChanged?.Invoke(worker, state);
    }

    public static void RaiseWorkerEnergyChanged(Worker worker, int currentEnergy, int maxEnergy)
    {
        WorkerEnergyChanged?.Invoke(worker, currentEnergy, maxEnergy);
    }

    public static void RaiseGoldChanged(int currentGold)
    {
        GoldChanged?.Invoke(currentGold);
    }

    public static void RaiseHiringCompleted()
    {
        HiringCompleted?.Invoke();
    }

    public static void RaiseTableBuildInfoRequested()
    {
        TableBuildInfoRequested?.Invoke();
    }

    public static void RaiseMissionPartChanged(int partIndex)
    {
        MissionPartChanged?.Invoke(partIndex);
    }

    public static void RaiseMissionPartCompleted(int partIndex)
    {
        MissionPartCompleted?.Invoke(partIndex);
    }

    public static void RaiseMainSceneSecondFloorRevealRequested()
    {
        MainSceneSecondFloorRevealRequested?.Invoke();
    }

    public static void RaiseSecondFloorUnlocked()
    {
        SecondFloorUnlocked?.Invoke();
    }

    public static void RaiseRestaurantFloorChanged(int floor)
    {
        RestaurantFloorChanged?.Invoke(floor);
    }

    public static void RaiseBusinessSessionStarted()
    {
        BusinessSessionStarted?.Invoke();
    }

    public static void RaiseBusinessSessionEnded()
    {
        BusinessSessionEnded?.Invoke();
    }

    public static void RaiseBusinessFloorCleared()
    {
        BusinessFloorCleared?.Invoke();
    }

    public static void RaiseBusinessDowntimeStarted()
    {
        BusinessDowntimeStarted?.Invoke();
    }

    public static void RaiseTableClicked(DiningTable table)
    {
        TableClicked?.Invoke(table);
    }

    public static void RaiseTableUpgraded(DiningTable table)
    {
        TableUpgraded?.Invoke(table);
    }

    public static void RaiseTableStatusChanged(DiningTable table)
    {
        TableStatusChanged?.Invoke(table);
    }

    public static void RaiseCustomerSpawned()
    {
        CustomerSpawned?.Invoke();
    }

    public static void RaiseBagInventoryChanged()
    {
        BagInventoryChanged?.Invoke();
    }
}
