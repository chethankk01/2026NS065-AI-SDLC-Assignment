namespace SpendWise.Core;

public interface IClock
{
    DateOnly Today { get; }
}

public interface IExpenseStore
{
    StoreData Load();

    void Save(StoreData data);
}

public sealed class ValidationException(string message) : Exception(message);

public sealed class StorageException(string message, Exception? inner = null) : Exception(message, inner);
