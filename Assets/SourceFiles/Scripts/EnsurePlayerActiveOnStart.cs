using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using StarterAssets;

public class EnsurePlayerActiveOnStart : MonoBehaviour
{
    private void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        if (!player.activeSelf)
            player.SetActive(true);

#if ENABLE_INPUT_SYSTEM
        var input = player.GetComponentInChildren<PlayerInput>(true);
        if (input != null)
            input.enabled = true;
#endif

        var characterController = player.GetComponent<CharacterController>();
        if (characterController != null && !characterController.enabled)
            characterController.enabled = true;

        var thirdPerson = player.GetComponent<ThirdPersonController>();
        if (thirdPerson != null && !thirdPerson.enabled)
            thirdPerson.enabled = true;
    }
}
