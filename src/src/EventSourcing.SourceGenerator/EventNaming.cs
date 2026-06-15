using System.Collections.Frozen;

namespace Purview.EventSourcing.SourceGenerator;

static class EventVerbMap
{
	static readonly FrozenDictionary<string, string> VerbPairs = new Dictionary<string, string>
	{
		// ── Original entries (Cancel now US-spelled) ──
		{ "Reactivate", "Reactivated" },
		{ "Unpublish", "Unpublished" },
		{ "Reschedule", "Rescheduled" },
		{ "Deactivate", "Deactivated" },
		{ "Unassign", "Unassigned" },
		{ "Unsuspend", "Unsuspended" },
		{ "Unverify", "Unverified" },
		{ "Publish", "Published" },
		{ "Complete", "Completed" },
		{ "Withdraw", "Withdrawn" },
		{ "Restore", "Restored" },
		{ "Archive", "Archived" },
		{ "Approve", "Approved" },
		{ "Decline", "Declined" },
		{ "Schedule", "Scheduled" },
		{ "Suspend", "Suspended" },
		{ "Confirm", "Confirmed" },
		{ "Disable", "Disabled" },
		{ "Enable", "Enabled" },
		{ "Replace", "Replaced" },
		{ "Register", "Registered" },
		{ "Reject", "Rejected" },
		{ "Attach", "Attached" },
		{ "Unlink", "Unlinked" },
		{ "Clear", "Cleared" },
		{ "Cancel", "Canceled" }, // US (UK: Cancelled)
		{ "Create", "Created" },
		{ "Change", "Changed" },
		{ "Update", "Updated" },
		{ "Remove", "Removed" },
		{ "Delete", "Deleted" },
		{ "Set", "Set" },
		{ "Close", "Closed" },
		{ "Start", "Started" },
		{ "Record", "Recorded" },
		{ "Assign", "Assigned" },
		{ "Link", "Linked" },
		{ "Detach", "Detached" },
		{ "Add", "Added" },
		{ "Import", "Imported" },
		{ "Export", "Exported" },
		{ "Mark", "Marked" },
		{ "Verify", "Verified" },
		{ "Increment", "Incremented" },
		{ "Decrement", "Decremented" },
		{ "Submit", "Submitted" },
		{ "Split", "Split" },
		{ "Move", "Moved" },
		{ "Rename", "Renamed" },
		{ "Open", "Opened" },
		{ "Reset", "Reset" },
		// ── CRUD / lifecycle ──
		{ "Edit", "Edited" },
		{ "Modify", "Modified" },
		{ "Save", "Saved" },
		{ "Load", "Loaded" },
		{ "Insert", "Inserted" },
		{ "Append", "Appended" },
		{ "Merge", "Merged" },
		{ "Duplicate", "Duplicated" },
		{ "Clone", "Cloned" },
		{ "Copy", "Copied" },
		{ "Apply", "Applied" },
		{ "Revert", "Reverted" },
		{ "Rollback", "RolledBack" },
		{ "Commit", "Committed" },
		{ "Finish", "Finished" },
		{ "Finalize", "Finalized" }, // US (UK: Finalise)
		{ "Begin", "Begun" },
		{ "End", "Ended" },
		{ "Pause", "Paused" },
		{ "Resume", "Resumed" },
		{ "Stop", "Stopped" },
		{ "Halt", "Halted" },
		{ "Terminate", "Terminated" },
		{ "Abort", "Aborted" },
		{ "Retry", "Retried" },
		{ "Resend", "Resent" },
		{ "Refresh", "Refreshed" },
		{ "Renew", "Renewed" },
		{ "Expire", "Expired" },
		// ── Approval / review / state transitions ──
		{ "Accept", "Accepted" },
		{ "Deny", "Denied" },
		{ "Authorize", "Authorized" }, // US (UK: Authorise)
		{ "Validate", "Validated" },
		{ "Invalidate", "Invalidated" },
		{ "Review", "Reviewed" },
		{ "Flag", "Flagged" },
		{ "Unflag", "Unflagged" },
		{ "Block", "Blocked" },
		{ "Unblock", "Unblocked" },
		{ "Ban", "Banned" },
		{ "Unban", "Unbanned" },
		{ "Lock", "Locked" },
		{ "Unlock", "Unlocked" },
		{ "Grant", "Granted" },
		{ "Revoke", "Revoked" },
		{ "Promote", "Promoted" },
		{ "Demote", "Demoted" },
		{ "Escalate", "Escalated" },
		{ "Resolve", "Resolved" },
		{ "Reopen", "Reopened" },
		{ "Dismiss", "Dismissed" },
		// ── Account / auth / identity ──
		{ "Login", "LoggedIn" },
		{ "Logout", "LoggedOut" },
		{ "SignIn", "SignedIn" },
		{ "SignOut", "SignedOut" },
		{ "SignUp", "SignedUp" },
		{ "Authenticate", "Authenticated" },
		{ "Onboard", "Onboarded" },
		{ "Offboard", "Offboarded" },
		{ "Enroll", "Enrolled" }, // US (UK: Enrol)
		{ "Subscribe", "Subscribed" },
		{ "Unsubscribe", "Unsubscribed" },
		{ "Invite", "Invited" },
		{ "Join", "Joined" },
		{ "Leave", "Left" }, // irregular
		{ "Impersonate", "Impersonated" },
		// ── Commerce / payments / fulfillment ──
		{ "Purchase", "Purchased" },
		{ "Order", "Ordered" },
		{ "Pay", "Paid" }, // irregular
		{ "Charge", "Charged" },
		{ "Refund", "Refunded" },
		{ "Bill", "Billed" },
		{ "Invoice", "Invoiced" },
		{ "Credit", "Credited" },
		{ "Debit", "Debited" },
		{ "Settle", "Settled" },
		{ "Reconcile", "Reconciled" },
		{ "Capture", "Captured" },
		{ "Void", "Voided" },
		{ "Dispute", "Disputed" },
		{ "Quote", "Quoted" },
		{ "Discount", "Discounted" },
		{ "Ship", "Shipped" },
		{ "Dispatch", "Dispatched" },
		{ "Deliver", "Delivered" },
		{ "Return", "Returned" },
		{ "Fulfill", "Fulfilled" }, // US (UK: Fulfil)
		{ "Pack", "Packed" },
		{ "Unpack", "Unpacked" },
		{ "Stock", "Stocked" },
		{ "Restock", "Restocked" },
		{ "Reserve", "Reserved" },
		{ "Allocate", "Allocated" },
		{ "Deallocate", "Deallocated" },
		{ "Release", "Released" },
		{ "Redeem", "Redeemed" },
		// ── Communication / notification ──
		{ "Send", "Sent" }, // irregular
		{ "Receive", "Received" },
		{ "Notify", "Notified" },
		{ "Alert", "Alerted" },
		{ "Remind", "Reminded" },
		{ "Email", "Emailed" },
		{ "Message", "Messaged" },
		{ "Post", "Posted" },
		{ "Comment", "Commented" },
		{ "Reply", "Replied" },
		{ "Forward", "Forwarded" },
		{ "Broadcast", "Broadcast" }, // also valid: Broadcasted
		{ "View", "Viewed" },
		{ "Acknowledge", "Acknowledged" },
		// ── Data / processing / transform ──
		{ "Process", "Processed" },
		{ "Generate", "Generated" },
		{ "Calculate", "Calculated" },
		{ "Compute", "Computed" },
		{ "Aggregate", "Aggregated" },
		{ "Transform", "Transformed" },
		{ "Convert", "Converted" },
		{ "Parse", "Parsed" },
		{ "Serialize", "Serialized" }, // US (UK: Serialise)
		{ "Deserialize", "Deserialized" }, // US (UK: Deserialise)
		{ "Encode", "Encoded" },
		{ "Decode", "Decoded" },
		{ "Encrypt", "Encrypted" },
		{ "Decrypt", "Decrypted" },
		{ "Compress", "Compressed" },
		{ "Decompress", "Decompressed" },
		{ "Index", "Indexed" },
		{ "Sort", "Sorted" },
		{ "Filter", "Filtered" },
		{ "Group", "Grouped" },
		{ "Map", "Mapped" },
		{ "Scan", "Scanned" },
		{ "Analyze", "Analyzed" }, // US (UK: Analyse)
		{ "Sync", "Synced" },
		{ "Synchronize", "Synchronized" }, // US (UK: Synchronise)
		{ "Migrate", "Migrated" },
		{ "Backup", "BackedUp" },
		{ "Snapshot", "Snapshotted" },
		{ "Truncate", "Truncated" },
		{ "Purge", "Purged" },
		// ── Files / documents / media ──
		{ "Upload", "Uploaded" },
		{ "Download", "Downloaded" },
		{ "Print", "Printed" },
		{ "Sign", "Signed" },
		{ "Stamp", "Stamped" },
		{ "Watermark", "Watermarked" },
		{ "Render", "Rendered" },
		{ "Compile", "Compiled" },
		{ "Deploy", "Deployed" },
		{ "Provision", "Provisioned" },
		{ "Configure", "Configured" },
		{ "Initialize", "Initialized" }, // US (UK: Initialise)
		// ── Quantity / state changes ──
		{ "Adjust", "Adjusted" },
		{ "Recalculate", "Recalculated" },
		{ "Rebalance", "Rebalanced" },
		{ "Scale", "Scaled" },
		{ "Resize", "Resized" },
		{ "Extend", "Extended" },
		{ "Shorten", "Shortened" },
		{ "Expand", "Expanded" },
		{ "Collapse", "Collapsed" },
		{ "Toggle", "Toggled" },
		{ "Select", "Selected" },
		{ "Deselect", "Deselected" },
		{ "Pick", "Picked" },
		{ "Tag", "Tagged" },
		{ "Untag", "Untagged" },
		{ "Label", "Labeled" }, // US (UK: Labelled)
		{ "Categorize", "Categorized" }, // US (UK: Categorise)
		{ "Classify", "Classified" },
		{ "Prioritize", "Prioritized" }, // US (UK: Prioritise)
		{ "Rank", "Ranked" },
		{ "Rate", "Rated" },
		{ "Score", "Scored" },
		{ "Vote", "Voted" },
		// ── Irregular verbs (past tense is not -ed) ──
		{ "Make", "Made" },
		{ "Take", "Took" },
		{ "Give", "Given" },
		{ "Get", "Got" },
		{ "Put", "Put" },
		{ "Cut", "Cut" },
		{ "Hold", "Held" },
		{ "Keep", "Kept" },
		{ "Lose", "Lost" },
		{ "Find", "Found" },
		{ "Buy", "Bought" },
		{ "Sell", "Sold" },
		{ "Bring", "Brought" },
		{ "Catch", "Caught" },
		{ "Teach", "Taught" },
		{ "Think", "Thought" },
		{ "Choose", "Chosen" },
		{ "Run", "Ran" },
		{ "Write", "Written" },
		{ "Draw", "Drawn" },
		{ "Throw", "Thrown" },
		{ "Grow", "Grown" },
		{ "Show", "Shown" },
		{ "Hide", "Hidden" },
		{ "Break", "Broken" },
		{ "Freeze", "Frozen" },
		{ "Forget", "Forgotten" },
		{ "Bind", "Bound" },
		{ "Rebind", "Rebound" },
		{ "Spend", "Spent" },
		{ "Build", "Built" },
		{ "Rebuild", "Rebuilt" },
		{ "Feed", "Fed" },
		{ "Read", "Read" },
		{ "Lead", "Led" },
		{ "Lay", "Laid" },
		{ "Say", "Said" },
		{ "Win", "Won" },
		{ "Hit", "Hit" },
		{ "Cost", "Cost" },
		{ "Quit", "Quit" },
		{ "Shut", "Shut" },
		{ "Bid", "Bid" },
		{ "Spread", "Spread" },
		{ "Cast", "Cast" },
		{ "Recast", "Recast" },
	}.ToFrozenDictionary();

