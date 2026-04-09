namespace KiwiCubed.Api;

using KiwiCubed.Engine;
using System;
using System.Runtime.CompilerServices;
using System.Xml;
using static KiwiCubed.Api.Globals;

public class KLoggerWrapper : ILogger {
	public void DEBUG(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) {
		KLogger.KDEBUG(message, sourceFunction, sourceFile, sourceLine);
	}
	public void INFO(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) {
		KLogger.KINFO(message, sourceFunction, sourceFile, sourceLine);
	}
	public void WARN(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) {
		KLogger.KWARN(message, sourceFunction, sourceFile, sourceLine);
	}
	public void ERR(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) {
		KLogger.KERR(message, sourceFunction, sourceFile, sourceLine);
	}
	public void CRITICAL(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) {
		KLogger.KCRITICAL(message, sourceFunction, sourceFile, sourceLine);
	}
	public void OVERRIDE_LOG_NAME_MOD(string replacement, [CallerMemberName] string sourceFunction = "Invalid") {
		KLogger.OVERRIDE_LOG_NAME(replacement, sourceFunction);
    }
	public void LOG_CHECK(bool condition, string success, string error) {
		KLogger.KLOG_CHECK(condition, success, error);
    }
	public void LOG_CHECK_BAD(bool condition, string error) {
		KLogger.KLOG_CHECK_BAD(condition, error);
    }
	public int LOG_CHECK_RETURN(bool condition, string success, string error, int returnCode) {
		return KLogger.KLOG_CHECK_RETURN(condition, success, error, returnCode);
    }
	public int LOG_CHECK_RETURN_BAD(bool condition, string error, int returnCode) {
		return KLogger.KLOG_CHECK_RETURN_BAD(condition, error, returnCode);
    }
	public void BREAK() {
		KLogger.KBREAK();
	}
}

public class KLogger {
	private static string headerStructure = "[{level} | {function}]";
	public static readonly Dictionary<string, string> logColors = new() {
		{"DEBUG", "\u001b[1;34m"},
		{"INFO", "\u001b[1;32m"},
		{"EXTERNAL", "\u001b[1;38;5;226m"},
		{"WARN", "\u001b[1;38;5;214m"},
		{"ERROR", "\u001b[1;31m"},
		{"CRITICAL", "\u001b[1;41m"},
		{"RESET", "\u001b[0m"},
		{"SRCLOC", "\u001b[1;30m"},
		{"FUNCTION", "\u001b[1;35m"}
	};
	public static ThreadLocal<Dictionary<string, string>> functionHeaderReplacements = new ThreadLocal<Dictionary<string, string>>(() => new Dictionary<string, string>());
    public Dictionary<string, string> modFunctionHeaderReplacements = new();

	public static void OVERRIDE_LOG_NAME(string replacement, [CallerMemberName] string sourceFunction = "Invalid") {
		if (sourceFunction == "Invalid") {
			return;
		}

        functionHeaderReplacements.Value[sourceFunction] = replacement;
	}

	public void OVERRIDE_LOG_NAME_MOD(string replacement, [CallerMemberName] string sourceFunction = "Invalid") {
		if (sourceFunction == "Invalid") {
			return;
		}

		modFunctionHeaderReplacements[sourceFunction] = replacement;
	}

	// Called from mods
	public void DEBUG(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) {
		WriteMessage(LogLevel.Debug, message, true, sourceFunction, sourceFile, sourceLine.ToString(), true);
	}
	public void INFO(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) {
		WriteMessage(LogLevel.Info, message, false, sourceFunction, sourceFile, sourceLine.ToString(), true);
	}
	public void WARN(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) {
		WriteMessage(LogLevel.Warn, message, false, sourceFunction, sourceFile, sourceLine.ToString(), true);
	}
	public void ERR(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) {
		WriteMessage(LogLevel.Error, message, false, sourceFunction, sourceFile, sourceLine.ToString(), true);
	}
	public void CRITICAL(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) {
		WriteMessage(LogLevel.Critical, message, false, sourceFunction, sourceFile, sourceLine.ToString(), true);
	}
	public void BREAK() {
		if (!disableCrashOnError) {
			CRITICAL("Performing emergency exit");
			System.Diagnostics.Debugger.Break();
		}
		WARN("Hit emergency exit, skipping");
	}

