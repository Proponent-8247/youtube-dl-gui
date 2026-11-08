using System;
using System.Reflection;
using System.Windows.Forms;

internal static partial class AuditRegression {
    private static void ShutdownGateRechecksActionsWhenLastCompletionPrecedesArming() {
        Type program = T("youtube_dl_gui.Program");
        Type handlerType = T("youtube_dl_gui.MessageHandler");
        PropertyInfo queue = program.GetProperty("QueueHandler", All);
        FieldInfo count = program.GetField("BackgroundActionCount", All);
        Require(queue != null && count != null, "Could not isolate the shutdown coordination fixture");
        object previousQueue = queue.GetValue(null, null);
        object previousCount = count.GetValue(null);
        Form empty = null;
        Form active = null;
        Form late = null;
        try {
            count.SetValue(null, 0);
            Require(!(bool)program.GetProperty("HasRunningActions", All).GetValue(null, null),
                "A previous regression left active processing actions behind");
            empty = (Form)Activator.CreateInstance(handlerType, true);
            // Last completion was observed before shutdown armed its exit gate.
            Call(handlerType, empty, "AwaitExit");
            Require(empty.IsDisposed, "Arming an empty exit gate did not retire the handler immediately");

            active = (Form)Activator.CreateInstance(handlerType, true);
            queue.SetValue(null, active, null);
            Call(program, null, "BeginBackgroundAction");
            Call(handlerType, active, "AwaitExit");
            Require(!active.IsDisposed, "Exit gate closed while a real background action was running");
            Call(program, null, "EndBackgroundAction");
            Require(active.IsDisposed, "Last completion did not release an already armed exit gate");

            late = (Form)Activator.CreateInstance(handlerType, true);
            queue.SetValue(null, late, null);
            // Same transition the normal shutdown and command-line paths use.
            // Must not start a hidden Application.Run on an already empty queue.
            Call(program, null, "AwaitActions");
            Require(late.IsDisposed, "Graceful shutdown failed to exit after a lost-wakeup interleaving");
        }
        finally {
            if (late != null && !late.IsDisposed) late.Dispose();
            if (active != null && !active.IsDisposed) active.Dispose();
            if (empty != null && !empty.IsDisposed) empty.Dispose();
            queue.SetValue(null, previousQueue, null);
            count.SetValue(null, previousCount);
        }
    }
}
