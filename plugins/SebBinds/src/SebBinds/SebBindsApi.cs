using System;
using System.Collections.Generic;

namespace SebBinds
{
    public static class SebBindsApi
    {
        public sealed class Page
        {
            public string Id;
            public string Title;
            public BindAction[] Actions;
        }

        private static readonly object Sync = new object();
        private static readonly List<Page> ExtraPages = new List<Page>();

        private static readonly List<IAxisProvider> AxisProviders = new List<IAxisProvider>();

        public static void RegisterActionsPage(string id, string title, params BindAction[] actions)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("id is required", nameof(id));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("title is required", nameof(title));
            if (actions == null) throw new ArgumentNullException(nameof(actions));

            var page = new Page
            {
                Id = id.Trim(),
                Title = title.Trim(),
                Actions = actions
            };

            lock (Sync)
            {
                // Replace by id.
                for (int i = 0; i < ExtraPages.Count; i++)
                {
                    if (string.Equals(ExtraPages[i].Id, page.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        ExtraPages[i] = page;
                        return;
                    }
                }
                ExtraPages.Add(page);
            }
        }

        public static void RegisterAxisProvider(IAxisProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            lock (Sync)
            {
                if (!AxisProviders.Contains(provider))
                {
                    AxisProviders.Add(provider);
                }
            }
        }

        internal static List<IAxisProvider> GetAxisProvidersSnapshot()
        {
            lock (Sync)
            {
                return new List<IAxisProvider>(AxisProviders);
            }
        }

        internal static List<Page> GetExtraPagesSnapshot()
        {
            lock (Sync)
            {
                return new List<Page>(ExtraPages);
            }
        }
    }
}
