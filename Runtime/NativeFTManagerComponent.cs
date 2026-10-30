using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDKBase;

namespace NativeFTManager
{
    /// <summary>
    /// ネイティブフェイストラッキングのトグルと表情補正をアバターへ組み込むコンポーネント
    ///
    /// 保持するのは設定値だけで、実際の生成はNDMFのビルド中に行う。
    /// コンポーネント自体はビルド中に取り除かれる。
    ///
    /// 設定項目は仕様書の6.2に沿って、機能を実装するときに追加する。
    /// 新しく足すフィールドは初期化子を「既存の挙動を変えない値」にし、
    /// 追加したてのコンポーネントへ入れたい既定値はResetで入れる。
    /// Unityはシリアライズデータに無いフィールドを初期化子の値のまま残すため、
    /// 初期化子に新しい既定を書くと、更新しただけで保存済みのアバターの挙動が変わる。
    /// M1のフィールドは、それより前に設定を持つ版を公開していないので、初期化子に既定値を書いている。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("KxVRCNativeFTManager/Kx VRC NativeFT Manager")]
    public class NativeFTManagerComponent : MonoBehaviour, IEditorOnly
    {
        /// <summary>Lerp Speedの上限（仕様書5.3）</summary>
        public const float MaxLerpSpeed = 20f;

        [Tooltip("Preset of the settings")]
        public NativeFTPreset preset = NativeFTPreset.UE;

        // メニューの作り方（仕様書8.3）。以前に保存したコンポーネントの挙動を変えないよう、初期化子はGeneratedにする。
        // 右クリックからの生成と「メニューをヒエラルキーに展開」ボタンがHierarchyにする

        [Tooltip("How the Expressions menu is made. Hierarchy reads the MA Menu Items placed in the hierarchy")]
        public NativeFTMenuMode menuMode = NativeFTMenuMode.Generated;

        [Tooltip("Toggles in the Face Tracking menu (used when the menu is generated at build time)")]
        public List<ToggleSettings> toggles = ToggleSettings.CreateDefaults();

        [Tooltip("Speed at which the Blendshapes Set weight follows a toggle (0 switches instantly)")]
        [Range(0f, MaxLerpSpeed)]
        public float lerpSpeed = 8f;

        [Tooltip("Names of Blendshapes Sets that are kept as they are instead of being split into groups")]
        public List<string> excludedSetNames = new List<string>();

        [Tooltip("Enable Use All Tracking Keys on the Avatar Descriptor at build time")]
        public bool setUseAllTrackingKeys = true;

        [Tooltip("Viseme Dampen written to the Avatar Descriptor at build time")]
        public VisemeDampenOverride visemeDampenOverride = VisemeDampenOverride.DoNotChange;

        [Tooltip("Viseme Dampen Amount written together with the mode")]
        [Range(0f, 1f)]
        public float visemeDampenAmount = 0.5f;

        // 0.1.0-test10までは列挙体GeneratorIntegration（Off=0、Auto=1）で保存していた。
        // シリアライズ上はどちらも0と1なので、旧名から読めばOffはfalse、Autoはtrueになる
        [Tooltip("Enable the Blendshapes Set output of the Kx VRC ARKit BlendShape Generator component on the avatar at build time")]
        [FormerlySerializedAs("generatorIntegration")]
        public bool useGeneratorComponent = true;

        // 表情補正（仕様書5.4、M2）。M1で保存したコンポーネントの挙動を変えないよう、初期化子は無効にしておく。
        // 追加したてのコンポーネントとプレハブでは、プリセットの値をResetで入れる

        [Tooltip("Generate the face tracking emulation (Parameter Behaviors ported from Jerry's Templates)")]
        public bool enableEmulation;

        [Tooltip("Generate the face tracking limits (Parameter Behaviors ported from Jerry's Templates)")]
        public bool enableLimits;

        [Tooltip("Generate Eye Sync, which closes both eyelids together")]
        public bool enableEyeSync;

        [Tooltip("Ids of the emulation and limit rules that are not generated")]
        public List<string> excludedRules = new List<string>();

        // FaceEmoの抑制（仕様書10.3、M3）。既定は無効で、M2までに保存したコンポーネントの挙動を変えない

        [Tooltip("Keep FaceEmo from writing expressions while face tracking is active")]
        public FaceEmoSuppressionSettings faceEmoSuppression = new FaceEmoSuppressionSettings();

        // 感度（仕様書5.6、M4）。既定はどの系統も作らず、M3までに保存したコンポーネントの挙動を変えない

        [Tooltip("Radials in the Face Tracking menu that change the strength of the eye and mouth tracking")]
        public SensitivitySettings sensitivity = new SensitivitySettings();

        /// <summary>
        /// 指定したトグルの設定。リストに無いトグルは、メニューに出さずM1までの挙動を変えない値として扱う
        /// </summary>
        public ToggleSettings GetToggle(NativeFTToggle toggle)
        {
            if (toggles != null)
            {
                foreach (var settings in toggles)
                {
                    if (settings != null && settings.toggle == toggle)
                    {
                        return settings;
                    }
                }
            }

            return ToggleSettings.CreateMissing(toggle);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editorアセンブリ側から差し込まれるOnValidateフック
        /// （同一アバター内の重複コンポーネント排除に使用）
        /// </summary>
        internal static Action<NativeFTManagerComponent> EditorOnValidateHook;
#endif

        private void Reset()
        {
            NativeFTPresets.Apply(this, preset);
        }

        private void OnValidate()
        {
            lerpSpeed = Mathf.Clamp(lerpSpeed, 0f, MaxLerpSpeed);
            if (sensitivity != null)
            {
                sensitivity.maxMultiplier = Mathf.Clamp(
                    sensitivity.maxMultiplier,
                    SensitivitySettings.MinMaxMultiplier,
                    SensitivitySettings.MaxMaxMultiplier);
            }

#if UNITY_EDITOR
            EditorOnValidateHook?.Invoke(this);
#endif
        }
    }
}
