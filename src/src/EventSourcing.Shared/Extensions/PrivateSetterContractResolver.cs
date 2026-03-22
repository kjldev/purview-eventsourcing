using System.Reflection;

namespace Newtonsoft.Json.Serialization;

/// <summary>
/// JSON.NET contract resolver for reading and writing into JSON properties with non-public set methods.
/// </summary>
sealed class PrivateSetterContractResolver : DefaultContractResolver
{
	/// <inheritdoc/>
	protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
	{
		var prop = base.CreateProperty(member, memberSerialization);
		if (prop.Ignored)
			return prop;

		if (prop.Writable)
			return prop;

		if (member is not PropertyInfo property)
			return prop;

		var hasPrivateSetter = property.GetSetMethod(true) != null;
		prop.Writable = hasPrivateSetter;

		return prop;
	}
}
