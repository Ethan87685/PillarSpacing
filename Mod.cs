using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;

namespace PillarSpacing
{
    public class Mod : IMod
    {
        public static ILog log = LogManager
            .GetLogger(nameof(PillarSpacing))
            .SetShowsErrorsInUI(false);

        public static Setting Setting { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info("PillarSpacing loading");

            Setting = new Setting(this);
            Setting.RegisterInOptionsUI();
            // 读取上次保存的设置；没有配置文件时使用默认值
            AssetDatabase.global.LoadSettings(nameof(PillarSpacing), Setting, new Setting(this));
            Setting.SnapIntervals();

            // 读完设置再初始化界面文本，这样上次选的语言能立即生效
            ModLocalization.Init(Setting);

            // 每帧检查一次“是否需要重新应用”，真正的修改只在需要时执行
            updateSystem.UpdateAt<PillarSpacingSystem>(SystemUpdatePhase.MainLoop);
        }

        public void OnDispose()
        {
            log.Info("PillarSpacing disposed");
            ModLocalization.Dispose();
            if (Setting != null)
            {
                Setting.UnregisterInOptionsUI();
                Setting = null;
            }
        }
    }
}
