using System.Collections.Generic;
using Colossal;
using Colossal.Localization;
using Game.SceneFlow;

namespace PillarSpacing
{
    /// <summary>
    /// 管理双语界面。
    /// - 始终向 en-US 注册一份文本，作为所有语言的回退。
    /// - 选择 Auto：en-US 提供英文，其他语言交给游戏回退和翻译类 Mod。
    /// - 选择强制语言：en-US 和玩家当前的游戏语言都显示所选语言。
    /// </summary>
    public static class ModLocalization
    {
        private const string kFallbackLocale = "en-US";

        private static Setting s_Setting;
        private static ModLocaleSource s_FallbackSource;
        private static ModLocaleSource s_OverrideSource;
        private static string s_OverrideLocaleId;

        private static ModLanguage s_AppliedLanguage;
        private static string s_AppliedActiveLocale;
        private static bool s_Reloading;

        private static LocalizationManager Manager => GameManager.instance?.localizationManager;

        public static void Init(Setting setting)
        {
            s_Setting = setting;
            s_FallbackSource = new ModLocaleSource(setting, () => FallbackLanguage());
            Manager.AddSource(kFallbackLocale, s_FallbackSource);

            // 玩家在游戏里切换语言时，强制语言要跟着挂到新语言上
            Manager.onActiveDictionaryChanged += OnActiveDictionaryChanged;

            s_AppliedLanguage = ModLanguage.Auto;
            s_AppliedActiveLocale = Manager.activeLocaleId;
            Refresh(force: true);
        }

        public static void Dispose()
        {
            if (Manager == null)
                return;

            Manager.onActiveDictionaryChanged -= OnActiveDictionaryChanged;
            RemoveOverride();
            if (s_FallbackSource != null)
                Manager.RemoveSource(kFallbackLocale, s_FallbackSource);
            s_FallbackSource = null;
            s_Setting = null;
        }

        // Auto 时 en-US 显示英文；强制语言时 en-US 也显示所选语言
        private static ModLanguage FallbackLanguage()
        {
            ModLanguage lang = s_Setting?.Language ?? ModLanguage.Auto;
            return lang == ModLanguage.Auto ? ModLanguage.English : lang;
        }

        private static void OnActiveDictionaryChanged()
        {
            // 自己触发的重新加载也会进到这里，避免死循环
            if (s_Reloading)
                return;
            Refresh();
        }

        public static void Refresh(bool force = false)
        {
            if (s_Setting == null || Manager == null)
                return;

            ModLanguage lang = s_Setting.Language;
            string active = Manager.activeLocaleId;

            if (!force && lang == s_AppliedLanguage && active == s_AppliedActiveLocale)
                return;

            s_AppliedLanguage = lang;
            s_AppliedActiveLocale = active;

            RemoveOverride();

            // 强制语言且当前游戏语言不是 en-US 时，在当前语言上再挂一份文本
            if (lang != ModLanguage.Auto && active != kFallbackLocale)
            {
                s_OverrideSource = new ModLocaleSource(s_Setting, () => s_Setting.Language);
                s_OverrideLocaleId = active;
                Manager.AddSource(active, s_OverrideSource);
            }

            // 让界面立即用上新文本（1.6.2f1 已确认接口存在）
            s_Reloading = true;
            try
            {
                Manager.ReloadActiveLocale();
            }
            finally
            {
                s_Reloading = false;
            }

            Mod.log.Info($"UI language: {lang} (game locale: {active})");
        }

        private static void RemoveOverride()
        {
            if (s_OverrideSource != null && s_OverrideLocaleId != null)
                Manager.RemoveSource(s_OverrideLocaleId, s_OverrideSource);
            s_OverrideSource = null;
            s_OverrideLocaleId = null;
        }
    }

    /// <summary>
    /// 按指定语言返回全部界面文本。每次游戏读取文本时都会重新取当前选择的语言。
    /// </summary>
    public class ModLocaleSource : IDictionarySource
    {
        private readonly Setting m;
        private readonly System.Func<ModLanguage> m_GetLanguage;

        public ModLocaleSource(Setting setting, System.Func<ModLanguage> getLanguage)
        {
            m = setting;
            m_GetLanguage = getLanguage;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return m_GetLanguage() == ModLanguage.SimplifiedChinese ? Chinese() : English();
        }

        public void Unload() { }

