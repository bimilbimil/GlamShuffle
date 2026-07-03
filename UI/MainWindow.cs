using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using GlamShuffle.Core;
using GlamShuffle.Services;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

namespace GlamShuffle.UI
{
    public class MainWindow : Window
    {
        private readonly Configuration _config;
        private readonly RotationState _state;
        private readonly GlamourerIpc _glamourer;

        // Cached design list for the Exclusions tab, refreshed on open and on demand
        private Dictionary<Guid, (string Name, string Path)> _designCache = new();
        private DateTimeOffset _designCacheRefreshedAt = DateTimeOffset.MinValue;
        private string _exclusionFilter = "";

        // Callback so the main plugin can trigger an immediate rotation apply
        public event Action OnApplyNow;

        public MainWindow(Configuration config, RotationState state, GlamourerIpc glamourer)
            : base("Glam Shuffle", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            _config = config;
            _state = state;
            _glamourer = glamourer;
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(420, 280),
                MaximumSize = new Vector2(700, 600),
            };
        }

        public override void Draw()
        {
            if (ImGui.BeginTabBar("##tabs"))
            {
                if (ImGui.BeginTabItem("Rotation"))
                {
                    DrawRotationTab();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Exclusions"))
                {
                    DrawExclusionsTab();
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }

        private void DrawRotationTab()
        {
            ImGui.Spacing();

            var glamAvailable = _glamourer.IsAvailable();
            if (!glamAvailable)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
                ImGui.TextUnformatted("Glamourer is not available. Install and enable Glamourer first.");
                ImGui.PopStyleColor();
                ImGui.Spacing();
            }

            // Enable toggle
            var enabled = _config.Enabled;
            if (ImGui.Checkbox("Enable rotation", ref enabled))
            {
                _config.Enabled = enabled;
                _config.Save();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Interval slider
            ImGui.TextUnformatted("Apply a random design every");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80);
            var interval = _config.IntervalMinutes;
            if (ImGui.InputInt("##interval", ref interval))
            {
                _config.IntervalMinutes = Math.Max(1, interval);
                _config.Save();
            }
            ImGui.SameLine();
            ImGui.TextUnformatted("minutes");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Status block
            ImGui.TextUnformatted("Status");
            ImGui.Spacing();

            if (_state.LastApplied == DateTime.MinValue)
            {
                ImGui.TextUnformatted("  Last applied:  —");
            }
            else
            {
                var ago = DateTime.UtcNow - _state.LastApplied;
                var agoStr = ago.TotalMinutes < 1 ? "just now"
                    : ago.TotalMinutes < 60 ? $"{(int)ago.TotalMinutes}m ago"
                    : $"{(int)ago.TotalHours}h {ago.Minutes}m ago";
                ImGui.TextUnformatted($"  Last applied:  {_state.LastAppliedName ?? "unknown"} ({agoStr})");
            }

            if (_config.Enabled && _state.LastApplied != DateTime.MinValue)
            {
                var nextAt = _state.LastApplied + TimeSpan.FromMinutes(_config.IntervalMinutes);
                var remaining = nextAt - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    ImGui.TextUnformatted("  Next rotation:  due now");
                else
                    ImGui.TextUnformatted($"  Next rotation:  in {(int)remaining.TotalMinutes}m {remaining.Seconds}s");
            }
            else if (_config.Enabled)
            {
                ImGui.TextUnformatted("  Next rotation:  will apply shortly after enabling");
            }

            var excluded = _config.ExcludedDesignGuids.Count;
            if (_designCache.Count > 0)
                ImGui.TextUnformatted($"  Active designs: {Math.Max(0, _designCache.Count - excluded)} / {_designCache.Count} ({excluded} excluded)");

            if (!string.IsNullOrEmpty(_state.StatusMessage))
            {
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.8f, 0.2f, 1f));
                ImGui.TextUnformatted($"  {_state.StatusMessage}");
                ImGui.PopStyleColor();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("Apply Now"))
                OnApplyNow?.Invoke();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Apply a random design immediately, resetting the timer.");
        }

        private void DrawExclusionsTab()
        {
            ImGui.Spacing();

            // Auto-refresh cache every 30 seconds, or when tab first opens
            if (DateTimeOffset.UtcNow - _designCacheRefreshedAt > TimeSpan.FromSeconds(30))
                RefreshDesignCache();

            var activeCount = Math.Max(0, _designCache.Count - _config.ExcludedDesignGuids.Count);
            ImGui.TextUnformatted($"Designs in rotation: {activeCount} / {_designCache.Count}");
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - 110);
            if (ImGui.Button("Refresh Designs"))
                RefreshDesignCache();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Re-fetch design list from Glamourer.");

            ImGui.Spacing();

            // Filter box
            ImGui.SetNextItemWidth(-1);
            var filter = _exclusionFilter;
            if (ImGui.InputText("##filter", ref filter, 128))
                _exclusionFilter = filter;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Filter designs by name");

            ImGui.Spacing();

            if (_designCache.Count == 0)
            {
                ImGui.TextUnformatted("No designs found. Make sure Glamourer is running and click Refresh.");
                return;
            }

            // Sorted design list with checkboxes
            ImGui.BeginChild("##designlist", new Vector2(0, 0), true);

            var sorted = _designCache
                .OrderBy(kv => kv.Value.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var (guid, design) in sorted)
            {
                if (!string.IsNullOrEmpty(_exclusionFilter) &&
                    design.Name.IndexOf(_exclusionFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var guidStr = guid.ToString();
                var inRotation = !_config.ExcludedDesignGuids.Contains(guidStr);

                if (ImGui.Checkbox($"##{guidStr}", ref inRotation))
                {
                    if (inRotation)
                        _config.ExcludedDesignGuids.Remove(guidStr);
                    else
                        _config.ExcludedDesignGuids.Add(guidStr);
                    _config.Save();
                }
                ImGui.SameLine();
                ImGui.TextUnformatted(design.Name);
            }

            ImGui.EndChild();
        }

        private void RefreshDesignCache()
        {
            var fetched = _glamourer.GetAllDesigns();
            if (fetched.Count > 0)
                _designCache = fetched;
            _designCacheRefreshedAt = DateTimeOffset.UtcNow;
        }

        public void Dispose() { }
    }
}
