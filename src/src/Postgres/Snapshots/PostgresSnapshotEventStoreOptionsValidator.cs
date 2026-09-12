using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Purview.EventSourcing.Postgres.Client;

namespace Purview.EventSourcing.Postgres.Snapshots;

sealed class PostgresSnapshotEventStoreOptionsValidator : IValidateOptions<PostgresSnapshotEventStoreOptions>
{
	public ValidateOptionsResult Validate(string? name, PostgresSnapshotEventStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		ValidationContext validationContext = new(options);
		List<ValidationResult> validationResults = [];
		if (!Validator.TryValidateObject(options, validationContext, validationResults, validateAllProperties: true))
			return ValidateOptionsResult.Fail(
				validationResults.Select(static x => x.ErrorMessage ?? "Options validation failed.")
			);

		try
		{
			_ = new PostgresClient(
				new PostgresClientOptions(options.ConnectionString, options.UseDataCompression)
				{
					SchemaName = options.SchemaName,
					TableName = options.TableName,
					AutoCreateTable = options.AutoCreateTable,
					JsonIndexOptions = options.JsonIndexOptions,
				}
			);

			return ValidateOptionsResult.Success;
		}
		catch (ArgumentException ex)
		{
			return ValidateOptionsResult.Fail(ex.Message);
		}
	}
}
