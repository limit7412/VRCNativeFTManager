namespace NativeFTManager.Domain
{
    /// <summary>
    /// Descriptorに1つもSetが無いときに、メッシュのシェイプ名から作る分割前のSet（仕様書5.1）
    ///
    /// アバターにKx VRC ARKit BlendShape Generatorのコンポーネントが無くても、Generatorがプロジェクトにあれば、
    /// Generatorの対応表で元からあるARKit名（またはUE名）のシェイプをキーへ割り当てられる。
    /// 作ったSetはほかのSetと同じく、この後でグループごとに分割する
    /// </summary>
    internal static class ShapeMappedSet
    {
        /// <summary>
        /// 分割前のSetの名前。分割で別の名前に置き換わるので、ビルドの成果物には残らない。
        /// 本製品の接頭辞（KxNFTM/）を付けると分割済みと見なされて分割されないので、付けない
        /// </summary>
        public const string SetName = "Kx VRC NativeFT Manager";

        /// <summary>
        /// キーの番号を添字にしたシェイプ名からSetを作る。
        /// 配列の長さがキーの数と合わないとき、または割り当てが1つも無いときはnull
        /// </summary>
        public static FaceTrackingSetData Create(string[] blendShapeNames)
        {
            if (blendShapeNames == null || blendShapeNames.Length != FaceTrackingGroupTable.KeyCount)
            {
                return null;
            }

            var set = FaceTrackingSetData.CreateEmpty(SetName, 1f);
            for (int i = 0; i < blendShapeNames.Length; i++)
            {
                set.BlendShapeNames[i] = blendShapeNames[i] ?? string.Empty;
            }

            return set.AssignedKeyCount > 0 ? set : null;
        }
    }
}
