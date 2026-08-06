#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class ChefAnimatorSetup
{
    private const string ControllerFolder = "Assets/Animations/Characters/ChefControllerDependencies";
    private const string ControllerPath = ControllerFolder + "/Chef.controller";
    private const string ResourcesControllerPath = "Assets/Resources/Chef.controller";

    private const string IdleClipPath = ControllerFolder + "/Idle.anim";
    private const string WalkModelPath =
        "Assets/2D 3D assets/TownExterior01/Animations/AnimationBaseLocomotion/Animations/Polygon/Masculine/Locomotion/Walk/A_Walk_F_Masc.fbx";

    private const string CookAddSauceModelPath = ControllerFolder + "/Chef_WorkStation_WokStirFry_AddSauce01.fbx";
    private const string CookLoopModelPath = ControllerFolder + "/Chef_WorkStation_WokStirFry_Loop.fbx";
    private const string CookBigFlipModelPath = ControllerFolder + "/Chef_WorkStation_WokStirFry_BigFlip.fbx";

    private static readonly string[] ChefModelPaths =
    {
        "Assets/2D 3D assets/Chefs/MDL_Character_Chef_Chef1.fbx",
    };

    [MenuItem("Jiangnan/Setup Chef Animator Controller", false, 110)]
    public static void BuildControllerAndWireModels()
    {
        EnsureFolder("Assets/Resources");
        EnsureHumanoidWalkImport();

        AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
        AnimationClip walkClip = LoadAnimationClip(WalkModelPath, "A_Walk_F_Masc");
        AnimationClip cookAddSauceClip = LoadAnimationClip(CookAddSauceModelPath, "Chef_WorkStation_WokStirFry_AddSauce01");
        AnimationClip cookLoopClip = LoadAnimationClip(CookLoopModelPath, "Chef_WorkStation_WokStirFry_Loop");
        AnimationClip cookBigFlipClip = LoadAnimationClip(CookBigFlipModelPath, "Chef_WorkStation_WokStirFry_BigFlip");

        if (idleClip == null || walkClip == null || cookAddSauceClip == null || cookLoopClip == null || cookBigFlipClip == null)
        {
            Debug.LogError("Chef animator setup failed: could not load idle, walk, or cook clips.");
            return;
        }

        AnimatorController controller = BuildChefController(
            idleClip,
            walkClip,
            cookAddSauceClip,
            cookLoopClip,
            cookBigFlipClip);

        if (controller == null)
            return;

        AssetDatabase.CopyAsset(ControllerPath, ResourcesControllerPath);
        controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        int wiredCount = 0;

        for (int i = 0; i < ChefModelPaths.Length; i++)
        {
            if (WireChefModel(ChefModelPaths[i], controller))
                wiredCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Chef animator setup complete. Wired {wiredCount} model(s).");
    }

    private static AnimatorController BuildChefController(
        AnimationClip idleClip,
        AnimationClip walkClip,
        AnimationClip cookAddSauceClip,
        AnimationClip cookLoopClip,
        AnimationClip cookBigFlipClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        else
            ClearController(controller);

        controller.name = "Chef";
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "IsWalking",
            type = AnimatorControllerParameterType.Bool,
            defaultBool = false,
        });
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "IsCooking",
            type = AnimatorControllerParameterType.Bool,
            defaultBool = false,
        });
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "CookAnimIndex",
            type = AnimatorControllerParameterType.Int,
            defaultInt = 0,
        });

        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = rootStateMachine.AddState("Idle", new Vector3(300f, 0f, 0f));
        AnimatorState walkState = rootStateMachine.AddState("Walk", new Vector3(300f, 100f, 0f));
        AnimatorState cookAddSauceState = rootStateMachine.AddState(
            "Chef_WorkStation_WokStirFry_AddSauce01",
            new Vector3(560f, 0f, 0f));
        AnimatorState cookLoopState = rootStateMachine.AddState(
            "Chef_WorkStation_WokStirFry_Loop",
            new Vector3(560f, 100f, 0f));
        AnimatorState cookBigFlipState = rootStateMachine.AddState(
            "Chef_WorkStation_WokStirFry_BigFlip",
            new Vector3(560f, 200f, 0f));

        idleState.motion = idleClip;
        walkState.motion = walkClip;
        cookAddSauceState.motion = cookAddSauceClip;
        cookLoopState.motion = cookLoopClip;
        cookBigFlipState.motion = cookBigFlipClip;
        rootStateMachine.defaultState = idleState;

        AddWalkingTransition(idleState, walkState, enteringWalk: true);
        AddWalkingTransition(walkState, idleState, enteringWalk: false);

        AddCookEntryTransition(idleState, cookAddSauceState, cookIndex: 0);
        AddCookEntryTransition(idleState, cookLoopState, cookIndex: 1);
        AddCookEntryTransition(idleState, cookBigFlipState, cookIndex: 2);
        AddCookEntryTransition(walkState, cookAddSauceState, cookIndex: 0);
        AddCookEntryTransition(walkState, cookLoopState, cookIndex: 1);
        AddCookEntryTransition(walkState, cookBigFlipState, cookIndex: 2);

        AddCookExitTransition(cookAddSauceState, idleState);
        AddCookExitTransition(cookLoopState, idleState);
        AddCookExitTransition(cookBigFlipState, idleState);

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static bool WireChefModel(string modelPath, RuntimeAnimatorController controller)
    {
        GameObject modelRoot = PrefabUtility.LoadPrefabContents(modelPath);

        if (modelRoot == null)
        {
            Debug.LogWarning($"Chef animator setup skipped missing model: {modelPath}");
            return false;
        }

        Animator animator = modelRoot.GetComponent<Animator>();

        if (animator == null)
            animator = modelRoot.AddComponent<Animator>();

        Avatar avatar = LoadAvatar(modelPath);

        if (avatar == null)
        {
            Debug.LogWarning($"Chef animator setup could not find avatar for {modelPath}");
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

    private static void AddWalkingTransition(AnimatorState source, AnimatorState destination, bool enteringWalk)
    {
        AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = false;
        transition.duration = 0.12f;
        transition.AddCondition(
            enteringWalk ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
            0f,
            "IsWalking");
        transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCooking");
    }

    private static void AddCookEntryTransition(AnimatorState source, AnimatorState destination, int cookIndex)
    {
        AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = false;
        transition.duration = 0.15f;
        transition.AddCondition(AnimatorConditionMode.If, 0f, "IsCooking");
        transition.AddCondition(AnimatorConditionMode.Equals, cookIndex, "CookAnimIndex");
    }

    private static void AddCookExitTransition(AnimatorState source, AnimatorState destination)
    {
        AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = false;
        transition.duration = 0.15f;
        transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCooking");
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
}
#endif
