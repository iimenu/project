using UnityEngine;
using UnityEngine.XR;

namespace iiMenu.CanvasMenuUI.Input
{
    internal static class GorillaTriggerBridge
    {
        internal const float PressThreshold = 0.5f;

        internal static void Sample(out float left, out float right)
        {
            left = ControllerInputPoller.TriggerFloat(XRNode.LeftHand);
            right = ControllerInputPoller.TriggerFloat(XRNode.RightHand);
        }

        internal static bool IsPressed(float v) => v > PressThreshold;
    }
}
