using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Forces Ground mesh material from the scene that owns this GameObject (not the globally active scene),
/// so Level 1 vs Level 2 stay distinct in Play mode and when multiple scenes are open in the Editor.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
[DefaultExecutionOrder(-100)]
public class GroundMaterialBySceneName : MonoBehaviour
{
    [SerializeField] private Material materialForLevel1;
    [SerializeField] private Material materialForLevel2;

    private void Awake()
    {
        ApplyMaterialForOwnerScene();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        ApplyMaterialForOwnerScene();
    }
#endif

    private void ApplyMaterialForOwnerScene()
    {
        var renderer = GetComponent<MeshRenderer>();
        if (renderer == null)
            return;

        var ownerScene = gameObject.scene;
        if (!ownerScene.IsValid() || string.IsNullOrEmpty(ownerScene.name))
            return;

        var sceneName = ownerScene.name;
        if (sceneName == "Level1_Scene" && materialForLevel1 != null)
            renderer.sharedMaterial = materialForLevel1;
        else if (sceneName == "Level2_Scene" && materialForLevel2 != null)
            renderer.sharedMaterial = materialForLevel2;
    }
}