        private Dictionary<string, string> English()
        {
            return new Dictionary<string, string>
            {
                { m.GetSettingsLocaleID(), "Pillar Spacing" },
                { m.GetOptionTabLocaleID(Setting.kSection), "Main" },
                { m.GetOptionGroupLocaleID(Setting.kMainGroup), "General" },
                { m.GetOptionGroupLocaleID(Setting.kRoadGroup), "Roads" },
                { m.GetOptionGroupLocaleID(Setting.kTrackGroup), "Tracks" },
                { m.GetOptionGroupLocaleID(Setting.kPathGroup), "Paths" },
                { m.GetOptionGroupLocaleID(Setting.kResetGroup), "Reset" },
                { m.GetOptionGroupLocaleID(Setting.kLanguageGroup), "Language" },

                { m.GetOptionLabelLocaleID(nameof(Setting.Enabled)), "Enable custom pillar spacing" },
                { m.GetOptionDescLocaleID(nameof(Setting.Enabled)), "When off, everything uses vanilla spacing. Changes only affect newly built roads; rebuild existing bridges to apply." },

                { m.GetOptionLabelLocaleID(nameof(Setting.RoadInterval)), "Road pillar spacing" },
                { m.GetOptionDescLocaleID(nameof(Setting.RoadInterval)), "Maximum distance between pillars on elevated roads, highways and bridges built with roads, including tram roads. Vanilla is 80 m. Above 200 m, road segments are also allowed to be longer." },

                { m.GetOptionLabelLocaleID(nameof(Setting.TrackInterval)), "Track pillar spacing" },
                { m.GetOptionDescLocaleID(nameof(Setting.TrackInterval)), "Maximum distance between pillars on elevated train, tram and subway tracks. Vanilla is 80 m." },

                { m.GetOptionLabelLocaleID(nameof(Setting.PathInterval)), "Path pillar spacing" },
                { m.GetOptionDescLocaleID(nameof(Setting.PathInterval)), "Maximum distance between pillars on elevated pedestrian and bicycle paths. Vanilla is 40 m." },



                { m.GetOptionLabelLocaleID(nameof(Setting.ResetToVanilla)), "Restore vanilla" },
                { m.GetOptionDescLocaleID(nameof(Setting.ResetToVanilla)), "Immediately restore vanilla pillar spacing on all prefabs." },

                { m.GetOptionLabelLocaleID(nameof(Setting.Language)), "Interface language" },
                { m.GetOptionDescLocaleID(nameof(Setting.Language)), "Auto follows the game language and works with translation mods. Choose a language to force it." },
                { m.GetEnumValueLocaleID(ModLanguage.Auto), "Auto" },
                { m.GetEnumValueLocaleID(ModLanguage.English), "English" },
                { m.GetEnumValueLocaleID(ModLanguage.SimplifiedChinese), "简体中文" },
            };
        }

        private Dictionary<string, string> Chinese()
        {
            return new Dictionary<string, string>
            {
                { m.GetSettingsLocaleID(), "桥墩间距" },
                { m.GetOptionTabLocaleID(Setting.kSection), "主要设置" },
                { m.GetOptionGroupLocaleID(Setting.kMainGroup), "通用" },
                { m.GetOptionGroupLocaleID(Setting.kRoadGroup), "道路" },
                { m.GetOptionGroupLocaleID(Setting.kTrackGroup), "轨道" },
                { m.GetOptionGroupLocaleID(Setting.kPathGroup), "步道" },
                { m.GetOptionGroupLocaleID(Setting.kResetGroup), "恢复" },
                { m.GetOptionGroupLocaleID(Setting.kLanguageGroup), "语言" },

                { m.GetOptionLabelLocaleID(nameof(Setting.Enabled)), "启用自定义桥墩间距" },
                { m.GetOptionDescLocaleID(nameof(Setting.Enabled)), "关闭后全部恢复原版间距。修改只影响之后新建的道路，已建好的桥需要重建。" },

                { m.GetOptionLabelLocaleID(nameof(Setting.RoadInterval)), "道路桥墩间距" },
                { m.GetOptionDescLocaleID(nameof(Setting.RoadInterval)), "高架道路、高速公路以及用道路拉出的普通桥梁（含电车道路）相邻桥墩的最大距离。原版为 80 米。超过 200 米时会同时放宽路段的最大长度。" },

                { m.GetOptionLabelLocaleID(nameof(Setting.TrackInterval)), "轨道桥墩间距" },
                { m.GetOptionDescLocaleID(nameof(Setting.TrackInterval)), "高架火车、电车、地铁轨道相邻桥墩的最大距离。原版为 80 米。" },

                { m.GetOptionLabelLocaleID(nameof(Setting.PathInterval)), "步道桥墩间距" },
                { m.GetOptionDescLocaleID(nameof(Setting.PathInterval)), "高架人行道、自行车道相邻桥墩的最大距离。原版为 40 米。" },



                { m.GetOptionLabelLocaleID(nameof(Setting.ResetToVanilla)), "恢复原版" },
                { m.GetOptionDescLocaleID(nameof(Setting.ResetToVanilla)), "立即恢复所有预设的原版桥墩间距。" },

                { m.GetOptionLabelLocaleID(nameof(Setting.Language)), "界面语言" },
                { m.GetOptionDescLocaleID(nameof(Setting.Language)), "自动：跟随游戏语言，可配合翻译类 Mod 使用。选择具体语言则强制显示该语言。" },
                { m.GetEnumValueLocaleID(ModLanguage.Auto), "自动" },
                { m.GetEnumValueLocaleID(ModLanguage.English), "English" },
                { m.GetEnumValueLocaleID(ModLanguage.SimplifiedChinese), "简体中文" },
            };
        }
    }
}
