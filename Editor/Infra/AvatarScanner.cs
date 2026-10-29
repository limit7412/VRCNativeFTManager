using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using nadena.dev.modular_avatar.core;
using NativeFTManager.Domain;

namespace NativeFTManager.Infra
{
    /// <summary>
    /// 警告の判定に使う事実をアバターから集める
    ///
    /// 非アクティブなオブジェクトも走査対象に含める。導入直後にオフのまま置かれたプレハブを
    /// 未導入と誤検出しないため
    /// </summary>
    internal static class AvatarScanner
    {
        /// <summary>FE-JsT Bridgeのコンポーネントの型名。型は参照しない（仕様書10.2）</summary>
        private const string JsTBridgeComponentType = "FEJsTBridge.FEJsTBridgeComponent";

        /// <summary>
        /// Generatorのコンポーネントの型名。連携用アセンブリが無いときに、Generatorの有無だけを判定するのに使う
        /// </summary>
        private const string GeneratorComponentType = "ARKitBlendShapeGenerator.ARKitBlendShapeGeneratorComponent";

        /// <summary>Avatar Optimizerのコンポーネントの名前空間。型は参照しない</summary>
        private const string AaoNamespace = "Anatawa12.AvatarOptimizer";

        public static AvatarFacts Scan(
            GameObject avatarRoot,
            NativeFTManagerComponent component,
            IReadOnlyList<ResolvedToggle> publishedParameters)
        {
            var descriptor = avatarRoot != null ? avatarRoot.GetComponent<VRCAvatarDescriptor>() : null;
            var facts = new AvatarFacts
            {
                Preset = component.preset,
                Integration = component.generatorIntegration,
                SetUseAllTrackingKeys = component.setUseAllTrackingKeys,
                DescriptorUseAllTrackingKeys = FaceTrackingDescriptorAccess.GetUseAllTrackingKeys(descriptor),
                EyeLookAngles = FaceTrackingDescriptorAccess.GetEyeLookAngles(descriptor),
            };

            if (avatarRoot == null)
            {
                return facts;
            }

            var behaviours = avatarRoot.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b != null).ToArray();

            facts.HasJsTBridge = behaviours.Any(b => b.GetType().FullName == JsTBridgeComponentType);

            if (behaviours.Any(b => IsInNamespace(b.GetType(), AaoNamespace)))
            {
                facts.Aao = GetAaoSupport();
            }

            var integration = GeneratorIntegrationRegistry.Current;
            if (integration != null && integration.IsPresent(avatarRoot))
            {
                facts.Generator = GeneratorPresence.Supported;
                facts.GeneratorSetWriteEnabled = integration.IsSetWriteEnabled(avatarRoot);
            }
            else if (behaviours.Any(b => b.GetType().FullName == GeneratorComponentType))
            {
                facts.Generator = GeneratorPresence.Unsupported;
            }

            var ownHolders = avatarRoot.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == GeneratedObjectsInstaller.HolderObjectName)
                .ToArray();
            var maParameterNames = avatarRoot.GetComponentsInChildren<ModularAvatarParameters>(true)
                .Where(p => p != null && !ownHolders.Contains(p.transform))
                .SelectMany(p => p.parameters.Select(config => config.nameOrPrefix))
                .Where(name => !string.IsNullOrEmpty(name))
                .ToArray();

            facts.JerrysEvidence = JerrysDetection.Detect(
                CollectControllerParameterNames(avatarRoot, descriptor),
                maParameterNames,
                avatarRoot.GetComponentsInChildren<Transform>(true).Select(t => t.name));

            var published = new HashSet<string>(publishedParameters.Select(toggle => toggle.ParameterName));
            facts.ConflictingParameters = maParameterNames.Where(published.Contains).Distinct().ToArray();

            facts.PublishedSyncedBits = publishedParameters.Count;
            facts.OtherSyncedBits = EstimateOtherSyncedBits(avatarRoot, descriptor, ownHolders, published);

