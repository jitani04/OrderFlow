namespace OrderFlow.Domain.Common;

/// <summary>Raised when a caller asks the domain to do something its rules forbid.</summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
