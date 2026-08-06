using UnityEngine;

public enum RestaurantFloor
{
    Ground = 1,
    Second = 2
}

public static class RestaurantFloorUtil
{
    public const string SecondFloorBuildSpotsRootName = "BuildSpots_SecondFloor";
    public const string SecondFloorEnvironmentName = "Second Floor";
    public const string SecondFloorLayerName = "SecondFloor";

    public static bool IsUnderSecondFloorHierarchy(Transform transform)
    {
        Transform current = transform;

        while (current != null)
        {
            if (string.Equals(current.name, SecondFloorBuildSpotsRootName, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(current.name, SecondFloorEnvironmentName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    public static RestaurantFloor ResolveFloor(Transform transform, RestaurantFloor authoredFloor = RestaurantFloor.Ground)
    {
        if (authoredFloor == RestaurantFloor.Second)
            return RestaurantFloor.Second;

        if (transform == null)
            return authoredFloor;

        if (IsUnderSecondFloorHierarchy(transform))
            return RestaurantFloor.Second;

        int secondFloorLayer = LayerMask.NameToLayer(SecondFloorLayerName);
        if (secondFloorLayer >= 0 && transform.gameObject.layer == secondFloorLayer)
            return RestaurantFloor.Second;

        // Authored second-floor camera height is ~5; treat clearly elevated spots as upstairs.
        if (transform.position.y >= 3.5f)
            return RestaurantFloor.Second;

        return RestaurantFloor.Ground;
    }

    public static bool IsUnlockedForCurrentPlayer()
    {
        if (PlayerProfileStorage.HasMainSceneSecondFloorRevealedForCurrentPlayer())
            return true;

        BuildSequenceController buildSequence = Object.FindFirstObjectByType<BuildSequenceController>();
        return buildSequence != null && buildSequence.IsSecondFloorUnlocked;
    }
}
