#nullable enable
namespace youtube_dl_gui;

internal sealed class QueueList<T> : List<T> {
    private readonly object SyncRoot = new();

    public bool IsEmpty {
        get {
            lock (SyncRoot) {
                return this.Count == 0;
            }
        }
    }

    public bool TryAdd(T item) {
        lock (SyncRoot) {
            int index = this.IndexOf(item);
            if (index > -1)
                return false;
            this.Add(item);
            return true;
        }
    }

    public bool TryRemove(T item) {
        lock (SyncRoot) {
            int index = this.IndexOf(item);
            if (index == -1)
                return false;
            this.RemoveAt(index);
            return true;
        }
    }

    public bool TryPeek(out T item) {
        lock (SyncRoot) {
            if (this.Count == 0) {
                item = default!;
                return false;
            }

            item = this[0];
            return true;
        }
    }
}