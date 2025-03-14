using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace XIVJitterFix;

public sealed class Plugin : IDalamudPlugin
{
    private readonly nint hookAddr;

    private readonly IPluginLog              logger;
    private readonly IFramework              framework;
    private readonly WindowSystem            windowSystem;
    private readonly IDalamudPluginInterface dalamudPluginInterface;
    private readonly ICommandManager         commandManager;
    private readonly MainWindow              mainWindow;
    private readonly Config                  pluginConfig;
    private readonly INotificationManager    notificationManager;

    public Plugin(
        IPluginLog           logger,    ISigScanner             sigScanner,
        IFramework           framework, IDalamudPluginInterface dalamudPluginInterface, ICommandManager commandManager,
        INotificationManager notificationManager)
    {
        this.logger                 = logger;
        this.framework              = framework;
        this.dalamudPluginInterface = dalamudPluginInterface;
        this.commandManager         = commandManager;
        this.notificationManager    = notificationManager;
        windowSystem                = new("XIVJitterFix");
        pluginConfig                = dalamudPluginInterface.GetPluginConfig() as Config ?? new();

        if (pluginConfig.Version == 0)
        {
            logger.Info("Migrating XIVJitterFix Config 0->1");
            if (pluginConfig.DownscaleBuffers == 0)
            {
                logger.Info("DownscaleBuffers was set to 0, setting SetDownscaleBuffers to true");
                pluginConfig.SetDownscaleBuffers = true;
            }

            pluginConfig.Version = 1;
            dalamudPluginInterface.SavePluginConfig(pluginConfig);
        }

        mainWindow = new MainWindow(pluginConfig, dalamudPluginInterface);
        windowSystem.AddWindow(mainWindow);

        commandManager.AddHandler("/jitterfix", new CommandInfo(OnCommand)
        {
            HelpMessage = "打开插件配置界面\n" +
                          "/jitterfix jitter <value> → 设置指定的抖动倍率",
            ShowInHelp = true
        });

        hookAddr = sigScanner.GetStaticAddressFromSig("48 89 05 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 8F ?? ?? ?? ??");

        framework.Update                              += Framework_Update;
        dalamudPluginInterface.UiBuilder.OpenConfigUi += UiBuilder_OpenConfigUi;
        dalamudPluginInterface.UiBuilder.Draw         += UiBuilder_Draw;
    }


    private void OnCommand(string command, string args)
    {
        var splitArgs = args.ToLowerInvariant().Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries); //Setting specific commands?

        switch (splitArgs.Length)
        {
            case 0:
                mainWindow.Toggle();
                break;
            case 2:
            {
                if (splitArgs[0] == "jitter")
                {
                    if (float.TryParse(splitArgs[1].Replace(",", "."), CultureInfo.InvariantCulture.NumberFormat, out var jittermulti))
                    {
                        pluginConfig.JitterMultiplier = jittermulti;
                        dalamudPluginInterface.SavePluginConfig(pluginConfig);
                        notificationManager.AddNotification(new Notification
                        {
                            Content = $"抖动值已被设置为 {jittermulti}", Title = "抖动值变更", Type = NotificationType.Success
                        });
                    }
                    else
                    {
                        logger.Warning("Provided value {0} is not a valid float number", splitArgs[1]);
                        notificationManager.AddNotification(new Notification
                        {
                            Content = "无法将抖动值设置为输入参数", Title = "抖动值变更", Type = NotificationType.Error
                        });
                    }
                }

                break;
            }
        }
    }

    private void UiBuilder_Draw()
    {
        windowSystem.Draw();
    }

    private void UiBuilder_OpenConfigUi()
    {
        mainWindow.Toggle();
    }

    private unsafe void Framework_Update(IFramework _)
    {
        if (hookAddr == nint.Zero) return;
        var addr1 = *(nint*)hookAddr;
        if (addr1 == nint.Zero) return;

        var config = (ProbablySomeGraphicsConfig*)addr1;

        var prevValue1 = config->NpcGposeJitter;
        var prevValue2 = config->CutsceneJitter;

        if (prevValue1 != 1 || prevValue2 != 1)
        {
            config->NpcGposeJitter = 1;
            config->CutsceneJitter = 1;
            logger.Verbose("Detected change NpcGpose {0} / Cutscene {1} -> NpcGpose {2} / Cutscene {3}", prevValue1, prevValue2,
                           config->NpcGposeJitter, config->CutsceneJitter);
        }

        if (pluginConfig.JitterMultiplier != config->JitterMultiplier)
        {
            logger.Verbose("Detected change JitterMult current {0} -> desired {1}", config->JitterMultiplier, pluginConfig.JitterMultiplier);

            config->JitterMultiplier = pluginConfig.JitterMultiplier;
        }

        if (pluginConfig.SetDownscaleBuffers && pluginConfig.DownscaleBuffers != config->DownscaleBuffers)
        {
            logger.Verbose("Detected change DownscaleBuffers current {0} -> desired {1}", config->DownscaleBuffers, pluginConfig.DownscaleBuffers);

            config->DownscaleBuffers = pluginConfig.DownscaleBuffers;
        }
    }

    public void Dispose()
    {
        commandManager.RemoveHandler("/jitterfix");
        windowSystem.RemoveAllWindows();

        dalamudPluginInterface.UiBuilder.OpenConfigUi -= UiBuilder_OpenConfigUi;
        dalamudPluginInterface.UiBuilder.Draw         -= UiBuilder_Draw;

        framework.Update -= Framework_Update;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct ProbablySomeGraphicsConfig
    {
        [FieldOffset(0x19)]
        public byte NpcGposeJitter;

        [FieldOffset(0x1a)]
        public byte CutsceneJitter;

        /// <summary>
        ///     0 = nothing, 1 = fxaa, 2 = tscmaa+jitter, 3 = tscmaa
        /// </summary>
        [FieldOffset(0x2c)]
        public byte AntiAliasingMode;

        /// <summary>
        ///     0 = off, 1 = on
        /// </summary>
        [FieldOffset(0x44)]
        public byte DynamicResolution;

        /// <summary>
        ///     seems like it affects dof/bloom shaders when running dlss or dynamic res
        /// </summary>
        [FieldOffset(0x45)]
        public byte DownscaleBuffers;

        /// <summary>
        ///     FSR = 1, DLSS = 2
        /// </summary>
        [FieldOffset(0x54)]
        public byte DlssFsrSwitch;

        [FieldOffset(0x64)]
        public float JitterMultiplier;
    }
}