            return facts;
        }

        /// <summary>
        /// 本製品以外の同期パラメーターのbit数の見込み（W-13）。
        /// DescriptorのExpression Parametersと、ほかのMA Parametersの同期するものを名前で重複を除いて数える。
        /// MAの自動リネームや、他のNDMFプラグインがビルド中に足すものは数えられないので目安である
        /// </summary>
        private static int EstimateOtherSyncedBits(
            GameObject avatarRoot,
            VRCAvatarDescriptor descriptor,
            Transform[] ownHolders,
            HashSet<string> published)
        {
            var bits = new Dictionary<string, int>();

            var expressionParameters = descriptor != null ? descriptor.expressionParameters : null;
            if (expressionParameters != null && expressionParameters.parameters != null)
            {
                foreach (var parameter in expressionParameters.parameters)
                {
                    if (parameter == null || string.IsNullOrEmpty(parameter.name) || !parameter.networkSynced)
                    {
                        continue;
                    }

                    bits[parameter.name] = parameter.valueType == VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Bool ? 1 : 8;
                }
            }

            foreach (var component in avatarRoot.GetComponentsInChildren<ModularAvatarParameters>(true))
            {
                if (component == null || ownHolders.Contains(component.transform))
                {
                    continue;
                }

                foreach (var config in component.parameters)
                {
                    if (string.IsNullOrEmpty(config.nameOrPrefix) || config.isPrefix || config.localOnly
                        || config.syncType == ParameterSyncType.NotSynced)
                    {
                        continue;
                    }

                    // 内部パラメーターはコンポーネントごとに別名へ変わるので、名前が同じでも別に数える
                    var key = config.internalParameter
                        ? config.nameOrPrefix + "#" + component.GetInstanceID()
                        : config.nameOrPrefix;
                    bits[key] = config.syncType == ParameterSyncType.Bool ? 1 : 8;
                }
            }

            return bits.Where(entry => !published.Contains(entry.Key)).Sum(entry => entry.Value);
        }

        /// <summary>
        /// AAOの版とSDKの版から、AAOのネイティブFT対応が効くかを決める。
        /// 版はasmdefのversionDefinesで判定する（仕様書11章）
        /// </summary>
        private static AaoPresence GetAaoSupport()
        {
#if !KX_AAO_NATIVE_FT
            return AaoPresence.Outdated;
#elif !KX_VRC_SDK_AAO_FT
            return AaoPresence.SdkTooOld;
#else
            return AaoPresence.Supported;
#endif
        }

        private static bool IsInNamespace(System.Type type, string name)
        {
            var ns = type.Namespace;
            return ns != null && (ns == name || ns.StartsWith(name + ".", System.StringComparison.Ordinal));
        }

        /// <summary>
        /// Merge Animatorのコントローラに加え、Descriptorに直接設定されたコントローラも集める。
        /// Jerry'sをDescriptorのFXへ直接入れる導入方法もあるため
        /// </summary>
        private static IEnumerable<IReadOnlyCollection<string>> CollectControllerParameterNames(
            GameObject avatarRoot,
            VRCAvatarDescriptor descriptor)
        {
            if (descriptor != null && descriptor.customizeAnimationLayers && descriptor.baseAnimationLayers != null)
            {
                foreach (var layer in descriptor.baseAnimationLayers)
                {
                    var controller = layer.isDefault ? null : Resolve(layer.animatorController);
                    if (controller != null)
                    {
                        yield return controller.parameters.Select(parameter => parameter.name).ToArray();
                    }
                }
            }

            foreach (var mergeAnimator in avatarRoot.GetComponentsInChildren<ModularAvatarMergeAnimator>(true))
            {
                var controller = Resolve(mergeAnimator != null ? mergeAnimator.animator : null);
                if (controller != null)
                {
                    yield return controller.parameters.Select(parameter => parameter.name).ToArray();
                }
            }
        }

        /// <summary>Override Controllerは元のコントローラまで辿る</summary>
        private static AnimatorController Resolve(RuntimeAnimatorController runtimeController)
        {
            var visited = new HashSet<RuntimeAnimatorController>();
            while (runtimeController != null && visited.Add(runtimeController))
            {
                if (runtimeController is AnimatorController controller)
                {
                    return controller;
                }

                if (runtimeController is AnimatorOverrideController overrideController)
                {
                    runtimeController = overrideController.runtimeAnimatorController;
                    continue;
                }

                return null;
            }

            return null;
        }
    }
}
