using System;
using System.IO;
using System.Reflection;

internal static partial class AuditRegression {
    private static string StaticString(Type type, string name) {
        PropertyInfo property = type.GetProperty(name, All);
        Require(property != null, "Missing static string property " + type.FullName + "." + name);
        return (string)property.GetValue(null, null);
    }

    private static string ConstantString(Type type, string name) {
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Require(field != null, "Missing static string field " + type.FullName + "." + name);
        return (string)field.GetValue(null);
    }

    private static void MainLanguageRejectsInvalidCompositeFormatsPerKey() {
        Type language = T("youtube_dl_gui.Language");
        Type english = T("youtube_dl_gui.Language+InternalEnglish");
        string path = Path.Combine(Environment.CurrentDirectory, "format-validation-main-" + Guid.NewGuid().ToString("N") + ".ini");
        try {
            File.WriteAllText(path,
                "[Audit]\r\n" +
                "GenericAltError = invalid {1}\r\n" +
                "dlgUpdateNoUpdateAvailable = {1} / {0}\r\n" +
                "lbAboutBody = {{literal}} {0} {1}\r\n");

            Equal(true, Call(language, null, "LoadLanguage", path));
            Equal(ConstantString(english, "GenericAltError"), StaticString(language, "GenericAltError"));
            Equal("{1} / {0}", StaticString(language, "dlgUpdateNoUpdateAvailable"));
            Equal("{{literal}} {0} {1}", StaticString(language, "lbAboutBody"));
        }
        finally {
            Call(language, null, "LoadInternalEnglish");
            try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static void UpdaterLanguageRejectsInvalidCompositeFormatsPerKey() {
        Assembly updater = LoadUpdaterAssembly();
        Type language = updater.GetType("youtube_dl_gui_updater.Language", true);
        Type english = updater.GetType("youtube_dl_gui_updater.Language+InternalEnglish", true);
        MethodInfo load = language.GetMethod("LoadLanguage", All);
        MethodInfo reset = language.GetMethod("LoadInternalEnglish", All);
        string invalidPath = Path.Combine(Environment.CurrentDirectory, "format-validation-updater-invalid-" + Guid.NewGuid().ToString("N") + ".ini");
        string validPath = Path.Combine(Environment.CurrentDirectory, "format-validation-updater-valid-" + Guid.NewGuid().ToString("N") + ".ini");
        try {
            File.WriteAllText(invalidPath,
                "[Audit]\r\n" +
                "dlgUpdaterUpdatedVersionHashNoMatch = invalid {2}\r\n");
            Equal(true, load.Invoke(null, new object[] { invalidPath }));
            Equal(ConstantString(english, "dlgUpdaterUpdatedVersionHashNoMatch"),
                StaticString(language, "dlgUpdaterUpdatedVersionHashNoMatch"));

            File.WriteAllText(validPath,
                "[Audit]\r\n" +
                "dlgUpdaterUpdatedVersionHashNoMatch = calculated={1}; expected={0}; {{ok}}\r\n");
            Equal(true, load.Invoke(null, new object[] { validPath }));
            Equal("calculated={1}; expected={0}; {{ok}}", StaticString(language, "dlgUpdaterUpdatedVersionHashNoMatch"));
        }
        finally {
            reset.Invoke(null, null);
            try { File.Delete(invalidPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            try { File.Delete(validPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static void RunRepairWave9Tests() {
        Test("CURRENT_O035.MainLanguageCompositeFormatsAreValidated", MainLanguageRejectsInvalidCompositeFormatsPerKey);
        Test("CURRENT_O035.UpdaterLanguageCompositeFormatsAreValidated", UpdaterLanguageRejectsInvalidCompositeFormatsPerKey);
        RunRepairWave10Tests();
    }
}
