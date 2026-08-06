using UnityEngine.SceneManagement;

public static class RestaurantSceneMode
{
    public const string TownSceneName = "2_Town Scene";
    public const string MainSceneName = "3_Main Scene";
    public const string CompetitorSceneName = "4_Competitor Scene";

    public static bool IsTownScene =>
        string.Equals(SceneManager.GetActiveScene().name, TownSceneName, System.StringComparison.Ordinal);

    public static bool IsCompetitorScene =>
        string.Equals(SceneManager.GetActiveScene().name, CompetitorSceneName, System.StringComparison.Ordinal);

    public static bool IsMainScene =>
        string.Equals(SceneManager.GetActiveScene().name, MainSceneName, System.StringComparison.Ordinal);

    public static bool UsesWorkerEnergyUi => !IsCompetitorScene;
}
