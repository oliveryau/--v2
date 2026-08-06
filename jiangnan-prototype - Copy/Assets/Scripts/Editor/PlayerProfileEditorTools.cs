#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PlayerProfileEditorTools
{
    private const string ProfilesFolderName = "PlayerProfiles";
    [MenuItem("Jiangnan/Reset All Player Profiles", false, 100)]
    private static void ResetAllPlayerProfiles()
    {
        string profilesDirectory = GetProfilesDirectory();
        int profileCount = Directory.Exists(profilesDirectory)
            ? Directory.GetFiles(profilesDirectory, "*.json").Length
            : 0;

        bool confirmed = EditorUtility.DisplayDialog(
            "Reset All Player Profiles",
            $"This deletes all saved player JSON files ({profileCount}) and clears the last-used player name.\n\n" +
            "Editor only. Does not affect builds on other machines.\n\n" +
            $"Folder:\n{profilesDirectory}",
            "Reset",
            "Cancel");

        if (!confirmed)
            return;

        int deletedCount = PlayerProfileStorage.ResetAllPlayerData();
        GoldManager.ClearSessionCache();

        Debug.Log($"Reset all player profiles. Deleted {deletedCount} file(s) from {profilesDirectory}");
        EditorUtility.DisplayDialog(
            "Player Profiles Reset",
            $"Deleted {deletedCount} profile file(s) and cleared saved player name prefs.\n\n" +
            "Stop and restart Play Mode before testing again.",
            "OK");
    }

    [MenuItem("Jiangnan/Open Player Profiles Folder", false, 101)]
    private static void OpenPlayerProfilesFolder()
    {
        string profilesDirectory = GetProfilesDirectory();
        Directory.CreateDirectory(profilesDirectory);
        EditorUtility.RevealInFinder(profilesDirectory);
    }

    private static string GetProfilesDirectory()
    {
        return Path.Combine(Application.persistentDataPath, ProfilesFolderName);
    }
}
#endif
