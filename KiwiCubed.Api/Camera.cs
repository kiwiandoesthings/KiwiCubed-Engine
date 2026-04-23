namespace KiwiCubed.Api;

using System.Numerics;

public interface ICamera {
    public void Update(Vector3 position, Quaternion orientation, float fov, Vector2 viewportSize);
    public void SetUniforms(IShader shader);
}