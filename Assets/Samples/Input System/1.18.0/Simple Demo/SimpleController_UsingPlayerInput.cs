using UnityEngine;
using UnityEngine.InputSystem;

public class SimpleController_UsingPlayerInput : MonoBehaviour
{
    public float moveSpeed   = 5f;
    public float rotateSpeed = 0.1f;
public float Pitch => m_Rotation.x;

    private Vector2 m_Rotation;
    private Vector2 m_Move;
    private Vector2 m_Look;

    public void OnMove(InputAction.CallbackContext context)
    {
        m_Move = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        m_Look = context.ReadValue<Vector2>();
    }

    public void Update()
    {
        Look(m_Look);
        Move(m_Move);
    }

    private void Move(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.01)
            return;
        var scaledMoveSpeed = moveSpeed * Time.deltaTime;
        var move = Quaternion.Euler(0, transform.eulerAngles.y, 0)
                   * new Vector3(direction.x, 0, direction.y);
        transform.position += move * scaledMoveSpeed;
    }

private void Look(Vector2 rotate)
{
    if (rotate.sqrMagnitude < 0.01)
        return;

    m_Rotation.y += rotate.x * rotateSpeed;
    m_Rotation.x  = Mathf.Clamp(m_Rotation.x - rotate.y * rotateSpeed, -89, 89);

    // apply Y rotation to the root transform, X rotation to camera only
    transform.localRotation = Quaternion.Euler(0, m_Rotation.y, 0);
}

}