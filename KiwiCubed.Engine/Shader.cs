namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using Silk.NET.OpenGL;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.KLogger;

public class Shader : IShader, IDisposable {
    private readonly GL gl;
    private readonly uint id;
    public readonly AssetStringID shaderStringID;

    public Shader(string vertexPath, string fragmentPath) {
        OVERRIDE_LOG_NAME("Shader Program Creation");

        this.gl = SystemsManager.Get<GL>();

        string vertexSource = File.ReadAllText(vertexPath);
        string fragmentSource = File.ReadAllText(fragmentPath);

        if (string.IsNullOrEmpty(vertexSource) || string.IsNullOrEmpty(fragmentSource)) {
            KERR("Shader source at path \"" + vertexSource + "\" or \"" + fragmentSource + "\" is empty or not found");
            return;
        }

        id = CreateShader(vertexSource, fragmentSource, vertexPath, fragmentPath);

        string shaderName;
        string fileName = Path.GetFileName(vertexPath); 
        if (fileName.Contains('_')) {
            shaderName = fileName.Split('_')[0];
        }
        else {
            shaderName = fileName;
        }
        shaderName = shaderName.ToLower();
        shaderStringID = new AssetStringID("kiwicubed", "shader/" + shaderName);

		KINFO("Successfully created shader program with numerical ID {" + id + "} and string ID of \"" + shaderName + "\"");
    }

    private uint CreateShader(string vertexSource, string fragmentSource, string vPath, string fPath) {
		OVERRIDE_LOG_NAME("Shader Program Creation");

		uint program = gl.CreateProgram();

        uint vertex = CompileShader(ShaderType.VertexShader, vertexSource, vPath);
        uint fragment = CompileShader(ShaderType.FragmentShader, fragmentSource, fPath);

        gl.AttachShader(program, vertex);
        gl.AttachShader(program, fragment);
        gl.LinkProgram(program);
        
        gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int status);
        if (status == 0) {
            KERR("Shader Program failed to link with error \"" + gl.GetProgramInfoLog(program) + "\"");
            return 0;
		}

        gl.ValidateProgram(program);
        
        gl.DetachShader(program, vertex);
        gl.DetachShader(program, fragment);
        gl.DeleteShader(vertex);
        gl.DeleteShader(fragment);

        return program;
    }

    private uint CompileShader(ShaderType type, string source, string path) {
		OVERRIDE_LOG_NAME("Shader Program Compilation");

		uint shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);

        gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status == 0) {
            string infoLog = gl.GetShaderInfoLog(shader);
            KERR("Failed to compile " + type + " at path \"" + path + "\" with error \"" + infoLog + "\"");
            return 0;
        }

        return shader;
    }

    public int GetUniformLocation(string name) {
		OVERRIDE_LOG_NAME("Shader");
		int location = gl.GetUniformLocation(id, name);
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

    public void Bind() => gl.UseProgram(id);
    public void Unbind() => gl.UseProgram(0);

    public void Dispose()  {
        gl.DeleteProgram(id);
    }
}