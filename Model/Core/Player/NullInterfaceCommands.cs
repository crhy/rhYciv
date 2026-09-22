using System;
using System.Collections.Generic;
using Model.Core;

namespace Model.Core.Player
{
    /// <summary>
    /// No-op <see cref="IInterfaceCommands"/> for when the UI has not provided one.
    /// Without this, any path that reaches IPlayer.Ui throws NullReferenceException.
    /// </summary>
    public sealed class NullInterfaceCommands : IInterfaceCommands
    {
        public static NullInterfaceCommands Instance { get; } = new();

        private NullInterfaceCommands()
        {
        }

        public void ShowDialog(string dialogKey)
        {
        }

        public Tuple<string, int, List<bool>> ShowDialog(PopupBox popupBox, List<bool>? checkBoxOptionStates = null)
        {
            var button = popupBox.Button is { Count: > 0 } buttons ? buttons[0] : string.Empty;
            return Tuple.Create(button, 0, checkBoxOptionStates ?? new List<bool>());
        }

        public void SavePopup(string key, PopupBox popup)
        {
        }
    }
}
