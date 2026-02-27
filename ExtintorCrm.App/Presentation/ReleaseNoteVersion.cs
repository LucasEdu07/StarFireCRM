using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtintorCrm.App.Presentation
{
    public sealed class ReleaseNoteVersion
    {
        public ReleaseNoteVersion(string version, string publishedOn, IEnumerable<string> highlights)
        {
            Version = string.IsNullOrWhiteSpace(version)
                ? throw new ArgumentException("Version is required.", nameof(version))
                : version.Trim();

            PublishedOn = string.IsNullOrWhiteSpace(publishedOn)
                ? "Sem data"
                : publishedOn.Trim();

            Highlights = highlights?
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .ToList()
                ?? new List<string>();
        }

        public string Version { get; }
        public string PublishedOn { get; }
        public IReadOnlyList<string> Highlights { get; }
    }
}
