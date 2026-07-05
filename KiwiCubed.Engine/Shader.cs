namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using Silk.NET.OpenGL;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;

public class Shader : IShader, IDisposable {
    private static KLogger logger;
    private static GL gl;
    private readonly uint id;
    public readonly AssetStringID shaderStringID;

    public static void SetupShaderResources() {
        logger = new KLogger("Shader");
        gl = MetaHandler.Get<GL>();
    }

    public Shader(AssetStringID shaderStringID, string[] shaderPaths, ShaderType[] shaderTypes) {
        this.shaderStringID = shaderStringID;

        string[] shaderSources = new string[shaderPaths.Length];
        for (int iterator = 0; iterator < shaderPaths.Length; iterator++) {
            string shaderSource = File.ReadAllText(shaderPaths[iterator]);
            if (string.IsNullOrEmpty(shaderSource)) {
                logger.ERR("Shader source at path \"" + shaderPaths[iterator] + "\" is empty or not found");
                logger.BREAK();
            }
            shaderSources[iterator] = shaderSource;
        }

        id = CreateShader(shaderSources, shaderPaths, shaderTypes);

		logger.INFO("Successfully created shader program with numerical ID {" + id + "} and string ID of " + shaderStringID);
    }

    private uint CreateShader(string[] shaderSources, string[] shaderPaths, ShaderType[] shaderTypes) {
		uint program = gl.CreateProgram();

        uint[] shaderIDs = new uint[shaderSources.Length];
        for (int iterator = 0; iterator < shaderSources.Length; iterator++) {
            uint shaderID = CompileShader(shaderTypes[iterator], shaderSources[iterator], shaderPaths[iterator]);
            shaderIDs[iterator] = shaderID;
            gl.AttachShader(program, shaderID);
        }

        gl.LinkProgram(program);
        
        gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int status);
        if (status == 0) {
            logger.ERR("Shader Program failed to link with error \"" + gl.GetProgramInfoLog(program) + "\"");
            logger.BREAK();
		}

        gl.ValidateProgram(program);
        
        for (int iterator = 0; iterator < shaderIDs.Length; iterator++) {
            gl.DetachShader(program, shaderIDs[iterator]);
            gl.DeleteShader(shaderIDs[iterator]);
        }

        return program;
    }

    private uint CompileShader(ShaderType type, string source, string path) {
		uint shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);

        gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status == 0) {
            string infoLog = gl.GetShaderInfoLog(shader);
            logger.ERR("Failed to compile " + type + " at path \"" + path + "\" with error \"" + infoLog + "\"");
            logger.BREAK();
        }

        return shader;
    }

    public int GetUniformLocation(string name) {
		int location = gl.GetUniformLocation(id, name);
        if (location == -1) {
            logger.ERR("Tried to get uniform with name \"" + name + "\" that didn't exist");
            logger.BREAK();
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