namespace EvCharge.Api.Infrastructure.Errors
{
    public class AppException : Exception
    {
        public string Code { get; }
        public AppException(string code, string message) : base(message) => Code = code;
    }

    public class RegistrationException : AppException { public RegistrationException(string code, string message) : base(code, message) { } }
    public class NotFoundException   : AppException { public NotFoundException(string code, string message)   : base(code, message) { } }
    public class UpdateException     : AppException { public UpdateException(string code, string message)     : base(code, message) { } }
    public class ValidationException : AppException { public ValidationException(string code, string message) : base(code, message) { } }
    public class AuthException       : AppException { public AuthException(string code, string message)       : base(code, message) { } }
}
