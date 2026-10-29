using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;

namespace NativeFTManager.Infra
{
    /// <summary>
    /// DescriptorのFXレイヤーのコントローラを取り出す
    /// </summary>
    internal static class FxControllerLocator
    {
        public static AnimatorController Find(VRCAvatarDescriptor descriptor)
        {
            if (descriptor == null || descriptor.baseAnimationLayers == null)
            {
                return null;
            }

            foreach (var layer in descriptor.baseAnimationLayers)
            {
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX && !layer.isDefault)
                {
                    return layer.animatorController as AnimatorController;
                }
            }

            return null;
        }
    }
}
