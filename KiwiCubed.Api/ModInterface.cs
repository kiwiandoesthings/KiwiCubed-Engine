using System.Runtime.CompilerServices;

namespace KiwiCubed.Api;

public abstract class IMod {
	public void DEBUG(string message) => Logger.DEBUG(message);
	public void INFO(string message) => Logger.INFO(message);
	public void WARN(string message) => Logger.WARN(message);
	public void ERR(string message) => Logger.ERR(message);
	public void CRITICAL(string message) => Logger.CRITICAL(message);

	public void LOG_CHECK(bool condition, string success, string error) => Logger.LOG_CHECK(condition, success, error);
	public void LOG_CHECK_BAD(bool condition, string error) => Logger.LOG_CHECK_BAD(condition, error);
	public int LOG_CHECK_RETURN(bool condition, string success, string error, int returnCode) => Logger.LOG_CHECK_RETURN(condition, success, error, returnCode);
	public int LOG_CHECK_RETURN_BAD(bool condition, string error, int returnCode) => Logger.LOG_CHECK_RETURN_BAD(condition, error, returnCode);

	public void OVERRIDE_LOG_NAME(string replacement, [CallerMemberName] string sourceFunction = "Invalid") => Logger.OVERRIDE_LOG_NAME_MOD(replacement, sourceFunction);

	public abstract bool Initialize();
	public abstract void Unload();
}