	public static void KDEBUG(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) {
		WriteMessage(LogLevel.Debug, message, true, sourceFunction, sourceFile, sourceLine.ToString(), false);
	}
	public static void KINFO(string message,[CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) {
		WriteMessage(LogLevel.Info, message, false, sourceFunction, sourceFile, sourceLine.ToString(), false);
	}
	public static void KWARN(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) {
		WriteMessage(LogLevel.Warn, message, false, sourceFunction, sourceFile, sourceLine.ToString(), false);
	}
	public static void KERR(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) {
		WriteMessage(LogLevel.Error, message, false, sourceFunction, sourceFile, sourceLine.ToString(), false);
	}
	public static void KCRITICAL(string message, [CallerMemberName] string sourceFunction = "Invalid", [CallerFilePath] string sourceFile = "Invalid", [CallerLineNumber] int sourceLine = -1) {
		WriteMessage(LogLevel.Critical, message, false, sourceFunction, sourceFile, sourceLine.ToString(), false);
	}
	public static void KBREAK() {
		if (!disableCrashOnError) {
			KCRITICAL("Performing emergency exit");
			System.Diagnostics.Debugger.Break();
		}
		KWARN("Hit emergency exit, skipping");
	}

	public void LOG_CHECK(bool condition, string success, string error) {
		if (condition) {
			INFO(success);
		} else {
			ERR(error);
		}
	}
	public void LOG_CHECK_BAD(bool condition, string error) {
		if (!condition) {
			ERR(error);
		}
	}
	public int LOG_CHECK_RETURN(bool condition, string success, string error, int returnCode) {
		if (condition) {
			INFO(success);
			return 0;
		} else {
			ERR(error);
			return returnCode;
		}
	}
	public int LOG_CHECK_RETURN_BAD(bool condition, string error, int returnCode) {
		if (!condition) {
			ERR(error);
			return returnCode;
		}
		return 0;
	}
	public void LOG_CHECK_CRITICAL(bool condition, string success, string error) {
		if (condition) {
			INFO(success);
		} else {
			CRITICAL(error);
		}
	}
	public void LOG_CHECK_BAD_CRITICAL(bool condition, string error) {
		if (!condition) {
			CRITICAL(error);
		}
	}
	public int LOG_CHECK_RETURN_CRITICAL(bool condition, string success, string error, int returnCode) {
		if (condition) {
			INFO(success);
			return 0;
		} else {
			CRITICAL(error);
			return returnCode;
		}
	}
	public int LOG_CHECK_RETURN_BAD_CRITICAL(bool condition, string error, int returnCode) {
		if (!condition) {
			CRITICAL(error);
			return returnCode;
		}
		return 0;
	}

	public static void KLOG_CHECK(bool condition, string success, string error) {
		if (condition) {
			KINFO(success);
		} else {
			KERR(error);
		}
	}
	public static void KLOG_CHECK_BAD(bool condition, string error) {
		if (!condition) {
			KERR(error);
		}
	}
	public static int KLOG_CHECK_RETURN(bool condition, string success, string error, int returnCode) {
		if (condition) {
			KINFO(success);
			return 0;
		} else {
			KERR(error);
			return returnCode;
		}
	}
	public static int KLOG_CHECK_RETURN_BAD(bool condition, string error, int returnCode) {
		if (!condition) {
			KERR(error);
			return returnCode;
		}
		return 0;
	}
	public static void KLOG_CHECK_CRITICAL(bool condition, string success, string error) {
		if (condition) {
			KINFO(success);
		} else {
			KCRITICAL(error);
		}
	}
	public static void KLOG_CHECK_BAD_CRITICAL(bool condition, string error) {
		if (!condition) {
			KCRITICAL(error);
		}
	}
	public static int KLOG_CHECK_RETURN_CRITICAL(bool condition, string success, string error, int returnCode) {
		if (condition) {
			KINFO(success);
			return 0;
		} else {
			KCRITICAL(error);
			return returnCode;
		}
	}
	public static int KLOG_CHECK_RETURN_BAD_CRITICAL(bool condition, string error, int returnCode) {
		if (!condition) {
			KCRITICAL(error);
			return returnCode;
		}
		return 0;
	}

	private static void WriteMessage(LogLevel level, string message, bool debugMode, string sourceFunction, string sourceFile, string sourceLine, bool fromMod) {
		if (level == LogLevel.Debug && !isDebug) {
			return;
		}

		string header = headerStructure;

		string prefix;
		if (MetaHandler.GetGameType() == GameType.SERVER) {
			prefix = "Server ";
		} else {
			prefix = "Client ";
		}

		string levelString = LogLevelToString(level);
		header = ReplaceString(header, "{level}", ColoredString(levelString, prefix + levelString));

		string fname;
		string replace2 = fromMod ? "Mod" : sourceFunction;
		if (functionHeaderReplacements.Value.TryGetValue(sourceFunction, out string replacement)) {
			fname = ColoredString("FUNCTION", replacement);
		} else {
			fname = ColoredString("FUNCTION", replace2);
		}

		
		header = ReplaceString(header, "{function}", fname);

		Console.WriteLine(header + " " + message);
	}

	private static string ReplaceString(string fullString, string oldString, string newString) {
		return fullString.Replace(oldString, newString);
	}

	private static string LogLevelToString(LogLevel level) {
		switch (level) {
			case LogLevel.Debug:
				return "DEBUG";
			case LogLevel.Info:
				return "INFO";
			case LogLevel.External:
				return "EXTERNAL";
			case LogLevel.Warn:
				return "WARN";
			case LogLevel.Error:
				return "ERROR";
			case LogLevel.Critical:
				return "CRITICAL";
			case LogLevel.Off:
				return "OFF";
			default:
				return "UNKNOWN";
		}
	}

	private static string ColoredString(string color, string originalString) {
		string colorString = "";
		if (logColors.TryGetValue(color, out colorString)) {
			colorString = logColors[color];
		}
		return colorString + originalString + logColors["RESET"];
	}
}

public enum LogLevel : byte {
	Debug,
	Info,
	External,
	Warn,
	Error,
	Critical,
	Off
}