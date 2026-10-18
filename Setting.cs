using System;
using System.Collections.Generic;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI.Localization;
using Game.UI.Widgets;

namespace PillarSpacing
{
    // 界面语言选项。Auto = 只提供英文，其他语言交给游戏回退和翻译类 Mod
    public enum ModLanguage
    {
        Auto,
        English,
        SimplifiedChinese,
    }

    [FileLocation(nameof(PillarSpacing))]
    [SettingsUIGroupOrder(kMainGroup, kRoadGroup, kTrackGroup, kPathGroup, kResetGroup, kLanguageGroup)]
    [SettingsUIShowGroupName(kMainGroup, kRoadGroup, kTrackGroup, kPathGroup, kResetGroup, kLanguageGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";
        public const string kMainGroup = "GeneralGroup";
        public const string kRoadGroup = "RoadGroup";
        public const string kTrackGroup = "TrackGroup";
        public const string kPathGroup = "PathGroup";
        public const string kResetGroup = "ResetGroup";
        public const string kLanguageGroup = "LanguageGroup";

        // 1.6.2f1 原版值
        public const int kVanillaRoad = 80;
        public const int kVanillaTrack = 80;
        public const int kVanillaPath = 40;

        public Setting(IMod mod) : base(mod) { }

        // 总开关
        [SettingsUISection(kSection, kMainGroup)]
        public bool Enabled { get; set; } = true;

        // 道路（含用道路拉的普通桥梁）
        [SettingsUISection(kSection, kRoadGroup)]
        [SettingsUIDropdown(typeof(Setting), nameof(GetIntervalItems))]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDisabled))]
        public int RoadInterval { get; set; } = kVanillaRoad;

        // 独立轨道：火车、电车、地铁等
        [SettingsUISection(kSection, kTrackGroup)]
        [SettingsUIDropdown(typeof(Setting), nameof(GetIntervalItems))]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDisabled))]
        public int TrackInterval { get; set; } = kVanillaTrack;

        // 步道：人行道、自行车道等
        [SettingsUISection(kSection, kPathGroup)]
        [SettingsUIDropdown(typeof(Setting), nameof(GetIntervalItems))]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDisabled))]
        public int PathInterval { get; set; } = kVanillaPath;

        [SettingsUISection(kSection, kResetGroup)]
        [SettingsUIButton]
        public bool ResetToVanilla
        {
            set
            {
                RoadInterval = kVanillaRoad;
                TrackInterval = kVanillaTrack;
                PathInterval = kVanillaPath;
                ApplyAndSave();
            }
        }

        // 枚举类型的设置项会显示为下拉框
        [SettingsUISection(kSection, kLanguageGroup)]
        public ModLanguage Language { get; set; } = ModLanguage.Auto;

        public bool IsDisabled() => !Enabled;

        // 下拉框选项：常用范围密、大数值疏
        //   40–200 米每 10 米一档，200–400 米每 25 米一档，400–1000 米每 50 米一档
        public static readonly int[] kIntervalOptions = BuildIntervalOptions();

        private static int[] BuildIntervalOptions()
        {
            var list = new List<int>();
            for (int v = 40; v <= 200; v += 10) list.Add(v);
            for (int v = 225; v <= 400; v += 25) list.Add(v);
            for (int v = 450; v <= 1000; v += 50) list.Add(v);
            return list.ToArray();
        }

        public static DropdownItem<int>[] GetIntervalItems()
        {
            var items = new DropdownItem<int>[kIntervalOptions.Length];
            for (int i = 0; i < kIntervalOptions.Length; i++)
            {
                int v = kIntervalOptions[i];
                items[i] = new DropdownItem<int> { value = v, displayName = LocalizedString.Value($"{v} m") };
            }
            return items;
        }

        // 旧版滑块存下的值（如 310）不在选项里时，吸附到最接近的一档，避免下拉框显示空白
        public static int SnapToOption(int value)
        {
            int best = kIntervalOptions[0];
            foreach (int v in kIntervalOptions)
                if (Math.Abs(v - value) < Math.Abs(best - value))
                    best = v;
            return best;
        }

        public void SnapIntervals()
        {
            RoadInterval = SnapToOption(RoadInterval);
            TrackInterval = SnapToOption(TrackInterval);
            PathInterval = SnapToOption(PathInterval);
        }

        public override void SetDefaults()
        {
            Enabled = true;
            RoadInterval = kVanillaRoad;
            TrackInterval = kVanillaTrack;
            PathInterval = kVanillaPath;
            Language = ModLanguage.Auto;
        }

        // 设置页里任何改动都会走到这里
        public override void Apply()
        {
            base.Apply();
            PillarSpacingSystem.Dirty = true;   // 重新应用桥墩间距
            ModLocalization.Refresh();          // 语言变了就刷新界面文本（没变则什么都不做）
        }
    }
}
