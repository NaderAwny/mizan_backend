namespace Mizan.Core.Exceptions;

public class ForbiddenException : Exception
{
    public ForbiddenException(string message = "ليس لديك صلاحية للقيام بهذا الإجراء") : base(message)
    {
    }
}
