namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using System.Numerics;

public class Camera : ICamera {
    private Matrix4x4 viewMatrix;
    private Matrix4x4 projectionMatrix;

    public void Update(Vector3 position, Quaternion orientation, float fov, Vector2 viewportSize) {
        float fovRadians = fov * (MathF.PI / 180.0f);

        Vector3 forward = Vector3.Transform(new Vector3(0, 0, -1), orientation);
        Vector3 up = Vector3.Transform(Vector3.UnitY, orientation);
        viewMatrix = Matrix4x4.CreateLookAt(position, position + forward, up);
        projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(fovRadians, viewportSize.X / viewportSize.Y, 0.1f, 1000.0f);
    }

    public void SetUniforms(IShader shader) {
        shader.SetMatrix4("viewMatrix", viewMatrix);
        shader.SetMatrix4("projectionMatrix", projectionMatrix);
    }
}