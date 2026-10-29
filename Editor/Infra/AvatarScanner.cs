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
                CollectControllerParameterNames(avatarRoot),
                maParameterNames,
                avatarRoot.GetComponentsInChildren<Transform>(true).Select(t => t.name));

            var published = new HashSet<string>(publishedParameters.Select(toggle => toggle.ParameterName));
            facts.ConflictingParameters = maParameterNames.Where(published.Contains).Distinct().ToArray();

            return facts;
        }

        private static IEnumerable<IReadOnlyCollection<string>> CollectControllerParameterNames(GameObject avatarRoot)
        {
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
