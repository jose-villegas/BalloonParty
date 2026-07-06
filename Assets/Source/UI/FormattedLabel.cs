using TMPro;

namespace BalloonParty.UI
{
    /// <summary>
    ///     Wraps a <see cref="TMP_Text"/> whose authored text is a <see cref="string.Format"/> template;
    ///     construct it before the label's text is first overwritten.
    /// </summary>
    internal readonly struct FormattedLabel
    {
        private readonly TMP_Text _label;
        private readonly string _format;

        public FormattedLabel(TMP_Text label)
        {
            _label = label;
            _format = label != null && !string.IsNullOrEmpty(label.text) ? label.text : "{0}";
        }

        public void Set(object value)
        {
            if (_label != null)
            {
                _label.text = string.Format(_format, value);
            }
        }
    }
}
