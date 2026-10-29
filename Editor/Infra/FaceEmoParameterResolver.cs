using System.Collections.Generic;
using UnityEngine;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using NativeFTManager.Domain;

namespace NativeFTManager.Infra
{
    /// <summary>
    /// アバターに載っているMA Parametersから、FaceEmoのパラメーターのリネームを読み取る
    /// </summary>
    internal static class FaceEmoParameterResolver
    {
        /// <summary>
        /// MA Parametersの登録内容を読み、FaceEmoのパラメーター名を解決する
        /// </summary>
        public static FaceEmoParameterNames Resolve(BuildContext context)
        {
            return FaceEmoParameterNames.Resolve(
                CollectParameterRemaps(ParameterInfo.ForContext(context), context.AvatarRootObject));
        }

        /// <summary>
        /// コンポーネントごとに、元の名前から最終的な名前への対応を作る
        /// </summary>
        /// <remarks>
        /// 最終的な名前は、コンポーネント自身と祖先のMA Parametersによるリネームをすべて当てたものである。
        /// FaceEmoの登録でA→Bとし、祖先でB→Cとしていれば、FaceEmoが使うのはCになる。
        /// MAと同じNDMFのParameterInfoで引くため、MAがあとで振る名前と一致する。
        ///
        /// 非アクティブなオブジェクトも対象に含める。環境検出と同じく、
        /// 導入直後にオフのまま置かれたプレハブを見落とさないためである。
        /// </remarks>
        internal static IReadOnlyList<IReadOnlyDictionary<string, string>> CollectParameterRemaps(
            ParameterInfo parameterInfo, GameObject avatarRoot)
        {
            var remaps = new List<IReadOnlyDictionary<string, string>>();
            if (avatarRoot == null)
            {
                return remaps;
            }

            foreach (var component in avatarRoot.GetComponentsInChildren<ModularAvatarParameters>(true))
            {
                if (component == null || component.parameters == null)
                {
                    continue;
                }

                var remap = new Dictionary<string, string>();
                var remappings = parameterInfo.GetParameterRemappingsAt(component, true);

                foreach (var config in component.parameters)
                {
                    if (string.IsNullOrEmpty(config.nameOrPrefix))
                    {
                        continue;
                    }

                    // 前置き一致の登録は名前そのものの対応を持たない。
                    // 内部パラメーターはMAがビルド時に固有の名前へ変えるため、外からは接続できない。
                    // どちらも読み飛ばし、解決できなかったものとして扱う
                    if (config.isPrefix || config.internalParameter)
                    {
                        continue;
                    }

                    remap[config.nameOrPrefix] =
                        remappings.TryGetValue((ParameterNamespace.Animator, config.nameOrPrefix), out var mapping)
                            ? mapping.ParameterName
                            : config.nameOrPrefix;
                }

                if (remap.Count > 0)
                {
                    remaps.Add(remap);
                }
            }

            return remaps;
        }
    }
}
