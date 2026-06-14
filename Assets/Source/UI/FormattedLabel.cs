using TMPro;

namespace BalloonParty.UI
{
    /// <summary>
    ///     Wraps a <see cref="TMP_Text"/> whose authored text is a <see cref="string.Format"/>
    ///     template (e.g. "Level: {0}", "Score: {0:N0}"). The template is captured once at
    ///     construction, so repeated <see cref="Set"/> calls keep substituting into the original
    ///     placeholder instead of consuming it. Construct it before the label's text is first
    ///     overwritten (e.g. in Awake). A label with no authored text falls back to a bare "{0}".
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
