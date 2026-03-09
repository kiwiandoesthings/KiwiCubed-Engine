namespace KiwiCubed.Api;

using System.Runtime.CompilerServices;

public interface ILogger {
	public abstract void DEBUG(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1);
	public abstract void INFO(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1);
	public abstract void WARN(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1);
	public abstract void ERR(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1);
	public abstract void CRITICAL(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1);

	public abstract void LOG_CHECK(bool condition, string success, string error);
	public abstract void LOG_CHECK_BAD(bool condition, string error);
	public abstract int LOG_CHECK_RETURN(bool condition, string success, string error, int returnCode);
	public abstract int LOG_CHECK_RETURN_BAD(bool condition, string error, int returnCode);

	public abstract void OVERRIDE_LOG_NAME_MOD(string replacement, [CallerMemberName] string sourceFunction = "Invalid");
}