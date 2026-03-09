using System.Runtime.CompilerServices;

namespace KiwiCubed.Api;

public abstract class IMod {
	public static ILogger logger;
	public void DEBUG(string message) => logger.DEBUG(message);
	public void INFO(string message) => logger.INFO(message);
	public void WARN(string message) => logger.WARN(message);
	public void ERR(string message) => logger.ERR(message);
	public void CRITICAL(string message) => logger.CRITICAL(message);

	public void LOG_CHECK(bool condition, string success, string error) => logger.LOG_CHECK(condition, success, error);
	public void LOG_CHECK_BAD(bool condition, string error) => logger.LOG_CHECK_BAD(condition, error);
	public int LOG_CHECK_RETURN(bool condition, string success, string error, int returnCode) => logger.LOG_CHECK_RETURN(condition, success, error, returnCode);
	public int LOG_CHECK_RETURN_BAD(bool condition, string error, int returnCode) => logger.LOG_CHECK_RETURN_BAD(condition, error, returnCode);

	public void OVERRIDE_LOG_NAME(string replacement, [CallerMemberName] string sourceFunction = "Invalid") => logger.OVERRIDE_LOG_NAME_MOD(replacement, sourceFunction);

	public abstract void Initialize();
	public abstract void Unload();
}