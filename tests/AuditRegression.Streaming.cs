using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

internal static partial class AuditRegression {
    private static void SubscribeOutput(object pump, string eventName, Action<string> callback) {
        EventInfo ev = pump.GetType().GetEvent(eventName, All);
        Type argsType = ev.EventHandlerType.GetGenericArguments()[0];
        ParameterExpression sender = Expression.Parameter(typeof(object), "sender");
        ParameterExpression args = Expression.Parameter(argsType, "args");
        Delegate handler = Expression.Lambda(ev.EventHandlerType,
            Expression.Invoke(Expression.Constant(callback), Expression.Property(args, "Data")), sender, args).Compile();
        ev.GetAddMethod(true).Invoke(pump, new object[] { handler });
    }
    private static void WithOutputFixture(string mode, Action<Process, object, List<string>, List<string>> action, Action<object> configure = null) {
        ProcessStartInfo info = Fixture(mode, Path.Combine(Environment.CurrentDirectory, "stream-" + Guid.NewGuid().ToString("N") + ".pid"));
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        using (Process child = new Process()) {
            child.StartInfo = info;
            object pump = New("murrty.controls.BoundedProcessOutput", child, 1024);
            var output = new List<string>(); var error = new List<string>();
            SubscribeOutput(pump, "OutputDataReceived", line => { lock (output) output.Add(line); });
            SubscribeOutput(pump, "ErrorDataReceived", line => { lock (error) error.Add(line); });
            if (configure != null) configure(pump);
            try {
                child.Start();
                Call(pump.GetType(), pump, "Start");
                action(child, pump, output, error);
            }
            finally {
                try { if (!child.HasExited) { child.Kill(); child.WaitForExit(5000); } } catch (InvalidOperationException) { }
                ((IDisposable)pump).Dispose();
            }
        }
    }
    static partial void RunSecurityBoundaryTests();
    static partial void RunBoundaryTests() {
        Test("R006.LiveOutputLineStorageIsBounded", () => WithOutputFixture("large", (child, pump, output, error) => {
            Require(child.WaitForExit(5000), "Output fixture hung");
            Call(pump.GetType(), pump, "Drain", 5000);
            Equal(1, output.Count); Require(output[0].Length <= 1024, "Line storage exceeded its configured bound");
            Require(output[0].EndsWith("[line truncated]", StringComparison.Ordinal), "Truncation was not disclosed");
        }));
        Test("D006.LiveOutputHandlesCarriageReturns", () => {
            using (Process child = new Process())
            using (StreamReader reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes("one\rtwo\nthree\r\nlast")))) {
                object pump = New("murrty.controls.BoundedProcessOutput", child, 1024);
                var output = new List<string>();
                SubscribeOutput(pump, "OutputDataReceived", line => output.Add(line));
                try {
                    ((Task)Call(pump.GetType(), pump, "ReadLines", reader, true)).GetAwaiter().GetResult();
                    Equal("one|two|three|last", string.Join("|", output));
                }
                finally { ((IDisposable)pump).Dispose(); }
            }
        });
        Test("D006.LiveOutputCallbackFaultIsContained", () => WithOutputFixture("large", (child, pump, output, error) => {
            Require(child.WaitForExit(5000), "Callback fixture hung");
            Throws<ApplicationException>(() => Call(pump.GetType(), pump, "Drain", 5000));
        }, pump => SubscribeOutput(pump, "OutputDataReceived", line => {
            throw new ApplicationException("fixture callback failure");
        })));
        Test("D007.LiveOutputDrainHasDeadline", () => WithOutputFixture("hang", (child, pump, output, error) => {
            Throws<TimeoutException>(() => Call(pump.GetType(), pump, "Drain", 100));
        }));
        Test("D006.LiveOutputStartIsSingleUse", () => WithOutputFixture("hang", (child, pump, output, error) => {
            Throws<InvalidOperationException>(() => Call(pump.GetType(), pump, "Start"));
        }));
        RunSecurityBoundaryTests();
    }
}
