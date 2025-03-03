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

		private class EventTypeEndingInEvent : EventBase
		{
			protected override void BuildEventHash(ref HashCode hash) { }
		}

		private class EventTypeNotEndingInEvent2 : EventBase
		{
			protected override void BuildEventHash(ref HashCode hash) { }
		}

		private class CorrectlyNamedAggregate : AggregateBase
		{
			protected override void RegisterEvents() { }
		}
	}
}

namespace Purview.Services.UserProfile.Aggregates.UserProfile.Events
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
