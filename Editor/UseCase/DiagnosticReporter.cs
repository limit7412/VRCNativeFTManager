using System.Collections.Generic;
using nadena.dev.ndmf;
using NativeFTManager.Domain;

namespace NativeFTManager.UseCase
{
    /// <summary>
    /// 警告とエラーをNDMFのErrorReportへ出す
    /// </summary>
    internal static class DiagnosticReporter
    {
        public static void Report(Diagnostic diagnostic, params object[] objects)
        {
            var args = new List<object>(diagnostic.Args);
            args.AddRange(objects);
            ErrorReport.ReportError(
                Localization.Localizer,
                ToErrorSeverity(diagnostic.Severity),
                diagnostic.LocalizationKey,
                args.ToArray());
        }

        public static void Report(IEnumerable<Diagnostic> diagnostics, params object[] objects)
        {
            foreach (var diagnostic in diagnostics)
            {
                Report(diagnostic, objects);
            }
        }

        private static ErrorSeverity ToErrorSeverity(DiagnosticSeverity severity)
        {
            switch (severity)
            {
                case DiagnosticSeverity.Error:
                    return ErrorSeverity.Error;
                case DiagnosticSeverity.Information:
                    return ErrorSeverity.Information;
                default:
                    // NDMFに警告の区分は無く、ビルドを止めないものはNonFatalで出す
                    return ErrorSeverity.NonFatal;
            }
        }
    }
}
