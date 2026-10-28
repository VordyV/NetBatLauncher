namespace NBLNet;

public class ApiException : Exception { public ApiException(string message = "") : base(message) {} }
public class NotFountException : ApiException { public NotFountException(string message = "") : base(message) { } } // 404