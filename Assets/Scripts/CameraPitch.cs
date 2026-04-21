using UnityEngine;

public class CameraPitch : MonoBehaviour
{
    public SimpleController_UsingPlayerInput player;

    private void Update()
    {
        if (player == null) return;
        transform.localRotation = Quaternion.Euler(player.Pitch, 0, 0);
    }
}