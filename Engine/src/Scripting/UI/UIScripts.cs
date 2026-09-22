using System.Text;
using Model.Core;
using Model.Core.Player;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace RhyCiv.Engine.Scripting.UI
{
    public class UIScripts
    {
        private IInterfaceCommands _uInterfaceCommands = NullInterfaceCommands.Instance;
        private readonly StringBuilder _log;

        public UIScripts( StringBuilder log)
        {
            _log = log;
        }

        internal void Connect(IInterfaceCommands? interfaceCommands)
        {
            _uInterfaceCommands = interfaceCommands ?? NullInterfaceCommands.Instance;
        }

        public void text(string text)
        {
            var pop = new PopupBox
            {
                Button = new[] { "OK" },
                Text = new[] { text },
                LineStyles = new[] { TextStyles.Left }
            };
            _uInterfaceCommands.ShowDialog(pop);
        }

        public Dialog createDialog()
        {
            return new Dialog(_uInterfaceCommands, _log);
        }
    }
}