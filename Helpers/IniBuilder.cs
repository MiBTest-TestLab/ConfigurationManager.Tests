using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConfigurationManager.Tests.Helpers
{
    /// <summary>
    /// Small fluent builder for INI text. Sections and keys keep their insertion order,
    /// which matters because the production code relies on the parser's enumeration order
    /// (e.g. FrameData1, FrameData2 ...).
    /// </summary>
    /// <remarks>
    /// A ';' used as the whole value (e.g. FrameDelimiter=;) is read correctly (see
    /// MiBTestConfigGoldenFileTests). A ';' in the middle of a value is untested, so avoid it here.
    /// </remarks>
    public class IniBuilder
    {
        private readonly List<KeyValuePair<string, List<KeyValuePair<string, string>>>> sections =
            new List<KeyValuePair<string, List<KeyValuePair<string, string>>>>();

        /// <summary>
        /// Adds an (empty) section if it does not exist yet.
        /// </summary>
        public IniBuilder Section(string name)
        {
            if (Find(name) == null)
                sections.Add(new KeyValuePair<string, List<KeyValuePair<string, string>>>(
                    name, new List<KeyValuePair<string, string>>()));
            return this;
        }

        /// <summary>
        /// Adds or replaces a key, creating the section if needed.
        /// </summary>
        public IniBuilder Set(string section, string key, string value)
        {
            Section(section);
            var keys = Find(section);
            int idx = keys.FindIndex(k => k.Key == key);
            var kv = new KeyValuePair<string, string>(key, value);
            if (idx >= 0) keys[idx] = kv;
            else keys.Add(kv);
            return this;
        }

        public IniBuilder RemoveKey(string section, string key)
        {
            Find(section)?.RemoveAll(k => k.Key == key);
            return this;
        }

        public IniBuilder RemoveSection(string section)
        {
            sections.RemoveAll(s => s.Key == section);
            return this;
        }

        /// <summary>
        /// Removes every key from a section but keeps the section header.
        /// </summary>
        public IniBuilder ClearSection(string section)
        {
            Find(section)?.Clear();
            return this;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var sec in sections)
            {
                sb.AppendLine($"[{sec.Key}]");
                foreach (var kv in sec.Value)
                    sb.AppendLine($"{kv.Key}={kv.Value}");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private List<KeyValuePair<string, string>> Find(string section) =>
            sections.Where(s => s.Key == section).Select(s => s.Value).FirstOrDefault();
    }
}
