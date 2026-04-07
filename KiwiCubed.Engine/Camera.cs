namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using System.Numerics;

public class Camera : ICamera {
    private Matrix4x4 viewMatrix;
    private Matrix4x4 projectionMatrix;

    public void Update(Vector3 position, Vector3 orientation, float fov, Vector2 viewportSize) {
        float fovRadians = fov * (MathF.PI / 180.0f);
        viewMatrix = System.Numerics.Matrix4x4.CreateLookAt(position, position + orientation, Vector3.UnitY);
        projectionMatrix = System.Numerics.Matrix4x4.CreatePerspectiveFieldOfView(fovRadians, viewportSize.X / viewportSize.Y, 0.1f, 1000.0f);
    }

    public void SetUniforms(IShader shader) {
        shader.SetMatrix4("viewMatrix", viewMatrix);
        shader.SetMatrix4("projectionMatrix", projectionMatrix);
    }
}