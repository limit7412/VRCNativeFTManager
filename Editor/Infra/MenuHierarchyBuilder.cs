using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;
using NativeFTManager.Domain;

namespace NativeFTManager.Infra
{
    /// <summary>
    /// ヒエラルキーにEXメニュー（MA Menu InstallerとMA Menu Item）を作り、項目を増減する（仕様書8.3）
    ///
    /// 作る形は次のとおり。利用者は並べ替え、表示名とアイコンの変更、別のサブメニューへの移動を自由にしてよい。
    /// 項目を足すときは、同じ種類の項目が今ある場所へ入れるので、移した先が保たれる
    /// <code>
    /// Manager
    /// └ Face Tracking   （MA Menu Installer + Menu Item: SubMenu）
    ///   ├ Face Tracking / Eye Tracking / Lip Tracking / Pupil / Visemes   （Toggle）
    ///   ├ Sensitivity   （SubMenu。Radialがあるときだけ）
    ///   └ Settings      （SubMenu。Emulation / Limits / Eye Sync）
    /// </code>
    /// オブジェクト名はエディタの言語によらず英語にする（ビルド時に作る形のメニュー名と同じ）。
    /// Menu Itemのlabelは空のままにし、MAがオブジェクト名をそのまま表示名に使う。そのため表示名も英語になる。
    /// 既存の項目はオブジェクト名ではなく、コンポーネントと参照するパラメーターで探す。
    /// そのため0.1.0-test12までに日本語のオブジェクト名で作った階層も、そのまま読み書きできる。
    /// 作る項目には同梱の既定アイコンを付ける。展開では、アイコンが空の既存項目にだけ既定アイコンを入れ、
    /// 利用者が設定したアイコンは変えない。
    /// 古い版が日本語で書いた表示名は、本製品が書いた文字列と一致するものだけを英語に戻す（RestoreEnglishNames）
    /// 操作はすべてUndoに記録する
    /// </summary>
    internal static class MenuHierarchyBuilder
    {
        /// <summary>
        /// メニューを作る。トグルはメニューに出すものだけ、Radialは有効な系統だけを作る。
        /// 生成しない表情補正のトグルは作らない。すでにある項目は作り直さず、アイコンが空なら既定アイコンを入れる
        /// </summary>
        public static GameObject Expand(NativeFTManagerComponent component, ToggleSet toggles, string undoName)
        {
            var root = FindOrCreateRoot(component, undoName);
            foreach (var toggle in toggles.Dynamic)
            {
                if (FindItems(component, toggle.ParameterName, MenuEntryKind.Toggle).Count == 0)
                {
                    AddToggle(component, toggle.Toggle, toggle.DefaultValue, toggle.Saved, undoName);
                }
            }

            foreach (var channel in toggles.Sensitivity.Channels)
            {
                if (FindItems(component, SensitivityConfig.GetParameterName(channel), MenuEntryKind.Radial).Count == 0)
                {
                    AddRadial(component, channel, toggles.Sensitivity.Saved, undoName);
                }
            }

            FillMissingIcons(component, undoName);
            RestoreEnglishNames(component, undoName);
            return root;
        }

        /// <summary>
        /// プリセットの構成に合わせる。プリセットでメニューに出すトグルが無ければ足し、出さないトグルは外し、
        /// 既定値をプリセットの値にする。今ある項目の名前、アイコン、場所、保存の設定は変えない
        /// </summary>
        public static void ApplyPreset(NativeFTManagerComponent component, NativeFTPreset preset, string undoName)
        {
            if (preset == NativeFTPreset.Custom)
            {
                return;
            }

            bool corrections = NativeFTPresets.EnablesCorrections(preset);
            foreach (var settings in NativeFTPresets.CreateToggles(preset))
            {
                bool show = settings.showInMenu && (!IsCorrection(settings.toggle) || corrections);
                if (!show)
                {
                    RemoveToggle(component, settings.toggle, undoName);
                }
                else if (FindItems(component, ResolvedToggle.GetParameterName(settings.toggle), MenuEntryKind.Toggle).Count == 0)
                {
                    AddToggle(component, settings.toggle, settings.defaultValue, settings.saved, undoName);
                }
                else
                {
                    SetDefault(component, settings.toggle, settings.defaultValue, undoName);
                }
            }
        }

