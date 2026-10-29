using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using NativeFTManager.Domain;
using NativeFTManager.Infra;

namespace NativeFTManager.UseCase
{
    /// <summary>
    /// インスペクタに出す状態と警告（仕様書6.4の1と2）
    ///
    /// ビルドと同じ判定をシーンのアバターに対して行う。
    /// GeneratorのSetはビルド中にしか存在しないので、Setの有無はGeneratorの設定から見込む
    /// </summary>
    internal static class InspectUseCase
    {
        internal sealed class Report
        {
            public bool HasAvatar;
            public int SetCount;
            public int AssignedKeyCount;
            public bool UseAllTrackingKeys;
            public GeneratorPresence Generator;
            public bool GeneratorSetWriteEnabled;
            public readonly List<Diagnostic> Diagnostics = new List<Diagnostic>();
        }

        public static Report Inspect(NativeFTManagerComponent component)
        {
            var report = new Report();
            var avatarRoot = FindAvatarRoot(component);
            if (avatarRoot == null)
            {
                report.Diagnostics.Add(new Diagnostic(DiagnosticCodes.OutsideAvatar));
                return report;
            }

            report.HasAvatar = true;

            var toggles = ToggleSet.Resolve(component.GetToggle);
            var facts = AvatarScanner.Scan(avatarRoot, component, toggles.Dynamic);
            report.Diagnostics.AddRange(AvatarDiagnostics.Evaluate(facts));

            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            var sets = FaceTrackingDescriptorAccess.ReadSets(descriptor);
            report.SetCount = sets.Count;
            foreach (var set in sets)
            {
                report.AssignedKeyCount += set.AssignedKeyCount;
            }

            report.UseAllTrackingKeys = facts.DescriptorUseAllTrackingKeys;
            report.Generator = facts.Generator;
            report.GeneratorSetWriteEnabled = facts.GeneratorSetWriteEnabled;

            bool generatorWillWrite = facts.Generator == GeneratorPresence.Supported
                                      && (component.generatorIntegration == GeneratorIntegration.Auto
                                          || facts.GeneratorSetWriteEnabled);
            if (sets.Count == 0 && !generatorWillWrite)
            {
                report.Diagnostics.Insert(0, new Diagnostic(DiagnosticCodes.NoSets));
            }

            return report;
        }

        /// <summary>
        /// 親を辿って最も外側のVRCAvatarDescriptorを持つオブジェクト（DuplicateComponentGuardと同じ規則）
        /// </summary>
        private static GameObject FindAvatarRoot(NativeFTManagerComponent component)
        {
            GameObject found = null;
            for (var cursor = component.transform; cursor != null; cursor = cursor.parent)
            {
                if (cursor.GetComponent<VRCAvatarDescriptor>() != null)
                {
                    found = cursor.gameObject;
                }
            }

            return found;
        }
    }
}
