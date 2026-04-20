using System;
using System.Collections.Generic;
using Facepunch;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Phone Search Bar", "wghilliard", "1.0.0")]
    [Description("Adds a search bar to the phone Directory to filter entries by name.")]
    public class PhoneSearchBar : RustPlugin
    {
        private const string SearchBarPanel = "PhoneSearchBar.Panel";
        private const string SearchBarInput = "PhoneSearchBar.Input";
        private const string SearchBarPlaceholder = "PhoneSearchBar.Placeholder";
        private const string SearchBarClear = "PhoneSearchBar.Clear";
        private const string FilterCommand = "phonesearch.filter";
        private const string ClearCommand = "phonesearch.clear";
        private const int DirectoryPageSize = 12;
        private const int MaxSearchLength = 30;
        private const float CommandCooldown = 0.25f;

        private readonly Dictionary<ulong, PhoneController> _activePhones = new Dictionary<ulong, PhoneController>();
        private readonly Dictionary<ulong, float> _lastCommandTime = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, string> _activeSearchText = new Dictionary<ulong, string>();

        #region Hooks

        private void OnActiveTelephoneUpdated(BasePlayer player, PhoneController controller)
        {
            if (player == null)
            {
                return;
            }

            if (controller != null)
            {
                _activePhones[player.userID] = controller;
                ShowSearchBar(player);
            }
            else
            {
                ClearPlayerState(player);
            }
        }

        private void OnPhoneDial(PhoneController phone, PhoneController callee, BasePlayer player)
        {
            if (player == null)
            {
                return;
            }

            DestroySearchBar(player);
        }

        private void OnPhoneDialFailed(PhoneController phone, Telephone.DialFailReason reason, BasePlayer player)
        {
            if (player == null)
            {
                return;
            }

            if (!_activePhones.TryGetValue(player.userID, out PhoneController activePhone))
            {
                return;
            }

            if (!IsPhoneValid(player, activePhone))
            {
                ClearPlayerState(player);
                return;
            }

            _activeSearchText.TryGetValue(player.userID, out string savedText);
            ShowSearchBar(player, savedText ?? "");

            if (!string.IsNullOrEmpty(savedText))
            {
                SendFilteredDirectory(player, activePhone, savedText);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null)
            {
                return;
            }

            ClearPlayerState(player);
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DestroySearchBar(player);
            }
            _activePhones.Clear();
            _lastCommandTime.Clear();
            _activeSearchText.Clear();
        }

        private void ClearPlayerState(BasePlayer player)
        {
            _activePhones.Remove(player.userID);
            _lastCommandTime.Remove(player.userID);
            _activeSearchText.Remove(player.userID);
            DestroySearchBar(player);
        }

        #endregion

        #region Console Command

        [ConsoleCommand(FilterCommand)]
        private void OnFilterCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
            {
                return;
            }

            if (!_activePhones.TryGetValue(player.userID, out PhoneController phone))
            {
                return;
            }

            if (!IsPhoneValid(player, phone))
            {
                ClearPlayerState(player);
                return;
            }

            if (!CheckCooldown(player))
            {
                return;
            }

            string searchText = arg.Args != null ? string.Join(" ", arg.Args).Trim() : "";
            if (searchText.Length > MaxSearchLength)
            {
                searchText = searchText.Substring(0, MaxSearchLength);
            }

            if (string.IsNullOrEmpty(searchText))
            {
                _activeSearchText.Remove(player.userID);
            }
            else
            {
                _activeSearchText[player.userID] = searchText;
            }

            SendFilteredDirectory(player, phone, searchText);
            ShowSearchBar(player, searchText);
        }

        [ConsoleCommand(ClearCommand)]
        private void OnClearCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
            {
                return;
            }

            if (!_activePhones.TryGetValue(player.userID, out PhoneController phone))
            {
                return;
            }

            if (!IsPhoneValid(player, phone))
            {
                ClearPlayerState(player);
                return;
            }

            if (!CheckCooldown(player))
            {
                return;
            }

            _activeSearchText.Remove(player.userID);
            SendFilteredDirectory(player, phone, "");
            ShowSearchBar(player);
        }

        private bool CheckCooldown(BasePlayer player)
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (_lastCommandTime.TryGetValue(player.userID, out float last) && now - last < CommandCooldown)
            {
                return false;
            }

            _lastCommandTime[player.userID] = now;
            return true;
        }

        #endregion

        #region Directory Filtering

        private static bool IsPhoneValid(BasePlayer player, PhoneController phone)
        {
            if (phone == null || phone.ParentEntity == null)
            {
                return false;
            }

            if (phone.ParentEntity.IsDestroyed)
            {
                return false;
            }

            if (phone.currentPlayer != player)
            {
                return false;
            }

            return true;
        }

        private static void SendFilteredDirectory(BasePlayer player, PhoneController phone, string searchText)
        {
            PhoneDirectory directory = Pool.Get<PhoneDirectory>();
            directory.entries = Pool.Get<List<PhoneDirectory.DirectoryEntry>>();
            try
            {
                foreach (KeyValuePair<int, PhoneController> kvp in TelephoneManager.allTelephones)
                {
                    if (kvp.Key == phone.PhoneNumber)
                    {
                        continue;
                    }

                    PhoneController otherPhone = kvp.Value;
                    if (otherPhone == null)
                    {
                        continue;
                    }

                    if (otherPhone.ParentEntity == null || otherPhone.ParentEntity.IsDestroyed)
                    {
                        continue;
                    }

                    string directoryName = otherPhone.GetDirectoryName();
                    if (string.IsNullOrEmpty(directoryName))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(searchText) &&
                        directoryName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    PhoneDirectory.DirectoryEntry entry = Pool.Get<PhoneDirectory.DirectoryEntry>();
                    entry.phoneName = directoryName;
                    entry.phoneNumber = otherPhone.PhoneNumber;
                    directory.entries.Add(entry);
                }

                directory.entries.Sort((a, b) =>
                    string.Compare(a.phoneName, b.phoneName, StringComparison.OrdinalIgnoreCase));

                // Dispose excess entries beyond page size and trim the list
                while (directory.entries.Count > DirectoryPageSize)
                {
                    PhoneDirectory.DirectoryEntry last = directory.entries[directory.entries.Count - 1];
                    directory.entries.RemoveAt(directory.entries.Count - 1);
                    last.Dispose();
                }

                directory.atEnd = true;

                phone.ParentEntity.ClientRPC(RpcTarget.Player("ReceivePhoneDirectory", player), directory);
            }
            finally
            {
                directory.Dispose();
            }
        }

        #endregion

        #region CUI

        private static void ShowSearchBar(BasePlayer player, string existingText = "")
        {
            DestroySearchBar(player);

            CuiElementContainer container = new CuiElementContainer();

            // Panel — aligned with the phone UI, olive-brown to match
            container.Add(new CuiPanel
            {
                Image = { Color = "0.27 0.26 0.22 0.95" },
                RectTransform =
                {
                    AnchorMin = "0.340 0.745",
                    AnchorMax = "0.659 0.768"
                }
            }, "Overlay", SearchBarPanel);

            // Placeholder "Search..." label (renders behind input text, hidden when restoring text)
            if (string.IsNullOrEmpty(existingText))
            {
                container.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "Search...",
                        FontSize = 12,
                        Font = "RobotoCondensed-Regular.ttf",
                        Color = "0.7 0.7 0.65 0.5",
                        Align = TextAnchor.MiddleLeft
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.04 0",
                        AnchorMax = "0.85 1"
                    }
                }, SearchBarPanel, SearchBarPlaceholder);
            }

            // Text input field
            container.Add(new CuiElement
            {
                Name = SearchBarInput,
                Parent = SearchBarPanel,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = existingText,
                        FontSize = 12,
                        Font = "RobotoCondensed-Regular.ttf",
                        Color = "0.9 0.9 0.85 1",
                        Command = FilterCommand,
                        Align = TextAnchor.MiddleLeft,
                        CharsLimit = 30
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.04 0",
                        AnchorMax = "0.88 1"
                    }
                }
            });

            // "x" clear button — subtle, matching the panel style
            container.Add(new CuiButton
            {
                Text =
                {
                    Text = "✕",
                    FontSize = 10,
                    Align = TextAnchor.MiddleCenter,
                    Color = "0.7 0.7 0.65 0.6"
                },
                Button =
                {
                    Command = ClearCommand,
                    Color = "0.22 0.21 0.18 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.90 0.1",
                    AnchorMax = "0.99 0.85"
                }
            }, SearchBarPanel, SearchBarClear);

            CuiHelper.AddUi(player, container);
        }

        private static void DestroySearchBar(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, SearchBarPanel);
        }

        #endregion
    }
}
