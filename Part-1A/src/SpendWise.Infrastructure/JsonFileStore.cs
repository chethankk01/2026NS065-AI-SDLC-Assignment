using System.Text.Json;
using System.Text.Json.Serialization;
using SpendWise.Core;

namespace SpendWise.Infrastructure;

public sealed class JsonFileStore(string path) : IExpenseStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public StoreData Load()
    {
        if (Directory.Exists(path))
        {
            throw new StorageException("Store path is a directory");
        }

        if (!File.Exists(path))
        {
            return new StoreData();
        }

        try
        {
            var data = JsonSerializer.Deserialize<StoreData>(File.ReadAllText(path), Options);
            return data ?? throw new StorageException("Store file is empty or invalid");
        }
        catch (JsonException ex)
        {
            throw new StorageException("Store file is corrupt", ex);
        }
        catch (IOException ex)
        {
            throw new StorageException("Store file cannot be read", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new StorageException("Store file cannot be read", ex);
        }
    }

    public void Save(StoreData data)
    {
        var temp = path + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(data, Options));
            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
            }
            else
            {
                File.Move(temp, path);
            }
        }
        catch (IOException ex)
        {
            throw new StorageException("Store file cannot be written", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new StorageException("Store file cannot be written", ex);
        }
    }
}
