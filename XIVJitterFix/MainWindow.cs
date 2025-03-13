using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;

namespace XIVJitterFix;

public class MainWindow : Window
{
    private readonly IDalamudPluginInterface dalamudPluginInterface;
    private readonly Config                  pluginConfig;

    public MainWindow(Config config, IDalamudPluginInterface dalamudPluginInterface) : base("XIVJitterFix 配置###XivJitterFix")
    {
        Size                        = new Vector2(500, 400);
        this.dalamudPluginInterface = dalamudPluginInterface;
        pluginConfig                = config;
    }

    public override void Draw()
    {
        ImGui.TextWrapped("启用插件代表启用相关功能, 如需关闭, 直接禁用本插件即可");
        ImGui.Separator();
        
        ImGui.TextWrapped("提示: 如果你并未在画面设置里使用 DLAA、DLSS 或 TSCMAA+ , 请勿启用本插件");
        ImGui.Separator();

        using (var help = ImRaii.TreeNode("什么是画面抖动？为什么需要它？"))
        {
            if (help)
            {
                ImGui.TextWrapped("相机抖动是时间性抗锯齿技术（如前述方案）的重要组成部分," +
                                  "通过细微位移可有效消除锯齿边缘和静态边缘的走样,"     +
                                  "尤其能改善植被等半透明物体的显示效果。建议在条件允许时始终启用抖动");
                ImGui.TextWrapped("游戏通常会自动启用抖动，FF14 也不例外。但 SE 出于某些原因，在过场动画、GPose 和 NPC 对话 期间禁用了该功能。");
                ImGui.TextWrapped("你可能已注意到在上述场景中游戏自带的抗锯齿效果明显下降, 这正是因为抖动功能被关闭所致");
                ImGui.TextWrapped("经测试, 我们认为没有任何理由需要禁用抖动功能。启用后能显著提升这些场景下的画面质量");
            }
        }

        ImGui.Separator();
        using (var expertConfig = ImRaii.TreeNode("高级设置"))
        {
            if (expertConfig)
            {
                var jitterMultiplier = pluginConfig.JitterMultiplier;
                var configDirty = false;
                ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
                if (ImGui.SliderFloat("抖动强度倍数", ref jitterMultiplier, 0.1f, 3.0f))
                {
                    pluginConfig.JitterMultiplier = jitterMultiplier;
                    configDirty                   = true;
                }

                ImGui.SameLine();
                if (ImGui.Button("重置"))
                {
                    pluginConfig.JitterMultiplier = 0.6f;
                    configDirty                   = true;
                }

                ImGui.TextWrapped(
                    "此参数控制画面抖动强度, 默认值为0.6, 建议在0.5-1.5范围内调试, Ctrl+单击滑块 可输入精确值。" +
                    "注意：使用 TSCMAA+ 抖动技术时，抖动效果比 DLAA 更为明显。");

                var setDownscaleBuffers = pluginConfig.SetDownscaleBuffers;
                if (ImGui.Checkbox("覆盖降采样设置", ref setDownscaleBuffers))
                {
                    pluginConfig.SetDownscaleBuffers = setDownscaleBuffers;
                    configDirty                      = true;
                }

                using (ImRaii.Disabled(!setDownscaleBuffers))
                {
                    using var _ = ImRaii.PushIndent(10f);

                    var ignoreDownscaling = pluginConfig.DownscaleBuffers == 0;
                    if (ImGui.Checkbox("忽略降采样", ref ignoreDownscaling))
                    {
                        pluginConfig.DownscaleBuffers = (byte)(ignoreDownscaling ? 0 : 1);
                        configDirty                   = true;
                    }

                    ImGui.TextWrapped("此选项可在使用降采样技术时修复光晕和景深缓冲区问题。启用 DLAA 时建议开启，注意：这将强制永久启用 DLAA 且不再进行任何降采样处理");
                    if (configDirty)
                        dalamudPluginInterface.SavePluginConfig(pluginConfig);
                }
            }
        }
    }
}
