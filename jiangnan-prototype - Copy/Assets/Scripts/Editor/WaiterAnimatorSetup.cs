#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class WaiterAnimatorSetup
{
    private const string ControllerFolder = "Assets/Animations/Characters/WaiterControllerDependencies";
    private const string ControllerPath = ControllerFolder + "/Waiter.controller";
    private const string ResourcesControllerPath = "Assets/Resources/Waiter.controller";

    private const string IdleClipPath = "Assets/Animations/Characters/ChefControllerDependencies/Idle.anim";
    private const string WalkModelPath =
        "Assets/2D 3D assets/TownExterior01/Animations/AnimationBaseLocomotion/Animations/Polygon/Masculine/Locomotion/Walk/A_Walk_F_Masc.fbx";

    private static readonly string[] WaiterModelPaths =
    {
        "Assets/2D 3D assets/Waiters/WaiterM1/MDL_Character_Waiter_WaiterM1_WaiterM1.fbx",
        "Assets/2D 3D assets/Waiters/WaiterF1/MDL_Character_Waiter_WaiterF1_WaiterF1.fbx",
    };

    [MenuItem("Jiangnan/Setup Waiter Animator Controller", false, 111)]
    public static void BuildControllerAndWireModels()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder(ControllerFolder);
        EnsureHumanoidWalkImport();

        AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
        AnimationClip walkClip = LoadAnimationClip(WalkModelPath, "A_Walk_F_Masc");

        if (idleClip == null || walkClip == null)
        {
            Debug.LogError("Waiter animator setup failed: could not load idle or walk clips.");
            return;
        }

        AnimatorController controller = BuildWaiterController(idleClip, walkClip);

        if (controller == null)
            return;

        AssetDatabase.CopyAsset(ControllerPath, ResourcesControllerPath);
        controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        int wiredCount = 0;

        for (int i = 0; i < WaiterModelPaths.Length; i++)
        {
            if (WireWaiterModel(WaiterModelPaths[i], controller))
                wiredCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Waiter animator setup complete. Wired {wiredCount} model(s).");
    }

    private static AnimatorController BuildWaiterController(AnimationClip idleClip, AnimationClip walkClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        else
            ClearController(controller);

        controller.name = "Waiter";
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "IsWalking",
            type = AnimatorControllerParameterType.Bool,
            defaultBool = false,
        });

        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = rootStateMachine.AddState("Idle", new Vector3(300f, 0f, 0f));
        AnimatorState walkState = rootStateMachine.AddState("Walk", new Vector3(300f, 100f, 0f));

        idleState.motion = idleClip;
        walkState.motion = walkClip;
        rootStateMachine.defaultState = idleState;

        AddBoolTransition(idleState, walkState, true);
        AddBoolTransition(walkState, idleState, false);

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static bool WireWaiterModel(string modelPath, RuntimeAnimatorController controller)
    {
        GameObject modelRoot = PrefabUtility.LoadPrefabContents(modelPath);

        if (modelRoot == null)
        {
            Debug.LogWarning($"Waiter animator setup skipped missing model: {modelPath}");
            return false;
        }

        Animator animator = modelRoot.GetComponent<Animator>();

        if (animator == null)
            animator = modelRoot.AddComponent<Animator>();

        Avatar avatar = LoadAvatar(modelPath);

        if (avatar == null)
        {
            Debug.LogWarning($"Waiter animator setup could not find avatar for {modelPath}");
            PrefabUtility.UnloadPrefabContents(modelRoot);
            return false;
        }

        animator.runtimeAnimatorController = controller;
        animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        if (modelRoot.GetComponent<WorkerCharacterAnimator>() == null)
            modelRoot.AddComponent<WorkerCharacterAnimator>();

        PrefabUtility.SaveAsPrefabAsset(modelRoot, modelPath);
        PrefabUtility.UnloadPrefabContents(modelRoot);
        return true;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string folderName = Path.GetFileName(folderPath);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static void EnsureHumanoidWalkImport()
    {
        ModelImporter importer = AssetImporter.GetAtPath(WalkModelPath) as ModelImporter;

        if (importer == null)
            return;

        bool changed = false;

        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            changed = true;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;

        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        if (clips != null && clips.Length > 0)
        {
            clips[0].name = "A_Walk_F_Masc";
            clips[0].loopTime = true;
            clips[0].loopPose = true;
            importer.clipAnimations = clips;
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();
    }

    private static AnimationClip LoadAnimationClip(string assetPath, string preferredName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__") && clip.name == preferredName)
                return clip;
        }

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;
        }

        return null;
    }

    private static Avatar LoadAvatar(string modelPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Avatar avatar)
                return avatar;
        }

        return null;
    }

    private static void ClearController(AnimatorController controller)
    {
        while (controller.parameters.Length > 0)
            controller.RemoveParameter(0);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        while (stateMachine.states.Length > 0)
            stateMachine.RemoveState(stateMachine.states[0].state);
    }

    private static void AddBoolTransition(AnimatorState source, AnimatorState destination, bool isWalking)
    {
        AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = false;
        transition.duration = 0.12f;
        transition.AddCondition(
            isWalking ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
            0f,
            "IsWalking");
    }
}
#endif
