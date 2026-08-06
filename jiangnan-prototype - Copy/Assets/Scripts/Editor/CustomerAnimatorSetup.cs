#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class CustomerAnimatorSetup
{
    private const string ControllerFolder = "Assets/Animations/Characters/CustomerController";
    private const string CustomerControllerPath = ControllerFolder + "/Customer.controller";
    private const string Vip1ControllerPath = ControllerFolder + "/Customer_VIP1.controller";
    private const string Vip2ControllerPath = ControllerFolder + "/Customer_VIP2.controller";

    private const string CustomerWalkModelPath =
        "Assets/2D 3D assets/TownExterior01/Animations/AnimationBaseLocomotion/Animations/Polygon/Masculine/Locomotion/Walk/A_Walk_F_Masc.fbx";

    private const string CustomerIdleClipPath =
        "Assets/Animations/Characters/ChefControllerDependencies/Idle.anim";

    private const string CustomerSitModelPath =
        "Assets/Animations/Characters/ChefControllerDependencies/Customer_Sit_Chair_Idle.fbx";

    private const string CustomerSitAnimFallbackPath =
        "Assets/2D 3D assets/TownExterior01/Animations/Vendors_and_Customers/Animations/A_Environment_TownExterior01_VendorsAndCustomers_SitIdle.anim";

    private const string CustomerEatModelPath =
        "Assets/2D 3D assets/TownExterior01/Animations/Vendors_and_Customers/Animations/Customer_Sit_Chair_Eat_NoodleBowl_Loop.fbx";

    private const string Noble01ModelPath =
        "Assets/2D 3D assets/Customers/Noble_01/Noble 01_Animated.fbx";

    private const string Noble01EatClipPath =
        "Assets/2D 3D assets/Customers/Noble_01/Noble01_Eating.anim";

    private const string Noble02ModelPath =
        "Assets/2D 3D assets/Customers/Noble_02/Noble 02_Animated.fbx";

    private const string Noble02EatClipPath =
        "Assets/2D 3D assets/Customers/Noble_02/Noble02_Eating.anim";

    private static readonly (string PrefabPath, string ModelPath, string ControllerPath)[] CustomerPrefabs =
    {
        (
            "Assets/Prefabs/Customer.prefab",
            "Assets/2D 3D assets/Customers/CustomerM3/MDL_Character_Customer_CustomerM3_CustomerM3.fbx",
            CustomerControllerPath
        ),
        (
            "Assets/Prefabs/CustomerVIP_1.prefab",
            Noble01ModelPath,
            Vip1ControllerPath
        ),
        (
            "Assets/Prefabs/CustomerVIP_2.prefab",
            Noble02ModelPath,
            Vip2ControllerPath
        ),
    };

    [MenuItem("Jiangnan/Setup Customer Animator Controller", false, 110)]
    public static void BuildControllerAndWirePrefabs()
    {
        EnsureFolder(ControllerFolder);
        EnsureHumanoidWalkImport();

        AnimationClip customerWalkClip = LoadAnimationClip(CustomerWalkModelPath, "A_Walk_F_Masc");
        AnimationClip customerIdleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(CustomerIdleClipPath);
        AnimationClip customerSitClip = LoadAnimationClip(CustomerSitModelPath, "Customer_Sit_Chair_Idle")
            ?? AssetDatabase.LoadAssetAtPath<AnimationClip>(CustomerSitAnimFallbackPath);
        AnimationClip customerEatClip = LoadAnimationClip(CustomerEatModelPath, "sit_chair_eat_noodlebowl_loop")
            ?? LoadAnimationClip(CustomerEatModelPath, "Customer_Sit_Chair_Eat_NoodleBowl_Loop");

        if (customerWalkClip == null || customerIdleClip == null || customerSitClip == null || customerEatClip == null)
        {
            Debug.LogError("Customer animator setup failed: could not load regular customer idle/walk/sit/eat clips.");
            return;
        }

        AnimatorController customerController = PopulateCustomerController(
            CustomerControllerPath,
            "Customer",
            customerIdleClip,
            customerWalkClip,
            customerSitClip,
            customerEatClip);

        AnimatorController vip1Controller = PopulateVipController(
            Vip1ControllerPath,
            "Customer_VIP1",
            Noble01ModelPath,
            Noble01EatClipPath);

        AnimatorController vip2Controller = PopulateVipController(
            Vip2ControllerPath,
            "Customer_VIP2",
            Noble02ModelPath,
            Noble02EatClipPath);

        if (customerController == null || vip1Controller == null || vip2Controller == null)
            return;

        int wiredCount = 0;

        for (int i = 0; i < CustomerPrefabs.Length; i++)
        {
            (string prefabPath, string modelPath, string controllerPath) = CustomerPrefabs[i];
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);

            if (controller != null && WireCustomerPrefab(prefabPath, modelPath, controller))
                wiredCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Customer animator setup complete. Wired {wiredCount} prefab(s).");
    }

    private static AnimatorController PopulateVipController(
        string controllerPath,
        string controllerName,
        string modelPath,
        string eatClipPath)
    {
        AnimationClip idleClip = LoadAnimationClip(modelPath, "Standing Idle");
        AnimationClip walkClip = LoadAnimationClip(modelPath, "Walking");
        AnimationClip sitClip = LoadAnimationClip(modelPath, "Sitting Idle");
        AnimationClip eatClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(eatClipPath);

        if (walkClip == null || sitClip == null || eatClip == null)
        {
            Debug.LogError($"VIP animator setup failed for {modelPath}: missing Walking, Sitting Idle, or eating clip.");
            return null;
        }

        return PopulateController(controllerPath, controllerName, idleClip, walkClip, sitClip, eatClip);
    }

    private static AnimatorController PopulateCustomerController(
        string controllerPath,
        string controllerName,
        AnimationClip idleClip,
        AnimationClip walkClip,
        AnimationClip sitClip,
        AnimationClip eatClip)
    {
        return PopulateController(controllerPath, controllerName, idleClip, walkClip, sitClip, eatClip);
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
        ModelImporter importer = AssetImporter.GetAtPath(CustomerWalkModelPath) as ModelImporter;

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

    private static AnimatorController PopulateController(
        string controllerPath,
        string controllerName,
        AnimationClip idleClip,
        AnimationClip walkClip,
        AnimationClip sitClip,
        AnimationClip eatClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }
        else
        {
            ClearController(controller);
        }

        controller.name = controllerName;
        AddBoolParameter(controller, "IsWalking");
        AddBoolParameter(controller, "IsSitting");

        if (eatClip != null)
            AddBoolParameter(controller, "IsEating");

        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = rootStateMachine.AddState("Idle", new Vector3(300f, 0f, 0f));
        AnimatorState walkState = rootStateMachine.AddState("Walk", new Vector3(300f, 100f, 0f));
        AnimatorState sitState = rootStateMachine.AddState("Sit", new Vector3(300f, 200f, 0f));

        idleState.motion = idleClip;
        walkState.motion = walkClip;
        sitState.motion = sitClip;
        rootStateMachine.defaultState = idleState;

        AddBoolTransition(idleState, walkState, "IsWalking", true);
        AddBoolTransition(walkState, idleState, "IsWalking", false);

        if (eatClip == null)
        {
            AddBoolTransition(idleState, sitState, "IsSitting", true);
            AddBoolTransition(walkState, sitState, "IsSitting", true);
            AddBoolTransition(sitState, walkState, "IsSitting", false, "IsWalking", true);
            AddBoolTransition(sitState, idleState, "IsSitting", false, "IsWalking", false);
        }
        else
        {
            AnimatorState eatState = rootStateMachine.AddState("Eat", new Vector3(300f, 300f, 0f));
            eatState.motion = eatClip;

            AddBoolTransition(idleState, sitState, "IsSitting", true, "IsEating", false);
            AddBoolTransition(walkState, sitState, "IsSitting", true, "IsEating", false);
            AddBoolTransition(idleState, eatState, "IsEating", true);
            AddBoolTransition(walkState, eatState, "IsEating", true);
            AddBoolTransition(sitState, eatState, "IsEating", true);
            AddBoolTransition(sitState, walkState, "IsSitting", false, "IsWalking", true);
            AddBoolTransition(sitState, idleState, "IsSitting", false, "IsWalking", false);
            AddThirdCondition(sitState, walkState, "IsEating", false);
            AddThirdCondition(sitState, idleState, "IsEating", false);
            AddBoolTransition(eatState, sitState, "IsEating", false, "IsSitting", true);
            AddBoolTransition(eatState, walkState, "IsEating", false, "IsWalking", true);
            AddBoolTransition(eatState, idleState, "IsEating", false, "IsSitting", false);
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void ClearController(AnimatorController controller)
    {
        while (controller.parameters.Length > 0)
            controller.RemoveParameter(0);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        while (stateMachine.states.Length > 0)
            stateMachine.RemoveState(stateMachine.states[0].state);
    }

    private static void AddBoolParameter(AnimatorController controller, string name)
    {
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = name,
            type = AnimatorControllerParameterType.Bool,
            defaultBool = false,
        });
    }

    private static void AddBoolTransition(
        AnimatorState source,
        AnimatorState destination,
        string parameterName,
        bool expectedValue)
    {
        AddBoolTransition(source, destination, parameterName, expectedValue, null, false);
    }

    private static void AddBoolTransition(
        AnimatorState source,
        AnimatorState destination,
        string parameterName,
        bool expectedValue,
        string secondParameterName,
        bool secondExpectedValue)
    {
        AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = false;
        transition.duration = 0.12f;
        transition.AddCondition(
            expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
            0f,
            parameterName);

        if (!string.IsNullOrEmpty(secondParameterName))
        {
            transition.AddCondition(
                secondExpectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                secondParameterName);
        }
    }

    private static void AddThirdCondition(
        AnimatorState source,
        AnimatorState destination,
        string parameterName,
        bool expectedValue)
    {
        AnimatorStateTransition[] transitions = source.transitions;

        for (int i = transitions.Length - 1; i >= 0; i--)
        {
            if (transitions[i].destinationState != destination)
                continue;

            transitions[i].AddCondition(
                expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                parameterName);
            return;
        }
    }

    private static bool WireCustomerPrefab(string prefabPath, string modelPath, RuntimeAnimatorController controller)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        if (prefabRoot == null)
        {
            Debug.LogWarning($"Customer animator setup skipped missing prefab: {prefabPath}");
            return false;
        }

        Animator animator = prefabRoot.GetComponent<Animator>();

        if (animator == null)
            animator = prefabRoot.AddComponent<Animator>();

        Avatar avatar = LoadAvatar(modelPath);

        if (avatar == null)
        {
            Debug.LogWarning($"Customer animator setup could not find avatar for {modelPath}");
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            return false;
        }

        animator.runtimeAnimatorController = controller;
        animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        if (prefabRoot.GetComponent<CustomerCharacterAnimator>() == null)
            prefabRoot.AddComponent<CustomerCharacterAnimator>();

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
        return true;
    }
}
#endif
