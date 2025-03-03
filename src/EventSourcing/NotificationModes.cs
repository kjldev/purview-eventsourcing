namespace Purview.EventSourcing;

/// <summary>
/// Controls have notifications are handled for aggregate save operations.
/// </summary>
[Flags]
public enum NotificationModes
{
	/// <summary>
	/// No notifications are generated.
	/// </summary>
	None = 0,

	/// <summary>
	/// Notifications are generated before saving occurs.
	/// </summary>
	BeforeSave = 1,

	/// <summary>
	/// Notifications are generated after save.
	/// </summary>
	AfterSave = 2,

	/// <summary>
	/// Notifications are generated before delete.
	/// </summary>
	BeforeDelete = 4,

	/// <summary>
	/// Notifications are generated before save and delete.
	/// </summary>
	BeforeSaveOrDelete = BeforeSave | BeforeDelete,

	/// <summary>
	/// Notifications are generated after delete.
	/// </summary>
	AfterDelete = 8,

	/// <summary>
	/// Notifications are generated after save and delete.
	/// </summary>
	AfterSaveOrDelete = AfterSave | AfterDelete,

	/// <summary>
	/// Notifications are generated when there is a failure to perform an operation.
	/// </summary>
	OnFailure = 16,

	/// <summary>
	/// Notifications are generated before saving and deleting, after saving or deleting and in the case of failure.
	/// </summary>
	All = BeforeSaveOrDelete | AfterSaveOrDelete | OnFailure
}
