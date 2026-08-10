namespace Mizan.Core.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string entityName, object key)
        : base($"{entityName} بالمعرف '{key}' غير موجود")
    {
    }
}
