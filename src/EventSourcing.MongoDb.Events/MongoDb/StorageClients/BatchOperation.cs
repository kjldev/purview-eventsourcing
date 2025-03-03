using Purview.EventSourcing.MongoDB.Entities;

namespace Purview.EventSourcing.MongoDB.StorageClients;

sealed class BatchOperation
{
	readonly List<TableTransactionAction> _operations = [];

	public bool Any() => _operations.Count > 0;

	public void Delete(IEntity document)
		=> AddAction(TransactionActionType.Delete, document);

	public void Insert(IEntity document)
		=> AddAction(TransactionActionType.Insert, document);

	public void Update(IEntity document)
		=> AddAction(TransactionActionType.Update, document);

	public void AddAction(TransactionActionType actionType, IEntity document)
	{
		TableTransactionAction tableOperation = new(actionType, document);
		_operations.Add(tableOperation);
	}

	public IEnumerable<TableTransactionAction> GetActions()
		=> _operations;
}

record TableTransactionAction(TransactionActionType ActionType, IEntity Document);
