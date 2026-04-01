namespace KiwiCubed.Api;

using System.Runtime.CompilerServices;

public static class Logger {
    private static ILogger logger;

    public static void Initialize(ILogger implementation) => logger = implementation;

    public static void DEBUG(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) => logger.DEBUG(message, sourceFunction, sourceFile, sourceLine);
	public static void INFO(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) => logger.INFO(message, sourceFunction, sourceFile, sourceLine);
	public static void WARN(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) => logger.WARN(message, sourceFunction, sourceFile, sourceLine);
	public static void ERR(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) => logger.ERR(message, sourceFunction, sourceFile, sourceLine);
	public static void CRITICAL(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) => logger.CRITICAL(message, sourceFunction, sourceFile, sourceLine);
	
	public static void LOG_CHECK(bool condition, string success, string error) => logger.LOG_CHECK(condition, success, error);
	public static void LOG_CHECK_BAD(bool condition, string error) => logger.LOG_CHECK_BAD(condition, error);
	public static int LOG_CHECK_RETURN(bool condition, string success, string error, int returnCode) => logger.LOG_CHECK_RETURN(condition, success, error, returnCode);
	public static int LOG_CHECK_RETURN_BAD(bool condition, string error, int returnCode) => logger.LOG_CHECK_RETURN_BAD(condition, error, returnCode);
	
	public static void OVERRIDE_LOG_NAME_MOD(string replacement, [CallerMemberName] string sourceFunction = "Invalid") => logger.OVERRIDE_LOG_NAME_MOD(replacement, sourceFunction);
}

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