using UnityEngine;
using Unity.Cinemachine;

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
    public class RespawnPlayer : MonoBehaviour
    {
        [Tooltip("The Y position threshold at which the player will respawn.")]
        public float yThreshold = -5f; 

        private Vector3 _startingPosition;

        private Quaternion _startingRotation;

        private CharacterController _characterController;

        public CinemachineCamera vCam;

        private ThirdPersonController _thirdPersonController;
        public AudioClip respawnSound;

        [Tooltip("Optional. When set, losing a life from a void fall calls TakeDamage on this amount (default 1). Set to 0 to never lose hearts on respawn.")]
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] int healthLossOnFall = 1;


        private void Start()
{
    // Save the starting position and rotation
    _startingPosition = transform.position;
    _startingRotation = transform.rotation;

    // Get the CharacterController reference
    _characterController = GetComponent<CharacterController>();
    if (_characterController == null)
    {
        Debug.LogError("CharacterController component is required for RespawnPlayer script!");
    }

    // Get ThirdPersonController reference
    _thirdPersonController = GetComponent<ThirdPersonController>();
    if (_thirdPersonController == null)
    {
        Debug.LogError("ThirdPersonController component is required for RespawnPlayer!");
    }
}

        private void Update()
        {
            // Check if the player's Y position has fallen below the threshold
            if (transform.position.y < yThreshold)
            {
                Respawn();
            }
        }

        private void Respawn()
{
    bool hadHealthBeforeDamage =
        playerHealth == null || playerHealth.CurrentHealth > 0;

    if (playerHealth != null && healthLossOnFall > 0)
        playerHealth.TakeDamage(healthLossOnFall);

    // Avoid moving / playing SFX every frame while stuck below threshold with 0 HP (game over).
    if (playerHealth != null && playerHealth.CurrentHealth <= 0 && !hadHealthBeforeDamage)
        return;

    // Disable the CharacterController so we can manually adjust position
    if (_characterController != null)
    {
        _characterController.enabled = false; // Disable to reset position/rotation correctly
    }

    // Reset the player's position and rotation
    transform.position = _startingPosition;
    transform.rotation = Quaternion.Euler(0f, 90f, 0f); // Reset player Y rotation to 90 degrees

    // Reset the CharacterController's vertical velocity to ensure the robot doesn't keep falling
    if (_characterController != null)
    {
        _characterController.enabled = true; // Enable it back after resetting position
        ResetVerticalVelocity();
    }

    // Reset the camera's rotation
    ThirdPersonController thirdPersonController = GetComponent<ThirdPersonController>();
    if (thirdPersonController != null)
    {
        thirdPersonController.ResetCameraRotation(90f); // Reset camera's Y rotation to 90 degrees
    }

    AudioSource.PlayClipAtPoint(respawnSound, transform.position);

}

        private void ResetVerticalVelocity()
        {
            // Ensures no residual vertical velocity after respawning
            if (TryGetComponent<ThirdPersonController>(out ThirdPersonController controller))
            {
                // Access the private _verticalVelocity via the public interface, if exposed
                var verticalVelocityField = typeof(ThirdPersonController).GetField("_verticalVelocity",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (verticalVelocityField != null)
                {
                    verticalVelocityField.SetValue(controller, 0f);
                }
            }
        }
    }
}
