namespace KiwiCubed.Api;

public interface ILogger {
	public static Func<string, ILogger> LoggerCreator;

	public static ILogger CreateLogger(string logName) {
	    return LoggerCreator(logName);
	}

    public static ILogger shared;

	public abstract void DEBUG(string message);
	public abstract void INFO(string message);
	public abstract void WARN(string message);
	public abstract void ERR(string message);
	public abstract void CRITICAL(string message);

	public abstract void LOG_CHECK(bool condition, string success, string error);
	public abstract void LOG_CHECK_BAD(bool condition, string error);
	public abstract int LOG_CHECK_RETURN(bool condition, string success, string error, int returnCode);
	public abstract int LOG_CHECK_RETURN_BAD(bool condition, string error, int returnCode);

	public abstract void BREAK();
}