namespace NBL;
public class LauncherException : Exception { public LauncherException(string text) : base(text) { } }
public class GameNotFoundException : LauncherException { public GameNotFoundException(string text) : base(text) { } }
public class GameAlreadyRegException : LauncherException { public GameAlreadyRegException(string text) : base(text) { } }
public class GameInstallException : LauncherException { public GameInstallException(string text) : base(text) { } }
public class GamePathNotSetException : LauncherException { public GamePathNotSetException(string text) : base(text) { } }
public class GamePathNotFoundException : LauncherException { public GamePathNotFoundException(string text) : base(text) { } }
public class GameRefClientNotSetException : LauncherException { public GameRefClientNotSetException(string text) : base(text) { } }
public class GameNotSetRegistryException : LauncherException { public GameNotSetRegistryException(string text) : base(text) { } }
public class LaunchParamNotFoundException : LauncherException { public LaunchParamNotFoundException(string text) : base(text) { } }