	static readonly (string Prefix, string Suffix)[] PropertySpecificPatterns =
	[
		("Change", "Changed"),
		("Update", "Updated"),
		("Set", "Set"),
		("Clear", "Cleared"),
		("Remove", "Removed"),
	];

	static readonly string[] PastTenseSuffixes =
	[
		.. VerbPairs
			.Select(static pair => pair.Value)
			.Distinct(StringComparer.Ordinal)
			.OrderByDescending(static value => value.Length)
			.ThenBy(static value => value, StringComparer.Ordinal),
	];

	static readonly FrozenDictionary<string, string> VerbPairsByPrefixLength = VerbPairs
		.OrderByDescending(static pair => pair.Key.Length)
		.ThenBy(static pair => pair.Key, StringComparer.Ordinal)
		.ToFrozenDictionary();

	public static bool TryGetPastTense(string verb, out string pastTense)
	{
		foreach (var kvp in VerbPairsByPrefixLength)
		{
			if (!string.Equals(kvp.Key, verb, StringComparison.Ordinal))
				continue;

			pastTense = kvp.Value;
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
		foreach (var kvp in VerbPairsByPrefixLength)
		{
			if (!methodName.StartsWith(kvp.Key, StringComparison.Ordinal))
				continue;

			verb = kvp.Key;
			pastTense = kvp.Value;
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
		name.EndsWith("Event", StringComparison.Ordinal) ? name.Substring(0, name.Length - "Event".Length) : name;

	static string TrimAggregateSuffix(string aggregateClassName) =>
		aggregateClassName.EndsWith("Aggregate", StringComparison.Ordinal)
			? aggregateClassName.Substring(0, aggregateClassName.Length - "Aggregate".Length)
			: aggregateClassName;
}
