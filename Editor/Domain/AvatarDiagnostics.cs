using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// アバターにあるGeneratorの状態
    /// </summary>
    internal enum GeneratorPresence
    {
        /// <summary>アバターに無い</summary>
        None,

        /// <summary>あるが、連携用のアセンブリがコンパイルされていない（版が古い、または新しすぎる）</summary>
        Unsupported,

        /// <summary>あり、連携できる</summary>
        Supported,
    }

    /// <summary>
    /// アバターにあるAvatar Optimizerの状態
    ///
    /// AAOは1.9.21-beta.1でネイティブFTに対応し、SetとParameter Behaviorsの対象シェイプを保護する。
    /// ただし対応が有効になるのはSDK 3.10.6-beta.3以降に限られる
    /// </summary>
    internal enum AaoPresence
    {
        /// <summary>アバターに無い</summary>
        None,

        /// <summary>あるが、ネイティブFTに対応していない版</summary>
        Outdated,

        /// <summary>対応した版だが、SDKが古く対応が無効</summary>
        SdkTooOld,

        /// <summary>対応した版で、SDKも対応範囲にある</summary>
        Supported,
    }

    /// <summary>
    /// 警告の判定に使う、アバターから読み取った事実
    ///
    /// ビルドとインスペクタの両方で同じ判定をするため、Unityの型を持たない形に写してから判定する
    /// </summary>
    internal sealed class AvatarFacts
    {
        public NativeFTPreset Preset;
        public GeneratorIntegration Integration;
        public GeneratorPresence Generator;
        public bool GeneratorSetWriteEnabled;
        public IReadOnlyList<string> JerrysEvidence = new string[0];
        public bool HasJsTBridge;

        /// <summary>アバターにFaceEmoがあるか（Merge AnimatorのコントローラがCN_FORCE_BYPASS_ENABLEを宣言している）</summary>
        public bool HasFaceEmo;

        /// <summary>コンポーネントでFaceEmoの抑制が有効か</summary>
        public bool FaceEmoSuppressionEnabled;

        /// <summary>FaceEmoの抑制の発動条件が、メニューに出さないトグルのせいで決して成り立たないか</summary>
        public bool FaceEmoSuppressionNeverEngaged;
        public AaoPresence Aao;
        public bool SetUseAllTrackingKeys;
        public bool DescriptorUseAllTrackingKeys;

        /// <summary>Eye Lookの上下左右の回転角（度）。Eye Lookが無効ならnull</summary>
        public IReadOnlyList<float> EyeLookAngles;

        /// <summary>公開パラメーターと同名で、他のMA Parametersにあるもの</summary>
        public IReadOnlyList<string> ConflictingParameters = new string[0];

        /// <summary>本製品が作る同期パラメーターのbit数</summary>
        public int PublishedSyncedBits;

        /// <summary>本製品以外の同期パラメーターのbit数の見込み（DescriptorのExpression ParametersとMA Parameters）</summary>
        public int OtherSyncedBits;
    }

    /// <summary>
    /// アバターの事実から警告を決める（仕様書12章のうち、Setの中身に依らないもの）
    /// </summary>
    internal static class AvatarDiagnostics
    {
        public static IReadOnlyList<Diagnostic> Evaluate(AvatarFacts facts)
        {
            var diagnostics = new List<Diagnostic>();

            if (facts.JerrysEvidence != null && facts.JerrysEvidence.Count > 0)
            {
                diagnostics.Add(new Diagnostic(DiagnosticCodes.JerrysDetected, string.Join(", ", facts.JerrysEvidence)));
            }

            if (facts.HasJsTBridge)
            {
                diagnostics.Add(new Diagnostic(DiagnosticCodes.JsTBridgePresent));
            }

            if (facts.FaceEmoSuppressionEnabled)
            {
                if (!facts.HasFaceEmo)
                {
                    diagnostics.Add(new Diagnostic(DiagnosticCodes.FaceEmoMissing));
                }

                if (facts.FaceEmoSuppressionNeverEngaged)
                {
                    diagnostics.Add(new Diagnostic(DiagnosticCodes.SuppressionNeverEngaged));
                }
            }
            else if (facts.HasFaceEmo)
            {
                diagnostics.Add(new Diagnostic(DiagnosticCodes.SuppressionDisabled));
            }

            switch (facts.Generator)
            {
                case GeneratorPresence.None:
                    if (facts.Preset == NativeFTPreset.ARKit)
                    {
                        diagnostics.Add(new Diagnostic(DiagnosticCodes.GeneratorMissing));
                    }

                    break;
                case GeneratorPresence.Unsupported:
                    diagnostics.Add(new Diagnostic(DiagnosticCodes.GeneratorUnsupported));
                    break;
                case GeneratorPresence.Supported:
                    if (facts.Integration == GeneratorIntegration.Off && !facts.GeneratorSetWriteEnabled)
                    {
                        diagnostics.Add(new Diagnostic(DiagnosticCodes.GeneratorSetWriteOff));
                    }

                    break;
            }

            switch (facts.Aao)
            {
                case AaoPresence.Outdated:
                    diagnostics.Add(new Diagnostic(DiagnosticCodes.AaoOutdated));
                    break;
                case AaoPresence.SdkTooOld:
                    diagnostics.Add(new Diagnostic(DiagnosticCodes.AaoSdkTooOld));
                    break;
            }

            if (!facts.SetUseAllTrackingKeys && !facts.DescriptorUseAllTrackingKeys)
            {
                diagnostics.Add(new Diagnostic(DiagnosticCodes.UseAllTrackingKeysOff));
            }

            if (facts.EyeLookAngles != null && EyeLookRange.IsTooNarrow(facts.EyeLookAngles))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticCodes.EyeLookTooNarrow,
                    facts.EyeLookAngles.Min().ToString("0.#")));
            }

            if (facts.ConflictingParameters != null && facts.ConflictingParameters.Count > 0)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticCodes.ParameterConflict,
                    string.Join(", ", facts.ConflictingParameters)));
            }

            int totalBits = facts.PublishedSyncedBits + facts.OtherSyncedBits;
            if (facts.PublishedSyncedBits > 0 && totalBits > BuildEstimate.SyncedBitLimit)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticCodes.SyncedBitsExceeded,
                    totalBits,
                    BuildEstimate.SyncedBitLimit,
                    facts.PublishedSyncedBits));
            }

            return diagnostics;
        }

        /// <summary>
        /// GeneratorがDescriptorへ書いたSetの割り当てが少なすぎるか（W-04）。
        /// Setが無ければnullを渡す
        /// </summary>
        public static bool IsGeneratorSetSparse(FaceTrackingSetData generatorSet)
        {
            return generatorSet == null || generatorSet.AssignedKeyCount < DiagnosticCodes.GeneratorSetMinimumKeys;
        }
    }
}