        public static ModularAvatarMenuItem AddToggle(
            NativeFTManagerComponent component,
            NativeFTToggle toggle,
            bool isDefault,
            bool saved,
            string undoName)
        {
            var parent = MenuPlan.IsSetting(toggle)
                ? FindOrCreateContainer(component, SettingsParameters(), MenuPlan.SettingsMenuName, MenuIcon.Settings, undoName)
                : FindContainer(component, MainParameters()) ?? FindOrCreateRoot(component, undoName).transform;

            var item = CreateItem(parent, MenuPlan.GetLabel(toggle), undoName);
            item.Control = CreateControl(VRCExpressionsMenu.Control.ControlType.Toggle, MenuIcons.For(toggle));
            item.Control.parameter.name = ResolvedToggle.GetParameterName(toggle);
            item.Control.value = 1f;
            item.isDefault = isDefault;
            item.isSaved = saved;
            PlaceInOrder(item.transform, (int)toggle);
            return item;
        }

        public static void RemoveToggle(NativeFTManagerComponent component, NativeFTToggle toggle, string undoName)
        {
            RemoveItems(component, ResolvedToggle.GetParameterName(toggle), MenuEntryKind.Toggle, undoName);
        }

        public static void SetDefault(NativeFTManagerComponent component, NativeFTToggle toggle, bool isDefault, string undoName)
        {
            foreach (var item in FindItems(component, ResolvedToggle.GetParameterName(toggle), MenuEntryKind.Toggle))
            {
                if (item.isDefault != isDefault)
                {
                    Undo.RecordObject(item, undoName);
                    item.isDefault = isDefault;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                }
            }
        }

        public static ModularAvatarMenuItem AddRadial(
            NativeFTManagerComponent component,
            SensitivityChannel channel,
            bool saved,
            string undoName)
        {
            var parent = FindOrCreateContainer(component, SensitivityParameters(), MenuPlan.SensitivityMenuName, MenuIcon.Sensitivity, undoName);
            var item = CreateItem(parent, SensitivityConfig.GetLabel(channel), undoName);
            item.Control = CreateControl(VRCExpressionsMenu.Control.ControlType.RadialPuppet, MenuIcons.For(channel));
            item.Control.subParameters = new[]
            {
                new VRCExpressionsMenu.Control.Parameter { name = SensitivityConfig.GetParameterName(channel) },
            };
            item.isSaved = saved;
            PlaceInOrder(item.transform, (int)channel);
            return item;
        }

        public static void RemoveRadial(NativeFTManagerComponent component, SensitivityChannel channel, string undoName)
        {
            RemoveItems(component, SensitivityConfig.GetParameterName(channel), MenuEntryKind.Radial, undoName);
        }

        /// <summary>
        /// Managerの配下にある、メニューの根（MA Menu InstallerとSubMenuのMenu Itemを同じオブジェクトに持つもの）
        /// </summary>
        public static GameObject FindRoot(NativeFTManagerComponent component)
        {
            foreach (var installer in component.GetComponentsInChildren<ModularAvatarMenuInstaller>(true))
            {
                var item = installer.GetComponent<ModularAvatarMenuItem>();
                if (item != null && item.Control != null
                    && item.Control.type == VRCExpressionsMenu.Control.ControlType.SubMenu
                    && item.MenuSource == SubmenuSource.Children)
                {
                    return installer.gameObject;
                }
            }

            return null;
        }

        private static List<ModularAvatarMenuItem> FindItems(
            NativeFTManagerComponent component,
            string parameter,
            MenuEntryKind kind)
        {
            return MenuHierarchyScanner.GetSearchRoot(component)
                .GetComponentsInChildren<ModularAvatarMenuItem>(true)
                .Where(item =>
                {
                    var entry = MenuHierarchyScanner.ToEntry(item);
                    return entry != null && entry.Kind == kind && entry.Parameter == parameter;
                })
                .ToList();
        }

        private static void RemoveItems(NativeFTManagerComponent component, string parameter, MenuEntryKind kind, string undoName)
        {
            foreach (var item in FindItems(component, parameter, kind))
            {
                var parent = item.transform.parent;
                if (item.transform.childCount == 0 && item.GetComponents<Component>().Length <= 2)
                {
                    // TransformとMenu Itemしか無いオブジェクトは、本製品が作ったものとみなしてオブジェクトごと消す
                    Undo.DestroyObjectImmediate(item.gameObject);
                }
                else
                {
                    Undo.DestroyObjectImmediate(item);
                }

                RemoveIfEmptySubMenu(component, parent);
            }
        }

