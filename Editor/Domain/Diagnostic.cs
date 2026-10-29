using System.Collections.Generic;

namespace NativeFTManager.Domain
{
    internal enum DiagnosticSeverity
    {
        /// <summary>ビルドを止める</summary>
        Error,

        /// <summary>本製品の処理の一部を飛ばし、ビルドは続ける</summary>
        NonFatal,

        Warning,

        Information,
    }

    /// <summary>
    /// 警告とエラー1件（仕様書12章）
    ///
    /// ビルド時はNDMFのErrorReportへ、インスペクタではHelpBoxへ、同じ文言で出す。
    /// 文言のキーは <c>diag.&lt;コード&gt;</c> と、その <c>:description</c>
    /// </summary>
    internal sealed class Diagnostic
    {
        public Diagnostic(string code, params object[] args)
        {
            Code = code;
            Args = args ?? new object[0];
        }

        public string Code { get; }

        public object[] Args { get; }

        public DiagnosticSeverity Severity => DiagnosticCodes.GetSeverity(Code);

        public string LocalizationKey => "diag." + Code;
    }

    /// <summary>
    /// 警告とエラーのコードと重さ。番号は仕様書12章の表に合わせる
    /// </summary>
    internal static class DiagnosticCodes
    {
        /// <summary>ManagerがVRCAvatarDescriptorを持つアバターの外にある</summary>
        public const string OutsideAvatar = "E-02";

        /// <summary>分割の時点でDescriptorにSetが1つも無い</summary>
        public const string NoSets = "E-03";

        /// <summary>Jerry's Face Tracking Templatesが残っている</summary>
        public const string JerrysDetected = "E-04";

        /// <summary>SDKにFace Tracking BlendshapesのState Behaviorが見つからない（M1で追加）</summary>
        public const string StateBehaviorUnavailable = "E-05";

        /// <summary>SDKのFaceTrackingKeyが本製品の対応表と食い違う（M1で追加）</summary>
        public const string KeyMismatch = "E-06";

        /// <summary>ARKitプリセットでGeneratorがアバターに無い</summary>
        public const string GeneratorMissing = "W-01";

        /// <summary>連携がOffで、GeneratorのSet書き込みもオフ</summary>
        public const string GeneratorSetWriteOff = "W-02";

        /// <summary>Generatorはあるが、連携に対応した版ではない</summary>
        public const string GeneratorUnsupported = "W-03";

        /// <summary>GeneratorのSetが空、または割り当てが少ない</summary>
        public const string GeneratorSetSparse = "W-04";

        /// <summary>Use All Tracking Keysを変更しない設定で、値がfalse</summary>
        public const string UseAllTrackingKeysOff = "W-05";

        /// <summary>Eye Lookの回転幅が狭い</summary>
        public const string EyeLookTooNarrow = "W-07";

        /// <summary>アバターにFE-JsT Bridgeがある</summary>
        public const string JsTBridgePresent = "W-08";

        /// <summary>公開パラメーターと同名のパラメーターが他のMA Parametersにある</summary>
        public const string ParameterConflict = "W-09";

        /// <summary>既存のParameter Behaviorが分割対象のSet名を参照している</summary>
        public const string SetReferencedByBehavior = "W-10";

        /// <summary>W-04の判定に使う、割り当てキー数の下限</summary>
        public const int GeneratorSetMinimumKeys = 10;

        private static readonly Dictionary<string, DiagnosticSeverity> Severities =
            new Dictionary<string, DiagnosticSeverity>
            {
                { OutsideAvatar, DiagnosticSeverity.Error },
                { NoSets, DiagnosticSeverity.NonFatal },
                { JerrysDetected, DiagnosticSeverity.NonFatal },
                { StateBehaviorUnavailable, DiagnosticSeverity.NonFatal },
                { KeyMismatch, DiagnosticSeverity.NonFatal },
                { GeneratorMissing, DiagnosticSeverity.Warning },
                { GeneratorSetWriteOff, DiagnosticSeverity.Warning },
                { GeneratorUnsupported, DiagnosticSeverity.Warning },
                { GeneratorSetSparse, DiagnosticSeverity.Warning },
                { UseAllTrackingKeysOff, DiagnosticSeverity.Warning },
                { EyeLookTooNarrow, DiagnosticSeverity.Warning },
                { JsTBridgePresent, DiagnosticSeverity.Warning },
                { ParameterConflict, DiagnosticSeverity.Warning },
                { SetReferencedByBehavior, DiagnosticSeverity.Warning },
            };

        public static IEnumerable<string> All => Severities.Keys;

        public static DiagnosticSeverity GetSeverity(string code)
        {
            return Severities.TryGetValue(code, out var severity) ? severity : DiagnosticSeverity.Warning;
        }
    }
}
