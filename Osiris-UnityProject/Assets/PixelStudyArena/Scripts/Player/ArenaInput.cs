using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Mockup 2 controls (old Input Manager). Keyboard, with basic gamepad support. Movement only.
    ///   Move: A/D or arrows   Jump: Space / W / Up
    ///   Drop through a platform: S/Down + Jump
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
    }
}
