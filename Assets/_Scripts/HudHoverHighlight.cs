using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Shared HUD hover outline. Buttons work automatically; add class="hud-hover"
/// and picking-mode="Position" only to other clickable or draggable HUD elements.
/// </summary>
public static class HudHoverHighlight
{
    public static void Install(VisualElement root)
    {
        if (root == null || root.Q<VisualElement>("hud-hover-outline") != null) return;

        VisualElement outline = new VisualElement { name = "hud-hover-outline", pickingMode = PickingMode.Ignore };
        outline.style.position = Position.Absolute;
        outline.style.display = DisplayStyle.None;
        outline.style.borderLeftWidth = outline.style.borderRightWidth = 2f;
        outline.style.borderTopWidth = outline.style.borderBottomWidth = 2f;
        Color color = new Color(0.45f, 0.9f, 1f, 1f);
        outline.style.borderLeftColor = outline.style.borderRightColor = color;
        outline.style.borderTopColor = outline.style.borderBottomColor = color;
        root.Add(outline);

        Vector2 pointerPosition = Vector2.zero;
        bool pointerInside = false;
        void Refresh()
        {
            VisualElement target = pointerInside && root.panel != null ? root.panel.Pick(pointerPosition) : null;
            while (target != null && target != root && !(target is Button) && !target.ClassListContains("hud-hover"))
                target = target.parent;

            if (target == null || target == root || !target.enabledInHierarchy || !root.Contains(target))
            {
                outline.style.display = DisplayStyle.None;
                return;
            }

            Rect bounds = root.WorldToLocal(target.worldBound);
            const float gap = 3f;
            outline.style.left = bounds.xMin - gap;
            outline.style.top = bounds.yMin - gap;
            outline.style.width = bounds.width + gap * 2;
            outline.style.height = bounds.height + gap * 2;
            outline.style.borderTopLeftRadius = target.resolvedStyle.borderTopLeftRadius + gap;
            outline.style.borderTopRightRadius = target.resolvedStyle.borderTopRightRadius + gap;
            outline.style.borderBottomLeftRadius = target.resolvedStyle.borderBottomLeftRadius + gap;
            outline.style.borderBottomRightRadius = target.resolvedStyle.borderBottomRightRadius + gap;
            outline.style.display = DisplayStyle.Flex;
            outline.BringToFront();
        }

        root.RegisterCallback<PointerMoveEvent>(evt =>
        {
            pointerPosition = evt.position;
            pointerInside = true;
            Refresh();
        }, TrickleDown.TrickleDown);
        root.RegisterCallback<PointerLeaveEvent>(evt =>
        {
            if (evt.target != root) return;
            pointerInside = false;
            outline.style.display = DisplayStyle.None;
        });
        // Re-pick while stationary so rerolls, hidden panels, and disabled buttons clear correctly.
        root.schedule.Execute(Refresh).Every(33);
    }
}
