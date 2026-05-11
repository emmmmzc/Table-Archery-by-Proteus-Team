using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MotorHotkeys : MonoBehaviour
{
    public MotorController motor;

    [Header("Raw Hex Commands")]
    public string keyAHex = "640002AA00000000000000000000000000000000000000000000000000BD0D0A";
    public string keyBHex = "64000100012C01F4000000000000000000000000000000000000000000660D0A";
    public string keyCHex = "64000102012C07D0641E00000000000000000000000000000000000000470D0A";
    public string keyDHex = "6400025500000000000000000000000000000000000000000000000000590D0A";
    public string keyEHex = "6400A00105050000000000000000000000000000000000000000000000C50D0A";

    void Update()
    {
        if (motor == null)
            return;

        if (IsKeyDown(KeyCode.A, Key.A))
            motor.SendRawHex(keyAHex);
        if (IsKeyDown(KeyCode.B, Key.B))
            motor.SendRawHex(keyBHex);
        if (IsKeyDown(KeyCode.C, Key.C))
            motor.SendRawHex(keyCHex);
        if (IsKeyDown(KeyCode.D, Key.D))
            motor.SendRawHex(keyDHex);
        if (IsKeyDown(KeyCode.E, Key.E))
            motor.SendRawHex(keyEHex);
    }

    private static bool IsKeyDown(KeyCode legacyKey, Key newKey)
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current[newKey].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(legacyKey);
#else
        return false;
#endif
    }
}
