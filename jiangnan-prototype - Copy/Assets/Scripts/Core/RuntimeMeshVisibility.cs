using UnityEngine;

public static class RuntimeMeshVisibility
{
    public static void PrepareHierarchyForRuntimeMove(Transform root)
    {
        if (root == null)
            return;

        root.gameObject.SetActive(true);
        Prepare(root);

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (child == null)
                continue;

            child.gameObject.SetActive(true);
            Prepare(child);
        }
    }

    public static void Prepare(Transform root)
    {
        if (root == null)
            return;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];

            if (current == null)
                continue;

            GameObject gameObject = current.gameObject;
            gameObject.isStatic = false;

            MeshRenderer[] meshRenderers = gameObject.GetComponents<MeshRenderer>();

            for (int rendererIndex = 0; rendererIndex < meshRenderers.Length; rendererIndex++)
            {
                MeshRenderer meshRenderer = meshRenderers[rendererIndex];

                if (meshRenderer == null)
                    continue;

                meshRenderer.enabled = true;
                meshRenderer.forceRenderingOff = false;
            }

            SkinnedMeshRenderer[] skinnedRenderers = gameObject.GetComponents<SkinnedMeshRenderer>();

            for (int rendererIndex = 0; rendererIndex < skinnedRenderers.Length; rendererIndex++)
            {
                SkinnedMeshRenderer skinnedRenderer = skinnedRenderers[rendererIndex];

                if (skinnedRenderer == null)
                    continue;

                skinnedRenderer.enabled = true;
                skinnedRenderer.forceRenderingOff = false;
                skinnedRenderer.updateWhenOffscreen = true;
            }
        }
    }
}
