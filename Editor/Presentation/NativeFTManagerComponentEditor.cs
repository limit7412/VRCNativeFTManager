using UnityEditor;
using nadena.dev.ndmf.ui;
using NativeFTManager.Infra;
using static NativeFTManager.Localization;

namespace NativeFTManager.Presentation
{
    /// <summary>
    /// NativeFTManagerComponentのインスペクタ
    ///
    /// 表示文言はNDMFのローカライズ機構から引く。
    /// 状態表示、警告、プリセットなどの並び（仕様書6.4）は、機能を実装するときにここへ足す。
    /// </summary>
    [CustomEditor(typeof(NativeFTManagerComponent))]
    [CanEditMultipleObjects]
    public class NativeFTManagerComponentEditor : Editor
    {
        private void OnEnable()
        {
            // 更新の確認は応答が返った時点で結果が変わる。
            // インスペクタが操作されるまで古い表示のままにしない
            UpdateCheck.ResultChanged += Repaint;
        }

        private void OnDisable()
        {
            UpdateCheck.ResultChanged -= Repaint;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            LanguageSwitcher.DrawImmediate();
            EditorGUILayout.LabelField("Kx VRC NativeFT Manager", EditorStyles.boldLabel);

            // 更新の案内はヘッダーの直下へ置く。読み飛ばされない位置で、設定の並びは崩さない
            UpdateNotice.Draw();

            EditorGUILayout.HelpBox(S("inspector.description"), MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
