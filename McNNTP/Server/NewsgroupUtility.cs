using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using McNNTP.Server.Data;
using NHibernate;

namespace McNNTP.Server
{
    internal static class NewsgroupUtility
    {
        [NotNull, Pure]
        public static IEnumerable<Newsgroup> AddMetagroups([NotNull] this IEnumerable<Newsgroup> baseList, [NotNull] ISession session,
            [CanBeNull] Administrator identity)
        {
            foreach (var group in baseList)
            {
                yield return group;

                // Add any metagroups
                if (identity != null && (identity.CanCancel || identity.Moderates.Any(g => g.Name == group.Name)))
                    yield return group.GetMetaCancelledGroup(session);
                if (identity != null && (identity.CanApproveAny || identity.Moderates.Any(g => g.Name == group.Name)))
                    yield return group.GetMetaPendinGroup(session);
            }
        }
    }
}
