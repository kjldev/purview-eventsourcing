namespace Purview.EventSourcing.Samples.Infrastructure;

public sealed class DomainValidationException : AggregateException
{
	public DomainValidationException(string message)
		: base(message) { }

	public DomainValidationException(string message, IEnumerable<Exception> innerExceptions)
		: base(message, innerExceptions) { }

	public DomainValidationException(string message, params Exception[] innerExceptions)
		: base(message, innerExceptions) { }

	public DomainValidationException(string message, Exception innerException)
		: base(message, innerException) { }

	public DomainValidationException() { }
}
