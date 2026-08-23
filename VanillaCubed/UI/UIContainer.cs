namespace VanillaCubed.UI;

using KiwiCubed.Api;
using System.Numerics;

public class UIContainer : UIElement {
    private int padding;

    public UIContainer(Vector2 size, int padding) : base(size) {
        this.padding = padding;
    }

    public override void ArrangeChildren() {
        int totalHeight = 0;
        for (int iterator = 0; iterator < children.Count; iterator++) {
            totalHeight += (int)children[iterator].size.Y;
            totalHeight += padding;
        }
        totalHeight -= padding;

        int currentHeight = ((int)size.Y / 2) - (totalHeight / 2);
        for (int iterator = 0; iterator < children.Count; iterator++) {
            UIElement child = children[iterator];

            Vector2 childPosition = position;
            childPosition.X = position.X + ((size.X / 2) - child.size.X / 2);
            childPosition.Y += currentHeight;
            child.RecalculateElement(childPosition, child.size);
            currentHeight += (int)child.size.Y + padding;
        }
    }

    public override void Render() {
        for (int iterator = 0; iterator < children.Count; iterator++) {
            children[iterator].Render();
        }
    }

    public override void OnClickUp() {
        for (int iterator = 0; iterator < children.Count; ++iterator) {
            UIElement uiElement = children[iterator];
            if (uiElement.GetHovered()) {
                uiElement.OnClickUp();
            }
        }
    }

    public override void OnClickDown() {
        for (int iterator = 0; iterator < children.Count; ++iterator) {
            UIElement uiElement = children[iterator];
            if (uiElement.GetHovered()) {
                uiElement.OnClickDown();
            }
        }
    }
}
