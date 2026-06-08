#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Wires Steel Sentinel GLBs from <c>Assets/SourceFiles/BadGuy/</c> into the
/// <c>BadGuy_GroundPatrol</c> prefab. Requires <c>com.unity.cloud.gltfast</c> so .glb imports as models.
/// </summary>
public static class BadGuyIntegrator
{
    const string PrefabPath = "Assets/Prefabs/BadGuy_GroundPatrol.prefab";
    const string CharacterGlb = "Assets/SourceFiles/BadGuy/Meshy_AI_Steel_Sentinel_biped_Character_output.glb";
    const string AnimGlb = "Assets/SourceFiles/BadGuy/Meshy_AI_Steel_Sentinel_biped_Meshy_AI_Meshy_Merged_Animations.glb";
    const string ControllerPath = "Assets/SourceFiles/BadGuy/BadGuy_Patrol.controller";
    const string VisualChildName = "SteelSentinel_Visual";
    const float TargetVisualHeight = 2f;

    [MenuItem("Tools/Bad Guy/Repair animator controller + avatar (keeps prefab layout)")]
    public static void RepairControllerAndAvatar()
    {
        if (!File.Exists(AnimGlb) || !File.Exists(CharacterGlb))
        {
            EditorUtility.DisplayDialog("Bad Guy", "Missing GLB under Assets/SourceFiles/BadGuy/.", "OK");
            return;
        }

        EnsureGlbMecanimAnimationImport(CharacterGlb);
        EnsureGlbMecanimAnimationImport(AnimGlb);
        AssetDatabase.ImportAsset(CharacterGlb, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(AnimGlb, ImportAssetOptions.ForceUpdate);

        var clips = AssetDatabase.LoadAllAssetsAtPath(AnimGlb)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__", StringComparison.Ordinal) && !c.legacy)
            .ToArray();

        if (clips.Length == 0)
        {
            EditorUtility.DisplayDialog("Bad Guy", "No Mecanim clips on the animations .glb. Set Animation import method to Mecanim, Apply, retry.", "OK");
            return;
        }

        var controller = BuildOrUpdateController(clips);

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var visual = root.transform.Find(VisualChildName);
            if (visual == null)
            {
                EditorUtility.DisplayDialog("Bad Guy", $"Child '{VisualChildName}' not found. Run full Integrate first.", "OK");
                return;
            }

            var anim = visual.GetComponent<Animator>();
            if (anim == null)
                anim = visual.gameObject.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            TryAssignAvatarFromCharacterGlb(anim);
            EnsureCharacterController(root);
            ApplyWalkClipToGroundPatrol(root, PickWalkClip(clips));

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Bad Guy", "Animator Controller, Avatar, CharacterController, walk clip on GroundPatrolHazard (Playables), and culling updated.", "OK");
    }

    [MenuItem("Tools/Bad Guy/Integrate Steel Sentinel into BadGuy_GroundPatrol")]
    public static void Integrate()
    {
        if (!File.Exists(CharacterGlb) || !File.Exists(AnimGlb))
        {
            EditorUtility.DisplayDialog("Bad Guy Integrator", "Missing GLB under Assets/SourceFiles/BadGuy/.", "OK");
            return;
        }

        // glTFast defaults ImportSettings.animationMethod to Legacy; legacy clips cannot be used in Animator Controllers.
        EnsureGlbMecanimAnimationImport(CharacterGlb);
        EnsureGlbMecanimAnimationImport(AnimGlb);

        AssetDatabase.ImportAsset(CharacterGlb, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(AnimGlb, ImportAssetOptions.ForceUpdate);

        var mainGo = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterGlb);
        if (mainGo == null)
        {
            EditorUtility.DisplayDialog(
                "Bad Guy Integrator",
                "Could not load the character .glb as a GameObject.\n\n" +
                "Install com.unity.cloud.gltfast (see Packages/manifest.json), wait for import to finish, then run this again.",
                "OK");
            return;
        }

        var clips = AssetDatabase.LoadAllAssetsAtPath(AnimGlb)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__", StringComparison.Ordinal) && !c.legacy)
            .ToArray();

        if (clips.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Bad Guy Integrator",
                "No non-legacy animation clips found in the merged animations .glb.\n\n" +
                "Select that asset in the Project window → Inspector → set Animation → Animation import method to **Mecanim**, then Apply, and run this again.",
                "OK");
            return;
        }

        var controller = BuildOrUpdateController(clips);
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Bad Guy Integrator", "Failed to build Animator Controller.", "OK");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            StripCapsuleVisual(root);

            var existing = root.transform.Find(VisualChildName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(mainGo, root.transform);
            instance.name = VisualChildName;
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            ScaleToApproximateHeight(instance, TargetVisualHeight);

            var anim = instance.GetComponent<Animator>();
            if (anim == null)
                anim = instance.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            TryAssignAvatarFromCharacterGlb(anim);

            EnsureCharacterController(root);

            ApplyWalkClipToGroundPatrol(root, PickWalkClip(clips));

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Bad Guy Integrator", "Steel Sentinel is wired into BadGuy_GroundPatrol (walk clip assigned for Playables playback).", "OK");
    }

    static AnimationClip PickWalkClip(AnimationClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        return clips.FirstOrDefault(c => c.name.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0)
               ?? clips.FirstOrDefault(c => c.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0)
               ?? clips[0];
    }

    static void ApplyWalkClipToGroundPatrol(GameObject root, AnimationClip clip)
    {
        if (clip == null || root == null)
            return;

        var hazard = root.GetComponent<GroundPatrolHazard>();
        if (hazard == null)
            return;

        var so = new SerializedObject(hazard);
        var p = so.FindProperty("walkClip");
        if (p != null)
        {
            p.objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void TryAssignAvatarFromCharacterGlb(Animator anim)
    {
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(CharacterGlb))
        {
            if (obj is Avatar avatar && avatar.isValid)
            {
                anim.avatar = avatar;
                anim.Rebind();
                return;
            }
        }
    }

    /// <summary>
    /// Forces GLTFast ScriptedImporter to use Mecanim clips (not Legacy) so clips work in an Animator Controller.
    /// </summary>
    static void EnsureGlbMecanimAnimationImport(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null)
            return;

        var so = new SerializedObject(importer);
        var importSettings = so.FindProperty("importSettings");
        if (importSettings == null)
            return;

        var animationMethod = importSettings.FindPropertyRelative("animationMethod");
        if (animationMethod == null)
            return;

        // GLTFast.AnimationMethod: None=0, Legacy=1, Mecanim=2
        const int mecanim = 2;
        if (animationMethod.intValue == mecanim)
            return;

        animationMethod.intValue = mecanim;
        so.ApplyModifiedPropertiesWithoutUndo();
        importer.SaveAndReimport();
    }

    static void StripCapsuleVisual(GameObject root)
    {
        var mf = root.GetComponent<MeshFilter>();
        if (mf != null)
            UnityEngine.Object.DestroyImmediate(mf);
        var mr = root.GetComponent<MeshRenderer>();
        if (mr != null)
            UnityEngine.Object.DestroyImmediate(mr);
    }

    /// <summary>
    /// Solid capsule collision for <see cref="GroundPatrolHazard"/> (replaces any legacy trigger <see cref="CapsuleCollider"/>).
    /// </summary>
    static void EnsureCharacterController(GameObject root)
    {
        var oldCap = root.GetComponent<CapsuleCollider>();
        if (oldCap != null)
            UnityEngine.Object.DestroyImmediate(oldCap);

        var cc = root.GetComponent<CharacterController>();
        if (cc == null)
            cc = root.AddComponent<CharacterController>();

        cc.center = new Vector3(0f, 1f, 0f);
        cc.height = 2f;
        cc.radius = 0.45f;
        cc.skinWidth = 0.08f;
        cc.stepOffset = 0.4f;
        cc.minMoveDistance = 0f;
    }

    static void ScaleToApproximateHeight(GameObject visualRoot, float targetWorldHeight)
    {
        var smrs = visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
        if (smrs == null || smrs.Length == 0)
            return;

        visualRoot.transform.localScale = Vector3.one;

        var b = smrs[0].bounds;
        foreach (var smr in smrs.Skip(1))
            b.Encapsulate(smr.bounds);

        var h = b.size.y;
        if (h < 1e-4f)
            return;

        var factor = targetWorldHeight / h;
        visualRoot.transform.localScale = Vector3.one * factor;
    }

    static AnimatorController BuildOrUpdateController(AnimationClip[] clips)
    {
        if (clips == null || clips.Length == 0)
        {
            if (File.Exists(ControllerPath))
                return AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            return AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        var chosen = PickWalkClip(clips);
        if (chosen == null)
            return null;

        TrySetLoopTime(chosen, loop: true);

        // Do not DeleteAsset the controller — that regenerates .meta GUID and breaks prefab references.
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ac == null)
            ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        var sm = ac.layers[0].stateMachine;
        while (sm.states.Length > 0)
            sm.RemoveState(sm.states[0].state);

        var st = sm.AddState(chosen.name);
        st.motion = chosen;
        sm.defaultState = st;

        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssetIfDirty(ac);

        if (sm.defaultState != null && sm.defaultState.motion == null)
        {
            sm.defaultState.motion = chosen;
            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssetIfDirty(ac);
        }

        return ac;
    }

    static void TrySetLoopTime(AnimationClip clip, bool loop)
    {
        if (clip == null)
            return;
        var so = new SerializedObject(clip);
        var p = so.FindProperty("m_AnimationClipSettings");
        if (p == null)
            return;
        var loopProp = p.FindPropertyRelative("m_LoopTime");
        if (loopProp != null)
        {
            loopProp.boolValue = loop;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
