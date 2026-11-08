using System;
using System.IO;

internal static partial class AuditRegression {
    private static void RedditUserscriptDistributionCannotSwitchToMutableUpstream() {
        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(App.Location), "..", "..", ".."));
        string script = File.ReadAllText(Path.Combine(root, "Addons", "reddit video download button.user.js"));
        string index = File.ReadAllText(Path.Combine(root, "USERSCRIPTS.md"));
        const string pinned = "a1b3109e41d06af733c58e330a6e43e884ce5060";
        const string url = "https://raw.githubusercontent.com/Proponent-8247/youtube-dl-gui/"
            + pinned + "/Addons/reddit%20video%20download%20button.user.js";

        Require(index.Contains(url), "Userscript index is not pinned to this fork's reviewed Git object");
        Require(!script.Contains("@updateURL") && !script.Contains("@downloadURL"),
            "Userscript metadata allows an uncontrolled code auto-update");
        Require(!script.Contains("github.com/murrty/youtube-dl-gui"),
            "Userscript metadata links to unrelated mutable code");
        Require(script.Contains("@match       http://*.reddit.com/r/*/comments/*") &&
            script.Contains("@match       https://*.reddit.com/r/*/comments/*") &&
            !script.Contains("@match http*://*.reddit.com/r/*"),
            "Audited Reddit permalink-only restriction was replaced");

        int exit;
        string checkedIn = GuardRunCli("git.exe",
            "show " + pinned + ":\"Addons/reddit video download button.user.js\"",
            root, out exit);
        Require(exit == 0,
            "Immutable userscript revision is missing from repository ancestry: " + checkedIn);
        Equal(script.Replace("\r\n", "\n").Trim(),
            checkedIn.Replace("\r\n", "\n").Trim());
    }
}
