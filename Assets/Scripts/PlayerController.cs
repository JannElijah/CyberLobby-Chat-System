using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 15f;

    private CharacterController characterController;
    private Animator animator;

    // Input actions defined programmatically for plug-and-play
    private InputAction moveAction;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        // Create the move action programmatically
        moveAction = new InputAction("Move", InputActionType.Value);
        // Bind WASD
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        // Bind Gamepad Left Stick
        moveAction.AddBinding("<Gamepad>/leftStick");
    }

    public override void OnNetworkSpawn()
    {
        // If we own this player, bump them up slightly into the air when they spawn.
        // This prevents them from spawning inside the floor geometry and falling through!
        if (IsOwner)
        {
            characterController.enabled = false; // Must disable CharacterController to manually change position
            transform.position = new Vector3(transform.position.x, transform.position.y + 10f, transform.position.z);
            characterController.enabled = true;
        }
    }

    private void OnEnable()
    {
        moveAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
    }

    private void Update()
    {
        // Only the client that owns this player can control it
        if (!IsOwner) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        // Don't process new movement if the player is typing in chat (or focusing any UI)
        bool isTyping = UnityEngine.EventSystems.EventSystem.current != null && 
                        UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null;

        // Read input from the new Input System only if not typing
        Vector2 moveInput = isTyping ? Vector2.zero : moveAction.ReadValue<Vector2>();
        Vector3 moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);

        // Apply gravity if not grounded (simple gravity for CharacterController)
        Vector3 velocity = moveDirection * moveSpeed;
        if (!characterController.isGrounded)
        {
            velocity.y -= 9.81f; 
        }

        // Move the character
        characterController.Move(velocity * Time.deltaTime);

        // Handle rapid rotation to face movement direction
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        // Update the Animator "Speed" parameter
        // Using the input magnitude is much more reliable than characterController.velocity for top-down games!
        animator.SetFloat("Speed", moveInput.magnitude);
    }
}