        /// <summary>
        /// 項目を外して空になったサブメニュー（Settings、Sensitivity）を消す。メニューの根は残す
        /// </summary>
        private static void RemoveIfEmptySubMenu(NativeFTManagerComponent component, Transform container)
        {
            if (container == null || container.childCount > 0 || container.gameObject == FindRoot(component))
            {
                return;
            }

            // 子オブジェクトから項目を組むサブメニューだけを空とみなす。
            // メニューアセットや別のオブジェクトの子を参照するものは、子が無くても中身がある
            var item = container.GetComponent<ModularAvatarMenuItem>();
            if (item == null || item.Control == null
                || item.Control.type != VRCExpressionsMenu.Control.ControlType.SubMenu
                || item.MenuSource != SubmenuSource.Children
                || item.menuSource_otherObjectChildren != null
                || container.GetComponents<Component>().Length > 2)
            {
                return;
            }

            Undo.DestroyObjectImmediate(container.gameObject);
        }

        private static GameObject FindOrCreateRoot(NativeFTManagerComponent component, string undoName)
        {
            return FindRoot(component) ?? CreateRoot(component, undoName);
        }

        private static GameObject CreateRoot(NativeFTManagerComponent component, string undoName)
        {
            var item = CreateItem(component.transform, MenuPlan.SubMenuName, undoName, false);
            item.Control = CreateControl(VRCExpressionsMenu.Control.ControlType.SubMenu, MenuIcon.FaceTrackingMenu);
            item.MenuSource = SubmenuSource.Children;
            item.gameObject.AddComponent<ModularAvatarMenuInstaller>();
            Undo.RegisterCreatedObjectUndo(item.gameObject, undoName);
            return item.gameObject;
        }

        /// <summary>
        /// 今ある項目が入っているオブジェクト。どれも無ければnull
        /// </summary>
        private static Transform FindContainer(NativeFTManagerComponent component, IEnumerable<(string, MenuEntryKind)> members)
        {
            foreach (var (parameter, kind) in members)
            {
                var item = FindItems(component, parameter, kind).FirstOrDefault();
                if (item != null && item.transform.parent != null)
                {
                    return item.transform.parent;
                }
            }

            return null;
        }

        /// <summary>
        /// Settings、Sensitivityのサブメニュー。項目が1つも無ければ、根の末尾に新しく作る
        /// </summary>
        private static Transform FindOrCreateContainer(
            NativeFTManagerComponent component,
            IEnumerable<(string, MenuEntryKind)> members,
            string objectName,
            MenuIcon icon,
            string undoName)
        {
            var found = FindContainer(component, members);
            if (found != null)
            {
                return found;
            }

            var root = FindOrCreateRoot(component, undoName);
            var item = CreateItem(root.transform, objectName, undoName);
            item.Control = CreateControl(VRCExpressionsMenu.Control.ControlType.SubMenu, icon);
            item.MenuSource = SubmenuSource.Children;

            // SensitivityはSettingsの前に置く（仕様書8.3の並び）
            if (objectName == MenuPlan.SensitivityMenuName)
            {
                var settings = FindContainer(component, SettingsParameters());
                if (settings != null && settings.parent == root.transform)
                {
                    item.transform.SetSiblingIndex(settings.GetSiblingIndex());
                }
            }

            return item.transform;
        }

        /// <summary>
        /// Menu Itemのオブジェクトを作る。nameは英語のオブジェクト名で、そのまま表示名になる
        /// </summary>
        private static ModularAvatarMenuItem CreateItem(
            Transform parent,
            string name,
            string undoName,
            bool register = true)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            var item = gameObject.AddComponent<ModularAvatarMenuItem>();

            // スクリプトから足したMenu ItemはMAのResetで初期化されないので、ここで全項目を書く
            item.MenuSource = SubmenuSource.Children;
            item.isSynced = true;
            item.isSaved = true;
            item.isDefault = false;
            item.automaticValue = false;
            item.label = string.Empty;
            if (register)
            {
                Undo.RegisterCreatedObjectUndo(gameObject, undoName);
            }

            return item;
        }

