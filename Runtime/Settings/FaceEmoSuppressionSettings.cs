using System;
using System.Collections.Generic;
using UnityEngine;
using nadena.dev.modular_avatar.core;

namespace NativeFTManager
{
    /// <summary>
    /// FaceEmoの抑制の設定（仕様書10.3、M3）
    ///
    /// FE-JsT Bridgeの同名の機能を移したもの。発動条件はJerry'sのパラメーターではなく、
    /// 本製品の公開パラメーター（FaceTracking、EyeTracking、LipTracking）で組む。
    /// 既定は無効で、無効のあいだは抑制のレイヤーも追加パラメーターもFXレイヤーの除去も行わない。
    ///
    /// FaceEmoの止め方は表情制御（表情ロック、まばたき停止、表情の切り替え）に固定する。
    /// FaceEmoごと止めるバイパス方式は持たない。素体の表情レイヤーが表に出て、別の対処が要るためである。
    /// 以前のバージョンが保存したcontrolMethodの値は、Unityが読み込み時に捨てる
    /// </summary>
    [Serializable]
    public class FaceEmoSuppressionSettings
    {
        /// <summary>
        /// 切り替え先の表情番号の既定値。
        /// FaceEmoは表情パターンの先頭から番号を振るため、0は最初の表情パターンのデフォルト表情を指す
        /// </summary>
        public const int DefaultFaceEmoteIndex = 0;

        [Tooltip("Keep FaceEmo from writing expressions while face tracking is active")]
        public bool enabled;

        [Tooltip("Emote number to switch to while face tracking is active. It is the same number the FaceEmo expression select menu writes")]
        [Min(0)]
        public int faceEmoteIndex = DefaultFaceEmoteIndex;

        [Tooltip("Which toggles have to be on for the suppression to take effect")]
        public FaceEmoSuppressionTrigger trigger = FaceEmoSuppressionTrigger.EyeOrLip;

        [Tooltip("Parameters to set while face tracking is active, such as toggles made with MA Menu Item")]
        public List<ExtraParameterEntry> extraParameters = new List<ExtraParameterEntry>();

        [Tooltip("Names of FX layers to remove at build time. The avatar's own assets are not modified")]
        public List<string> removeFxLayers = new List<string>();
    }

    /// <summary>
    /// フェイストラッキング有効中に追加で書き込むパラメーター1件分の設定
    ///
    /// 書き込む先は、MA Menu Itemの参照か、パラメーター名の直接指定で決める。
    /// どちらの指定を使うかはsourceで選び、使わない側のフィールドは無視する。
    /// </summary>
    [Serializable]
    public class ExtraParameterEntry
    {
        [Tooltip("How to specify the parameter to write")]
        public ExtraParameterSource source = ExtraParameterSource.MenuItem;

        [Tooltip("MA Menu Item whose parameter is written. Its parameter name and toggle value are read at build time")]
        public ModularAvatarMenuItem menuItem;

        [Tooltip("State the menu item is switched to while face tracking is active")]
        public ExtraToggleState menuItemState = ExtraToggleState.Off;

        [Tooltip("Name of the parameter to write, as it appears in the FX layer after Modular Avatar renames it")]
        public string parameterName = "";

        [Tooltip("Type of the parameter")]
        public ExtraParameterType parameterType = ExtraParameterType.Bool;

        [Tooltip("Value written while face tracking is active")]
        public float engagedValue;

        [Tooltip("What to do when face tracking is turned off")]
        public ExtraReleaseMode releaseMode = ExtraReleaseMode.Restore;

        [Tooltip("Value written when face tracking is turned off. Used only when Release is Revert")]
        public float releasedValue = 1f;

        [Tooltip("Whether the parameter is synced over the network. Auto reads it from Expression Parameters, MA Parameters and MA Menu Item")]
        public ExtraSyncMode syncMode = ExtraSyncMode.Auto;
    }
}
