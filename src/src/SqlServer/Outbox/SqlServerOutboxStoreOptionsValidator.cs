using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Purview.EventSourcing.SqlServer.Outbox;

sealed class SqlServerOutboxStoreOptionsValidator : IValidateOptions<SqlServerOutboxStoreOptions>
{
	public ValidateOptionsResult Validate(string? name, SqlServerOutboxStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		ValidationContext validationContext = new(options);
		List<ValidationResult> validationResults = [];
		if (!Validator.TryValidateObject(options, validationContext, validationResults, validateAllProperties: true))
			return ValidateOptionsResult.Fail(
				validationResults.Select(static x => x.ErrorMessage ?? "Options validation failed.")
			);

		//	If the options are valid, return success
		return ValidateOptionsResult.Success;
	}
}
