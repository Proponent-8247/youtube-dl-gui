using System;
using System.Runtime.ExceptionServices;

internal static partial class AuditRegression {
    // The test harness continues after a testcase fails. Cleanup must restore
    // process-wide environment/fields even when a bounded worker join faults.
    private static void RestoreBeforeReportingCleanupFailure(Action observe, Action restore) {
        Exception observationFailure = null;
        Exception restorationFailure = null;
        try { if (observe != null) observe(); }
        catch (Exception ex) { observationFailure = ex; }
        try { if (restore != null) restore(); }
        catch (Exception ex) { restorationFailure = ex; }

        if (observationFailure != null && restorationFailure != null)
            throw new AggregateException("Worker cleanup and global restoration both failed.",
                observationFailure, restorationFailure);
        if (restorationFailure != null)
            ExceptionDispatchInfo.Capture(restorationFailure).Throw();
        if (observationFailure != null)
            ExceptionDispatchInfo.Capture(observationFailure).Throw();
    }

    private static void HarnessCleanupRestoresGlobalsBeforeThrowing() {
        string captured = "not restored";
        bool observed = false;
        bool rejected = false;
        try {
            RestoreBeforeReportingCleanupFailure(() => {
                observed = true;
                throw new TimeoutException("Injected worker join timeout");
            }, () => { captured = "restored"; });
        }
        catch (TimeoutException ex) { rejected = ex.Message.Contains("Injected worker join timeout"); }
        Require(observed && rejected, "Test cleanup dropped its original failure");
        Equal("restored", captured);

        // The restoration callback itself must also run when a timeout happened.
        rejected = false;
        try {
            RestoreBeforeReportingCleanupFailure(
                () => { throw new TimeoutException("Observed failure"); },
                () => { captured = "again restored"; throw new InvalidOperationException("Restore failed"); });
        }
        catch (AggregateException ex) { rejected = ex.InnerExceptions.Count == 2; }
        Equal("again restored", captured);
        Require(rejected, "Cleanup failed to preserve both errors for investigation");
    }
}
