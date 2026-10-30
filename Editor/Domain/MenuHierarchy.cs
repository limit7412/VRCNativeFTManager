using System;
using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// MA Menu Itemの種類のうち、本製品が読むもの
    /// </summary>
    internal enum MenuEntryKind
    {
        Toggle,
        Radial,
        Other,
    }

    /// <summary>
    /// アバターに置かれたMA Menu Item 1つから読んだ値。DomainはMAの型に触れないので、Infraでここへ写す
    /// </summary>
    internal sealed class MenuEntry
    {
        public MenuEntry(
            MenuEntryKind kind,
            string parameter,
            float value = 1f,
            bool automaticValue = false,
            bool isDefault = false,
            bool isSaved = true)
        {
            Kind = kind;
            Parameter = parameter;
            Value = value;
            AutomaticValue = automaticValue;
            IsDefault = isDefault;
            IsSaved = isSaved;
        }

        public MenuEntryKind Kind { get; }

        /// <summary>トグルはパラメーター、Radialは1つ目のサブパラメーターの名前</summary>
        public string Parameter { get; }

        public float Value { get; }

        public bool AutomaticValue { get; }

        public bool IsDefault { get; }

        public bool IsSaved { get; }
    }

    /// <summary>
    /// ヒエラルキーのMA Menu Itemから読んだトグルと感度（仕様書8.3、menuModeがHierarchyのとき）
    ///
    /// メニューの形はMenu Itemの側を正にする。公開パラメーター（KxNFTM/）を指すMenu Itemがあるトグルを
    /// メニューに出すトグルとし、既定値と保存もそのMenu Itemから決める。コンポーネントのtogglesは読まない。
    /// 公開名と一致しないMenu Itemは利用者のものとして扱い、読まない
    /// </summary>
    internal sealed class MenuHierarchy
    {
        private readonly Dictionary<NativeFTToggle, ToggleSettings> _toggles;

        private MenuHierarchy(
            Dictionary<NativeFTToggle, ToggleSettings> toggles,
            IReadOnlyList<SensitivityChannel> radials,
            bool sensitivitySaved,
            IReadOnlyList<string> invalidValues)
        {
            _toggles = toggles;
            Radials = radials;
            SensitivitySaved = sensitivitySaved;
            InvalidValues = invalidValues;
        }

        public static MenuHierarchy Empty => Read(new MenuEntry[0]);

        /// <summary>Menu Itemが見つかった感度の系統（列挙体の順）</summary>
        public IReadOnlyList<SensitivityChannel> Radials { get; }

        /// <summary>感度の値を保存するか。系統が2つあっても保存の設定は1つなので、どちらかが保存なら保存する</summary>
        public bool SensitivitySaved { get; }

        /// <summary>トグル値が1でないため読まなかったMenu Itemのパラメーター名</summary>
        public IReadOnlyList<string> InvalidValues { get; }

        /// <summary>公開パラメーターを指すMenu Itemが1つも無いか</summary>
        public bool IsEmpty => _toggles.Count == 0 && Radials.Count == 0;

        public bool Contains(NativeFTToggle toggle)
        {
            return _toggles.ContainsKey(toggle);
        }

        public bool Contains(SensitivityChannel channel)
        {
            return Radials.Contains(channel);
        }

        /// <summary>
        /// トグルの設定。Menu Itemが無いトグルはメニューに出さず、固定の値をhiddenから取る。
        /// hiddenを渡さなければ、設定がリストに無いときと同じ扱い（Eye Sync以外は有効）にする。
        /// Menu Itemには出さないトグルの値の置き場が無いので、コンポーネントのtogglesの既定値をここで使う
        /// </summary>
        public ToggleSettings GetToggle(NativeFTToggle toggle, Func<NativeFTToggle, ToggleSettings> hidden = null)
        {
            if (_toggles.TryGetValue(toggle, out var settings))
            {
                return settings.Clone();
            }

            var fixedSettings = hidden?.Invoke(toggle) ?? ToggleSettings.CreateMissing(toggle);
            return new ToggleSettings(toggle, false, fixedSettings.defaultValue) { saved = fixedSettings.saved };
        }

        /// <summary>
        /// Menu Itemの値から読む。並びはヒエラルキーの順で渡す。
        /// 同じパラメーターを指すMenu Itemが複数あるときは、どれかが「既定値にする」なら既定値を有効にし、
        /// 保存は先頭のものに従う
        /// </summary>
        public static MenuHierarchy Read(IEnumerable<MenuEntry> entries)
        {
            var toggles = new Dictionary<NativeFTToggle, ToggleSettings>();
            var radials = new List<SensitivityChannel>();
            var invalid = new List<string>();
            bool sensitivitySaved = false;

            foreach (var entry in entries.Where(entry => entry != null && !string.IsNullOrEmpty(entry.Parameter)))
            {
                switch (entry.Kind)
                {
                    case MenuEntryKind.Toggle:
                        var toggle = FindToggle(entry.Parameter);
                        if (!toggle.HasValue)
                        {
                            break;
                        }

                        // 値の自動割り当てでは、本製品が宣言する既定値（0か1）を避けずに1が振られる（MenuItemToggleValue）。
                        // 手で1以外にしたトグルはBoolのオン・オフにならないので読まない
                        if (!entry.AutomaticValue && Math.Abs(entry.Value - 1f) > 0.01f)
                        {
                            if (!invalid.Contains(entry.Parameter))
                            {
                                invalid.Add(entry.Parameter);
                            }

                            break;
                        }

                        if (toggles.TryGetValue(toggle.Value, out var existing))
                        {
                            existing.defaultValue |= entry.IsDefault;
                        }
                        else
                        {
                            toggles.Add(
                                toggle.Value,
                                new ToggleSettings(toggle.Value, true, entry.IsDefault) { saved = entry.IsSaved });
                        }

                        break;
                    case MenuEntryKind.Radial:
                        var channel = FindChannel(entry.Parameter);
                        if (!channel.HasValue)
                        {
                            break;
                        }

                        if (!radials.Contains(channel.Value))
                        {
                            radials.Add(channel.Value);
                        }

                        sensitivitySaved |= entry.IsSaved;
                        break;
                }
            }

            return new MenuHierarchy(
                toggles,
                radials.OrderBy(channel => (int)channel).ToArray(),
                sensitivitySaved,
                invalid);
        }

        /// <summary>パラメーター名に対応するトグル。公開名でなければnull</summary>
        public static NativeFTToggle? FindToggle(string parameter)
        {
            foreach (NativeFTToggle toggle in Enum.GetValues(typeof(NativeFTToggle)))
            {
                if (ResolvedToggle.GetParameterName(toggle) == parameter)
                {
                    return toggle;
                }
            }

            return null;
        }

        /// <summary>パラメーター名に対応する感度の系統。公開名でなければnull</summary>
        public static SensitivityChannel? FindChannel(string parameter)
        {
            foreach (SensitivityChannel channel in Enum.GetValues(typeof(SensitivityChannel)))
            {
                if (SensitivityConfig.GetParameterName(channel) == parameter)
                {
                    return channel;
                }
            }

            return null;
        }
    }
}
