using System.Linq;

namespace Purview.EventSourcing.SourceGenerator;

static class EventVerbMap
{
	static readonly (string Verb, string PastTense)[] VerbPairs =
	[
		("Reactivate", "Reactivated"),
		("Unpublish", "Unpublished"),
		("Reschedule", "Rescheduled"),
		("Deactivate", "Deactivated"),
		("Unassign", "Unassigned"),
		("Unsuspend", "Unsuspended"),
		("Unverify", "Unverified"),
		("Publish", "Published"),
		("Complete", "Completed"),
		("Withdraw", "Withdrawn"),
		("Restore", "Restored"),
		("Archive", "Archived"),
		("Approve", "Approved"),
		("Decline", "Declined"),
		("Schedule", "Scheduled"),
		("Suspend", "Suspended"),
		("Confirm", "Confirmed"),
		("Disable", "Disabled"),
		("Enable", "Enabled"),
		("Replace", "Replaced"),
		("Register", "Registered"),
		("Reject", "Rejected"),
		("Attach", "Attached"),
		("Unlink", "Unlinked"),
		("Clear", "Cleared"),
		("Cancel", "Cancelled"),
		("Create", "Created"),
		("Change", "Changed"),
		("Update", "Updated"),
		("Remove", "Removed"),
		("Delete", "Deleted"),
		("Set", "Set"),
		("Close", "Closed"),
		("Start", "Started"),
		("Record", "Recorded"),
		("Assign", "Assigned"),
		("Link", "Linked"),
		("Detach", "Detached"),
		("Add", "Added"),
		("Import", "Imported"),
		("Export", "Exported"),
		("Mark", "Marked"),
		("Verify", "Verified"),
		("Increment", "Incremented"),
		("Decrement", "Decremented"),
		("Submit", "Submitted"),
		("Split", "Split"),
		("Move", "Moved"),
		("Rename", "Renamed"),
		("Open", "Opened"),
		("Reset", "Reset"),
	];

	static readonly (string Prefix, string Suffix)[] PropertySpecificPatterns =
	[
		("Change", "Changed"),
		("Update", "Updated"),
		("Set", "Set"),
		("Clear", "Cleared"),
		("Remove", "Removed"),
	];

	static readonly string[] PastTenseSuffixes =
		VerbPairs.Select(static pair => pair.PastTense)
			.Distinct(StringComparer.Ordinal)
			.OrderByDescending(static value => value.Length)
			.ThenBy(static value => value, StringComparer.Ordinal)
			.ToArray();

	static readonly (string Verb, string PastTense)[] VerbPairsByPrefixLength =
		VerbPairs.OrderByDescending(static pair => pair.Verb.Length)
			.ThenBy(static pair => pair.Verb, StringComparer.Ordinal)
			.ToArray();

	public static bool TryGetPastTense(string verb, out string pastTense)
	{
		foreach (var pair in VerbPairsByPrefixLength)
		{
			if (!string.Equals(pair.Verb, verb, StringComparison.Ordinal))
				continue;

			pastTense = pair.PastTense;
			return true;
		}

		pastTense = string.Empty;
		return false;
	}

	public static bool TryCreateGeneratedEventName(string methodName, string aggregateClassName, out string eventName)
	{
		if (TryCreatePropertySpecificEventName(methodName, out eventName))
			return true;

		if (TryCreateVerbMappedEventName(methodName, aggregateClassName, out eventName))
			return true;

		eventName = string.Empty;
		return false;
	}

	public static bool IsVerbPhrase(string methodName)
	{
		if (TryCreatePropertySpecificEventName(methodName, out _))
			return true;

		return TryGetVerbPrefix(methodName, out _, out _);
	}

	public static bool IsPastTenseEventName(string eventName)
	{
		var coreName = TrimEventSuffix(eventName);
		if (string.IsNullOrWhiteSpace(coreName))
			return false;

		return TryGetPastTenseSuffix(coreName, out _);
	}

	public static bool TryGetPastTenseEventNameCore(string eventName, out string coreName)
	{
		coreName = TrimEventSuffix(eventName);
		return !string.IsNullOrWhiteSpace(coreName) && IsPastTenseEventName(coreName);
	}

	public static bool TrySuggestVerbPhrase(string methodName, out string suggestedMethodName)
	{
		if (methodName.StartsWith("New", StringComparison.Ordinal) && methodName.Length > 3)
		{
			var subject = methodName.Substring(3);
			suggestedMethodName = $"Register{subject}";
			return true;
		}

		suggestedMethodName = string.Empty;
		return false;
	}

	static bool TryCreatePropertySpecificEventName(string methodName, out string eventName)
	{
		foreach (var (prefix, suffix) in PropertySpecificPatterns)
		{
			if (!methodName.StartsWith(prefix, StringComparison.Ordinal))
				continue;

			var subject = methodName.Substring(prefix.Length);
			if (subject.Length == 0)
				continue;

			eventName = subject + suffix;
			return true;
		}

		eventName = string.Empty;
		return false;
	}

	static bool TryCreateVerbMappedEventName(string methodName, string aggregateClassName, out string eventName)
	{
		if (!TryGetVerbPrefix(methodName, out var verb, out var pastTense))
		{
			eventName = string.Empty;
			return false;
		}

		var subject = methodName.Substring(verb.Length);
		if (subject.Length == 0)
			subject = TrimAggregateSuffix(aggregateClassName);

		if (string.IsNullOrWhiteSpace(subject))
		{
			eventName = string.Empty;
			return false;
		}

		eventName = subject + pastTense;
		return true;
	}

	static bool TryGetVerbPrefix(string methodName, out string verb, out string pastTense)
	{
		foreach (var pair in VerbPairsByPrefixLength)
		{
			if (!methodName.StartsWith(pair.Verb, StringComparison.Ordinal))
				continue;

			verb = pair.Verb;
			pastTense = pair.PastTense;
			return true;
		}

		verb = string.Empty;
		pastTense = string.Empty;
		return false;
	}

	static bool TryGetPastTenseSuffix(string eventName, out string suffix)
	{
		foreach (var pastTense in PastTenseSuffixes)
		{
			if (!eventName.EndsWith(pastTense, StringComparison.Ordinal))
				continue;

			suffix = pastTense;
			return true;
		}

		suffix = string.Empty;
		return false;
	}

	static string TrimEventSuffix(string name) =>
		name.EndsWith("Event", StringComparison.Ordinal)
			? name.Substring(0, name.Length - "Event".Length)
			: name;

	static string TrimAggregateSuffix(string aggregateClassName) =>
		aggregateClassName.EndsWith("Aggregate", StringComparison.Ordinal)
			? aggregateClassName.Substring(0, aggregateClassName.Length - "Aggregate".Length)
			: aggregateClassName;
}
