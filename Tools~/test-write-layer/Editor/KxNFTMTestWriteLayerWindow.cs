using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace NativeFTManager.Verification
{
    /// <summary>
    /// 確認項目 1（アニメーターと FT の出力の合成）を確かめるためのテスト用レイヤーを作る（issue #2 の A4）。
    ///
    /// FT が動かすのと同じシェイプへ 0／50／100 を書き続ける FX レイヤーを、
    /// MA Merge Animator と MA Menu Item を持つ GameObject 1 つにまとめてアバターの直下に置く。
    /// 確認が終わったら、その GameObject と生成フォルダを消せば元に戻る。
    ///
    /// 配布物には含めない。使うときはこのフォルダを Assets/ の下へコピーする。
    /// </summary>
    public sealed class KxNFTMTestWriteLayerWindow : EditorWindow
    {
        private const string ObjectName = "KxNFTMTest Write";
        private const string ParameterName = "KxNFTMTest/Write";
        private const string LayerName = "KxNFTMTest Write";
        private const string GeneratedRoot = "Assets/KxNFTMTest/Generated";

        // VRCAvatarDescriptor.FaceTrackingKey の並び（SDK 3.10.6）。Set の faceTrackingBlendshapeNames はこの順
        private static readonly string[] Keys =
        {
            "EyeLookOutRight", "EyeLookInRight", "EyeLookUpRight", "EyeLookDownRight",
            "EyeLookOutLeft", "EyeLookInLeft", "EyeLookUpLeft", "EyeLookDownLeft",
            "EyeClosedRight", "EyeClosedLeft",
            "EyeDilationRight", "EyeDilationLeft", "EyeConstrictRight", "EyeConstrictLeft",
            "EyeSquintRight", "EyeSquintLeft", "EyeWideRight", "EyeWideLeft",
            "BrowPinchRight", "BrowPinchLeft", "BrowLowererRight", "BrowLowererLeft",
            "BrowInnerUpRight", "BrowInnerUpLeft", "BrowOuterUpRight", "BrowOuterUpLeft",
            "NasalDilationRight", "NasalDilationLeft", "NasalConstrictRight", "NasalConstrictLeft",
            "CheekSquintRight", "CheekSquintLeft", "CheekPuffRight", "CheekPuffLeft",
            "CheekSuckRight", "CheekSuckLeft",
            "JawOpen", "JawRight", "JawLeft", "JawForward", "JawBackward", "JawClench", "JawMandibleRaise",
            "MouthClosed",
            "LipSuckUpperRight", "LipSuckUpperLeft", "LipSuckLowerRight", "LipSuckLowerLeft",
            "LipSuckCornerRight", "LipSuckCornerLeft",
            "LipFunnelUpperRight", "LipFunnelUpperLeft", "LipFunnelLowerRight", "LipFunnelLowerLeft",
            "LipPuckerUpperRight", "LipPuckerUpperLeft", "LipPuckerLowerRight", "LipPuckerLowerLeft",
            "MouthUpperUpRight", "MouthUpperUpLeft", "MouthUpperDeepenRight", "MouthUpperDeepenLeft",
            "NoseSneerRight", "NoseSneerLeft", "MouthLowerDownRight", "MouthLowerDownLeft",
            "MouthUpperRight", "MouthUpperLeft", "MouthLowerRight", "MouthLowerLeft",
            "MouthCornerPullRight", "MouthCornerPullLeft", "MouthCornerSlantRight", "MouthCornerSlantLeft",
            "MouthFrownRight", "MouthFrownLeft", "MouthStretchRight", "MouthStretchLeft",
            "MouthDimpleRight", "MouthDimpleLeft", "MouthRaiserUpper", "MouthRaiserLower",
            "MouthPressRight", "MouthPressLeft", "MouthTightenerRight", "MouthTightenerLeft",
            "TongueOut", "TongueUp", "TongueDown", "TongueRight", "TongueLeft", "TongueRoll",
            "TongueBendDown", "TongueCurlUp", "TongueSquish", "TongueFlat",
            "TongueTwistRight", "TongueTwistLeft",
            "SoftPalateClose", "ThroatSwallow", "NeckFlexRight", "NeckFlexLeft",
        };

        // メニューの値と、そのとき書く値（0 は何も書かないステート）
        private static readonly (int value, float weight, string label)[] WriteStates =
        {
            (1, 0f, "Write 0"),
            (2, 50f, "Write 50"),
            (3, 100f, "Write 100"),
        };

        private VRCAvatarDescriptor _avatar;
        private SkinnedMeshRenderer _renderer;
        private int _keyIndex = 36; // JawOpen
        private string _shapeName = string.Empty;
        private Vector2 _scroll;

        [MenuItem("Tools/Kx VRC NativeFT Manager/Verification/Test Write Layer")]
        private static void Open()
        {
            var window = GetWindow<KxNFTMTestWriteLayerWindow>("KxNFTM Test Write");
            window.minSize = new Vector2(420, 320);
            window.PickFromSelection();
        }

        private void PickFromSelection()
        {
            var selected = Selection.activeGameObject;
            if (selected != null)
            {
                _avatar = selected.GetComponentInParent<VRCAvatarDescriptor>();
            }

            SuggestTarget();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.HelpBox(
                "FT が動かすシェイプへ、FX の一番下から 0／50／100 を書き続けるテスト用レイヤーを置きます（issue #2 の A4、確認項目 1）。" +
                "EX メニューの Test Write から切り替えます。確認が終わったら「取り除く」で消してください。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _avatar = (VRCAvatarDescriptor)EditorGUILayout.ObjectField("アバター", _avatar, typeof(VRCAvatarDescriptor), true);
            if (EditorGUI.EndChangeCheck())
            {
                _renderer = null;
                SuggestTarget();
            }

            if (_avatar == null)
            {
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUI.BeginChangeCheck();
            _keyIndex = EditorGUILayout.Popup("FT のキー", _keyIndex, Keys);
            if (EditorGUI.EndChangeCheck())
            {
                SuggestTarget();
            }

            _renderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("メッシュ", _renderer, typeof(SkinnedMeshRenderer), true);
            DrawShapeField();

            var suggested = FindMappedShape(_avatar, _keyIndex);
            EditorGUILayout.HelpBox(
                suggested != null
                    ? $"Descriptor の Set では、{Keys[_keyIndex]} は「{suggested}」に割り当てられています。"
                    : "Descriptor に Set が無いか、このキーの割り当てがありません（Set を Generator や Manager がビルド時に作る構成では、ここには出ません）。" +
                      "FT がこのキーで動かすシェイプ名を、下の一覧か手入力で指定してください。",
                MessageType.None);

            using (new EditorGUI.DisabledScope(_renderer == null || string.IsNullOrEmpty(_shapeName)))
            {
                if (GUILayout.Button(FindExisting(_avatar) == null ? "テスト用レイヤーを置く" : "作り直す"))
                {
                    Create(_avatar, _renderer, _shapeName);
                }
            }

            using (new EditorGUI.DisabledScope(FindExisting(_avatar) == null))
            {
                if (GUILayout.Button("取り除く"))
                {
                    Remove(_avatar);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawShapeField()
        {
            var names = _renderer != null && _renderer.sharedMesh != null
                ? Enumerable.Range(0, _renderer.sharedMesh.blendShapeCount).Select(_renderer.sharedMesh.GetBlendShapeName).ToArray()
                : new string[0];

            _shapeName = EditorGUILayout.TextField("シェイプ名", _shapeName);
            if (names.Length == 0)
            {
                return;
            }

            var current = System.Array.IndexOf(names, _shapeName);
            var picked = EditorGUILayout.Popup("（メッシュの一覧から）", current, names);
            if (picked != current && picked >= 0)
            {
                _shapeName = names[picked];
            }

            if (!string.IsNullOrEmpty(_shapeName) && current < 0)
            {
                EditorGUILayout.HelpBox(
                    "このシェイプは今のメッシュにありません。Generator などがビルド時に足すシェイプなら、そのままで構いません。",
                    MessageType.Warning);
            }
        }

        /// <summary>
        /// Descriptor の Set の割り当てから、メッシュとシェイプ名の候補を決める。
        /// 見つからなければ、Face Tracking Meshes か Body という名前のメッシュから、キーと同じ名前のシェイプを探す
        /// </summary>
        private void SuggestTarget()
        {
            if (_avatar == null)
            {
                return;
            }

            var meshes = (_avatar.faceTrackingMeshes ?? new List<SkinnedMeshRenderer>()).Where(m => m != null).ToList();
            if (meshes.Count == 0)
            {
                meshes = _avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .OrderByDescending(m => m.name == "Body")
                    .ToList();
            }

            var mapped = FindMappedShape(_avatar, _keyIndex);
            var key = Keys[_keyIndex];
            var candidates = mapped != null
                ? new[] { mapped }
                : new[] { key, char.ToLowerInvariant(key[0]) + key.Substring(1) };

            foreach (var mesh in meshes)
            {
                if (mesh.sharedMesh == null)
                {
                    continue;
                }

                foreach (var candidate in candidates)
                {
                    if (mesh.sharedMesh.GetBlendShapeIndex(candidate) >= 0)
                    {
                        _renderer = mesh;
                        _shapeName = candidate;
                        return;
                    }
                }
            }

            if (_renderer == null)
            {
                _renderer = meshes.FirstOrDefault();
            }

            _shapeName = mapped ?? key;
        }

        private static string FindMappedShape(VRCAvatarDescriptor avatar, int keyIndex)
        {
            if (avatar == null || avatar.faceTrackingBlendshapesSetList == null)
            {
                return null;
            }

            foreach (var set in avatar.faceTrackingBlendshapesSetList)
            {
                var names = set?.faceTrackingBlendshapeNames;
                if (names != null && keyIndex < names.Length && !string.IsNullOrEmpty(names[keyIndex]))
                {
                    return names[keyIndex];
                }
            }

            return null;
        }

        private static GameObject FindExisting(VRCAvatarDescriptor avatar)
        {
            if (avatar == null)
            {
                return null;
            }

            var found = avatar.transform.Find(ObjectName);
            return found != null ? found.gameObject : null;
        }

        private static void Remove(VRCAvatarDescriptor avatar)
        {
            var existing = FindExisting(avatar);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            var folder = $"{GeneratedRoot}/{SafeName(avatar.name)}";
            if (AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static void Create(VRCAvatarDescriptor avatar, SkinnedMeshRenderer renderer, string shapeName)
        {
            Remove(avatar);

            var folder = $"{GeneratedRoot}/{SafeName(avatar.name)}";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();

            var path = AnimationUtility.CalculateTransformPath(renderer.transform, avatar.transform);
            var binding = EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), "blendShape." + shapeName);

            var controller = AnimatorController.CreateAnimatorControllerAtPath($"{folder}/KxNFTMTestWrite.controller");
            controller.AddParameter(ParameterName, AnimatorControllerParameterType.Int);

            var layers = controller.layers;
            layers[0].name = LayerName;
            layers[0].defaultWeight = 1f;
            controller.layers = layers;
            var stateMachine = controller.layers[0].stateMachine;

            // 0: 何も書かない。Write Defaults を切っておき、既定値も書かせない
            var idle = stateMachine.AddState("Idle");
            idle.writeDefaultValues = false;
            stateMachine.defaultState = idle;
            AddTransition(stateMachine, idle, 0);

            foreach (var (value, weight, label) in WriteStates)
            {
                var clip = new AnimationClip { name = label };
                AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, 1f / 60f, weight));
                AssetDatabase.CreateAsset(clip, $"{folder}/{label.Replace(' ', '_')}.anim");

                var state = stateMachine.AddState(label);
                state.motion = clip;
                state.writeDefaultValues = false;
                AddTransition(stateMachine, state, value);
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            var holder = new GameObject(ObjectName);
            Undo.RegisterCreatedObjectUndo(holder, "Create KxNFTM Test Write");
            holder.transform.SetParent(avatar.transform, false);

            var merge = holder.AddComponent<ModularAvatarMergeAnimator>();
            merge.animator = controller;
            merge.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            merge.deleteAttachedAnimator = true;
            merge.pathMode = MergeAnimatorPathMode.Absolute;
            merge.matchAvatarWriteDefaults = false;

            // 優先度の高い Merge Animator ほど FX の後ろに入る。一番下へ置き、アニメーター側でいちばん強い状態にする
            merge.layerPriority = 1000;

            var parameters = holder.AddComponent<ModularAvatarParameters>();
            parameters.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = ParameterName,
                syncType = ParameterSyncType.Int,
                defaultValue = 0f,
                hasExplicitDefaultValue = true,
                saved = false,
                localOnly = true,
                internalParameter = false,
            });

            holder.AddComponent<ModularAvatarMenuInstaller>();
            var root = holder.AddComponent<ModularAvatarMenuItem>();
            InitItem(root);
            root.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            root.MenuSource = SubmenuSource.Children;
            root.label = "Test Write";

            foreach (var (value, _, label) in WriteStates)
            {
                var child = new GameObject(label);
                child.transform.SetParent(holder.transform, false);
                var item = child.AddComponent<ModularAvatarMenuItem>();
                InitItem(item);
                item.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
                item.Control.parameter.name = ParameterName;
                item.Control.value = value;
            }

            EditorGUIUtility.PingObject(holder);
            Selection.activeGameObject = holder;
            Debug.Log($"[KxNFTMTest] {avatar.name} に {ObjectName} を置いた（{path} の {shapeName}）。生成物: {folder}");
        }

        private static void AddTransition(AnimatorStateMachine stateMachine, AnimatorState state, int value)
        {
            var transition = stateMachine.AddAnyStateTransition(state);
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.Equals, value, ParameterName);
        }

        private static void InitItem(ModularAvatarMenuItem item)
        {
            // スクリプトから足した Menu Item は MA の Reset で初期化されないので、全項目を書く
            item.Control = new VRCExpressionsMenu.Control
            {
                name = string.Empty,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = string.Empty },
                subParameters = new VRCExpressionsMenu.Control.Parameter[0],
                labels = new VRCExpressionsMenu.Control.Label[0],
                value = 1f,
            };
            item.MenuSource = SubmenuSource.Children;
            item.isSynced = false;
            item.isSaved = false;
            item.isDefault = false;
            item.automaticValue = false;
            item.label = string.Empty;
        }

        private static string SafeName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name;
        }
    }
}
