using System;

namespace Ordering.Application.Exceptions
{
    public class NotFoundException : ApplicationException
    {
        public NotFoundException(string name, object key)
        : base($"Entity \"{name}\" and key \"{key}\" was not found") { }
    }
}