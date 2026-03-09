namespace KiwiCubed;

using Silk.NET.OpenGL;
using System.Numerics;

using static KiwiCubed.Api.KLogger;

public class Shader : IDisposable {
    private readonly GL gl;
    private readonly uint handle;
    public string shaderName { get; private set; }

    public Shader(GL gl, string vertexPath, string fragmentPath) {
        this.gl = gl;

        string vertexSource = File.ReadAllText(vertexPath);
        string fragmentSource = File.ReadAllText(fragmentPath);

        if (string.IsNullOrEmpty(vertexSource) || string.IsNullOrEmpty(fragmentSource)) {
            throw new Exception("Shader source is empty or file not found.");
        }

        handle = CreateShader(vertexSource, fragmentSource, vertexPath, fragmentPath);
        
        string fileName = Path.GetFileName(vertexPath); 
        if (fileName.Contains('_')) {
            shaderName = fileName.Split('_')[0];
        }
        else {
            shaderName = fileName;
        }
    }

    private uint CreateShader(string vertexSource, string fragmentSource, string vPath, string fPath) {
        uint program = gl.CreateProgram();

        uint vertex = CompileShader(ShaderType.VertexShader, vertexSource, vPath);
        uint fragment = CompileShader(ShaderType.FragmentShader, fragmentSource, fPath);

        gl.AttachShader(program, vertex);
        gl.AttachShader(program, fragment);
        gl.LinkProgram(program);
        
        gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int status);
        if (status == 0)
        {
            throw new Exception($"Program failed to link: {gl.GetProgramInfoLog(program)}");
        }

        gl.ValidateProgram(program);
        
        gl.DetachShader(program, vertex);
        gl.DetachShader(program, fragment);
        gl.DeleteShader(vertex);
        gl.DeleteShader(fragment);

        return program;
    }

    private uint CompileShader(ShaderType type, string source, string path) {
        uint shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);

        gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status == 0) {
            string infoLog = gl.GetShaderInfoLog(shader);
            throw new Exception($"Failed to compile {type} at {path}: {infoLog}");
        }

        return shader;
    }

    public int GetUniformLocation(string name) {
        int location = gl.GetUniformLocation(handle, name);
        if (location == -1) {
            KERR("Tried to get uniform with name \"" + name + "\" that didn't exist");
        }
        return location;
    }
    
    public void SetInt(string name, int value) {
        Bind();
        gl.Uniform1(GetUniformLocation(name), value);
    }

    public void SetUInt(string name, uint value) {
        Bind();
        gl.Uniform1(GetUniformLocation(name), value);
    }

    public void SetFloat(string name, float value) {
        Bind();
        gl.Uniform1(GetUniformLocation(name), value);
    }

    public void SetVector2(string name, Vector2 value) {
        Bind();
        gl.Uniform2(GetUniformLocation(name), value.X, value.Y);
    }

    public void SetVector3(string name, Vector3 value) {
        Bind();
        gl.Uniform3(GetUniformLocation(name), value.X, value.Y, value.Z);
    }

    public void SetVector4(string name, Vector4 value) {
        Bind();
        gl.Uniform4(GetUniformLocation(name), value.X, value.Y, value.Z, value.W);
    }

    public unsafe void SetMatrix4(string name, Matrix4x4 value) {
        Bind();
        gl.UniformMatrix4(GetUniformLocation(name), 1, false, (float*)&value);
    }

    public void Bind() => gl.UseProgram(handle);
    public void Unbind() => gl.UseProgram(0);

    public void Dispose()  {
        gl.DeleteProgram(handle);
    }
}