namespace McNNTP.Data
{
    using System.Collections.Generic;
    using System.Linq;

    using JetBrains.Annotations;
    using NHibernate;
    using McNNTP.Common;

    public static class NewsgroupUtility
    {
        [NotNull, Pure]
        public static IEnumerable<Newsgroup> AddMetagroups([NotNull] this IEnumerable<Newsgroup> baseList, [NotNull] ISession session,
            [CanBeNull] IIdentity identity)
        {
            foreach (var group in baseList)
            {
                yield return group;

                // Add any metagroups
                var groupClosure = group;
                if (identity != null && (identity.CanCancel || identity.Moderates.Any(g => g.Name == groupClosure.Name)))
                    yield return group.GetMetaCancelledGroup(session);
                if (identity != null && (identity.CanApproveAny || identity.Moderates.Any(g => g.Name == groupClosure.Name)))
                    yield return group.GetMetaPendingGroup(session);
            }
        }
    }
}
