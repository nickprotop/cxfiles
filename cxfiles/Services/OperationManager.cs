namespace CXFiles.Services;

public enum OperationStatus { Running, Completed, Failed, Cancelled }
public enum OperationType { Copy, Move, Delete }

public class FileOperation
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
    public OperationType Type { get; init; }
    public string Description { get; init; } = "";
    public OperationStatus Status { get; set; } = OperationStatus.Running;
    public long BytesCompleted { get; set; }
    public long BytesTotal { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartTime { get; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    public CancellationTokenSource Cts { get; } = new();

    public int ProgressPercent => BytesTotal > 0
        ? (int)(BytesCompleted * 100 / BytesTotal)
        : 0;

    public string StatusText => Status switch
    {
        OperationStatus.Running => BytesTotal > 0 ? $"{ProgressPercent}%" : "...",
        OperationStatus.Completed => "Done",
        OperationStatus.Failed => "Failed",
        OperationStatus.Cancelled => "Cancelled",
        _ => ""
    };
}

public class OperationManager
{
    private readonly List<FileOperation> _operations = new();
    private readonly object _lock = new();
    private long _lastProgressNotify;

    public event Action? OperationsChanged;

    public IReadOnlyList<FileOperation> Operations
    {
        get { lock (_lock) return _operations.ToList(); }
    }

    public int ActiveCount
    {
        get { lock (_lock) return _operations.Count(o => o.Status == OperationStatus.Running); }
    }

    public int TotalCount
    {
        get { lock (_lock) return _operations.Count; }
    }

    public IReadOnlyList<FileOperation> RunningOperations
    {
        get { lock (_lock) return _operations.Where(o => o.Status == OperationStatus.Running).ToList(); }
    }

    public FileOperation StartOperation(OperationType type, string description)
    {
        var op = new FileOperation { Type = type, Description = description };
        lock (_lock) _operations.Add(op);
        OperationsChanged?.Invoke();
        return op;
    }

    public void ReportProgress(FileOperation op, long bytesCompleted, long bytesTotal)
    {
        op.BytesCompleted = bytesCompleted;
        op.BytesTotal = bytesTotal;

        // Throttle notifications to ~10 per second
        var now = Environment.TickCount64;
        if (now - _lastProgressNotify >= 100)
        {
            _lastProgressNotify = now;
            OperationsChanged?.Invoke();
        }
    }

    public void CompleteOperation(FileOperation op, OperationStatus status, string? error = null)
    {
        op.Status = status;
        op.ErrorMessage = error;
        op.EndTime = DateTime.Now;
        OperationsChanged?.Invoke();

        // Auto-remove completed operations after 30 seconds
        _ = Task.Delay(30_000).ContinueWith(_ =>
        {
            lock (_lock) _operations.Remove(op);
            OperationsChanged?.Invoke();
        });
    }

    public void CancelOperation(FileOperation op)
    {
        op.Cts.Cancel();
        CompleteOperation(op, OperationStatus.Cancelled);
    }

    public void CancelByIndex(int index)
    {
        var running = RunningOperations;
        if (index >= 0 && index < running.Count)
            CancelOperation(running[index]);
    }

    public void ClearCompleted()
    {
        lock (_lock) _operations.RemoveAll(o => o.Status != OperationStatus.Running);
        OperationsChanged?.Invoke();
    }
}
