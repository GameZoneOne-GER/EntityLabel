using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Entity Label", "gamezoneone", "1.0.0")]
    [Description("Label any deployed entity with custom text that appears when looking at it.")]
    public class EntityLabel : RustPlugin
    {
        #region Config

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty("Command")]
            public string Command { get; set; } = "label";

            [JsonProperty("Permission")]
            public string Permission { get; set; } = "entitylabel.use";

            [JsonProperty("MaxDistance")]
            public float MaxDistance { get; set; } = 5f;

            [JsonProperty("MaxLabelLength")]
            public int MaxLabelLength { get; set; } = 50;

            [JsonProperty("LabelFontSize")]
            public int LabelFontSize { get; set; } = 14;

            [JsonProperty("LabelColor")]
            public string LabelColor { get; set; } = "1 1 1 1";
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new System.Exception();
            }
            catch
            {
                PrintWarning("Invalid config — loading defaults.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Lang

        private string T(string key, string userId = null) => lang.GetMessage(key, this, userId);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permission to use this.",
                ["NoEntity"]     = "No labelable object in range.",
                ["NotAuthed"]    = "Only TC-authorized players can label here.",
                ["TooLong"]      = "Text too long (max. {0} characters).",
                ["LabelSet"]     = "Label set.",
                ["LabelRemoved"] = "Label removed.",
                ["UI.Title"]     = "Set Label",
                ["UI.SaveHint"]  = "↵ Save",
                ["UI.Delete"]    = "Delete",
                ["UI.Cancel"]    = "Cancel",
            }, this);
        }

        #endregion

        #region Data

        private readonly Dictionary<NetworkableId, string> _labels  = new Dictionary<NetworkableId, string>();
        private readonly Dictionary<ulong, NetworkableId>  _showing = new Dictionary<ulong, NetworkableId>();
        private readonly Dictionary<ulong, NetworkableId>  _pending = new Dictionary<ulong, NetworkableId>();

        private static readonly int _raycastMask = LayerMask.GetMask("Construction", "Deployed");
        private Timer _displayTimer;

        private const string UI_INPUT   = "entitylabel.input";
        private const string UI_DISPLAY = "entitylabel.display";

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(_config.Permission, this);
            cmd.AddChatCommand(_config.Command, this, nameof(CmdLabel));
            cmd.AddConsoleCommand("entitylabel.submit", this, nameof(ConsoleSubmit));
            cmd.AddConsoleCommand("entitylabel.cancel", this, nameof(ConsoleCancel));
            cmd.AddConsoleCommand("entitylabel.delete", this, nameof(ConsoleDelete));
        }

        private void OnServerInitialized()
        {
            _displayTimer = timer.Every(0.3f, TickDisplay);
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            if (entity?.net == null) return;
            _labels.Remove(entity.net.ID);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, UI_DISPLAY);
            CuiHelper.DestroyUi(player, UI_INPUT);
            _showing.Remove(player.userID);
            _pending.Remove(player.userID);
        }

        private void Unload()
        {
            _displayTimer?.Destroy();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null) continue;
                CuiHelper.DestroyUi(player, UI_DISPLAY);
                CuiHelper.DestroyUi(player, UI_INPUT);
            }
        }

        #endregion

        #region Command

        private void CmdLabel(BasePlayer player, string command, string[] args)
        {
            if (player == null || !player.IsConnected) return;

            if (!permission.UserHasPermission(player.UserIDString, _config.Permission))
            {
                player.ChatMessage(T("NoPermission", player.UserIDString));
                return;
            }

            if (!Physics.Raycast(player.eyes.HeadRay(), out var hit, _config.MaxDistance,
                _raycastMask, QueryTriggerInteraction.Ignore))
            {
                player.ChatMessage(T("NoEntity", player.UserIDString));
                return;
            }

            var entity = hit.transform.GetComponentInParent<BaseEntity>();
            if (entity == null || entity is BasePlayer || entity is BaseNpc || entity.net == null)
            {
                player.ChatMessage(T("NoEntity", player.UserIDString));
                return;
            }

            var priv = entity.GetBuildingPrivilege();
            if (priv != null && !priv.authorizedPlayers.Contains(player.userID))
            {
                player.ChatMessage(T("NotAuthed", player.UserIDString));
                return;
            }

            _pending[player.userID] = entity.net.ID;
            _labels.TryGetValue(entity.net.ID, out var existing);
            OpenInputUI(player, existing ?? string.Empty);
        }

        private void ConsoleSubmit(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, _config.Permission)) return;

            if (!_pending.TryGetValue(player.userID, out var netId))
            {
                CloseInputUI(player);
                return;
            }

            CloseInputUI(player);

            var text = arg.Args != null ? string.Join(" ", arg.Args).Trim() : string.Empty;

            if (text.Length > _config.MaxLabelLength)
            {
                player.ChatMessage(string.Format(T("TooLong", player.UserIDString), _config.MaxLabelLength));
                return;
            }

            if (string.IsNullOrEmpty(text))
            {
                _labels.Remove(netId);
                player.ChatMessage(T("LabelRemoved", player.UserIDString));
            }
            else
            {
                _labels[netId] = text;
                player.ChatMessage(T("LabelSet", player.UserIDString));
            }
        }

        private void ConsoleCancel(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null) CloseInputUI(player);
        }

        private void ConsoleDelete(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, _config.Permission)) return;
            if (!_pending.TryGetValue(player.userID, out var netId)) { CloseInputUI(player); return; }
            CloseInputUI(player);
            _labels.Remove(netId);
            player.ChatMessage(T("LabelRemoved", player.UserIDString));
        }

        private void OpenInputUI(BasePlayer player, string existing)
        {
            var uid = player.UserIDString;
            CuiHelper.DestroyUi(player, UI_INPUT);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = "0.08 0.08 0.08 0.92" },
                RectTransform = { AnchorMin = "0.3 0.43", AnchorMax = "0.7 0.59" },
                CursorEnabled = true
            }, "Hud", UI_INPUT);

            container.Add(new CuiLabel
            {
                Text = { Text = T("UI.Title", uid), FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "0.9 0.9 0.9 1" },
                RectTransform = { AnchorMin = "0 0.78", AnchorMax = "1 1" }
            }, UI_INPUT);

            container.Add(new CuiPanel
            {
                Image = { Color = "0.18 0.18 0.18 1" },
                RectTransform = { AnchorMin = "0.04 0.46", AnchorMax = "0.96 0.76" }
            }, UI_INPUT, UI_INPUT + ".bg");

            container.Add(new CuiElement
            {
                Name   = UI_INPUT + ".field",
                Parent = UI_INPUT + ".bg",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text          = existing,
                        FontSize      = 14,
                        Align         = TextAnchor.MiddleLeft,
                        Color         = "1 1 1 1",
                        Command       = "entitylabel.submit",
                        CharsLimit    = _config.MaxLabelLength,
                        IsPassword    = false,
                        NeedsKeyboard = true
                    },
                    new CuiRectTransformComponent { AnchorMin = "0.03 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiLabel
            {
                Text = { Text = T("UI.SaveHint", uid), FontSize = 10, Align = TextAnchor.MiddleRight, Color = "0.5 0.5 0.5 1" },
                RectTransform = { AnchorMin = "0 0.30", AnchorMax = "0.97 0.46" }
            }, UI_INPUT);

            container.Add(new CuiButton
            {
                Button        = { Command = "entitylabel.delete", Color = "0.6 0.15 0.15 1" },
                RectTransform = { AnchorMin = "0.04 0.04", AnchorMax = "0.49 0.28" },
                Text          = { Text = T("UI.Delete", uid), FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, UI_INPUT);

            container.Add(new CuiButton
            {
                Button        = { Command = "entitylabel.cancel", Color = "0.25 0.25 0.25 1" },
                RectTransform = { AnchorMin = "0.51 0.04", AnchorMax = "0.96 0.28" },
                Text          = { Text = T("UI.Cancel", uid), FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, UI_INPUT);

            CuiHelper.AddUi(player, container);
        }

        private void CloseInputUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_INPUT);
            _pending.Remove(player.userID);
        }

        #endregion

        #region Display

        private void TickDisplay()
        {
            var players = BasePlayer.activePlayerList.ToArray();
            foreach (var player in players)
            {
                if (player == null || !player.IsConnected) continue;
                if (_pending.ContainsKey(player.userID)) continue;

                NetworkableId hitId = default;
                string label = null;
                BaseEntity hitEntity = null;

                if (Physics.Raycast(player.eyes.HeadRay(), out var hit, _config.MaxDistance,
                    _raycastMask, QueryTriggerInteraction.Ignore))
                {
                    var entity = hit.transform.GetComponentInParent<BaseEntity>();
                    if (entity != null && entity.net != null && !(entity is BasePlayer) && !(entity is BaseNpc)
                        && _labels.TryGetValue(entity.net.ID, out var l))
                    {
                        hitId     = entity.net.ID;
                        label     = l;
                        hitEntity = entity;
                    }
                }

                var wasShowing = _showing.TryGetValue(player.userID, out var currentId);

                if (label != null)
                {
                    if (!wasShowing || currentId != hitId)
                    {
                        ShowLabel(player, label);
                        _showing[player.userID] = hitId;
                    }
                }
                else if (wasShowing)
                {
                    HideLabel(player);
                }
            }
        }

        private void ShowLabel(BasePlayer player, string label)
        {
            CuiHelper.DestroyUi(player, UI_DISPLAY);
            var container = new CuiElementContainer();

            float halfW = Mathf.Max(0.04f, label.Length * 0.0038f + 0.022f);
            string aMin = $"{0.5f - halfW:F3} 0.638";
            string aMax = $"{0.5f + halfW:F3} 0.668";

            container.Add(new CuiPanel
            {
                Image         = { Color = "0.08 0.08 0.08 0.78" },
                RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
            }, "Hud", UI_DISPLAY);

            container.Add(new CuiLabel
            {
                Text          = { Text = label, FontSize = _config.LabelFontSize, Align = TextAnchor.MiddleCenter, Color = _config.LabelColor },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, UI_DISPLAY);

            CuiHelper.AddUi(player, container);
        }

        private void HideLabel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_DISPLAY);
            _showing.Remove(player.userID);
        }

        #endregion
    }
}