        private static VRCExpressionsMenu.Control CreateControl(VRCExpressionsMenu.Control.ControlType type, MenuIcon icon)
        {
            return new VRCExpressionsMenu.Control
            {
                name = string.Empty,
                icon = MenuIconAssets.Load(icon),
                type = type,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = string.Empty },
                value = 1f,
                subParameters = new VRCExpressionsMenu.Control.Parameter[0],
                labels = new VRCExpressionsMenu.Control.Label[0],
            };
        }

        /// <summary>
        /// 同じ入れ物の中で、列挙体の順で前にある項目の直後へ置く。前に何も無ければ先頭へ置く。
        /// 利用者が並べ替えた後でも、足した項目だけがその並びの中へ入る
        /// </summary>
        private static void PlaceInOrder(Transform transform, int order)
        {
            var parent = transform.parent;
            int index = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                var sibling = parent.GetChild(i);
                if (sibling == transform)
                {
                    continue;
                }

                var siblingOrder = GetOrder(sibling.GetComponent<ModularAvatarMenuItem>());
                if (siblingOrder.HasValue && siblingOrder.Value < order)
                {
                    index = sibling.GetSiblingIndex() + 1;
                }
            }

            if (index > transform.GetSiblingIndex())
            {
                index--;
            }

            transform.SetSiblingIndex(index);
        }

        private static int? GetOrder(ModularAvatarMenuItem item)
        {
            var entry = MenuHierarchyScanner.ToEntry(item);
            if (entry == null)
            {
                return null;
            }

            if (entry.Kind == MenuEntryKind.Toggle)
            {
                var toggle = MenuHierarchy.FindToggle(entry.Parameter);
                return toggle.HasValue ? (int)toggle.Value : (int?)null;
            }

            var channel = MenuHierarchy.FindChannel(entry.Parameter);
            return channel.HasValue ? (int)channel.Value : (int?)null;
        }

        /// <summary>
        /// アイコンが空の項目に既定アイコンを入れる。利用者が設定したアイコンは変えない
        /// </summary>
        public static void FillMissingIcons(NativeFTManagerComponent component, string undoName)
        {
            foreach (var own in FindOwnItems(component))
            {
                if (own.Item.Control.icon != null)
                {
                    continue;
                }

                var texture = MenuIconAssets.Load(own.Icon);
                if (texture == null)
                {
                    continue;
                }

                Undo.RecordObject(own.Item, undoName);
                own.Item.Control.icon = texture;
                PrefabUtility.RecordPrefabInstancePropertyModifications(own.Item);
            }
        }

        /// <summary>
        /// 0.1.0-test15までの版が日本語のエディタで書いた表示名を英語に戻す（仕様書8.3）。
        /// labelが本製品の書いた日本語と一致すれば空にし、オブジェクト名（英語）を表示名に使わせる。
        /// labelが空で、オブジェクト名が0.1.0-test12までの日本語名のままなら、オブジェクト名を英語にする。
        /// 利用者が付けた別の名前は一致しないので変えない
        /// </summary>
        public static void RestoreEnglishNames(NativeFTManagerComponent component, string undoName)
        {
            foreach (var own in FindOwnItems(component))
            {
                if (own.Item.label == own.LegacyJapaneseName)
                {
                    Undo.RecordObject(own.Item, undoName);
                    own.Item.label = string.Empty;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(own.Item);
                }

                var gameObject = own.Item.gameObject;
                if (string.IsNullOrEmpty(own.Item.label) && gameObject.name == own.LegacyJapaneseName)
                {
                    Undo.RecordObject(gameObject, undoName);
                    gameObject.name = own.EnglishName;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
                }
            }
        }

        /// <summary>
        /// 本製品が作ったとみなす項目と、その既定アイコン、英語名、古い版が日本語のエディタで付けた名前
        /// </summary>
        private readonly struct OwnItem
        {
            public readonly ModularAvatarMenuItem Item;
            public readonly MenuIcon Icon;
            public readonly string EnglishName;
            public readonly string LegacyJapaneseName;

            public OwnItem(ModularAvatarMenuItem item, MenuIcon icon, string englishName, string legacyJapaneseName)
            {
                Item = item;
                Icon = icon;
                EnglishName = englishName;
                LegacyJapaneseName = legacyJapaneseName;
            }
        }

        /// <summary>
        /// 本製品が作ったとみなす項目。対象は公開パラメーターを指すトグルとRadial、メニューの根、
        /// 根の直下にあるSettingsとSensitivityのサブメニュー。
        /// 利用者が項目を自分のサブメニューへ移した場合、そのサブメニューは本製品のものと限らないので含めない
        /// </summary>
        private static List<OwnItem> FindOwnItems(NativeFTManagerComponent component)
        {
            var targets = new List<OwnItem>();
            foreach (NativeFTToggle toggle in Enum.GetValues(typeof(NativeFTToggle)))
            {
                foreach (var item in FindItems(component, ResolvedToggle.GetParameterName(toggle), MenuEntryKind.Toggle))
                {
                    targets.Add(new OwnItem(item, MenuIcons.For(toggle), MenuPlan.GetLabel(toggle), LegacyJapaneseName(toggle)));
                }
            }

            foreach (SensitivityChannel channel in Enum.GetValues(typeof(SensitivityChannel)))
            {
                foreach (var item in FindItems(component, SensitivityConfig.GetParameterName(channel), MenuEntryKind.Radial))
                {
                    targets.Add(new OwnItem(
                        item,
                        MenuIcons.For(channel),
                        SensitivityConfig.GetLabel(channel),
                        channel == SensitivityChannel.Eye ? "目" : "口"));
                }
            }

            var root = FindRoot(component);
            if (root != null)
            {
                targets.Add(new OwnItem(
                    root.GetComponent<ModularAvatarMenuItem>(), MenuIcon.FaceTrackingMenu, MenuPlan.SubMenuName, "フェイストラッキング"));
                AddOwnSubMenu(
                    targets, root, FindContainer(component, SettingsParameters()), MenuIcon.Settings, MenuPlan.SettingsMenuName, "設定");
                AddOwnSubMenu(
                    targets, root, FindContainer(component, SensitivityParameters()), MenuIcon.Sensitivity, MenuPlan.SensitivityMenuName, "感度");
            }

            return targets.Where(target => target.Item != null && target.Item.Control != null).ToList();
        }

        /// <summary>
        /// 0.1.0-test15までの版が日本語のエディタで付けたトグルの名前。
        /// test12まではオブジェクト名に、test13からtest15まではlabelに書いた。翻訳ファイルが変わっても、書いた文字列は変わらないので固定で持つ
        /// </summary>
        private static string LegacyJapaneseName(NativeFTToggle toggle)
        {
            switch (toggle)
            {
                case NativeFTToggle.FaceTracking:
                    return "フェイストラッキング";
                case NativeFTToggle.EyeTracking:
                    return "目のトラッキング";
                case NativeFTToggle.LipTracking:
                    return "口のトラッキング";
                case NativeFTToggle.Pupil:
                    return "瞳孔";
                case NativeFTToggle.Visemes:
                    return "ビゼーム";
                case NativeFTToggle.Emulation:
                    return "エミュレーション";
                case NativeFTToggle.Limits:
                    return "制限";
                case NativeFTToggle.EyeSync:
                    return "両目の連動";
                default:
                    throw new ArgumentOutOfRangeException(nameof(toggle), toggle, null);
            }
        }

        /// <summary>
        /// 本製品が作ったサブメニューだけを対象に加える。根の直下にあり、作ったときのオブジェクト名
        /// （英語名か、0.1.0-test12までの日本語名）のままのものを本製品のものとみなす。
        /// 利用者が根の直下に作った別名のサブメニューへ項目を移した場合は、そのサブメニューを含めない。
        /// 本製品のサブメニューを利用者が改名した場合も含めないが、アイコンが付かないだけで済むので、取り違えるよりよい
        /// </summary>
        private static void AddOwnSubMenu(
            List<OwnItem> targets,
            GameObject root,
            Transform container,
            MenuIcon icon,
            string englishName,
            string legacyJapaneseName)
        {
            if (container == null || container.parent != root.transform
                || (container.name != englishName && container.name != legacyJapaneseName))
            {
                return;
            }

            var item = container.GetComponent<ModularAvatarMenuItem>();
            if (item != null && item.Control != null && item.Control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
            {
                targets.Add(new OwnItem(item, icon, englishName, legacyJapaneseName));
            }
        }

        private static bool IsCorrection(NativeFTToggle toggle)
        {
            return MenuPlan.IsSetting(toggle);
        }

        private static IEnumerable<(string, MenuEntryKind)> MainParameters()
        {
            return Enum.GetValues(typeof(NativeFTToggle)).Cast<NativeFTToggle>()
                .Where(toggle => !MenuPlan.IsSetting(toggle))
                .Select(toggle => (ResolvedToggle.GetParameterName(toggle), MenuEntryKind.Toggle));
        }

        private static IEnumerable<(string, MenuEntryKind)> SettingsParameters()
        {
            return Enum.GetValues(typeof(NativeFTToggle)).Cast<NativeFTToggle>()
                .Where(MenuPlan.IsSetting)
                .Select(toggle => (ResolvedToggle.GetParameterName(toggle), MenuEntryKind.Toggle));
        }

        private static IEnumerable<(string, MenuEntryKind)> SensitivityParameters()
        {
            return Enum.GetValues(typeof(SensitivityChannel)).Cast<SensitivityChannel>()
                .Select(channel => (SensitivityConfig.GetParameterName(channel), MenuEntryKind.Radial));
        }
    }
}
