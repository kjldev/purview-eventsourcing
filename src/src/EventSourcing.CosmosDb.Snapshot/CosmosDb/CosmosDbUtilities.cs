using System.Collections.Concurrent;
using System.Dynamic;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

namespace Purview.EventSourcing.CosmosDb;

static class CosmosDbUtilities
{
	static readonly ConcurrentDictionary<Type, SerializeProcessData> _documentSerializers = new();

	public static string GetDocumentId(object document)
	{
		var documentType = document.GetType();
		var processData = GetOrCreateSerializeProcess(documentType);

		return processData.GetId(document);
	}

	public static Task<Stream> SerializeDocumentAsync(object document, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(document);

		var documentType = document.GetType();
		var processData = GetOrCreateSerializeProcess(documentType);

		return processData.SerializeAsync(document, cancellationToken);
	}

	static SerializeProcessData GetOrCreateSerializeProcess(Type documentType)
	{
		if (!_documentSerializers.TryGetValue(documentType, out var processData))
		{
			var properties = documentType.GetProperties();
			var idProperties = FindIdProperties(properties);
			if (idProperties == null || !idProperties.Any())
				throw new NullReferenceException($"Unable to locate an `Id` property on {documentType.FullName}.");

			processData = new SerializeProcessData([.. idProperties], properties);
			if (!_documentSerializers.TryAdd(documentType, processData))
				processData = _documentSerializers[documentType];
		}

		return processData;
	}

	static IEnumerable<PropertyInfo>? FindIdProperties(IEnumerable<PropertyInfo> properties)
	{
		foreach (var property in properties)
		{
			if (string.Equals(property.Name, "id", StringComparison.OrdinalIgnoreCase))
				return [property];

			var propertyType = property.PropertyType;
			if (propertyType == typeof(string) || !propertyType.GetTypeInfo().IsClass)
				continue;

			var childProperties = FindIdProperties(property.PropertyType.GetProperties());
			if (childProperties != null && childProperties.Any())
				return new[] { property }.Concat(childProperties);
		}

		return null;
	}

	sealed class SerializeProcessData
	{
		const string _idPropertyName = "id";

		readonly PropertyInfo[] _idProperties;
		readonly PropertyInfo[] _properties;

		public SerializeProcessData(PropertyInfo[] idProperties, PropertyInfo[] properties)
		{
			if (idProperties.Length == 1)
			{
				var p = idProperties.Single();
				if (p.Name != _idPropertyName)
				{
					var jsonAttrib = p.GetCustomAttribute<JsonPropertyNameAttribute>();
					if (jsonAttrib?.Name != _idPropertyName)
						throw new NullReferenceException(
							"Unable to process object, missing property with name 'id' or System.Text.Json.Serialization.JsonPropertyNameAttribute using that name."
						);
				}
			}

			_idProperties = idProperties;
			_properties = properties;
		}

		public bool IsIdDefinedAtTopLevel => _idProperties.Length == 1;

		public async Task<Stream> SerializeAsync(object document, CancellationToken cancellationToken)
		{
			var documentResponse = GetDocument(document);

			cancellationToken.ThrowIfCancellationRequested();

			var memoryStream = new MemoryStream();
			await JsonHelpers.SerializeAsync(memoryStream, documentResponse, documentResponse.GetType(), cancellationToken);

			cancellationToken.ThrowIfCancellationRequested();

			memoryStream.Position = 0;

			return memoryStream;
		}

		public string GetId(object document)
		{
			object? currentItem = document;
			for (var i = 0; i < _idProperties.Length; i++)
			{
				var idProperty = _idProperties[i];
				currentItem = idProperty.GetValue(currentItem);
				if (currentItem == null)
					throw new NullReferenceException("Some or all of id parts are null.");
			}

			var id = currentItem.ToString() ?? throw new NullReferenceException("Some or all of id parts are null.");
			return id;
		}

		public object GetDocument(object document)
		{
			if (IsIdDefinedAtTopLevel)
				return document;

			var documentResponse = new ExpandoObject();
			documentResponse.TryAdd(_idPropertyName, GetId(document));

			object? currentItem;
			for (var i = 0; i < _properties.Length; i++)
			{
				var property = _properties[i];

				currentItem = property.GetValue(document);
				documentResponse.TryAdd(property.Name, currentItem);
			}

			return documentResponse;
		}
	}
}
