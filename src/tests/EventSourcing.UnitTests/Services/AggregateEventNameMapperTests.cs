using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Events;
using Purview.EventSourcing.Services;

namespace Purview.EventSourcing
{
	public partial class AggregateEventNameMapperTests
	{
		const string CorrectlyNamedAggregateName = "correctly-named";

		static AggregateEventNameMapper CreateMapper<T>()
			where T : class, IAggregate, new()
		{
			AggregateEventNameMapper? eventNameMapper = new();
			eventNameMapper.InitializeAggregate<T>();

			return eventNameMapper;
		}

		sealed class EventTypeEndingInEvent : EventBase
		{
			protected override void BuildEventHash(ref HashCode hash) { }
		}

		sealed class EventTypeNotEndingInEvent2 : EventBase
		{
			protected override void BuildEventHash(ref HashCode hash) { }
		}

		sealed class CorrectlyNamedAggregate : AggregateBase
		{
			protected override void RegisterEvents() { }
		}
	}
}

#pragma warning disable IDE0130 // Namespace does not match folder structure
// This is for a specific set of tests
namespace Purview.Services.UserProfile.Aggregates.UserProfile.Events
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
	public sealed class ClearProfileAttributesEvent : EventBase
	{
		protected override void BuildEventHash(ref HashCode hash) { }
	}

	public sealed class ClearRolesEvent : EventBase
	{
		protected override void BuildEventHash(ref HashCode hash) { }
	}
}
