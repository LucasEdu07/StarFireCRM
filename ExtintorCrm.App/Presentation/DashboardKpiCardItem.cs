using System;

namespace ExtintorCrm.App.Presentation
{
    public sealed class DashboardKpiCardItem : ViewModelBase
    {
        private int _value;

        public DashboardKpiCardItem(
            string title,
            string badgeText,
            string iconGlyph,
            string commandParameter,
            string toolTip,
            string severity)
        {
            Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("Title is required.", nameof(title)) : title;
            BadgeText = string.IsNullOrWhiteSpace(badgeText) ? throw new ArgumentException("BadgeText is required.", nameof(badgeText)) : badgeText;
            IconGlyph = string.IsNullOrWhiteSpace(iconGlyph) ? throw new ArgumentException("IconGlyph is required.", nameof(iconGlyph)) : iconGlyph;
            CommandParameter = string.IsNullOrWhiteSpace(commandParameter) ? throw new ArgumentException("CommandParameter is required.", nameof(commandParameter)) : commandParameter;
            ToolTip = string.IsNullOrWhiteSpace(toolTip) ? "Clique para listar os clientes deste aviso" : toolTip;
            Severity = string.Equals(severity, "Danger", StringComparison.OrdinalIgnoreCase) ? "Danger" : "Warning";
        }

        public string Title { get; }
        public string BadgeText { get; }
        public string IconGlyph { get; }
        public string CommandParameter { get; }
        public string ToolTip { get; }
        public string Severity { get; }

        public int Value
        {
            get => _value;
            set
            {
                if (_value == value)
                {
                    return;
                }

                _value = value;
                OnPropertyChanged();
            }
        }
    }
}
