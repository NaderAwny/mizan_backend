namespace Mizan.Core.Exceptions;

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "غير مصرح لك بالوصول") : base(message)
    {
    }
}
