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

        /// <summary>既存のParameter BehaviorかState Behaviorが分割対象のSet名を参照している</summary>
        public const string SetReferencedByBehavior = "W-10";

        /// <summary>Custom式の命令数が上限（512）を超えるので、優先度の低い規則を落とした</summary>
        public const string CorrectionInstructionsExceeded = "W-12";

        /// <summary>Expression Parametersの同期bitが上限を超える見込み</summary>
        public const string SyncedBitsExceeded = "W-13";

        /// <summary>対象キーのシェイプがSetに無く、表情補正の規則を生成しなかった</summary>
        public const string CorrectionTargetMissing = "I-01";

        /// <summary>アバターにAvatar Optimizerがあるが、ネイティブFTに対応していない版（1.9.21-beta.1未満）</summary>
        public const string AaoOutdated = "W-14";

        /// <summary>Avatar OptimizerはネイティブFTに対応した版だが、SDKが古く対応が無効（3.10.6-beta.3未満）</summary>
        public const string AaoSdkTooOld = "W-15";

        /// <summary>FaceEmoの抑制の発動条件が、メニューに出さないトグルのせいで決して成り立たない（M3で追加）</summary>
        public const string SuppressionNeverEngaged = "W-16";

        /// <summary>表情制御方式で、FaceEmoのパラメーター名をMA Parametersから解決できない（M3で追加）</summary>
        public const string FaceEmoParametersUnresolved = "W-17";

        /// <summary>アバターにFaceEmoがあるが、FaceEmoの抑制が無効（M3で追加）</summary>
        public const string SuppressionDisabled = "I-03";

        /// <summary>FaceEmoの抑制が有効だが、アバターにFaceEmoが無い（M3で追加）</summary>
        public const string FaceEmoMissing = "I-04";

        /// <summary>切り替えるSetが無いトグルを、メニューと同期パラメーターから外した</summary>
        public const string UnusedToggleRemoved = "I-05";

        /// <summary>ヒエラルキーのメニューに、公開パラメーターを指すMA Menu Itemが1つも無い（仕様書8.3）</summary>
        public const string MenuItemsMissing = "W-18";

        /// <summary>公開パラメーターを指すトグルのMenu Itemで、値が1でない（仕様書8.3）</summary>
        public const string MenuItemValueInvalid = "W-19";

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
                { GeneratorSetSparse, DiagnosticSeverity.Warning },
                { UseAllTrackingKeysOff, DiagnosticSeverity.Warning },
                { EyeLookTooNarrow, DiagnosticSeverity.Warning },
                { JsTBridgePresent, DiagnosticSeverity.Warning },
                { ParameterConflict, DiagnosticSeverity.Warning },
                { SetReferencedByBehavior, DiagnosticSeverity.Warning },
                { CorrectionInstructionsExceeded, DiagnosticSeverity.Warning },
                { SyncedBitsExceeded, DiagnosticSeverity.Warning },
                { CorrectionTargetMissing, DiagnosticSeverity.Information },
                { AaoOutdated, DiagnosticSeverity.Warning },
                { AaoSdkTooOld, DiagnosticSeverity.Warning },
                { SuppressionNeverEngaged, DiagnosticSeverity.Warning },
                { FaceEmoParametersUnresolved, DiagnosticSeverity.Warning },
                { SuppressionDisabled, DiagnosticSeverity.Information },
                { FaceEmoMissing, DiagnosticSeverity.Information },
                { UnusedToggleRemoved, DiagnosticSeverity.Information },
                { MenuItemsMissing, DiagnosticSeverity.Warning },
                { MenuItemValueInvalid, DiagnosticSeverity.Warning },
            };

        public static IEnumerable<string> All => Severities.Keys;

        public static DiagnosticSeverity GetSeverity(string code)
        {
            return Severities.TryGetValue(code, out var severity) ? severity : DiagnosticSeverity.Warning;
        }
    }
}
