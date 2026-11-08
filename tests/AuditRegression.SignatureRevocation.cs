using System;
using System.IO;
using System.Reflection;
using System.Text;

internal static partial class AuditRegression {
    private static void AuthenticodeTrustChecksWholeSignerChainRevocation() {
        Type verifier = T("youtube_dl_gui.ExecutableTrust");
        object policy = Call(verifier, null, "CreateTrustData", IntPtr.Zero);
        Type type = policy.GetType();
        FieldInfo revocation = type.GetField("fdwRevocationChecks", All);
        FieldInfo provider = type.GetField("dwProvFlags", All);
        Require(revocation != null && provider != null, "WinVerifyTrust revocation policy fields are inaccessible");
        Equal(1U, (uint)revocation.GetValue(policy));
        uint flags = (uint)provider.GetValue(policy);
        const uint chainWithoutRoot = 0x00000080;
        const uint disabled = 0x00000010;
        Require((flags & chainWithoutRoot) == chainWithoutRoot,
            "Signer/certificate-chain revocation is not enabled for Authenticode and catalog signatures");
        Require((flags & disabled) == 0, "Authenticode revocation was explicitly disabled");

        string unsigned = Path.Combine(Path.GetTempPath(), "audit-unsigned-" + Guid.NewGuid().ToString("N") + ".exe");
        try {
            File.WriteAllText(unsigned, "UNSIGNED-FIXTURE", new UTF8Encoding(false));
            Equal("Unsigned", Call(verifier, null, "GetStatus", unsigned).ToString());
            Equal("Invalid", Call(verifier, null, "GetStatus", unsigned + ".missing").ToString());
            string systemExecutable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            Require(File.Exists(systemExecutable), "Windows signed-executable fixture is unavailable");
            // With unavailable CRL/OCSP transport a signed binary must fail closed
            // as Invalid; a fully verified chain may be Valid. Never downgrade an
            // existing signed binary to the unsigned compatibility branch.
            string signedStatus = Call(verifier, null, "GetStatus", systemExecutable).ToString();
            Require(signedStatus == "Valid" || signedStatus == "Invalid",
                "Catalog/embedded signed Windows executable was classified as Unsigned");
        }
        finally { try { File.Delete(unsigned); } catch { } }
    }
}
