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
    public const float SecondFloorElevationY = 3.5f;

    public static int SecondFloorLayer => LayerMask.NameToLayer(SecondFloorLayerName);

    public static bool IsUnderSecondFloorHierarchy(Transform transform)
    {
        Transform current = transform;

        while (current != null)
        {
            if (string.Equals(current.name, SecondFloorBuildSpotsRootName, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(current.name, SecondFloorEnvironmentName, System.StringComparison.OrdinalIgnoreCase)
                || current.name.StartsWith(SecondFloorEnvironmentName, System.StringComparison.OrdinalIgnoreCase))
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

        // Overlay UI uses screen pixels as world position; never treat that Y as restaurant elevation.
        if (IsUnderScreenSpaceOverlayCanvas(transform))
            return authoredFloor;

        if (IsUnderSecondFloorHierarchy(transform))
            return RestaurantFloor.Second;

        int secondFloorLayer = SecondFloorLayer;
        if (secondFloorLayer >= 0 && transform.gameObject.layer == secondFloorLayer)
            return RestaurantFloor.Second;

        // Authored second-floor camera height is ~5; treat clearly elevated spots as upstairs.
        if (transform.position.y >= SecondFloorElevationY)
            return RestaurantFloor.Second;

        return RestaurantFloor.Ground;
    }

    public static bool IsUnderScreenSpaceOverlayCanvas(Transform transform)
    {
        Transform current = transform;

        while (current != null)
        {
            Canvas canvas = current.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return true;

            current = current.parent;
        }

        return false;
    }

    public static bool IsUnlockedForCurrentPlayer()
    {
        if (PlayerProfileStorage.HasMainSceneSecondFloorRevealedForCurrentPlayer())
            return true;

        BuildSequenceController buildSequence = Object.FindFirstObjectByType<BuildSequenceController>();
        return buildSequence != null && buildSequence.IsSecondFloorUnlocked;
    }

    /// <summary>
    /// True when the transform is physically on the upstairs elevation (not just VIP-tagged).
    /// </summary>
    public static bool IsAtSecondFloorElevation(Transform transform)
    {
        return transform != null && transform.position.y >= SecondFloorElevationY;
    }

    /// <summary>
    /// Cull layer follows current elevation so ground-level VIP/waiter traffic stays visible on floor 1.
    /// </summary>
    public static void SyncActorFloorViewLayerByElevation(GameObject root)
    {
        if (root == null)
            return;

        bool upstairs = IsAtSecondFloorElevation(root.transform);
        int targetLayer = upstairs ? SecondFloorLayer : 0;

        if (upstairs && targetLayer < 0)
            return;

        if (root.layer == targetLayer)
            return;

        SetBelongsToSecondFloorView(root, upstairs);
    }

    /// <summary>
    /// Puts an actor on the SecondFloor layer only while upstairs so floor-1 camera/light culling hides them.
    /// </summary>
    public static void SetBelongsToSecondFloorView(GameObject root, bool belongsToSecondFloor)
    {
        if (root == null)
            return;

        int layer = belongsToSecondFloor ? SecondFloorLayer : 0;

        if (belongsToSecondFloor && layer < 0)
            return;

        SetLayerRecursively(root, layer);
    }

    public static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null || layer < 0)
            return;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null)
                transforms[i].gameObject.layer = layer;
        }
    }
}
