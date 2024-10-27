using UnityEngine;

public class AdvancedFirstPersonController : MonoBehaviour
{
    public float walkSpeed = 5f;
    public float flySpeed = 10f;
    public float mouseSensitivity = 2f;
    public float jumpForce = 5f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Camera playerCamera;
    private float verticalRotation = 0f;
    private Vector3 velocity;
    private bool isFlying = false;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Mouse look
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Horizontal rotation (around Y-axis)
        transform.Rotate(Vector3.up * mouseX);

        // Vertical rotation (around X-axis)
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -90f, 90f);
        playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);

        // Movement
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        float moveY = 0f;

        // Toggle flying mode
        if (Input.GetKeyDown(KeyCode.F))
        {
            isFlying = !isFlying;
            velocity = Vector3.zero; // Reset velocity when toggling flight mode
        }

        if (isFlying)
        {
            // Flying controls
            if (Input.GetKey(KeyCode.Space))
            {
                moveY = 1f;
            }
            else if (Input.GetKey(KeyCode.LeftShift))
            {
                moveY = -1f;
            }

            Vector3 move = transform.right * moveX + transform.forward * moveZ + transform.up * moveY;
            controller.Move(move * flySpeed * Time.deltaTime);
        }
        else
        {
            // Walking controls
            Vector3 move = transform.right * moveX + transform.forward * moveZ;

            if (controller.isGrounded)
            {
                velocity.y = -2f; // Small downward force when grounded

                if (Input.GetButtonDown("Jump"))
                {
                    velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
                }
            }
            else
            {
                velocity.y += gravity * Time.deltaTime;
            }

            controller.Move(move * walkSpeed * Time.deltaTime);
            controller.Move(velocity * Time.deltaTime);
        }
    }
}

