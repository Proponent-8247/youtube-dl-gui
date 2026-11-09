using System;

internal static partial class AuditRegression {
    private static void DownloadHistoryFailureTeardownJoinsWorkersBeforeDisposingEvents() {
        bool previous = HistoryLeaseCleanupFaultInjection;
        try {
            HistoryLeaseCleanupFaultInjection = true;
            bool threw = false;
            try { DownloadHistoryLeaseSerializesWorkers(); }
            catch (InvalidOperationException ex) {
                threw = ex.Message.Contains("Injected lease-serialization cleanup failure");
            }
            Require(threw, "Lease serialization did not exercise the injected early failure.");
            threw = false;
            try { DownloadHistoryLeaseWaitHonorsCancellation(); }
            catch (InvalidOperationException ex) {
                threw = ex.Message.Contains("Injected protected-lease cancellation cleanup failure");
            }
            Require(threw, "Protected lease wait did not exercise early-failure cleanup.");
        }
        finally { HistoryLeaseCleanupFaultInjection = previous; }

        // Both tests must subsequently run normally, without orphaned worker
        // exceptions, lock ownership, or disposed-event access.
        DownloadHistoryLeaseSerializesWorkers();
        DownloadHistoryLeaseWaitHonorsCancellation();
    }
}
