using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GlamShuffle.Core;
using GlamShuffle.Services;
using GlamShuffle.UI;

namespace GlamShuffle
{
    public sealed class GlamShufflePlugin : IDalamudPlugin
    {
        public string Name => "Glam Shuffle";
        private const string Command = "/gshuffle";

        private readonly IDalamudPluginInterface _pi;
        private readonly ICommandManager _commands;
        private readonly IChatGui _chat;
        private readonly IPluginLog _log;
        private readonly IFramework _framework;

        public Configuration Configuration { get; }

        private readonly GlamourerIpc _glamourer;
        private readonly RotationState _state = new();
        private readonly MainWindow _mainWindow;
        private readonly WindowSystem _windowSystem = new("GlamShuffle");

        // Cached design list, refreshed before each rotation pick
        private Dictionary<Guid, string> _designCache = new();
        private DateTime _designCacheRefreshedAt = DateTime.MinValue;

        public GlamShufflePlugin(
            IDalamudPluginInterface pluginInterface,
            ICommandManager commandManager,
            IPluginLog pluginLog,
            IChatGui chatGui,
            IFramework framework)
        {
            _pi = pluginInterface;
            _commands = commandManager;
            _log = pluginLog;
            _chat = chatGui;
            _framework = framework;

            Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(pluginInterface);

            _glamourer = new GlamourerIpc(pluginInterface, pluginLog);

            _mainWindow = new MainWindow(Configuration, _state, _glamourer);
            _mainWindow.OnApplyNow += ApplyRandomDesign;
            _windowSystem.AddWindow(_mainWindow);

            _framework.Update += OnFrameworkUpdate;

            _commands.AddHandler(Command, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Glam Shuffle  |  on  |  off  |  now"
            });

            pluginInterface.UiBuilder.Draw += DrawUi;
            pluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
            pluginInterface.UiBuilder.OpenConfigUi += OpenMainUi;
        }

        public void Dispose()
        {
            _framework.Update -= OnFrameworkUpdate;
            _mainWindow.OnApplyNow -= ApplyRandomDesign;

            _pi.UiBuilder.Draw -= DrawUi;
            _pi.UiBuilder.OpenMainUi -= OpenMainUi;
            _pi.UiBuilder.OpenConfigUi -= OpenMainUi;

            _commands.RemoveHandler(Command);
            _windowSystem.RemoveAllWindows();
            _mainWindow.Dispose();
        }

        private void DrawUi() => _windowSystem.Draw();
        private void OpenMainUi() => _mainWindow.IsOpen = true;

        private void OnFrameworkUpdate(IFramework framework)
        {
            if (!Configuration.Enabled) return;
            if (!_glamourer.IsAvailable()) return;

            var elapsed = DateTime.UtcNow - _state.LastApplied;
            if (elapsed < TimeSpan.FromMinutes(Configuration.IntervalMinutes)) return;

            ApplyRandomDesign();
        }

        private void ApplyRandomDesign()
        {
            // Refresh design cache at most once per minute
            if (DateTime.UtcNow - _designCacheRefreshedAt > TimeSpan.FromMinutes(1))
            {
                var fetched = _glamourer.GetAllDesigns();
                if (fetched.Count > 0)
                {
                    _designCache = fetched;
                    _designCacheRefreshedAt = DateTime.UtcNow;
                }
            }

            var active = _designCache
                .Where(kv => !Configuration.ExcludedDesignGuids.Contains(kv.Key.ToString()))
                .ToList();

            if (active.Count == 0)
            {
                _state.StatusMessage = "No designs in rotation — check Exclusions tab or enable Glamourer.";
                _chat.Print("[GlamShuffle] No active designs in rotation. Rotation paused.");
                Configuration.Enabled = false;
                Configuration.Save();
                return;
            }

            var pick = active[Random.Shared.Next(active.Count)];
            var success = _glamourer.ApplyDesign(pick.Key, pick.Value);

            if (success)
            {
                _state.LastApplied = DateTime.UtcNow;
                _state.LastAppliedName = pick.Value;
                _state.StatusMessage = null;
                _log.Info("[GlamShuffle] Applied design '{Name}'", pick.Value);
            }
            else
            {
                _state.StatusMessage = $"Failed to apply \"{pick.Value}\" — check Dalamud log.";
                _chat.Print($"[GlamShuffle] Failed to apply design \"{pick.Value}\". Check /xldev for details.");
            }
        }

        private void OnCommand(string command, string args)
        {
            var arg = args.Trim().ToLowerInvariant();
            switch (arg)
            {
                case "on":
                    Configuration.Enabled = true;
                    Configuration.Save();
                    _chat.Print("[GlamShuffle] Rotation enabled.");
                    break;
                case "off":
                    Configuration.Enabled = false;
                    Configuration.Save();
                    _chat.Print("[GlamShuffle] Rotation disabled.");
                    break;
                case "now":
                    ApplyRandomDesign();
                    break;
                default:
                    _mainWindow.IsOpen = true;
                    break;
            }
        }
    }
}
