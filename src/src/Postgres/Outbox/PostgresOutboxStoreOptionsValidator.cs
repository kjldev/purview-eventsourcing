using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Purview.EventSourcing.Postgres.Outbox;

sealed class PostgresOutboxStoreOptionsValidator : IValidateOptions<PostgresOutboxStoreOptions>
{
	public ValidateOptionsResult Validate(string? name, PostgresOutboxStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		ValidationContext validationContext = new(options);
		List<ValidationResult> validationResults = [];
		if (!Validator.TryValidateObject(options, validationContext, validationResults, validateAllProperties: true))
			return ValidateOptionsResult.Fail(
				validationResults.Select(static x => x.ErrorMessage ?? "Options validation failed.")
			);

		// Additional custom validation logic can be added here if needed
		return ValidateOptionsResult.Success;
	}
}
