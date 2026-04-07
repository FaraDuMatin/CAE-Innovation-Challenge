using UnityEngine;
using UnityEngine.InputSystem;

public class MouseCameraLook : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private bool invertY;

    [Header("Cursor")]
    [SerializeField] private bool lockCursorWhileRotating = true;

    private float yaw;
    private float pitch;

    private void Awake()
    {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = NormalizeAngle(euler.x);
    }

    private void Update()
    {
        bool rotating = IsRightMousePressed();

        if (!rotating)
        {
            ReleaseCursorIfNeeded();
            return;
        }

        LockCursorIfNeeded();

        Vector2 mouseDelta = ReadMouseDelta();
        float mouseX = mouseDelta.x * mouseSensitivity;
        float mouseY = mouseDelta.y * mouseSensitivity;

        yaw += mouseX;
        pitch += invertY ? mouseY : -mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private bool IsRightMousePressed()
    {
        return Mouse.current != null && Mouse.current.rightButton.isPressed;
    }

    private Vector2 ReadMouseDelta()
    {
        return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
    }

    private void OnDisable()
    {
        ReleaseCursorIfNeeded();
    }

    private void LockCursorIfNeeded()
    {
        if (!lockCursorWhileRotating)
        {
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ReleaseCursorIfNeeded()
    {
        if (!lockCursorWhileRotating)
        {
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        while (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }
}
