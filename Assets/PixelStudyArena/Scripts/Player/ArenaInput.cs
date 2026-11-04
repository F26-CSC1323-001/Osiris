using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Arena controls (old Input Manager). Keyboard + mouse, with basic gamepad support.
    ///   Move: A/D or arrows   Jump: Space / W / Up (press again in the air to double jump)
    ///   Drop through a platform: S/Down + Jump
    ///   Attack: J or Left Mouse (tap = slash, hold = heavy stab)
    ///   Dodge roll: Left Shift or L (no direction held = backstep)     Shield: K or Right Mouse (hold)
    ///   Heal (crimson flask): R
    ///   Skills: Q Z X C V E F   Ultimate (Heavenfall, when its gauge is full): G
    /// </summary>
    public static class ArenaInput
    {
        public static float Move
        {
            get
            {
                float x = 0f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
                if (x == 0f)
                {
                    float axis = Input.GetAxisRaw("Horizontal");
                    if (Mathf.Abs(axis) > 0.3f) x = Mathf.Sign(axis);
                }
                return x;
            }
        }

        public static bool Down =>
            Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || Input.GetAxisRaw("Vertical") < -0.5f;

        public static bool JumpDown =>
            Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) ||
            Input.GetKeyDown(KeyCode.JoystickButton0);

        public static bool JumpHeld =>
            Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ||
            Input.GetKey(KeyCode.JoystickButton0);

        public static bool AttackDown =>
            Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.JoystickButton2);

        public static bool AttackHeld =>
            Input.GetKey(KeyCode.J) || Input.GetMouseButton(0) || Input.GetKey(KeyCode.JoystickButton2);

        public static bool DodgeDown =>
            Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.JoystickButton1);

        public static bool FlaskDown =>
            Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.JoystickButton6);

        public static bool BlockHeld =>
            Input.GetKey(KeyCode.K) || Input.GetMouseButton(1) || Input.GetKey(KeyCode.JoystickButton4) || Input.GetKey(KeyCode.JoystickButton5);
    }
}
