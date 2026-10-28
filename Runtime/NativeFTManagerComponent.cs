using System;
using UnityEngine;
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
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("KxVRCNativeFTManager/Kx VRC NativeFT Manager")]
    public class NativeFTManagerComponent : MonoBehaviour, IEditorOnly
    {
#if UNITY_EDITOR
        /// <summary>
        /// Editorアセンブリ側から差し込まれるOnValidateフック
        /// （同一アバター内の重複コンポーネント排除に使用）
        /// </summary>
        internal static Action<NativeFTManagerComponent> EditorOnValidateHook;
#endif

        private void OnValidate()
        {
#if UNITY_EDITOR
            EditorOnValidateHook?.Invoke(this);
#endif
        }
    }
}
